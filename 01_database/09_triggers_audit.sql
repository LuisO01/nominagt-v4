-- ============================================================================
-- NominaGT v4 - Triggers de auditoria automatica
-- ============================================================================

-- Trigger empleados
CREATE OR REPLACE TRIGGER trg_aud_empleados
AFTER INSERT OR UPDATE OR DELETE ON empleados
FOR EACH ROW
DECLARE
    v_accion VARCHAR2(20);
    v_anterior CLOB;
    v_nuevo CLOB;
BEGIN
    IF INSERTING THEN
        v_accion := 'INSERT';
        v_nuevo := 'codigo=' || :NEW.codigo_empleado || ';nombre=' || :NEW.primer_nombre ||
                   ' ' || :NEW.primer_apellido || ';estado=' || :NEW.estado;
    ELSIF UPDATING THEN
        v_accion := 'UPDATE';
        v_anterior := 'estado=' || :OLD.estado;
        v_nuevo    := 'estado=' || :NEW.estado;
    ELSE
        v_accion := 'DELETE';
        v_anterior := 'codigo=' || :OLD.codigo_empleado;
    END IF;

    INSERT INTO auditoria (usuario, accion, tabla, registro_id, valor_anterior, valor_nuevo)
    VALUES (NVL(SYS_CONTEXT('USERENV','SESSION_USER'),'sistema'),
            v_accion, 'empleados',
            COALESCE(:NEW.empleado_id, :OLD.empleado_id),
            v_anterior, v_nuevo);
END;
/

-- Trigger periodos_nomina
CREATE OR REPLACE TRIGGER trg_aud_periodos
AFTER UPDATE ON periodos_nomina
FOR EACH ROW
WHEN (OLD.estado != NEW.estado)
BEGIN
    INSERT INTO auditoria (usuario, accion, tabla, registro_id, valor_anterior, valor_nuevo)
    VALUES (NVL(SYS_CONTEXT('USERENV','SESSION_USER'),'sistema'),
            'UPDATE', 'periodos_nomina', :NEW.periodo_id,
            'estado=' || :OLD.estado, 'estado=' || :NEW.estado);
END;
/

PROMPT Triggers de auditoria creados.
