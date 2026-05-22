-- ============================================================================
-- NominaGT v4 - Vincular usuario 'empleado' a un empleado real
--
-- Por defecto la tabla usuarios tiene la columna empleado_id en NULL para los
-- usuarios demo. Este script asocia el usuario 'empleado' al empleado real
-- 'EMP-003 Jose Antonio Hernandez Castillo' (que coincide con el email del seed)
-- y al usuario 'admin' al gerente general 'EMP-001'. Ejecutar en SQL Developer
-- conectado como NOMINAGT.
-- ============================================================================

UPDATE usuarios u
   SET u.empleado_id = (SELECT empleado_id FROM empleados
                         WHERE codigo_empleado = 'EMP-003' AND empresa_id = u.empresa_id)
 WHERE u.nombre_usuario = 'empleado';

UPDATE usuarios u
   SET u.empleado_id = (SELECT empleado_id FROM empleados
                         WHERE codigo_empleado = 'EMP-001' AND empresa_id = u.empresa_id)
 WHERE u.nombre_usuario = 'admin' AND u.empleado_id IS NULL;

UPDATE usuarios u
   SET u.empleado_id = (SELECT empleado_id FROM empleados
                         WHERE codigo_empleado = 'EMP-005' AND empresa_id = u.empresa_id)
 WHERE u.nombre_usuario = 'rrhh.op1' AND u.empleado_id IS NULL;

UPDATE usuarios u
   SET u.empleado_id = (SELECT empleado_id FROM empleados
                         WHERE codigo_empleado = 'EMP-002' AND empresa_id = u.empresa_id)
 WHERE u.nombre_usuario = 'nomina.op1' AND u.empleado_id IS NULL;

COMMIT;

-- Verificacion
SELECT u.nombre_usuario, u.empleado_id, e.codigo_empleado,
       e.primer_nombre || ' ' || e.primer_apellido AS nombre
  FROM usuarios u
  LEFT JOIN empleados e ON e.empleado_id = u.empleado_id
 ORDER BY u.nombre_usuario;
