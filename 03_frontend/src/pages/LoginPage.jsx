import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Shield, AlertCircle, Loader2 } from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import { getErrorMessage } from '../services/api';

export default function LoginPage() {
  const { login, user } = useAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState({ nombreUsuario: '', password: '' });
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  if (user) { navigate('/', { replace: true }); return null; }

  const submit = async (e) => {
    e.preventDefault();
    setError(''); setLoading(true);
    try {
      await login(form.nombreUsuario, form.password);
      navigate('/', { replace: true });
    } catch (err) {
      // Caso especial: 401 en login = credenciales incorrectas, no "sesion expirada"
      if (err?.response?.status === 401) {
        setError('Usuario o contraseña incorrectos.');
      } else {
        setError(getErrorMessage(err));
      }
    } finally { setLoading(false); }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-stone-50">
      <div className="w-full max-w-sm p-8 bg-white rounded-2xl shadow-sm border border-stone-200">
        <div className="flex items-center gap-3 mb-6">
          <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-amber-400 to-orange-500 flex items-center justify-center">
            <Shield size={20} className="text-white" />
          </div>
          <div>
            <h1 className="text-xl font-bold text-stone-800">NominaGT v4</h1>
            <p className="text-xs text-stone-400">Sistema empresarial</p>
          </div>
        </div>

        {error && (
          <div className="flex items-center gap-2 p-3 mb-4 rounded-lg bg-red-50 text-red-600 text-sm">
            <AlertCircle size={14} /> {error}
          </div>
        )}

        <form onSubmit={submit} className="space-y-4">
          <div>
            <label className="block text-xs font-semibold text-stone-500 uppercase mb-1">Usuario</label>
            <input type="text" value={form.nombreUsuario}
              onChange={e => setForm({...form, nombreUsuario: e.target.value})}
              className="w-full px-3 py-2 text-sm border border-stone-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500/20"
              required />
          </div>
          <div>
            <label className="block text-xs font-semibold text-stone-500 uppercase mb-1">Contrasena</label>
            <input type="password" value={form.password}
              onChange={e => setForm({...form, password: e.target.value})}
              className="w-full px-3 py-2 text-sm border border-stone-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500/20"
              required />
          </div>
          <button type="submit" disabled={loading}
            className="w-full flex items-center justify-center gap-2 bg-stone-900 hover:bg-stone-800 disabled:bg-stone-300 text-white font-semibold text-sm py-2.5 rounded-lg">
            {loading && <Loader2 size={14} className="animate-spin" />}
            {loading ? 'Verificando...' : 'Ingresar'}
          </button>
        </form>
      </div>
    </div>
  );
}
