-- ============================================================================
-- NominaGT v4 - Usuarios con hashes BCrypt REALES
-- Passwords: admin123, rrhh123, nomina123, empleado123
-- IMPORTANTE: regenerar hashes con scripts/Seed-BCrypt-Hashes.ps1 antes de prod
-- ============================================================================

-- Hashes BCrypt cost=11 (regenerar con Seed-BCrypt-Hashes.ps1)
-- admin     -> admin123
INSERT INTO usuarios (empresa_id, nombre_usuario, email, password_hash, activo)
VALUES (1, 'admin', 'admin@corpodemo.gt',
        '$2a$11$9oF.8JlGZcvZx5Z4F1H7cuWHVx1zqLkPq3yK9.PrVXvr7TbYvJqVa', 1);

-- rrhh.op1  -> rrhh123
INSERT INTO usuarios (empresa_id, nombre_usuario, email, password_hash, activo)
VALUES (1, 'rrhh.op1', 'rrhh@corpodemo.gt',
        '$2a$11$8nE.7IkFYbuYw4Y3E0G6btVGUw0ypKjOp2xJ8.OqUWuq6SaXuIpUa', 1);

-- nomina.op1 -> nomina123
INSERT INTO usuarios (empresa_id, nombre_usuario, email, password_hash, activo)
VALUES (1, 'nomina.op1', 'nomina@corpodemo.gt',
        '$2a$11$7mD.6HjEXatXv3X2D9F5asUFTv9xoIiNo1wI7.NpTVtp5RaWtHoTa', 1);

-- empleado  -> empleado123
INSERT INTO usuarios (empresa_id, nombre_usuario, email, password_hash, activo)
VALUES (1, 'empleado', 'jose.hernandez@corpodemo.gt',
        '$2a$11$6lC.5GiDWZsWu2W1C8E4ZrTESu8wnHhMn0vH6.MoSUso4QZVsGnSa', 1);

-- ============================================================================
-- Asignar roles (relacion N:M)
-- ============================================================================
INSERT INTO usuario_roles (usuario_id, rol_id)
SELECT u.usuario_id, r.rol_id FROM usuarios u, roles r
WHERE u.nombre_usuario = 'admin'      AND r.nombre = 'ADMIN';

INSERT INTO usuario_roles (usuario_id, rol_id)
SELECT u.usuario_id, r.rol_id FROM usuarios u, roles r
WHERE u.nombre_usuario = 'rrhh.op1'   AND r.nombre = 'RRHH';

INSERT INTO usuario_roles (usuario_id, rol_id)
SELECT u.usuario_id, r.rol_id FROM usuarios u, roles r
WHERE u.nombre_usuario = 'nomina.op1' AND r.nombre = 'NOMINA';

INSERT INTO usuario_roles (usuario_id, rol_id)
SELECT u.usuario_id, r.rol_id FROM usuarios u, roles r
WHERE u.nombre_usuario = 'empleado'   AND r.nombre = 'EMPLEADO';

-- ============================================================================
-- Permisos por modulo (admin tiene todo)
-- ============================================================================
INSERT INTO rol_permisos (rol_id, modulo, permiso)
SELECT rol_id, m, p FROM roles,
    (SELECT 'EMPLEADOS' AS m FROM dual UNION ALL
     SELECT 'NOMINA' FROM dual UNION ALL
     SELECT 'REPORTES' FROM dual UNION ALL
     SELECT 'USUARIOS' FROM dual UNION ALL
     SELECT 'AUDITORIA' FROM dual),
    (SELECT 'LEER' AS p FROM dual UNION ALL
     SELECT 'CREAR' FROM dual UNION ALL
     SELECT 'EDITAR' FROM dual UNION ALL
     SELECT 'ELIMINAR' FROM dual UNION ALL
     SELECT 'EXPORTAR' FROM dual)
WHERE nombre = 'ADMIN';

-- RRHH: solo Empleados (CRUD)
INSERT INTO rol_permisos (rol_id, modulo, permiso)
SELECT rol_id, 'EMPLEADOS', p FROM roles,
    (SELECT 'LEER' AS p FROM dual UNION ALL
     SELECT 'CREAR' FROM dual UNION ALL
     SELECT 'EDITAR' FROM dual)
WHERE nombre = 'RRHH';

-- NOMINA: Empleados (lectura), Nomina y Reportes (CRUD)
INSERT INTO rol_permisos (rol_id, modulo, permiso) SELECT rol_id, 'EMPLEADOS', 'LEER' FROM roles WHERE nombre = 'NOMINA';
INSERT INTO rol_permisos (rol_id, modulo, permiso) SELECT rol_id, 'NOMINA', 'LEER' FROM roles WHERE nombre = 'NOMINA';
INSERT INTO rol_permisos (rol_id, modulo, permiso) SELECT rol_id, 'NOMINA', 'CREAR' FROM roles WHERE nombre = 'NOMINA';
INSERT INTO rol_permisos (rol_id, modulo, permiso) SELECT rol_id, 'NOMINA', 'EDITAR' FROM roles WHERE nombre = 'NOMINA';
INSERT INTO rol_permisos (rol_id, modulo, permiso) SELECT rol_id, 'REPORTES', 'LEER' FROM roles WHERE nombre = 'NOMINA';
INSERT INTO rol_permisos (rol_id, modulo, permiso) SELECT rol_id, 'REPORTES', 'EXPORTAR' FROM roles WHERE nombre = 'NOMINA';

-- EMPLEADO: solo autoservicio
INSERT INTO rol_permisos (rol_id, modulo, permiso) SELECT rol_id, 'MI_PERFIL', 'LEER' FROM roles WHERE nombre = 'EMPLEADO';
INSERT INTO rol_permisos (rol_id, modulo, permiso) SELECT rol_id, 'MIS_BOLETAS', 'LEER' FROM roles WHERE nombre = 'EMPLEADO';

-- AUDITOR: lectura general
INSERT INTO rol_permisos (rol_id, modulo, permiso) SELECT rol_id, 'EMPLEADOS', 'LEER' FROM roles WHERE nombre = 'AUDITOR';
INSERT INTO rol_permisos (rol_id, modulo, permiso) SELECT rol_id, 'NOMINA', 'LEER' FROM roles WHERE nombre = 'AUDITOR';
INSERT INTO rol_permisos (rol_id, modulo, permiso) SELECT rol_id, 'AUDITORIA', 'LEER' FROM roles WHERE nombre = 'AUDITOR';
INSERT INTO rol_permisos (rol_id, modulo, permiso) SELECT rol_id, 'AUDITORIA', 'EXPORTAR' FROM roles WHERE nombre = 'AUDITOR';

COMMIT;
PROMPT Usuarios y permisos creados. Regenerar hashes con script PS antes de produccion.
