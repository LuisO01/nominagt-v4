-- ============================================================================
-- NominaGT v4 - Permitir email duplicado en usuarios
--
-- El constraint UNIQUE en usuarios.email era demasiado restrictivo:
--   - Impedia compartir un email familiar entre dos cuentas
--   - El login es por nombre_usuario, no por email
--   - Impide sincronizar empleados.email_corporativo cuando dos empleados
--     comparten el mismo correo (caso real en empresas pequeñas o pruebas).
-- ============================================================================

-- Buscar y eliminar el constraint UNIQUE de email (su nombre puede variar)
DECLARE
    v_cons_name VARCHAR2(100);
BEGIN
    SELECT constraint_name
      INTO v_cons_name
      FROM user_constraints
     WHERE table_name = 'USUARIOS'
       AND constraint_type = 'U'
       AND constraint_name IN (
            SELECT constraint_name FROM user_cons_columns
             WHERE table_name = 'USUARIOS' AND column_name = 'EMAIL'
         )
     FETCH FIRST 1 ROWS ONLY;

    EXECUTE IMMEDIATE 'ALTER TABLE usuarios DROP CONSTRAINT ' || v_cons_name;
    DBMS_OUTPUT.PUT_LINE('Constraint eliminado: ' || v_cons_name);
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        DBMS_OUTPUT.PUT_LINE('No se encontro constraint UNIQUE en email; ya estaba sin uno.');
END;
/

-- Tambien buscamos el indice unico (Oracle crea uno automaticamente con UNIQUE)
DECLARE
    v_idx_name VARCHAR2(100);
BEGIN
    SELECT index_name INTO v_idx_name
      FROM user_indexes
     WHERE table_name = 'USUARIOS' AND uniqueness = 'UNIQUE'
       AND index_name IN (
            SELECT index_name FROM user_ind_columns
             WHERE table_name = 'USUARIOS' AND column_name = 'EMAIL'
         )
     FETCH FIRST 1 ROWS ONLY;

    EXECUTE IMMEDIATE 'DROP INDEX ' || v_idx_name;
    DBMS_OUTPUT.PUT_LINE('Indice unico eliminado: ' || v_idx_name);
EXCEPTION
    WHEN NO_DATA_FOUND THEN NULL;
END;
/

-- Indice normal (no unico) para que las busquedas por email sigan siendo rapidas
CREATE INDEX ix_usuarios_email ON usuarios(LOWER(email));

PROMPT Listo: email ya no es UNIQUE en usuarios. nombre_usuario sigue siendo UNIQUE.
