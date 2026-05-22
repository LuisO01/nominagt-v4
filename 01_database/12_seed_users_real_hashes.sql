-- ============================================================================
-- NominaGT v4 - Seed REAL de hashes BCrypt
-- Reemplaza los hashes placeholder de 11_users_bcrypt.sql con hashes reales
-- generados con BCrypt.Net-Next (cost=11).
--
-- Credenciales:
--   admin      / admin
--   rrhh.op1   / rrhh123
--   nomina.op1 / nomina123
--   empleado   / empleado123
--
-- Uso:
--   1. Conecta a Oracle como NOMINAGT/NominaGT2026#@localhost:1521/XEPDB1
--   2. Ejecuta este script completo (en SQL Developer pulsa F5).
-- ============================================================================

UPDATE usuarios
   SET password_hash      = '$2a$11$OrXElGcPyhy3SYWC3bQgve6D6NvhZ.CFU4woED5pWjyGYSPp0mL0i',
       intentos_fallidos  = 0,
       bloqueado_hasta    = NULL
 WHERE nombre_usuario = 'admin';

UPDATE usuarios
   SET password_hash      = '$2a$11$oTtyYPzusqofYFo/0KmgL.PvWuAtc0dtBW9GaqT4eG1h8gp7Db3ma',
       intentos_fallidos  = 0,
       bloqueado_hasta    = NULL
 WHERE nombre_usuario = 'rrhh.op1';

UPDATE usuarios
   SET password_hash      = '$2a$11$RsQ8Y0CvGkx1vIvqWxp5D.jgFB.hv6nR/5HWB.qxB9usVkdpiFWcW',
       intentos_fallidos  = 0,
       bloqueado_hasta    = NULL
 WHERE nombre_usuario = 'nomina.op1';

UPDATE usuarios
   SET password_hash      = '$2a$11$VKdJ0GNlYdeFvOgB0StX0OK24/HA98QOzgu6taetQXs6bemNosa5q',
       intentos_fallidos  = 0,
       bloqueado_hasta    = NULL
 WHERE nombre_usuario = 'empleado';

COMMIT;

-- Verificacion (debe mostrar 4 filas con hashes que comienzan con $2a$11$):
SELECT nombre_usuario,
       SUBSTR(password_hash, 1, 20) AS hash_preview,
       LENGTH(password_hash)         AS largo_hash,
       intentos_fallidos,
       bloqueado_hasta
  FROM usuarios
 ORDER BY nombre_usuario;
