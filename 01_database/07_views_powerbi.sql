-- ============================================================================
-- NominaGT v4 - Vistas optimizadas para Power BI Desktop
-- Power BI conecta a Oracle via ODBC y consulta estas vistas directamente.
-- ============================================================================

-- VW_PBI_EMPLEADOS
CREATE OR REPLACE VIEW vw_pbi_empleados AS
SELECT
    e.empleado_id,
    e.codigo_empleado                                          AS codigo,
    e.primer_nombre || ' ' || COALESCE(e.segundo_nombre,'') ||
        ' ' || e.primer_apellido || ' ' ||
        COALESCE(e.segundo_apellido,'')                        AS nombre_completo,
    e.dpi, e.nit, e.fecha_nacimiento, e.fecha_ingreso,
    TRUNC(MONTHS_BETWEEN(SYSDATE, e.fecha_ingreso) / 12)       AS anios_servicio,
    e.genero, e.estado_civil, e.estado,
    emp.razon_social   AS empresa,
    suc.nombre         AS sucursal,
    dep.nombre         AS departamento,
    pue.nombre         AS puesto,
    c.salario_base, c.bonificacion, c.tipo_contrato, c.forma_pago
FROM empleados e
LEFT JOIN empresas       emp ON emp.empresa_id      = e.empresa_id
LEFT JOIN sucursales     suc ON suc.sucursal_id     = e.sucursal_id
LEFT JOIN departamentos  dep ON dep.departamento_id = e.departamento_id
LEFT JOIN puestos        pue ON pue.puesto_id       = e.puesto_id
LEFT JOIN contratos_laborales c ON c.empleado_id    = e.empleado_id AND c.activo = 1;

-- VW_PBI_NOMINA_MENSUAL
CREATE OR REPLACE VIEW vw_pbi_nomina_mensual AS
SELECT
    p.periodo_id, p.anio, p.mes,
    TO_CHAR(TO_DATE(p.anio || '-' || LPAD(p.mes,2,'0') || '-01','YYYY-MM-DD'), 'fmMonth') AS nombre_mes,
    p.tipo_periodo, p.fecha_pago,
    p.estado AS estado_periodo,
    ne.nomina_enc_id, ne.empleado_id,
    e.codigo_empleado,
    e.primer_nombre || ' ' || e.primer_apellido AS nombre_empleado,
    dep.nombre AS departamento,
    pue.nombre AS puesto,
    ne.salario_base, ne.total_ingresos, ne.total_deducciones, ne.salario_neto,
    NVL((SELECT SUM(monto) FROM nomina_detalle nd
          WHERE nd.nomina_enc_id = ne.nomina_enc_id AND nd.concepto LIKE '%IGSS%'), 0) AS igss,
    NVL((SELECT SUM(monto) FROM nomina_detalle nd
          WHERE nd.nomina_enc_id = ne.nomina_enc_id AND nd.concepto LIKE '%ISR%'), 0) AS isr,
    NVL((SELECT SUM(monto) FROM nomina_detalle nd
          WHERE nd.nomina_enc_id = ne.nomina_enc_id AND nd.concepto LIKE '%Bonificacion%'), 0) AS bonificacion,
    NVL((SELECT SUM(monto) FROM nomina_detalle nd
          WHERE nd.nomina_enc_id = ne.nomina_enc_id AND nd.concepto LIKE '%Prestamo%'), 0) AS descuento_prestamos
FROM periodos_nomina p
JOIN nomina_encabezado ne ON ne.periodo_id = p.periodo_id
JOIN empleados          e ON e.empleado_id = ne.empleado_id
LEFT JOIN departamentos dep ON dep.departamento_id = e.departamento_id
LEFT JOIN puestos       pue ON pue.puesto_id      = e.puesto_id;

-- VW_PBI_RESUMEN_MENSUAL
CREATE OR REPLACE VIEW vw_pbi_resumen_mensual AS
SELECT
    p.empresa_id, p.anio, p.mes,
    TO_CHAR(TO_DATE(p.anio || '-' || LPAD(p.mes,2,'0') || '-01','YYYY-MM-DD'), 'fmMonth') AS nombre_mes,
    p.tipo_periodo,
    COUNT(ne.nomina_enc_id)    AS total_empleados,
    SUM(ne.total_ingresos)     AS suma_ingresos,
    SUM(ne.total_deducciones)  AS suma_deducciones,
    SUM(ne.salario_neto)       AS suma_neto,
    AVG(ne.salario_neto)       AS promedio_neto,
    MIN(ne.salario_neto)       AS minimo_neto,
    MAX(ne.salario_neto)       AS maximo_neto
FROM periodos_nomina p
JOIN nomina_encabezado ne ON ne.periodo_id = p.periodo_id
GROUP BY p.empresa_id, p.anio, p.mes, p.tipo_periodo;

-- VW_PBI_HISTORICO_EMPLEADO
CREATE OR REPLACE VIEW vw_pbi_historico_empleado AS
SELECT
    e.empleado_id, e.codigo_empleado,
    e.primer_nombre || ' ' || e.primer_apellido AS nombre,
    p.anio, p.mes,
    TO_DATE(p.anio || '-' || LPAD(p.mes,2,'0') || '-01', 'YYYY-MM-DD') AS fecha_periodo,
    ne.salario_base, ne.total_ingresos, ne.total_deducciones, ne.salario_neto
FROM empleados e
JOIN nomina_encabezado ne ON ne.empleado_id = e.empleado_id
JOIN periodos_nomina   p  ON p.periodo_id   = ne.periodo_id
ORDER BY e.empleado_id, p.anio, p.mes;

-- VW_PBI_DETALLE_CONCEPTOS
CREATE OR REPLACE VIEW vw_pbi_detalle_conceptos AS
SELECT
    p.anio, p.mes, p.tipo_periodo,
    e.codigo_empleado,
    e.primer_nombre || ' ' || e.primer_apellido AS empleado,
    nd.concepto,
    CASE nd.tipo_movimiento WHEN 'I' THEN 'Ingreso' ELSE 'Deduccion' END AS tipo,
    nd.monto, nd.es_calculado, nd.referencia
FROM nomina_detalle nd
JOIN nomina_encabezado ne ON ne.nomina_enc_id = nd.nomina_enc_id
JOIN periodos_nomina    p ON p.periodo_id     = ne.periodo_id
JOIN empleados          e ON e.empleado_id    = ne.empleado_id;

-- VW_PBI_PRESTAMOS
CREATE OR REPLACE VIEW vw_pbi_prestamos AS
SELECT
    pr.prestamo_id,
    e.codigo_empleado,
    e.primer_nombre || ' ' || e.primer_apellido AS empleado,
    dep.nombre AS departamento,
    pr.descripcion,
    pr.monto_original, pr.saldo_pendiente, pr.cuota_mensual,
    pr.numero_cuotas, pr.cuotas_pagadas,
    pr.numero_cuotas - pr.cuotas_pagadas AS cuotas_restantes,
    pr.estado, pr.fecha_inicio
FROM prestamos pr
JOIN empleados      e   ON e.empleado_id      = pr.empleado_id
LEFT JOIN departamentos dep ON dep.departamento_id = e.departamento_id;

PROMPT Vistas Power BI creadas: 6 vistas
