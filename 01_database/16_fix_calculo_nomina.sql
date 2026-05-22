-- ============================================================================
-- NominaGT v4 - Mejoras al calculo de nomina
--
-- 1. Actualiza parametros_legales con valores 2026 (salario minimo, IGSS).
-- 2. Reescribe sp_calcular_nomina para soportar:
--      - MENSUAL    : con IGSS 4.83% + ISR progresivo + prestamos
--      - QUINCENAL  : salario/2 sin descuentos legales (anticipo);
--                     descuentos legales en la 2da quincena
--      - BONO14     : salario_base proporcional al periodo Jul-Jun.
--                     EXENTO de IGSS e ISR (Decreto 42-92).
--      - AGUINALDO  : salario_base proporcional al periodo Dic-Nov.
--                     EXENTO de IGSS e ISR (Codigo de Trabajo art. 137).
-- 3. La bonificacion incentivo Q250 NUNCA se grava con IGSS ni ISR (Decreto 78-89).
-- ============================================================================

-- 1. Actualizar parametros 2026
MERGE INTO parametros_legales pl
USING (SELECT 2026 AS anio FROM dual) src
   ON (pl.anio = src.anio)
 WHEN MATCHED THEN UPDATE SET
        pl.igss_laboral_pct      = 0.0483,
        pl.igss_patronal_pct     = 0.1067,
        pl.salario_minimo        = 3591.85,
        pl.bonificacion_decreto  = 250.00,
        pl.deduccion_personal_isr = 48000.00
 WHEN NOT MATCHED THEN INSERT (anio, igss_laboral_pct, igss_patronal_pct,
        salario_minimo, bonificacion_decreto, deduccion_personal_isr, activo)
   VALUES (2026, 0.0483, 0.1067, 3591.85, 250.00, 48000.00, 1);

COMMIT;

-- 2. Reescribir el SP de calculo
CREATE OR REPLACE PROCEDURE sp_calcular_nomina(
    p_periodo_id      IN  NUMBER,
    p_total_empleados OUT NUMBER
) AS
    -- Datos del periodo a calcular
    v_anio              NUMBER;
    v_mes               NUMBER;
    v_tipo              VARCHAR2(20);
    v_fecha_inicio_per  DATE;
    v_fecha_fin_per     DATE;

    -- Parametros legales
    v_igss_pct          NUMBER(5,4);
    v_deduc_personal    NUMBER(12,2);
    v_bonif_decreto     NUMBER(12,2);

    -- Calculados por empleado
    v_nomina_enc_id     NUMBER;
    v_igss              NUMBER(12,2);
    v_renta_anual       NUMBER(12,2);
    v_renta_imponible   NUMBER(12,2);
    v_isr_anual         NUMBER(12,2);
    v_isr_mensual       NUMBER(12,2);
    v_total_ingresos    NUMBER(12,2);
    v_total_deduc       NUMBER(12,2);
    v_neto              NUMBER(12,2);
    v_total_prest       NUMBER(12,2);

    -- BONO14 / AGUINALDO
    v_inicio_legal      DATE;
    v_fin_legal         DATE;
    v_dias_trab         NUMBER;
    v_monto_proporcional NUMBER(12,2);

    CURSOR c_empleados IS
        SELECT e.empleado_id, e.empresa_id,
               c.salario_base, c.bonificacion, c.contrato_id, c.fecha_inicio,
               e.fecha_baja
          FROM empleados e
          JOIN contratos_laborales c ON c.empleado_id = e.empleado_id AND c.activo = 1
          JOIN periodos_nomina p     ON p.empresa_id = e.empresa_id
         WHERE p.periodo_id = p_periodo_id
           AND e.estado = 'ACTIVO';
BEGIN
    p_total_empleados := 0;

    SELECT anio, mes, tipo_periodo, fecha_inicio, fecha_fin
      INTO v_anio, v_mes, v_tipo, v_fecha_inicio_per, v_fecha_fin_per
      FROM periodos_nomina
     WHERE periodo_id = p_periodo_id;

    SELECT igss_laboral_pct, deduccion_personal_isr, bonificacion_decreto
      INTO v_igss_pct, v_deduc_personal, v_bonif_decreto
      FROM parametros_legales WHERE anio = v_anio;

    -- Limpia detalles previos del periodo (recalculo)
    DELETE FROM cuotas_prestamo
     WHERE nomina_enc_id IN (SELECT nomina_enc_id FROM nomina_encabezado WHERE periodo_id = p_periodo_id);
    DELETE FROM nomina_encabezado WHERE periodo_id = p_periodo_id;

    FOR emp IN c_empleados LOOP
        v_total_ingresos := 0; v_total_deduc := 0; v_neto := 0;
        v_igss := 0; v_isr_mensual := 0; v_total_prest := 0;

        -- ════════════════════════════════════════════════════════════
        IF v_tipo = 'MENSUAL' THEN
        -- ════════════════════════════════════════════════════════════
            v_igss := ROUND(emp.salario_base * v_igss_pct, 2);

            -- Bonificacion incentivo NO entra a la base imponible ISR.
            v_renta_anual     := emp.salario_base * 12;
            v_renta_imponible := v_renta_anual - v_deduc_personal - (v_igss * 12);

            IF v_renta_imponible <= 0 THEN
                v_isr_anual := 0;
            ELSIF v_renta_imponible <= 300000 THEN
                v_isr_anual := v_renta_imponible * 0.05;
            ELSE
                v_isr_anual := 15000 + (v_renta_imponible - 300000) * 0.07;
            END IF;
            v_isr_mensual := ROUND(v_isr_anual / 12, 2);

            SELECT NVL(SUM(cuota_mensual), 0) INTO v_total_prest
              FROM prestamos
             WHERE empleado_id = emp.empleado_id AND estado = 'VIGENTE';

            v_total_ingresos := emp.salario_base + emp.bonificacion;
            v_total_deduc    := v_igss + v_isr_mensual + v_total_prest;
            v_neto           := v_total_ingresos - v_total_deduc;

            INSERT INTO nomina_encabezado (periodo_id, empleado_id, salario_base,
                    total_ingresos, total_deducciones, salario_neto, estado)
            VALUES (p_periodo_id, emp.empleado_id, emp.salario_base,
                    v_total_ingresos, v_total_deduc, v_neto, 'CALCULADO')
            RETURNING nomina_enc_id INTO v_nomina_enc_id;

            INSERT INTO nomina_detalle (nomina_enc_id, tipo_movimiento, concepto, monto, es_calculado)
            VALUES (v_nomina_enc_id, 'I', 'Salario base', emp.salario_base, 1);
            INSERT INTO nomina_detalle (nomina_enc_id, tipo_movimiento, concepto, monto, es_calculado)
            VALUES (v_nomina_enc_id, 'I', 'Bonificacion incentivo (Decreto 78-89)', emp.bonificacion, 1);
            INSERT INTO nomina_detalle (nomina_enc_id, tipo_movimiento, concepto, monto, es_calculado)
            VALUES (v_nomina_enc_id, 'D', 'IGSS Laboral (4.83%)', v_igss, 1);

            IF v_isr_mensual > 0 THEN
                INSERT INTO nomina_detalle (nomina_enc_id, tipo_movimiento, concepto, monto, es_calculado)
                VALUES (v_nomina_enc_id, 'D', 'ISR sobre rentas en relacion de dependencia', v_isr_mensual, 1);
            END IF;

            -- Prestamos
            FOR pr IN (SELECT prestamo_id, descripcion, cuota_mensual, cuotas_pagadas, numero_cuotas
                         FROM prestamos
                        WHERE empleado_id = emp.empleado_id AND estado = 'VIGENTE') LOOP
                INSERT INTO nomina_detalle (nomina_enc_id, tipo_movimiento, concepto, monto, es_calculado, referencia)
                VALUES (v_nomina_enc_id, 'D', 'Prestamo: ' || pr.descripcion, pr.cuota_mensual, 0,
                        'Cuota ' || (pr.cuotas_pagadas + 1) || '/' || pr.numero_cuotas);
                INSERT INTO cuotas_prestamo (prestamo_id, nomina_enc_id, numero_cuota, monto)
                VALUES (pr.prestamo_id, v_nomina_enc_id, pr.cuotas_pagadas + 1, pr.cuota_mensual);
                UPDATE prestamos
                   SET cuotas_pagadas  = cuotas_pagadas + 1,
                       saldo_pendiente = saldo_pendiente - pr.cuota_mensual,
                       estado = CASE WHEN cuotas_pagadas + 1 >= numero_cuotas THEN 'PAGADO' ELSE estado END
                 WHERE prestamo_id = pr.prestamo_id;
            END LOOP;

        -- ════════════════════════════════════════════════════════════
        ELSIF v_tipo = 'QUINCENAL' THEN
        -- ════════════════════════════════════════════════════════════
            -- Quincenal: medio salario + media bonificacion, sin descuentos legales
            -- (los descuentos se aplican en la nomina mensual de cierre).
            v_total_ingresos := ROUND(emp.salario_base / 2, 2) + ROUND(emp.bonificacion / 2, 2);
            v_neto := v_total_ingresos;

            INSERT INTO nomina_encabezado (periodo_id, empleado_id, salario_base,
                    total_ingresos, total_deducciones, salario_neto, estado)
            VALUES (p_periodo_id, emp.empleado_id, emp.salario_base,
                    v_total_ingresos, 0, v_neto, 'CALCULADO')
            RETURNING nomina_enc_id INTO v_nomina_enc_id;

            INSERT INTO nomina_detalle (nomina_enc_id, tipo_movimiento, concepto, monto, es_calculado)
            VALUES (v_nomina_enc_id, 'I', 'Salario quincenal (50%)', ROUND(emp.salario_base / 2, 2), 1);
            INSERT INTO nomina_detalle (nomina_enc_id, tipo_movimiento, concepto, monto, es_calculado)
            VALUES (v_nomina_enc_id, 'I', 'Bonificacion quincenal (50%)', ROUND(emp.bonificacion / 2, 2), 1);

        -- ════════════════════════════════════════════════════════════
        ELSIF v_tipo = 'BONO14' THEN
        -- ════════════════════════════════════════════════════════════
            -- Periodo legal: 1 julio anio anterior - 30 junio anio actual.
            -- Decreto 42-92. EXENTO de IGSS e ISR.
            v_inicio_legal := TO_DATE((v_anio - 1) || '-07-01', 'YYYY-MM-DD');
            v_fin_legal    := TO_DATE(v_anio || '-06-30', 'YYYY-MM-DD');

            v_dias_trab := LEAST(v_fin_legal,
                                 NVL(emp.fecha_baja, v_fin_legal))
                           - GREATEST(v_inicio_legal, emp.fecha_inicio) + 1;

            IF v_dias_trab < 0 THEN v_dias_trab := 0; END IF;
            IF v_dias_trab > 365 THEN v_dias_trab := 365; END IF;

            v_monto_proporcional := ROUND((emp.salario_base * v_dias_trab) / 365, 2);
            v_total_ingresos := v_monto_proporcional;
            v_neto := v_monto_proporcional;

            INSERT INTO nomina_encabezado (periodo_id, empleado_id, salario_base,
                    total_ingresos, total_deducciones, salario_neto, estado)
            VALUES (p_periodo_id, emp.empleado_id, emp.salario_base,
                    v_total_ingresos, 0, v_neto, 'CALCULADO')
            RETURNING nomina_enc_id INTO v_nomina_enc_id;

            INSERT INTO nomina_detalle (nomina_enc_id, tipo_movimiento, concepto, monto, es_calculado, referencia)
            VALUES (v_nomina_enc_id, 'I', 'Bono 14 proporcional (Decreto 42-92)',
                    v_monto_proporcional, 1,
                    v_dias_trab || ' dias / 365');

        -- ════════════════════════════════════════════════════════════
        ELSIF v_tipo = 'AGUINALDO' THEN
        -- ════════════════════════════════════════════════════════════
            -- Periodo legal: 1 diciembre anio anterior - 30 noviembre anio actual.
            -- Codigo de Trabajo art. 137. EXENTO de IGSS e ISR.
            v_inicio_legal := TO_DATE((v_anio - 1) || '-12-01', 'YYYY-MM-DD');
            v_fin_legal    := TO_DATE(v_anio || '-11-30', 'YYYY-MM-DD');

            v_dias_trab := LEAST(v_fin_legal,
                                 NVL(emp.fecha_baja, v_fin_legal))
                           - GREATEST(v_inicio_legal, emp.fecha_inicio) + 1;

            IF v_dias_trab < 0 THEN v_dias_trab := 0; END IF;
            IF v_dias_trab > 365 THEN v_dias_trab := 365; END IF;

            v_monto_proporcional := ROUND((emp.salario_base * v_dias_trab) / 365, 2);
            v_total_ingresos := v_monto_proporcional;
            v_neto := v_monto_proporcional;

            INSERT INTO nomina_encabezado (periodo_id, empleado_id, salario_base,
                    total_ingresos, total_deducciones, salario_neto, estado)
            VALUES (p_periodo_id, emp.empleado_id, emp.salario_base,
                    v_total_ingresos, 0, v_neto, 'CALCULADO')
            RETURNING nomina_enc_id INTO v_nomina_enc_id;

            INSERT INTO nomina_detalle (nomina_enc_id, tipo_movimiento, concepto, monto, es_calculado, referencia)
            VALUES (v_nomina_enc_id, 'I', 'Aguinaldo proporcional (Codigo de Trabajo art. 137)',
                    v_monto_proporcional, 1,
                    v_dias_trab || ' dias / 365');
        END IF;

        p_total_empleados := p_total_empleados + 1;
    END LOOP;

    UPDATE periodos_nomina SET estado = 'CALCULADO' WHERE periodo_id = p_periodo_id;
    COMMIT;
END;
/

PROMPT sp_calcular_nomina actualizado con soporte de MENSUAL/QUINCENAL/BONO14/AGUINALDO
