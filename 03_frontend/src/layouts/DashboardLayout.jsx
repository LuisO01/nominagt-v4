import { useState } from 'react';
import { NavLink, Outlet, Navigate } from 'react-router-dom';
import {
  LogOut, LayoutDashboard, Users, Calculator, FileBarChart, Shield, User,
  FileText, Calendar, Menu, X,
} from 'lucide-react';
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
  const [sidebarOpen, setSidebarOpen] = useState(false);

  if (!user) return <Navigate to="/login" replace />;
  const items = ITEMS.filter(i => tieneModulo(i.modulo));

  const Sidebar = (
    <aside className="w-64 h-full bg-stone-900 text-white flex flex-col">
      <div className="p-5 border-b border-stone-800 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-amber-400 to-orange-500 flex items-center justify-center">
            <Shield size={16} className="text-white" />
          </div>
          <div>
            <p className="text-sm font-bold">NominaGT v4</p>
            <p className="text-[10px] text-stone-400">{user.rolActivo}</p>
          </div>
        </div>
        {/* Boton cerrar en mobile */}
        <button
          onClick={() => setSidebarOpen(false)}
          className="md:hidden text-stone-400 hover:text-white p-1"
          aria-label="Cerrar menú"
        >
          <X size={18} />
        </button>
      </div>
      <nav className="flex-1 p-3 space-y-1 overflow-y-auto">
        {items.map(({ to, label, icon: Icon }) => (
          <NavLink
            key={to}
            to={to}
            onClick={() => setSidebarOpen(false)}
            className={({ isActive }) =>
              `flex items-center gap-3 px-3 py-2 rounded-lg text-sm transition-colors ${
                isActive ? 'bg-amber-500/10 text-amber-400' : 'text-stone-300 hover:bg-stone-800'
              }`
            }
          >
            <Icon size={16} /> {label}
          </NavLink>
        ))}
      </nav>
      <div className="p-3 border-t border-stone-800">
        <p className="text-xs text-stone-500 mb-1">Sesión</p>
        <p className="text-sm font-medium mb-2 truncate">{user.nombreUsuario}</p>
        <button
          onClick={logout}
          className="w-full flex items-center justify-center gap-2 text-xs text-stone-400 hover:text-red-400 py-2"
        >
          <LogOut size={12} /> Cerrar sesión
        </button>
      </div>
    </aside>
  );

  return (
    <div className="flex h-screen overflow-hidden bg-stone-50">
      {/* Sidebar — visible siempre en md+, drawer en mobile */}
      <div className="hidden md:block">{Sidebar}</div>

      {/* Drawer mobile */}
      {sidebarOpen && (
        <>
          <div
            className="md:hidden fixed inset-0 z-40 bg-black/50"
            onClick={() => setSidebarOpen(false)}
            aria-label="Cerrar overlay"
          />
          <div className="md:hidden fixed inset-y-0 left-0 z-50 animate-[slideInLeft_.2s_ease-out]">
            {Sidebar}
          </div>
        </>
      )}

      {/* Contenido principal */}
      <main className="flex-1 overflow-auto flex flex-col">
        {/* Topbar mobile con hamburger */}
        <div className="md:hidden sticky top-0 z-30 bg-white border-b border-stone-200 px-4 py-3 flex items-center justify-between">
          <button
            onClick={() => setSidebarOpen(true)}
            className="text-stone-700 hover:text-stone-900 p-1"
            aria-label="Abrir menú"
          >
            <Menu size={22} />
          </button>
          <div className="flex items-center gap-2">
            <div className="w-7 h-7 rounded-md bg-gradient-to-br from-amber-400 to-orange-500 flex items-center justify-center">
              <Shield size={13} className="text-white" />
            </div>
            <span className="font-bold text-stone-800 text-sm">NominaGT v4</span>
          </div>
          <div className="w-7" /> {/* spacer derecho para centrar el titulo */}
        </div>

        <div className="flex-1 overflow-auto">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
