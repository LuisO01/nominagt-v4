-- ============================================================================
-- NominaGT v4 - Stored Procedures de calculo
-- ============================================================================

-- ============================================================================
-- SP: Calcular nomina mensual completa
-- ============================================================================
CREATE OR REPLACE PROCEDURE sp_calcular_nomina(
    p_periodo_id IN NUMBER,
    p_total_empleados OUT NUMBER
) AS
    CURSOR c_empleados IS
        SELECT e.empleado_id, c.salario_base, c.bonificacion, c.contrato_id
        FROM empleados e
        JOIN contratos_laborales c ON c.empleado_id = e.empleado_id AND c.activo = 1
        JOIN periodos_nomina p ON p.empresa_id = e.empresa_id
        WHERE p.periodo_id = p_periodo_id
          AND e.estado = 'ACTIVO';

    v_anio              NUMBER;
    v_igss_pct          NUMBER(5,4);
    v_deduc_personal    NUMBER(12,2);
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
BEGIN
    p_total_empleados := 0;

    SELECT anio INTO v_anio FROM periodos_nomina WHERE periodo_id = p_periodo_id;

    SELECT igss_laboral_pct, deduccion_personal_isr
      INTO v_igss_pct, v_deduc_personal
      FROM parametros_legales WHERE anio = v_anio;

    DELETE FROM nomina_encabezado WHERE periodo_id = p_periodo_id;

    FOR emp IN c_empleados LOOP
        -- Calculos
        v_igss          := ROUND(emp.salario_base * v_igss_pct, 2);
        v_renta_anual   := emp.salario_base * 12;
        v_renta_imponible := v_renta_anual - v_deduc_personal - (v_igss * 12);

        IF v_renta_imponible <= 0 THEN
            v_isr_anual := 0;
        ELSIF v_renta_imponible <= 300000 THEN
            v_isr_anual := v_renta_imponible * 0.05;
        ELSE
            v_isr_anual := 15000 + (v_renta_imponible - 300000) * 0.07;
        END IF;
        v_isr_mensual := ROUND(v_isr_anual / 12, 2);

        -- Prestamos vigentes
        SELECT NVL(SUM(cuota_mensual), 0) INTO v_total_prest
        FROM prestamos
        WHERE empleado_id = emp.empleado_id AND estado = 'VIGENTE';

        v_total_ingresos := emp.salario_base + emp.bonificacion;
        v_total_deduc    := v_igss + v_isr_mensual + v_total_prest;
        v_neto           := v_total_ingresos - v_total_deduc;

        -- Encabezado
        INSERT INTO nomina_encabezado (periodo_id, empleado_id, salario_base,
                total_ingresos, total_deducciones, salario_neto, estado)
        VALUES (p_periodo_id, emp.empleado_id, emp.salario_base,
                v_total_ingresos, v_total_deduc, v_neto, 'CALCULADO')
        RETURNING nomina_enc_id INTO v_nomina_enc_id;

        -- Detalles - Ingresos
        INSERT INTO nomina_detalle (nomina_enc_id, tipo_movimiento, concepto, monto, es_calculado)
        VALUES (v_nomina_enc_id, 'I', 'Salario Base', emp.salario_base, 1);

        INSERT INTO nomina_detalle (nomina_enc_id, tipo_movimiento, concepto, monto, es_calculado)
        VALUES (v_nomina_enc_id, 'I', 'Bonificacion Incentivo', emp.bonificacion, 1);

        -- Detalles - Deducciones
        INSERT INTO nomina_detalle (nomina_enc_id, tipo_movimiento, concepto, monto, es_calculado)
        VALUES (v_nomina_enc_id, 'D', 'IGSS Laboral (4.83%)', v_igss, 1);

        IF v_isr_mensual > 0 THEN
            INSERT INTO nomina_detalle (nomina_enc_id, tipo_movimiento, concepto, monto, es_calculado)
            VALUES (v_nomina_enc_id, 'D', 'ISR Mensual', v_isr_mensual, 1);
        END IF;

        -- Detalles - Prestamos
        FOR pr IN (SELECT prestamo_id, descripcion, cuota_mensual, cuotas_pagadas, numero_cuotas
                     FROM prestamos
                     WHERE empleado_id = emp.empleado_id AND estado = 'VIGENTE') LOOP

            INSERT INTO nomina_detalle (nomina_enc_id, tipo_movimiento, concepto, monto, es_calculado, referencia)
            VALUES (v_nomina_enc_id, 'D', 'Prestamo: ' || pr.descripcion,
                    pr.cuota_mensual, 0,
                    'Cuota ' || (pr.cuotas_pagadas + 1) || '/' || pr.numero_cuotas);

            INSERT INTO cuotas_prestamo (prestamo_id, nomina_enc_id, numero_cuota, monto)
            VALUES (pr.prestamo_id, v_nomina_enc_id, pr.cuotas_pagadas + 1, pr.cuota_mensual);

            UPDATE prestamos
               SET cuotas_pagadas  = cuotas_pagadas + 1,
                   saldo_pendiente = saldo_pendiente - pr.cuota_mensual,
                   estado = CASE WHEN cuotas_pagadas + 1 >= numero_cuotas
                                 THEN 'PAGADO' ELSE estado END
             WHERE prestamo_id = pr.prestamo_id;
        END LOOP;

        p_total_empleados := p_total_empleados + 1;
    END LOOP;

    UPDATE periodos_nomina SET estado = 'CALCULADO' WHERE periodo_id = p_periodo_id;
    COMMIT;
END;
/

-- ============================================================================
-- SP: Aprobar nomina (cambiar estado)
-- ============================================================================
CREATE OR REPLACE PROCEDURE sp_aprobar_nomina(
    p_periodo_id IN NUMBER,
    p_usuario    IN VARCHAR2
) AS
BEGIN
    UPDATE periodos_nomina
       SET estado = 'APROBADO',
           aprobado_por = p_usuario,
           aprobado_en = SYSTIMESTAMP
     WHERE periodo_id = p_periodo_id AND estado = 'CALCULADO';

    UPDATE nomina_encabezado SET estado = 'APROBADO' WHERE periodo_id = p_periodo_id;
    COMMIT;
END;
/

PROMPT Stored Procedures creados: sp_calcular_nomina, sp_aprobar_nomina
