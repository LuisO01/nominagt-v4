-- ============================================================================
-- NominaGT v4 - Crear usuario y otorgar privilegios
-- Conexion: como SYSTEM contra XEPDB1
-- ============================================================================
ALTER SESSION SET CONTAINER = XEPDB1;

CREATE USER nominagt IDENTIFIED BY NominaGT2026
DEFAULT TABLESPACE USERS
TEMPORARY TABLESPACE TEMP
QUOTA UNLIMITED ON USERS;

GRANT CONNECT, RESOURCE TO nominagt;
GRANT CREATE VIEW, CREATE PROCEDURE, CREATE TRIGGER TO nominagt;
GRANT CREATE MATERIALIZED VIEW TO nominagt;
GRANT CREATE SYNONYM, CREATE SEQUENCE TO nominagt;
GRANT SELECT_CATALOG_ROLE TO nominagt;

-- Para desarrollo: quitar el bloqueo automatico tras intentos fallidos
ALTER PROFILE DEFAULT LIMIT FAILED_LOGIN_ATTEMPTS UNLIMITED;
ALTER PROFILE DEFAULT LIMIT PASSWORD_LIFE_TIME UNLIMITED;

PROMPT Usuario nominagt creado. Password: NominaGT2026
