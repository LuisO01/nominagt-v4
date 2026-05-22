-- ============================================================================
-- NominaGT v4 - Fix ORA-01821: formato de fecha no reconocido
--
-- Las vistas vw_pbi_nomina_mensual y vw_pbi_resumen_mensual usaban:
--    TO_CHAR(TO_DATE(p.mes,'MM'), 'TMMonth')
-- que falla porque TO_DATE('4','MM') depende de NLS_DATE_FORMAT.
-- Se reemplaza por una construccion completa de fecha YYYYMMDD.
--
-- Aplicar UNA VEZ en SQL Developer conectado como NOMINAGT.
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
          WHERE nd.nomina_enc_id = ne.nomina_enc_id AND nd.concepto LIKE '%IGSS%'), 0) AS igss,
    NVL((SELECT SUM(monto) FROM nomina_detalle nd
          WHERE nd.nomina_enc_id = ne.nomina_enc_id AND nd.concepto LIKE '%ISR%'), 0)  AS isr
FROM periodos_nomina p
JOIN nomina_encabezado ne ON ne.periodo_id = p.periodo_id
JOIN empleados          e ON e.empleado_id = ne.empleado_id
LEFT JOIN departamentos dep ON dep.departamento_id = e.departamento_id
LEFT JOIN puestos       pue ON pue.puesto_id      = e.puesto_id;

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

-- Verificacion (no debe lanzar ORA-01821):
SELECT * FROM vw_pbi_resumen_mensual WHERE empresa_id = 1 ORDER BY anio, mes;
