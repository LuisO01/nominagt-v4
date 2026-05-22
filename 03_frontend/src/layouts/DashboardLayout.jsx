import { NavLink, Outlet, Navigate } from 'react-router-dom';
import { LogOut, LayoutDashboard, Users, Calculator, FileBarChart, Shield, User, FileText, Calendar } from 'lucide-react';
import { useAuth } from '../context/AuthContext';

const ITEMS = [
  { to: '/dashboard',   label: 'Dashboard',   icon: LayoutDashboard, modulo: 'dashboard' },
  { to: '/empleados',   label: 'Empleados',   icon: Users,           modulo: 'empleados' },
  { to: '/nomina',      label: 'Nómina',      icon: Calculator,      modulo: 'nomina' },
  { to: '/vacaciones',  label: 'Vacaciones',  icon: Calendar,        modulo: 'vacaciones' },
  { to: '/reportes',    label: 'Reportes',    icon: FileBarChart,    modulo: 'reportes' },
  { to: '/mis-boletas', label: 'Mis boletas', icon: FileText,        modulo: 'mis-boletas' },
  { to: '/mi-perfil',   label: 'Mi perfil',   icon: User,            modulo: 'mi-perfil' },
];

export default function DashboardLayout() {
  const { user, logout, tieneModulo } = useAuth();
  if (!user) return <Navigate to="/login" replace />;
  const items = ITEMS.filter(i => tieneModulo(i.modulo));

  return (
    <div className="flex h-screen overflow-hidden bg-stone-50">
      <aside className="w-64 bg-stone-900 text-white flex flex-col">
        <div className="p-5 border-b border-stone-800">
          <div className="flex items-center gap-2">
            <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-amber-400 to-orange-500 flex items-center justify-center">
              <Shield size={16} className="text-white" />
            </div>
            <div>
              <p className="text-sm font-bold">NominaGT v4</p>
              <p className="text-[10px] text-stone-400">{user.rolActivo}</p>
            </div>
          </div>
        </div>
        <nav className="flex-1 p-3 space-y-1">
          {items.map(({ to, label, icon: Icon }) => (
            <NavLink key={to} to={to}
              className={({ isActive }) =>
                `flex items-center gap-3 px-3 py-2 rounded-lg text-sm transition-colors ${
                  isActive ? 'bg-amber-500/10 text-amber-400' : 'text-stone-300 hover:bg-stone-800'
                }`}>
              <Icon size={16} /> {label}
            </NavLink>
          ))}
        </nav>
        <div className="p-3 border-t border-stone-800">
          <p className="text-xs text-stone-500 mb-1">Sesion</p>
          <p className="text-sm font-medium mb-2">{user.nombreUsuario}</p>
          <button onClick={logout}
            className="w-full flex items-center justify-center gap-2 text-xs text-stone-400 hover:text-red-400 py-2">
            <LogOut size={12} /> Cerrar sesion
          </button>
        </div>
      </aside>
      <main className="flex-1 overflow-auto"><Outlet /></main>
    </div>
  );
}
