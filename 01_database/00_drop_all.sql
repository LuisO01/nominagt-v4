-- ============================================================================
-- NominaGT v4 - DROP completo (SOLO PARA DESARROLLO - NO USAR EN PROD)
-- Conexion: SQL Developer como SYSTEM contra XEPDB1
-- ============================================================================
ALTER SESSION SET CONTAINER = XEPDB1;

BEGIN
    EXECUTE IMMEDIATE 'DROP USER nominagt CASCADE';
EXCEPTION WHEN OTHERS THEN NULL;
END;
/

COMMIT;
PROMPT Schema eliminado. Listo para reinstalar.
