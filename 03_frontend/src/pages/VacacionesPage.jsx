import { useEffect, useMemo, useState } from 'react';
import { vacacionesApi, empleadosApi, getErrorMessage } from '../services/api';
import { useToast } from '../context/ToastContext';
import { useAuth } from '../context/AuthContext';
import {
  Loader2, Calendar, AlertCircle, RefreshCw, Plus, X,
  CheckCircle2, XCircle, Filter,
} from 'lucide-react';

const fmt = (d) => (d ? new Date(d).toLocaleDateString('es-GT', { day:'2-digit', month:'short', year:'numeric' }) : '—');
const ESTADOS = ['SOLICITADA','APROBADA','RECHAZADA','GOZADA','CANCELADA'];

const colorEstado = {
  SOLICITADA: 'bg-amber-50 text-amber-700',
  APROBADA:   'bg-blue-50 text-blue-700',
  RECHAZADA:  'bg-red-50 text-red-700',
  GOZADA:     'bg-emerald-50 text-emerald-700',
  CANCELADA:  'bg-stone-100 text-stone-500',
};

export default function VacacionesPage() {
  const toast = useToast();
  const { puede, isEmpleado } = useAuth();
  const verTodos    = puede('vacacion.ver-todos');
  const puedeAprobar = puede('vacacion.aprobar');
  const puedeSolicitar = puede('vacacion.solicitar');

  const [vacaciones, setVacaciones] = useState([]);
  const [saldos, setSaldos] = useState([]);
  const [miSaldo, setMiSaldo] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [filtroEstado, setFiltroEstado] = useState('TODOS');
  const [filtroEmpleado, setFiltroEmpleado] = useState('');
  const [showSolicitar, setShowSolicitar] = useState(false);
  const [empleadosOpts, setEmpleadosOpts] = useState([]);

  const cargar = async () => {
    setLoading(true); setError('');
    try {
      const params = {};
      if (filtroEstado !== 'TODOS') params.estado = filtroEstado;
      if (verTodos && filtroEmpleado) params.empleadoId = filtroEmpleado;

      const [v, s, mi] = await Promise.allSettled([
        vacacionesApi.listar(params),
        verTodos ? vacacionesApi.saldos() : Promise.resolve([]),
        vacacionesApi.miSaldo().catch(() => null),
      ]);
      if (v.status === 'fulfilled') setVacaciones(v.value);
      else setError(getErrorMessage(v.reason));
      if (s.status === 'fulfilled') setSaldos(s.value);
      if (mi.status === 'fulfilled') setMiSaldo(mi.value);
    } finally { setLoading(false); }
  };

  useEffect(() => { cargar(); }, [filtroEstado, filtroEmpleado]);

  // Cargar empleados para el dropdown (solo RRHH/ADMIN)
  useEffect(() => {
    if (!verTodos) return;
    (async () => {
      try {
        const { items } = await empleadosApi.listar({ pageSize: 200 });
        setEmpleadosOpts(items || []);
      } catch { /* ignore */ }
    })();
  }, [verTodos]);

  const cambiarEstado = async (vac, nuevo) => {
    try {
      await vacacionesApi.cambiarEstado(vac.vacacionId, nuevo);
      toast.success(`Solicitud ${nuevo.toLowerCase()}.`);
      await cargar();
    } catch (e) { toast.error(getErrorMessage(e), { title: 'No se pudo actualizar' }); }
  };

  return (
    <div className="p-4 md:p-8 max-w-7xl mx-auto">
      <div className="flex flex-wrap items-center justify-between gap-3 mb-6">
        <h1 className="text-2xl font-bold text-stone-800 flex items-center gap-2">
          <Calendar size={22} /> Vacaciones
        </h1>
        <div className="flex items-center gap-2">
          <button
            onClick={cargar}
            disabled={loading}
            className="px-3 py-2 text-sm rounded-lg border border-stone-200 bg-white hover:bg-stone-50 disabled:opacity-50 flex items-center gap-1.5"
          >
            <RefreshCw size={14} className={loading ? 'animate-spin' : ''} /> Actualizar
          </button>
          {puedeSolicitar && (
            <button
              onClick={() => setShowSolicitar(true)}
              className="flex items-center gap-2 bg-stone-900 text-white text-sm px-4 py-2 rounded-lg hover:bg-stone-800"
            >
              <Plus size={14} /> Nueva solicitud
            </button>
          )}
        </div>
      </div>

      {/* Mi saldo */}
      {miSaldo && (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3 mb-6">
          <SaldoCard label="Días acumulados" value={miSaldo.diasAcumulados} color="emerald"
                     hint={`${miSaldo.aniosTrabajados} año${miSaldo.aniosTrabajados === 1 ? '' : 's'} × 15`} />
          <SaldoCard label="Días gozados" value={miSaldo.diasGozados} color="blue" />
          <SaldoCard label="En solicitud" value={miSaldo.diasEnSolicitud} color="amber" />
          <SaldoCard label="Disponibles" value={miSaldo.diasPendientes - miSaldo.diasEnSolicitud} color="violet"
                     emphasize />
        </div>
      )}

      {/* Tabla saldos para RRHH/ADMIN */}
      {verTodos && saldos.length > 0 && !filtroEmpleado && (
        <div className="bg-white rounded-xl border border-stone-200 overflow-x-auto mb-6">
          <div className="px-4 py-3 text-xs font-semibold text-stone-500 uppercase border-b border-stone-100 bg-stone-50">
            Saldos por empleado ({saldos.length})
          </div>
          <table className="w-full text-sm">
            <thead className="bg-stone-50 border-b border-stone-100 text-left">
              <tr>
                <th className="px-4 py-2 text-xs font-bold text-stone-400 uppercase">Empleado</th>
                <th className="px-4 py-2 text-xs font-bold text-stone-400 uppercase">Antigüedad</th>
                <th className="px-4 py-2 text-xs font-bold text-stone-400 uppercase text-right">Acumulados</th>
                <th className="px-4 py-2 text-xs font-bold text-stone-400 uppercase text-right">Gozados</th>
                <th className="px-4 py-2 text-xs font-bold text-stone-400 uppercase text-right">En solicitud</th>
                <th className="px-4 py-2 text-xs font-bold text-stone-400 uppercase text-right">Disponibles</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-stone-100">
              {saldos.map(s => (
                <tr key={s.empleadoId} className="hover:bg-amber-50/20 cursor-pointer"
                    onClick={() => setFiltroEmpleado(String(s.empleadoId))}>
                  <td className="px-4 py-2">
                    <span className="font-mono text-xs text-amber-600 font-bold">{s.codigoEmpleado}</span>
                    <span className="text-stone-700 ml-2">{s.nombre}</span>
                  </td>
                  <td className="px-4 py-2 text-stone-500">{s.aniosTrabajados} año{s.aniosTrabajados === 1 ? '' : 's'}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{s.diasAcumulados}</td>
                  <td className="px-4 py-2 text-right tabular-nums text-blue-700">{s.diasGozados}</td>
                  <td className="px-4 py-2 text-right tabular-nums text-amber-700">{s.diasEnSolicitud}</td>
                  <td className="px-4 py-2 text-right tabular-nums font-bold text-violet-700">
                    {s.diasPendientes - s.diasEnSolicitud}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Filtros */}
      <div className="flex flex-wrap gap-2 mb-3">
        <div className="relative">
          <Filter size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-stone-400 pointer-events-none" />
          <select
            value={filtroEstado}
            onChange={(e) => setFiltroEstado(e.target.value)}
            className="pl-9 pr-8 py-2 text-sm border border-stone-200 rounded-lg bg-white"
          >
            <option value="TODOS">Todos los estados</option>
            {ESTADOS.map(s => <option key={s} value={s}>{s}</option>)}
          </select>
        </div>
        {verTodos && (
          <select
            value={filtroEmpleado}
            onChange={(e) => setFiltroEmpleado(e.target.value)}
            className="px-3 py-2 text-sm border border-stone-200 rounded-lg bg-white"
          >
            <option value="">Todos los empleados</option>
            {empleadosOpts.map(e => (
              <option key={e.empleadoId} value={e.empleadoId}>
                {e.codigoEmpleado} — {e.nombreCompleto}
              </option>
            ))}
          </select>
        )}
        {filtroEmpleado && (
          <button
            onClick={() => setFiltroEmpleado('')}
            className="px-3 py-2 text-xs text-stone-500 hover:text-stone-700 underline"
          >Quitar filtro</button>
        )}
      </div>

      {/* Errores */}
      {error && !loading && (
        <div className="mb-4 flex items-start gap-3 p-4 rounded-xl border border-red-200 bg-red-50 text-red-700">
          <AlertCircle size={18} className="mt-0.5" />
          <div className="flex-1 text-sm">
            <p className="font-medium">No se pudieron cargar las vacaciones</p>
            <p className="text-red-600/80">{error}</p>
          </div>
        </div>
      )}

      {/* Tabla de vacaciones */}
      {loading ? (
        <div className="text-stone-400 p-12 text-center">
          <Loader2 className="animate-spin inline mr-2" /> Cargando...
        </div>
      ) : vacaciones.length === 0 ? (
        <div className="bg-white rounded-xl border border-stone-200 p-12 text-center">
          <Calendar size={28} className="mx-auto text-stone-300 mb-2" />
          <p className="text-stone-500">No hay solicitudes de vacaciones.</p>
        </div>
      ) : (
        <div className="bg-white rounded-xl border border-stone-200 overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-stone-50 border-b border-stone-100">
              <tr>
                {(verTodos ? ['Empleado','Inicio','Fin','Días','Motivo','Estado','Acciones']
                           : ['Inicio','Fin','Días','Motivo','Estado','Acciones']).map((h, i) => (
                  <th key={i} className="px-4 py-3 text-left text-xs font-bold text-stone-400 uppercase">{h}</th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-stone-100">
              {vacaciones.map(v => (
                <tr key={v.vacacionId} className="hover:bg-amber-50/20">
                  {verTodos && (
                    <td className="px-4 py-3">
                      <span className="font-mono text-xs text-amber-600 font-bold">{v.codigoEmpleado}</span>
                      <span className="ml-2 text-stone-700">{v.nombreEmpleado}</span>
                    </td>
                  )}
                  <td className="px-4 py-3 text-stone-700">{fmt(v.fechaInicio)}</td>
                  <td className="px-4 py-3 text-stone-700">{fmt(v.fechaFin)}</td>
                  <td className="px-4 py-3 text-right tabular-nums font-medium">{v.dias}</td>
                  <td className="px-4 py-3 text-stone-500 text-xs max-w-[200px] truncate" title={v.motivo}>
                    {v.motivo || '—'}
                  </td>
                  <td className="px-4 py-3">
                    <span className={`inline-block px-2 py-0.5 rounded-full text-xs font-semibold ${colorEstado[v.estado] || 'bg-stone-100 text-stone-500'}`}>
                      {v.estado}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-3">
                      {puedeAprobar && v.estado === 'SOLICITADA' && (
                        <>
                          <button onClick={() => cambiarEstado(v, 'APROBADA')}
                            className="text-xs font-medium text-emerald-600 hover:underline flex items-center gap-1">
                            <CheckCircle2 size={12} /> Aprobar
                          </button>
                          <button onClick={() => cambiarEstado(v, 'RECHAZADA')}
                            className="text-xs font-medium text-red-600 hover:underline flex items-center gap-1">
                            <XCircle size={12} /> Rechazar
                          </button>
                        </>
                      )}
                      {puedeAprobar && v.estado === 'APROBADA' && (
                        <button onClick={() => cambiarEstado(v, 'GOZADA')}
                          className="text-xs font-medium text-blue-600 hover:underline flex items-center gap-1">
                          <CheckCircle2 size={12} /> Marcar gozada
                        </button>
                      )}
                      {!puedeAprobar && v.estado === 'SOLICITADA' && (
                        <button onClick={() => cambiarEstado(v, 'CANCELADA')}
                          className="text-xs font-medium text-stone-500 hover:underline">
                          Cancelar
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {showSolicitar && (
        <ModalSolicitar
          esRRHH={verTodos}
          empleadosOpts={empleadosOpts}
          miSaldo={miSaldo}
          onClose={() => setShowSolicitar(false)}
          onCreated={async () => { setShowSolicitar(false); await cargar(); }}
        />
      )}
    </div>
  );
}

function SaldoCard({ label, value, color, hint, emphasize }) {
  const colorMap = {
    emerald: 'text-emerald-700 bg-emerald-50',
    blue:    'text-blue-700 bg-blue-50',
    amber:   'text-amber-700 bg-amber-50',
    violet:  'text-violet-700 bg-violet-50',
  };
  return (
    <div className={`p-4 rounded-xl border border-stone-200 ${emphasize ? 'bg-stone-900 text-white' : 'bg-white'}`}>
      <p className={`text-xs uppercase tracking-wide ${emphasize ? 'text-stone-300' : 'text-stone-400'}`}>{label}</p>
      <p className={`text-2xl font-bold mt-1 tabular-nums ${emphasize ? 'text-amber-400' : ''}`}>
        {value} <span className={`text-sm font-normal ${emphasize ? 'text-stone-300' : 'text-stone-400'}`}>días</span>
      </p>
      {hint && <p className={`text-xs mt-1 ${emphasize ? 'text-stone-400' : 'text-stone-400'}`}>{hint}</p>}
    </div>
  );
}

function ModalSolicitar({ esRRHH, empleadosOpts, miSaldo, onClose, onCreated }) {
  const toast = useToast();
  const [form, setForm] = useState({
    empleadoId: '',
    fechaInicio: '',
    fechaFin: '',
    motivo: '',
  });
  const [saving, setSaving] = useState(false);

  const dias = useMemo(() => {
    if (!form.fechaInicio || !form.fechaFin) return 0;
    const a = new Date(form.fechaInicio);
    const b = new Date(form.fechaFin);
    if (b < a) return 0;
    return Math.floor((b - a) / 86400000) + 1;
  }, [form]);

  const submit = async (e) => {
    e.preventDefault();
    if (esRRHH && !form.empleadoId) {
      toast.warning('Selecciona un empleado.');
      return;
    }
    if (dias <= 0) {
      toast.warning('Rango de fechas inválido.');
      return;
    }
    setSaving(true);
    try {
      await vacacionesApi.crear({
        empleadoId: esRRHH ? Number(form.empleadoId) : 0,
        fechaInicio: form.fechaInicio,
        fechaFin: form.fechaFin,
        motivo: form.motivo || null,
      });
      toast.success('Solicitud creada.');
      onCreated();
    } catch (err) {
      toast.error(getErrorMessage(err), { title: 'No se pudo crear la solicitud' });
    } finally { setSaving(false); }
  };

  const disponibles = miSaldo ? (miSaldo.diasPendientes - miSaldo.diasEnSolicitud) : null;

  return (
    <div className="fixed inset-0 z-50 bg-stone-900/40 flex items-center justify-center p-4" onClick={() => !saving && onClose()}>
      <div className="bg-white rounded-xl shadow-xl max-w-md w-full p-6" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-start justify-between mb-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-full bg-amber-50 flex items-center justify-center">
              <Calendar size={18} className="text-amber-600" />
            </div>
            <div>
              <h3 className="font-bold text-stone-800">Solicitar vacaciones</h3>
              <p className="text-xs text-stone-500">Código de Trabajo art. 130</p>
            </div>
          </div>
          <button onClick={() => !saving && onClose()} className="text-stone-400 hover:text-stone-600 p-1">
            <X size={18} />
          </button>
        </div>

        <form onSubmit={submit} className="space-y-3">
          {esRRHH && (
            <div>
              <label className="block text-xs font-medium text-stone-600 mb-1">Empleado *</label>
              <select
                value={form.empleadoId}
                onChange={(e) => setForm({ ...form, empleadoId: e.target.value })}
                className="w-full px-3 py-2 text-sm border border-stone-200 rounded-lg bg-white"
                required
              >
                <option value="">Selecciona un empleado…</option>
                {empleadosOpts.map(e => (
                  <option key={e.empleadoId} value={e.empleadoId}>
                    {e.codigoEmpleado} — {e.nombreCompleto}
                  </option>
                ))}
              </select>
            </div>
          )}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs font-medium text-stone-600 mb-1">Inicio *</label>
              <input type="date" value={form.fechaInicio}
                onChange={(e) => setForm({ ...form, fechaInicio: e.target.value })}
                className="w-full px-3 py-2 text-sm border border-stone-200 rounded-lg" required />
            </div>
            <div>
              <label className="block text-xs font-medium text-stone-600 mb-1">Fin *</label>
              <input type="date" value={form.fechaFin}
                onChange={(e) => setForm({ ...form, fechaFin: e.target.value })}
                className="w-full px-3 py-2 text-sm border border-stone-200 rounded-lg" required />
            </div>
          </div>
          <div>
            <label className="block text-xs font-medium text-stone-600 mb-1">Motivo (opcional)</label>
            <textarea value={form.motivo}
              onChange={(e) => setForm({ ...form, motivo: e.target.value })}
              rows={2}
              className="w-full px-3 py-2 text-sm border border-stone-200 rounded-lg resize-none" />
          </div>

          <div className="flex items-center justify-between bg-stone-50 rounded-lg p-3 text-sm">
            <span className="text-stone-600">Días solicitados:</span>
            <span className="font-bold tabular-nums text-stone-900">{dias}</span>
          </div>
          {!esRRHH && disponibles !== null && (
            <p className={`text-xs ${dias > disponibles ? 'text-red-600' : 'text-stone-500'}`}>
              {dias > disponibles
                ? `Excede tu saldo disponible (${disponibles} días).`
                : `Te quedarán ${disponibles - dias} días disponibles.`}
            </p>
          )}

          <div className="flex justify-end gap-2 pt-2">
            <button type="button" onClick={onClose} disabled={saving}
              className="px-3 py-2 text-sm rounded-lg border border-stone-200 bg-white hover:bg-stone-50 disabled:opacity-50">
              Cancelar
            </button>
            <button type="submit"
              disabled={saving || dias <= 0 || (!esRRHH && disponibles !== null && dias > disponibles)}
              className="px-4 py-2 text-sm rounded-lg bg-stone-900 hover:bg-stone-800 disabled:bg-stone-300 text-white font-semibold flex items-center gap-2">
              {saving ? <><Loader2 size={14} className="animate-spin" /> Guardando…</> : <>Crear solicitud</>}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
