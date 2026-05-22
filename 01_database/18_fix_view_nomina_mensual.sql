-- ============================================================================
-- NominaGT v4 - Fix: agregar BONIFICACION y DESCUENTO_PRESTAMOS a la vista
--
-- El generador de Excel/PDF pide estas columnas que no estaban en
-- vw_pbi_nomina_mensual. Se agregan como subqueries a nomina_detalle, mismo
-- patron que IGSS e ISR.
-- ============================================================================

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
          WHERE nd.nomina_enc_id = ne.nomina_enc_id
            AND UPPER(nd.concepto) LIKE '%BONIFICACION%'), 0) AS bonificacion,
    NVL((SELECT SUM(monto) FROM nomina_detalle nd
          WHERE nd.nomina_enc_id = ne.nomina_enc_id
            AND UPPER(nd.concepto) LIKE '%IGSS%'), 0) AS igss,
    NVL((SELECT SUM(monto) FROM nomina_detalle nd
          WHERE nd.nomina_enc_id = ne.nomina_enc_id
            AND UPPER(nd.concepto) LIKE '%ISR%'), 0) AS isr,
    NVL((SELECT SUM(monto) FROM nomina_detalle nd
          WHERE nd.nomina_enc_id = ne.nomina_enc_id
            AND UPPER(nd.concepto) LIKE '%PRESTAMO%'), 0) AS descuento_prestamos
FROM periodos_nomina p
JOIN nomina_encabezado ne ON ne.periodo_id = p.periodo_id
JOIN empleados          e ON e.empleado_id = ne.empleado_id
LEFT JOIN departamentos dep ON dep.departamento_id = e.departamento_id
LEFT JOIN puestos       pue ON pue.puesto_id      = e.puesto_id;

PROMPT Vista vw_pbi_nomina_mensual actualizada con BONIFICACION y DESCUENTO_PRESTAMOS.
