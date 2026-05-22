-- ============================================================================
-- NominaGT v4 - Datos iniciales
-- ============================================================================

-- EMPRESAS
INSERT INTO empresas (razon_social, nit, direccion, telefono, email, representante_legal)
VALUES ('CorpoDemo GT, S.A.', '12345678-9', 'Zona 10, Guatemala', '50223334455', 'admin@corpodemo.gt', 'Juan Perez');

-- SUCURSALES
INSERT INTO sucursales (empresa_id, nombre, codigo, direccion)
VALUES (1, 'Casa Matriz', 'CM', 'Zona 10, Guatemala');

INSERT INTO sucursales (empresa_id, nombre, codigo, direccion)
VALUES (1, 'Sucursal Zona 1', 'Z1', 'Zona 1, Guatemala');

-- ROLES
INSERT INTO roles (nombre, descripcion, es_sistema) VALUES ('ADMIN',    'Administrador del sistema', 1);
INSERT INTO roles (nombre, descripcion, es_sistema) VALUES ('RRHH',     'Recursos Humanos',          1);
INSERT INTO roles (nombre, descripcion, es_sistema) VALUES ('NOMINA',   'Operador de Nomina',        1);
INSERT INTO roles (nombre, descripcion, es_sistema) VALUES ('EMPLEADO', 'Empleado autoservicio',     1);
INSERT INTO roles (nombre, descripcion, es_sistema) VALUES ('AUDITOR',  'Auditor de bitacoras',      1);

-- DEPARTAMENTOS
INSERT INTO departamentos (empresa_id, nombre, codigo) VALUES (1, 'Gerencia General',          'GER');
INSERT INTO departamentos (empresa_id, nombre, codigo) VALUES (1, 'Finanzas y Contabilidad',   'FIN');
INSERT INTO departamentos (empresa_id, nombre, codigo) VALUES (1, 'Tecnologia',                'TIK');
INSERT INTO departamentos (empresa_id, nombre, codigo) VALUES (1, 'Operaciones',               'OPR');
INSERT INTO departamentos (empresa_id, nombre, codigo) VALUES (1, 'Recursos Humanos',          'RHH');
INSERT INTO departamentos (empresa_id, nombre, codigo) VALUES (1, 'Ventas',                    'VTA');

-- PUESTOS
INSERT INTO puestos (empresa_id, nombre, codigo, salario_minimo, salario_maximo)
VALUES (1, 'Gerente General',         'GG',   25000, 45000);
INSERT INTO puestos (empresa_id, nombre, codigo, salario_minimo, salario_maximo)
VALUES (1, 'Gerente Financiero',      'GFIN', 18000, 30000);
INSERT INTO puestos (empresa_id, nombre, codigo, salario_minimo, salario_maximo)
VALUES (1, 'Analista Desarrollador',  'ADEV',  8000, 18000);
INSERT INTO puestos (empresa_id, nombre, codigo, salario_minimo, salario_maximo)
VALUES (1, 'Contador',                'CONT',  7000, 14000);
INSERT INTO puestos (empresa_id, nombre, codigo, salario_minimo, salario_maximo)
VALUES (1, 'Asistente Administrativo','ASST',  3500,  6000);
INSERT INTO puestos (empresa_id, nombre, codigo, salario_minimo, salario_maximo)
VALUES (1, 'Ejecutivo de Ventas',     'EJVT',  4000,  8000);

-- PARAMETROS LEGALES 2026
INSERT INTO parametros_legales (anio, igss_laboral_pct, igss_patronal_pct, salario_minimo, bonificacion_decreto, deduccion_personal_isr)
VALUES (2026, 0.0483, 0.1267, 3166.28, 250.00, 48000.00);

-- TABLA ISR 2026
INSERT INTO tablas_isr (anio, rango_min, rango_max, impuesto_fijo, porcentaje, sobre_excedente_de)
VALUES (2026, 0.01, 300000, 0, 0.05, 0);
INSERT INTO tablas_isr (anio, rango_min, rango_max, impuesto_fijo, porcentaje, sobre_excedente_de)
VALUES (2026, 300000.01, NULL, 15000, 0.07, 300000);

-- TIPOS DE INGRESO
INSERT INTO tipos_ingreso (codigo, nombre, afecta_igss, afecta_isr, es_calculado)
VALUES ('SALARIO',  'Salario Base',          1, 1, 1);
INSERT INTO tipos_ingreso (codigo, nombre, afecta_igss, afecta_isr, es_calculado)
VALUES ('BONI_INC', 'Bonificacion Incentivo',0, 0, 1);
INSERT INTO tipos_ingreso (codigo, nombre, afecta_igss, afecta_isr, es_calculado)
VALUES ('BONO14',   'Bono 14',               0, 1, 1);
INSERT INTO tipos_ingreso (codigo, nombre, afecta_igss, afecta_isr, es_calculado)
VALUES ('AGUINALDO','Aguinaldo',             0, 1, 1);
INSERT INTO tipos_ingreso (codigo, nombre, afecta_igss, afecta_isr, es_calculado)
VALUES ('HORAS_EX', 'Horas Extras',          1, 1, 0);

-- TIPOS DE DEDUCCION
INSERT INTO tipos_deduccion (codigo, nombre, es_legal, es_calculado)
VALUES ('IGSS_LAB', 'IGSS Laboral 4.83%', 1, 1);
INSERT INTO tipos_deduccion (codigo, nombre, es_legal, es_calculado)
VALUES ('ISR',      'ISR Mensual',        1, 1);
INSERT INTO tipos_deduccion (codigo, nombre, es_legal, es_calculado)
VALUES ('PRESTAMO', 'Prestamo personal',  0, 0);
INSERT INTO tipos_deduccion (codigo, nombre, es_legal, es_calculado)
VALUES ('ANTICIPO', 'Anticipo de salario',0, 0);

-- PERMISOS POR ROL
INSERT INTO rol_permisos (rol_id, modulo, permiso) SELECT rol_id, 'EMPLEADOS',     'TODO'   FROM roles WHERE nombre='ADMIN';
INSERT INTO rol_permisos (rol_id, modulo, permiso) SELECT rol_id, 'NOMINA',        'TODO'   FROM roles WHERE nombre='ADMIN';
INSERT INTO rol_permisos (rol_id, modulo, permiso) SELECT rol_id, 'REPORTES',      'TODO'   FROM roles WHERE nombre='ADMIN';
INSERT INTO rol_permisos (rol_id, modulo, permiso) SELECT rol_id, 'USUARIOS',      'TODO'   FROM roles WHERE nombre='ADMIN';
INSERT INTO rol_permisos (rol_id, modulo, permiso) SELECT rol_id, 'AUDITORIA',     'LEER'   FROM roles WHERE nombre='ADMIN';

INSERT INTO rol_permisos (rol_id, modulo, permiso) SELECT rol_id, 'EMPLEADOS',     'TODO'   FROM roles WHERE nombre='RRHH';
INSERT INTO rol_permisos (rol_id, modulo, permiso) SELECT rol_id, 'DEPARTAMENTOS', 'LEER'   FROM roles WHERE nombre='RRHH';

INSERT INTO rol_permisos (rol_id, modulo, permiso) SELECT rol_id, 'NOMINA',        'TODO'   FROM roles WHERE nombre='NOMINA';
INSERT INTO rol_permisos (rol_id, modulo, permiso) SELECT rol_id, 'REPORTES',      'TODO'   FROM roles WHERE nombre='NOMINA';
INSERT INTO rol_permisos (rol_id, modulo, permiso) SELECT rol_id, 'EMPLEADOS',     'LEER'   FROM roles WHERE nombre='NOMINA';

INSERT INTO rol_permisos (rol_id, modulo, permiso) SELECT rol_id, 'MI_PERFIL',     'LEER'   FROM roles WHERE nombre='EMPLEADO';
INSERT INTO rol_permisos (rol_id, modulo, permiso) SELECT rol_id, 'MIS_BOLETAS',   'LEER'   FROM roles WHERE nombre='EMPLEADO';

INSERT INTO rol_permisos (rol_id, modulo, permiso) SELECT rol_id, 'AUDITORIA',     'LEER'   FROM roles WHERE nombre='AUDITOR';

-- EMPLEADOS DEMO (6 empleados)
INSERT INTO empleados (empresa_id, sucursal_id, departamento_id, puesto_id, codigo_empleado,
    primer_nombre, segundo_nombre, primer_apellido, segundo_apellido, dpi, nit, num_afiliacion_igss,
    fecha_nacimiento, genero, estado_civil, telefono, email_corporativo, fecha_ingreso, estado)
VALUES (1, 1, 1, 1, 'EMP-001', 'Carlos', 'Eduardo', 'Lopez', 'Mendez',
    '2580199001001', '98765432-1', 'AF-10001', DATE '1990-03-15', 'M', 'CASADO',
    '50212345678', 'carlos.lopez@corpodemo.gt', DATE '2024-01-15', 'ACTIVO');

INSERT INTO empleados (empresa_id, sucursal_id, departamento_id, puesto_id, codigo_empleado,
    primer_nombre, segundo_nombre, primer_apellido, segundo_apellido, dpi, nit, num_afiliacion_igss,
    fecha_nacimiento, genero, estado_civil, telefono, email_corporativo, fecha_ingreso, estado)
VALUES (1, 1, 2, 2, 'EMP-002', 'Maria', 'Fernanda', 'Garcia', 'Rosales',
    '2580199502002', '87654321-0', 'AF-10002', DATE '1995-07-22', 'F', 'SOLTERA',
    '50287654321', 'maria.garcia@corpodemo.gt', DATE '2024-03-01', 'ACTIVO');

INSERT INTO empleados (empresa_id, sucursal_id, departamento_id, puesto_id, codigo_empleado,
    primer_nombre, segundo_nombre, primer_apellido, segundo_apellido, dpi, nit, num_afiliacion_igss,
    fecha_nacimiento, genero, estado_civil, telefono, email_corporativo, fecha_ingreso, estado)
VALUES (1, 1, 3, 3, 'EMP-003', 'Jose', 'Antonio', 'Hernandez', 'Castillo',
    '2580198803003', '76543210-9', 'AF-10003', DATE '1988-11-08', 'M', 'CASADO',
    '50255512345', 'jose.hernandez@corpodemo.gt', DATE '2024-06-10', 'ACTIVO');

INSERT INTO empleados (empresa_id, sucursal_id, departamento_id, puesto_id, codigo_empleado,
    primer_nombre, segundo_nombre, primer_apellido, segundo_apellido, dpi, nit, num_afiliacion_igss,
    fecha_nacimiento, genero, estado_civil, telefono, email_corporativo, fecha_ingreso, estado)
VALUES (1, 2, 4, 4, 'EMP-004', 'Ana', 'Lucia', 'Morales', 'Perez',
    '2580199204004', '65432109-8', 'AF-10004', DATE '1992-01-30', 'F', 'SOLTERA',
    '50233344556', 'ana.morales@corpodemo.gt', DATE '2024-08-01', 'ACTIVO');

INSERT INTO empleados (empresa_id, sucursal_id, departamento_id, puesto_id, codigo_empleado,
    primer_nombre, segundo_nombre, primer_apellido, segundo_apellido, dpi, nit, num_afiliacion_igss,
    fecha_nacimiento, genero, estado_civil, telefono, email_corporativo, fecha_ingreso, estado)
VALUES (1, 2, 5, 5, 'EMP-005', 'Pedro', 'Jose', 'Ramirez', 'Soto',
    '2580200005005', '54321098-7', 'AF-10005', DATE '2000-06-12', 'M', 'SOLTERO',
    '50211122334', 'pedro.ramirez@corpodemo.gt', DATE '2025-01-15', 'ACTIVO');

INSERT INTO empleados (empresa_id, sucursal_id, departamento_id, puesto_id, codigo_empleado,
    primer_nombre, segundo_nombre, primer_apellido, segundo_apellido, dpi, nit, num_afiliacion_igss,
    fecha_nacimiento, genero, estado_civil, telefono, email_corporativo, fecha_ingreso, estado)
VALUES (1, 1, 6, 6, 'EMP-006', 'Luisa', 'Maria', 'Vasquez', 'Turcios',
    '2580199706006', '43210987-6', 'AF-10006', DATE '1997-09-25', 'F', 'CASADA',
    '50299887766', 'luisa.vasquez@corpodemo.gt', DATE '2025-04-01', 'ACTIVO');

-- CONTRATOS LABORALES
INSERT INTO contratos_laborales (empleado_id, tipo_contrato, fecha_inicio, salario_base, bonificacion, jornada_laboral, forma_pago, activo)
VALUES (1, 'INDEFINIDO', DATE '2024-01-15', 35000, 250, 'DIURNA', 'MENSUAL', 1);
INSERT INTO contratos_laborales (empleado_id, tipo_contrato, fecha_inicio, salario_base, bonificacion, jornada_laboral, forma_pago, activo)
VALUES (2, 'INDEFINIDO', DATE '2024-03-01', 22000, 250, 'DIURNA', 'MENSUAL', 1);
INSERT INTO contratos_laborales (empleado_id, tipo_contrato, fecha_inicio, salario_base, bonificacion, jornada_laboral, forma_pago, activo)
VALUES (3, 'INDEFINIDO', DATE '2024-06-10', 15000, 250, 'DIURNA', 'MENSUAL', 1);
INSERT INTO contratos_laborales (empleado_id, tipo_contrato, fecha_inicio, salario_base, bonificacion, jornada_laboral, forma_pago, activo)
VALUES (4, 'INDEFINIDO', DATE '2024-08-01', 10000, 250, 'DIURNA', 'MENSUAL', 1);
INSERT INTO contratos_laborales (empleado_id, tipo_contrato, fecha_inicio, salario_base, bonificacion, jornada_laboral, forma_pago, activo)
VALUES (5, 'PLAZO_FIJO', DATE '2025-01-15',  4000, 250, 'DIURNA', 'QUINCENAL', 1);
INSERT INTO contratos_laborales (empleado_id, tipo_contrato, fecha_inicio, salario_base, bonificacion, jornada_laboral, forma_pago, activo)
VALUES (6, 'INDEFINIDO', DATE '2025-04-01',  6000, 250, 'DIURNA', 'MENSUAL', 1);

-- CUENTAS BANCARIAS
INSERT INTO cuentas_bancarias (empleado_id, banco_nombre, banco_codigo, numero_cuenta, tipo_cuenta, es_principal)
VALUES (1, 'Banco Industrial', 'BI',  '001-234567-8', 'MONETARIA', 1);
INSERT INTO cuentas_bancarias (empleado_id, banco_nombre, banco_codigo, numero_cuenta, tipo_cuenta, es_principal)
VALUES (2, 'BAC Credomatic',   'BAC', '200-987654-1', 'MONETARIA', 1);
INSERT INTO cuentas_bancarias (empleado_id, banco_nombre, banco_codigo, numero_cuenta, tipo_cuenta, es_principal)
VALUES (3, 'BANTRAB',          'BTR', '050-112233-4', 'AHORRO',    1);
INSERT INTO cuentas_bancarias (empleado_id, banco_nombre, banco_codigo, numero_cuenta, tipo_cuenta, es_principal)
VALUES (4, 'Banco Industrial', 'BI',  '001-556677-2', 'MONETARIA', 1);
INSERT INTO cuentas_bancarias (empleado_id, banco_nombre, banco_codigo, numero_cuenta, tipo_cuenta, es_principal)
VALUES (5, 'Banrural',         'BRL', '300-445566-9', 'AHORRO',    1);
INSERT INTO cuentas_bancarias (empleado_id, banco_nombre, banco_codigo, numero_cuenta, tipo_cuenta, es_principal)
VALUES (6, 'BAC Credomatic',   'BAC', '200-778899-3', 'MONETARIA', 1);

-- CUENTAS CONTABLES BASICAS
INSERT INTO cuentas_contables (empresa_id, codigo, nombre, tipo, naturaleza)
VALUES (1, '5101', 'Sueldos y Salarios',      'GASTO',   'D');
INSERT INTO cuentas_contables (empresa_id, codigo, nombre, tipo, naturaleza)
VALUES (1, '5102', 'Bonificaciones',          'GASTO',   'D');
INSERT INTO cuentas_contables (empresa_id, codigo, nombre, tipo, naturaleza)
VALUES (1, '2101', 'IGSS por Pagar',          'PASIVO',  'C');
INSERT INTO cuentas_contables (empresa_id, codigo, nombre, tipo, naturaleza)
VALUES (1, '2102', 'ISR Retenido',            'PASIVO',  'C');
INSERT INTO cuentas_contables (empresa_id, codigo, nombre, tipo, naturaleza)
VALUES (1, '1101', 'Bancos',                  'ACTIVO',  'D');

COMMIT;
PROMPT Datos iniciales cargados.
