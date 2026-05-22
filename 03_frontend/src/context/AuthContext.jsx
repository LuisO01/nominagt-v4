import { createContext, useContext, useState, useEffect, useCallback, useMemo } from 'react';
import { authApi } from '../services/api';

const AuthContext = createContext(null);

// ─── Permisos por rol ─────────────────────────────────────────────────
// Alineado con los [Authorize(Roles="...")] del backend.
//
// MODULOS = a que paginas accede (visibilidad en sidebar / rutas)
// CAPS    = capacidades concretas (botones de accion)
//
// Mantener ESTO sincronizado con los Authorize() del API es CRITICO.
const MODULOS = {
  ADMIN:    ['dashboard','empleados','nomina','reportes','auditoria','vacaciones','mi-perfil','mis-boletas'],
  RRHH:     ['dashboard','empleados','vacaciones','mi-perfil'],
  NOMINA:   ['dashboard','empleados','nomina','reportes','vacaciones','mi-perfil'],
  AUDITOR:  ['dashboard','empleados','nomina','auditoria','vacaciones','mi-perfil'],
  EMPLEADO: ['vacaciones','mi-perfil','mis-boletas'],
};

// Capacidades por rol — refleja [Authorize(Roles=...)] del backend.
// El sufijo es la operacion, no el modulo. Mantener identico a las restricciones del API.
const CAPS = {
  ADMIN: new Set([
    'empleado.crear','empleado.editar','empleado.cambiar-estado',
    'periodo.crear','periodo.calcular','periodo.aprobar',
    'reporte.exportar','reporte.exportar-excel','reporte.exportar-pdf',
    'auditoria.ver',
    'vacacion.solicitar','vacacion.aprobar','vacacion.ver-todos',
  ]),
  RRHH: new Set([
    'empleado.crear','empleado.editar',
    'vacacion.solicitar','vacacion.aprobar','vacacion.ver-todos',
  ]),
  NOMINA: new Set([
    'periodo.crear','periodo.calcular',
    'reporte.exportar','reporte.exportar-excel','reporte.exportar-pdf',
    'vacacion.ver-todos',
  ]),
  AUDITOR: new Set([
    'reporte.exportar','reporte.exportar-excel','reporte.exportar-pdf',
    'auditoria.ver',
    'vacacion.ver-todos',
  ]),
  EMPLEADO: new Set([
    'mis-boletas.ver',
    'vacacion.solicitar',
  ]),
};

// ─── Provider ────────────────────────────────────────────────────────
export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const s = localStorage.getItem('nominagt_session');
    if (s) try { setUser(JSON.parse(s)); } catch { localStorage.removeItem('nominagt_session'); }
    setLoading(false);
  }, []);

  const login = useCallback(async (nombreUsuario, password) => {
    const data = await authApi.login(nombreUsuario, password);
    const roles = (data.roles || []).map(r => r.toUpperCase());
    const session = {
      token: data.token,
      refreshToken: data.refreshToken,
      nombreUsuario: data.nombreUsuario,
      email: data.email,
      roles,
      rolActivo: roles[0] || 'EMPLEADO',
      empresaId: 1,
      expira: data.expira,
    };
    localStorage.setItem('nominagt_session', JSON.stringify(session));
    setUser(session);
    return session;
  }, []);

  const logout = useCallback(() => {
    localStorage.removeItem('nominagt_session');
    setUser(null);
  }, []);

  // Helpers de permisos: combinan TODOS los roles del usuario, no solo el rol activo.
  const tieneModulo = useCallback((modulo) => {
    if (!user) return false;
    const rolesUsuario = user.roles?.length ? user.roles : [user.rolActivo];
    return rolesUsuario.some(r => MODULOS[r]?.includes(modulo));
  }, [user]);

  const puede = useCallback((capacidad) => {
    if (!user) return false;
    const rolesUsuario = user.roles?.length ? user.roles : [user.rolActivo];
    return rolesUsuario.some(r => CAPS[r]?.has(capacidad));
  }, [user]);

  const value = useMemo(() => ({
    user, loading, login, logout,
    tieneModulo,
    puede,
    isAdmin: user?.roles?.includes('ADMIN') || user?.rolActivo === 'ADMIN',
    isEmpleado: (user?.roles?.length === 1 && user?.roles[0] === 'EMPLEADO')
                || user?.rolActivo === 'EMPLEADO',
  }), [user, loading, login, logout, tieneModulo, puede]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export const useAuth = () => {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth dentro de AuthProvider');
  return ctx;
};
