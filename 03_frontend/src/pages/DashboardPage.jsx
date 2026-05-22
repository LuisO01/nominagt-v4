import { useEffect, useMemo, useState, useCallback } from 'react';
import { dashboardApi, getErrorMessage } from '../services/api';
import { useAuth } from '../context/AuthContext';
import {
  Users, UserMinus, DollarSign, FileCheck, AlertCircle,
  RefreshCw, TrendingUp, Wallet, CalendarDays, ArrowUpRight
} from 'lucide-react';

// ─── Helpers ──────────────────────────────────────────────────────────
const fmtMoney = (n) =>
  new Intl.NumberFormat('es-GT', { style: 'currency', currency: 'GTQ', maximumFractionDigits: 2 })
    .format(Number(n) || 0);

const fmtInt = (n) => new Intl.NumberFormat('es-GT').format(Number(n) || 0);

const MESES = ['Ene','Feb','Mar','Abr','May','Jun','Jul','Ago','Sep','Oct','Nov','Dic'];

// Static Tailwind class maps (dynamic class names get purged by JIT).
const COLOR = {
  emerald: { ring: 'bg-emerald-50', icon: 'text-emerald-600', accent: 'bg-emerald-500' },
  red:     { ring: 'bg-red-50',     icon: 'text-red-600',     accent: 'bg-red-500' },
  amber:   { ring: 'bg-amber-50',   icon: 'text-amber-600',   accent: 'bg-amber-500' },
  blue:    { ring: 'bg-blue-50',    icon: 'text-blue-600',    accent: 'bg-blue-500' },
  violet:  { ring: 'bg-violet-50',  icon: 'text-violet-600',  accent: 'bg-violet-500' },
};

// ─── Sub-components ───────────────────────────────────────────────────
function KpiCard({ label, value, icon: Icon, color = 'amber', hint }) {
  const c = COLOR[color] || COLOR.amber;
  return (
    <div className="group bg-white p-5 rounded-xl border border-stone-200 hover:border-stone-300 hover:shadow-sm transition">
      <div className="flex items-start justify-between">
        <div className={`w-10 h-10 rounded-lg ${c.ring} flex items-center justify-center`}>
          <Icon size={18} className={c.icon} />
        </div>
        <ArrowUpRight size={16} className="text-stone-300 group-hover:text-stone-400 transition" />
      </div>
      <p className="text-xs text-stone-500 uppercase tracking-wide mt-4">{label}</p>
      <p className="text-2xl font-bold text-stone-800 mt-1 tabular-nums">{value}</p>
      {hint && <p className="text-xs text-stone-400 mt-1">{hint}</p>}
    </div>
  );
}

function SkeletonCard() {
  return (
    <div className="bg-white p-5 rounded-xl border border-stone-200 animate-pulse">
      <div className="w-10 h-10 rounded-lg bg-stone-100" />
      <div className="h-3 w-24 bg-stone-100 rounded mt-4" />
      <div className="h-7 w-32 bg-stone-100 rounded mt-2" />
    </div>
  );
}

function ErrorCard({ message, onRetry }) {
  return (
    <div className="bg-red-50 border border-red-200 text-red-700 rounded-xl p-5 flex items-start gap-3">
      <AlertCircle size={20} className="flex-shrink-0 mt-0.5" />
      <div className="flex-1">
        <p className="font-medium">No se pudieron cargar los KPIs</p>
        <p className="text-sm text-red-600/80 mt-0.5">{message}</p>
      </div>
      {onRetry && (
        <button
          onClick={onRetry}
          className="text-sm font-medium px-3 py-1.5 rounded-lg bg-white border border-red-200 hover:bg-red-100 transition flex items-center gap-1.5"
        >
          <RefreshCw size={14} /> Reintentar
        </button>
      )}
    </div>
  );
}

function ResumenAnual({ data }) {
  // data is expected to be an array of { mes, total } or similar.
  const rows = useMemo(() => {
    if (!Array.isArray(data)) return [];
    return data
      .map((r) => ({
        mes: Number(r.MES ?? r.mes ?? r.Mes ?? 0),
        total: Number(
          r.SUMA_NETO ?? r.sumaNeto ?? r.suma_neto ??
          r.totalNeto ?? r.TotalNeto ?? r.total ?? 0
        ),
      }))
      .filter((r) => r.mes >= 1 && r.mes <= 12);
  }, [data]);

  const max = Math.max(1, ...rows.map((r) => r.total));
  const total = rows.reduce((s, r) => s + r.total, 0);

  if (rows.length === 0) {
    return (
      <div className="text-sm text-stone-400 py-8 text-center">
        Sin datos de nómina para el año actual.
      </div>
    );
  }

  return (
    <div>
      <div className="flex items-end gap-2 h-40 mt-2">
        {Array.from({ length: 12 }, (_, i) => {
          const row = rows.find((r) => r.mes === i + 1);
          const v = row?.total || 0;
          const h = (v / max) * 100;
          return (
            <div key={i} className="flex-1 flex flex-col items-center gap-1 group">
              <div className="w-full flex-1 flex items-end">
                <div
                  className="w-full rounded-t bg-amber-400 group-hover:bg-amber-500 transition"
                  style={{ height: `${h}%`, minHeight: v > 0 ? '4px' : '0' }}
                  title={v > 0 ? fmtMoney(v) : 'Sin datos'}
                />
              </div>
              <span className="text-[10px] text-stone-400 uppercase">{MESES[i]}</span>
            </div>
          );
        })}
      </div>
      <div className="flex justify-between text-xs text-stone-500 mt-3 pt-3 border-t border-stone-100">
        <span>Total acumulado del año</span>
        <span className="font-semibold tabular-nums text-stone-700">{fmtMoney(total)}</span>
      </div>
    </div>
  );
}

// ─── Main page ────────────────────────────────────────────────────────
const ROL_GREETING = {
  ADMIN:    { titulo: 'Panel ejecutivo',         lead: 'Visión completa de la operación' },
  RRHH:     { titulo: 'Recursos Humanos',         lead: 'Gestión de personal y plantilla' },
  NOMINA:   { titulo: 'Operación de nómina',      lead: 'Cálculo, aprobación y reportes' },
  AUDITOR:  { titulo: 'Auditoría',                lead: 'Lectura y verificación' },
  EMPLEADO: { titulo: 'Mi espacio',               lead: 'Tus datos y tus boletas' },
};

export default function DashboardPage() {
  const { user, puede, isAdmin } = useAuth();
  // Capacidades que filtran las tarjetas de KPI
  const verNomina    = isAdmin || puede('reporte.exportar') || puede('periodo.crear');
  const verPrestamos = isAdmin; // dato sensible: solo ADMIN
  const verPeriodos  = isAdmin || puede('periodo.crear') || puede('periodo.aprobar');
  const verResumen   = isAdmin || puede('reporte.exportar') || puede('periodo.crear');
  const [kpis, setKpis] = useState(null);
  const [resumen, setResumen] = useState(null);
  const [loadingKpis, setLoadingKpis] = useState(true);
  const [loadingResumen, setLoadingResumen] = useState(true);
  const [errKpis, setErrKpis] = useState('');
  const [errResumen, setErrResumen] = useState('');
  const [updatedAt, setUpdatedAt] = useState(null);

  const cargar = useCallback(async () => {
    setLoadingKpis(true);
    setLoadingResumen(true);
    setErrKpis('');
    setErrResumen('');

    // Cargamos KPIs (todos los roles con dashboard) y, solo si el rol lo amerita,
    // tambien el resumen anual.
    const anio = new Date().getFullYear();
    const promesas = [dashboardApi.kpis()];
    if (verResumen) promesas.push(dashboardApi.resumenAnual(anio));

    const [kpisRes, resumenRes] = await Promise.allSettled(promesas);

    if (kpisRes.status === 'fulfilled') setKpis(kpisRes.value);
    else                                setErrKpis(getErrorMessage(kpisRes.reason));

    if (verResumen && resumenRes) {
      if (resumenRes.status === 'fulfilled') setResumen(resumenRes.value);
      else                                   setErrResumen(getErrorMessage(resumenRes.reason));
    }

    setLoadingKpis(false);
    setLoadingResumen(false);
    setUpdatedAt(new Date());
  }, [verResumen]);

  useEffect(() => { cargar(); }, [cargar]);

  // Auto-refresh: cada 60s mientras la pestaña este visible, y siempre al volver
  // a la pestaña (asi si calculas/apruebas un periodo en otra ventana y vuelves,
  // el dashboard ya muestra los nuevos datos).
  useEffect(() => {
    const onFocus = () => { if (!document.hidden) cargar(); };
    document.addEventListener('visibilitychange', onFocus);
    window.addEventListener('focus', onFocus);
    const interval = setInterval(() => { if (!document.hidden) cargar(); }, 60_000);
    return () => {
      document.removeEventListener('visibilitychange', onFocus);
      window.removeEventListener('focus', onFocus);
      clearInterval(interval);
    };
  }, [cargar]);

  // Filtramos las tarjetas KPI segun el rol. Cada una indica que capacidad la habilita.
  const cards = useMemo(() => {
    const todas = [
      {
        show: true,
        label: 'Empleados activos',
        value: loadingKpis ? '—' : fmtInt(kpis?.totalEmpleadosActivos),
        icon: Users, color: 'emerald',
      },
      {
        show: true,
        label: 'Empleados de baja',
        value: loadingKpis ? '—' : fmtInt(kpis?.totalEmpleadosBaja),
        icon: UserMinus, color: 'red',
      },
      {
        show: verNomina,
        label: 'Nómina último mes',
        value: loadingKpis ? '—' : fmtMoney(kpis?.nominaUltimoMes),
        icon: DollarSign, color: 'amber',
        hint: 'Suma neta del periodo más reciente',
      },
      {
        show: verPeriodos,
        label: 'Periodos aprobados',
        value: loadingKpis ? '—' : fmtInt(kpis?.periodosAprobadosAnio),
        icon: FileCheck, color: 'blue',
        hint: `Año ${new Date().getFullYear()}`,
      },
      {
        show: verPrestamos,
        label: 'Saldo de préstamos',
        value: loadingKpis ? '—' : fmtMoney(kpis?.saldoPrestamosVigentes),
        icon: Wallet, color: 'violet',
        hint: 'Vigentes en toda la empresa',
      },
    ];
    return todas.filter(c => c.show);
  }, [kpis, loadingKpis, verNomina, verPeriodos, verPrestamos]);

  const hoy = new Date().toLocaleDateString('es-GT', {
    weekday: 'long', year: 'numeric', month: 'long', day: 'numeric',
  });
  const greet = ROL_GREETING[user?.rolActivo] || ROL_GREETING.EMPLEADO;

  return (
    <div className="p-8 max-w-7xl mx-auto">
      {/* Header */}
      <div className="flex flex-wrap items-end justify-between gap-4 mb-6">
        <div>
          <h1 className="text-2xl font-bold text-stone-800">
            Hola, {user?.nombreUsuario || 'usuario'} 👋
          </h1>
          <p className="text-sm text-stone-500 mt-1 flex items-center gap-2 capitalize">
            <CalendarDays size={14} /> {hoy}
            <span className="inline-flex items-center gap-1 text-xs font-semibold uppercase tracking-wide text-amber-700 bg-amber-50 px-2 py-0.5 rounded normal-case">
              {greet.titulo}
            </span>
          </p>
          <p className="text-xs text-stone-400 mt-0.5">{greet.lead}</p>
        </div>
        <div className="flex items-center gap-3">
          {updatedAt && !loadingKpis && (
            <span className="text-xs text-stone-400">
              Actualizado {updatedAt.toLocaleTimeString('es-GT', { hour: '2-digit', minute: '2-digit' })}
            </span>
          )}
          <button
            onClick={cargar}
            disabled={loadingKpis || loadingResumen}
            className="text-sm font-medium px-3 py-2 rounded-lg bg-white border border-stone-200 hover:bg-stone-50 disabled:opacity-50 disabled:cursor-not-allowed transition flex items-center gap-1.5"
          >
            <RefreshCw size={14} className={loadingKpis || loadingResumen ? 'animate-spin' : ''} />
            Actualizar
          </button>
        </div>
      </div>

      {/* KPIs */}
      {errKpis && !loadingKpis ? (
        <ErrorCard message={errKpis} onRetry={cargar} />
      ) : (
        <div className={`grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 ${cards.length >= 5 ? 'xl:grid-cols-5' : cards.length >= 4 ? 'xl:grid-cols-4' : ''} gap-4`}>
          {loadingKpis
            ? Array.from({ length: cards.length || 3 }).map((_, i) => <SkeletonCard key={i} />)
            : cards.map((c, i) => <KpiCard key={i} {...c} />)}
        </div>
      )}

      {/* Resumen anual: oculto para roles que no lo necesitan */}
      {verResumen && (
      <div className="mt-8 bg-white rounded-xl border border-stone-200 p-5">
        <div className="flex items-center justify-between mb-3">
          <div>
            <h2 className="font-semibold text-stone-800 flex items-center gap-2">
              <TrendingUp size={16} className="text-amber-500" />
              Nómina por mes — {new Date().getFullYear()}
            </h2>
            <p className="text-xs text-stone-500 mt-0.5">Suma neta liquidada en el año en curso</p>
          </div>
        </div>

        {loadingResumen ? (
          <div className="h-40 bg-stone-50 rounded animate-pulse" />
        ) : errResumen ? (
          <div className="text-sm text-stone-500 py-8 text-center">
            <AlertCircle size={16} className="inline mr-1 text-stone-400" />
            {errResumen}
          </div>
        ) : (
          <ResumenAnual data={resumen} />
        )}
      </div>
      )}
    </div>
  );
}
