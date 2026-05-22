import { User, Mail, Shield, Calendar, Info } from 'lucide-react';
import { useAuth } from '../context/AuthContext';

export default function MiPerfilPage() {
  const { user } = useAuth();
  if (!user) return null;

  const expira = user.expira ? new Date(user.expira) : null;

  return (
    <div className="p-4 md:p-8 max-w-3xl mx-auto">
      <h1 className="text-2xl font-bold text-stone-800 mb-6 flex items-center gap-2">
        <User size={22} /> Mi perfil
      </h1>

      <div className="bg-white rounded-xl border border-stone-200 p-6 mb-4">
        <div className="flex items-center gap-4 mb-6 pb-6 border-b border-stone-100">
          <div className="w-16 h-16 rounded-full bg-gradient-to-br from-amber-400 to-orange-500 flex items-center justify-center">
            <span className="text-white text-xl font-bold">
              {user.nombreUsuario?.[0]?.toUpperCase() || '?'}
            </span>
          </div>
          <div>
            <p className="text-lg font-bold text-stone-800">{user.nombreUsuario}</p>
            <p className="text-sm text-stone-500">{user.email || 'Sin correo registrado'}</p>
          </div>
        </div>

        <div className="space-y-4">
          <Field icon={Mail}     label="Correo"            value={user.email || '—'} />
          <Field icon={Shield}   label="Rol activo"        value={user.rolActivo} highlight />
          <Field icon={Shield}   label="Roles asignados"   value={(user.roles || []).join(', ') || user.rolActivo} />
          <Field icon={Calendar} label="Sesión expira"
                 value={expira ? expira.toLocaleString('es-GT') : '—'} />
        </div>
      </div>

      <div className="bg-blue-50 border border-blue-200 rounded-xl p-5 flex items-start gap-3">
        <Info size={18} className="text-blue-600 flex-shrink-0 mt-0.5" />
        <div className="text-sm text-blue-800">
          <p className="font-medium">Cuenta de empleado</p>
          <p className="text-blue-700/80 mt-1">
            Pronto podrás consultar tus boletas de pago y descargar tus recibos directamente desde aquí.
            Si necesitas cambios en tus datos, contacta a Recursos Humanos.
          </p>
        </div>
      </div>
    </div>
  );
}

function Field({ icon: Icon, label, value, highlight }) {
  return (
    <div className="flex items-start gap-3">
      <div className="w-8 h-8 rounded-lg bg-stone-50 flex items-center justify-center flex-shrink-0">
        <Icon size={14} className="text-stone-500" />
      </div>
      <div className="flex-1 min-w-0">
        <p className="text-xs text-stone-400 uppercase tracking-wide">{label}</p>
        <p className={`mt-0.5 break-words ${highlight ? 'font-semibold text-amber-600' : 'text-stone-700'}`}>
          {value}
        </p>
      </div>
    </div>
  );
}
