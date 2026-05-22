-- ============================================================================
-- NominaGT v4 - Indices para optimizacion
-- ============================================================================

-- Empleados
CREATE INDEX idx_emp_estado     ON empleados(empresa_id, estado);
CREATE INDEX idx_emp_nombres    ON empleados(LOWER(primer_nombre), LOWER(primer_apellido));
CREATE INDEX idx_emp_codigo     ON empleados(LOWER(codigo_empleado));
CREATE INDEX idx_emp_dpi        ON empleados(dpi);

-- Nomina (reportes mensuales)
CREATE INDEX idx_per_anio_mes   ON periodos_nomina(empresa_id, anio, mes);
CREATE INDEX idx_ne_periodo     ON nomina_encabezado(periodo_id);
CREATE INDEX idx_nd_enc         ON nomina_detalle(nomina_enc_id);
CREATE INDEX idx_nd_concepto    ON nomina_detalle(concepto);

-- Prestamos
CREATE INDEX idx_pres_emp_estado ON prestamos(empleado_id, estado);

-- Auditoria
CREATE INDEX idx_aud_fecha      ON auditoria(fecha DESC);
CREATE INDEX idx_aud_usuario    ON auditoria(usuario, fecha DESC);
CREATE INDEX idx_aud_tabla      ON auditoria(tabla, registro_id);

-- Usuarios y seguridad
CREATE INDEX idx_usr_email      ON usuarios(LOWER(email));
CREATE INDEX idx_usr_token      ON usuarios(refresh_token);

PROMPT Indices creados: 14 indices
