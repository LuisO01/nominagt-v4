import { Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider, useAuth } from './context/AuthContext';
import { ToastProvider } from './context/ToastContext';
import BackendStatusBanner from './components/BackendStatusBanner';
import DashboardLayout from './layouts/DashboardLayout';
import LoginPage from './pages/LoginPage';
import DashboardPage from './pages/DashboardPage';
import EmpleadosPage from './pages/EmpleadosPage';
import NominaPage from './pages/NominaPage';
import ReportesPage from './pages/ReportesPage';
import MiPerfilPage from './pages/MiPerfilPage';
import MisBoletasPage from './pages/MisBoletasPage';
import VacacionesPage from './pages/VacacionesPage';

function ProtectedRoute({ modulo, children }) {
  const { user, tieneModulo, loading } = useAuth();
  if (loading) return null;
  if (!user) return <Navigate to="/login" replace />;
  if (modulo && !tieneModulo(modulo)) return <Navigate to="/" replace />;
  return children;
}

// Decide a donde aterrizar segun los modulos del rol
function HomeRedirect() {
  const { user, tieneModulo, loading } = useAuth();
  if (loading) return null;
  if (!user) return <Navigate to="/login" replace />;
  if (tieneModulo('dashboard'))    return <Navigate to="/dashboard" replace />;
  if (tieneModulo('mis-boletas'))  return <Navigate to="/mis-boletas" replace />;
  if (tieneModulo('mi-perfil'))    return <Navigate to="/mi-perfil" replace />;
  if (tieneModulo('empleados'))    return <Navigate to="/empleados" replace />;
  return <Navigate to="/mi-perfil" replace />;
}

function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<DashboardLayout />}>
        <Route path="/" element={<HomeRedirect />} />
        <Route path="/dashboard" element={
          <ProtectedRoute modulo="dashboard"><DashboardPage /></ProtectedRoute>
        } />
        <Route path="/empleados" element={
          <ProtectedRoute modulo="empleados"><EmpleadosPage /></ProtectedRoute>
        } />
        <Route path="/nomina" element={
          <ProtectedRoute modulo="nomina"><NominaPage /></ProtectedRoute>
        } />
        <Route path="/reportes" element={
          <ProtectedRoute modulo="reportes"><ReportesPage /></ProtectedRoute>
        } />
        <Route path="/mi-perfil" element={
          <ProtectedRoute modulo="mi-perfil"><MiPerfilPage /></ProtectedRoute>
        } />
        <Route path="/mis-boletas" element={
          <ProtectedRoute modulo="mis-boletas"><MisBoletasPage /></ProtectedRoute>
        } />
        <Route path="/vacaciones" element={
          <ProtectedRoute modulo="vacaciones"><VacacionesPage /></ProtectedRoute>
        } />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

export default function App() {
  return (
    <ToastProvider>
      <AuthProvider>
        <BackendStatusBanner />
        <AppRoutes />
      </AuthProvider>
    </ToastProvider>
  );
}
