-- ============================================================================
-- NominaGT v4 - Rescate del usuario NOMINAGT
--
-- Cuando ejecutar:
--   * SQL Developer dice ORA-01017 aunque la password sea correcta
--   * El backend tambien empezo a fallar con 'invalid credential'
--   * Sospechas que la cuenta se bloqueo por intentos fallidos
--
-- COMO EJECUTAR:
--   1. Crea (si no la tienes) una conexion en SQL Developer al usuario SYSTEM:
--        - Usuario: system
--        - Password: la que pusiste al instalar Oracle XE 21c
--        - Servicio: XEPDB1
--      O conectate desde PowerShell con:    sqlplus / as sysdba
--   2. Abre este archivo y pulsa F5 (ejecutar como script).
-- ============================================================================

ALTER SESSION SET CONTAINER = XEPDB1;

-- 1. Desbloquear la cuenta (si esta bloqueada)
ALTER USER nominagt ACCOUNT UNLOCK;

-- 2. Resetear la password al valor que espera el backend (sin caracteres
--    especiales para evitar bugs de SQL Developer con '#').
ALTER USER nominagt IDENTIFIED BY NominaGT2026;

-- 3. Quitar el bloqueo automatico tras intentos fallidos. Esto evita que
--    la cuenta se vuelva a bloquear cuando uno se equivoca al loguearse en
--    SQL Developer durante el desarrollo.
ALTER PROFILE DEFAULT LIMIT FAILED_LOGIN_ATTEMPTS UNLIMITED;
ALTER PROFILE DEFAULT LIMIT PASSWORD_LIFE_TIME UNLIMITED;

-- 4. Verificar
SELECT username, account_status, lock_date, expiry_date
  FROM dba_users
 WHERE username = 'NOMINAGT';

-- account_status debe quedar en 'OPEN'.

-- ============================================================================
-- Una vez listo:
--   - En SQL Developer, edita la conexion 'NominaGT' y pon password = NominaGT2026
--   - El backend ya tiene appsettings.json con esa misma password
--   - Si tenias el backend corriendo, REINICIALO (Ctrl+C y dotnet run de nuevo)
--     para que tome la nueva connection string.
-- ============================================================================
