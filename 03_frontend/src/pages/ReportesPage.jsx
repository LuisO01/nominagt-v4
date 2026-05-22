import { useEffect, useMemo, useState } from 'react';
import { reportesApi, getErrorMessage } from '../services/api';
import { useToast } from '../context/ToastContext';
import { useAuth } from '../context/AuthContext';
import {
  Download, FileSpreadsheet, FileText, Loader2, Database, AlertCircle,
  Users, DollarSign, TrendingDown, Wallet, BarChart3, RefreshCw, BadgeDollarSign, Mail,
} from 'lucide-react';
import EnviarEmailModal from '../components/EnviarEmailModal';

const MESES = [
  'Enero','Febrero','Marzo','Abril','Mayo','Junio',
  'Julio','Agosto','Septiembre','Octubre','Noviembre','Diciembre',
];

const fmtQ   = (n) => new Intl.NumberFormat('es-GT', { style: 'currency', currency: 'GTQ' }).format(Number(n) || 0);
const fmtInt = (n) => new Intl.NumberFormat('es-GT').format(Number(n) || 0);
const fmtPct = (n) => `${(Number(n) || 0).toFixed(2)} %`;

// Cargas patronales (IGSS patronal 10.67% + IRTRA 1% + INTECAP 1% = 12.67%)
const TASA_PATRONAL_TOTAL = 0.1267;

export default function ReportesPage() {
  const toast = useToast();
  const { puede } = useAuth();
  const puedeExcel = puede('reporte.exportar-excel');
  const puedePdf   = puede('reporte.exportar-pdf');

  const [anio, setAnio] = useState(2026);
  const [mes, setMes] = useState(new Date().getMonth() + 1);
  const [tipoPeriodo, setTipoPeriodo] = useState('MENSUAL');
  const [loading, setLoading] = useState({ xlsx: false, pdf: false });
  const [emailModal, setEmailModal] = useState(null); // { formato: 'excel' | 'pdf' }
  const [previewLoading, setPreviewLoading] = useState(false);
  const [previewError, setPreviewError] = useState('');
  const [previewData, setPreviewData] = useState(null);

  const descargar = async (formato) => {
    setLoading((s) => ({ ...s, [formato]: true }));
    try {
      if (formato === 'xlsx') await reportesApi.descargarExcelMensual(anio, mes, tipoPeriodo);
      else                    await reportesApi.descargarPdfMensual(anio, mes, tipoPeriodo);
      toast.success(`Reporte ${formato.toUpperCase()} descargado correctamente.`);
    } catch (e) {
      toast.error(getErrorMessage(e), { title: `No se pudo descargar el ${formato.toUpperCase()}` });
    } finally {
      setLoading((s) => ({ ...s, [formato]: false }));
    }
  };

  // ─── Preview ───
  // Cuando cambia el filtro, descargamos los datos JSON y calculamos agregados.
  useEffect(() => {
    let cancelled = false;
    (async () => {
      setPreviewLoading(true);
      setPreviewError('');
      try {
        const json = await reportesApi.jsonMensual(anio, mes, tipoPeriodo);
        if (cancelled) return;
        // El backend devuelve { data: [...], total: N } o similar
        const rows = json?.data ?? json ?? [];
        setPreviewData(Array.isArray(rows) ? rows : []);
      } catch (e) {
        if (cancelled) return;
        setPreviewError(getErrorMessage(e));
        setPreviewData(null);
      } finally {
        if (!cancelled) setPreviewLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [anio, mes, tipoPeriodo]);

  // ─── Agregados del preview ───
  const agg = useMemo(() => {
    if (!previewData || previewData.length === 0) return null;
    const get = (r, k) => Number(r[k] ?? r[k.toLowerCase()] ?? 0);
    const getS = (r, k) => String(r[k] ?? r[k.toLowerCase()] ?? '');

    let totalIngresos = 0, totalDeducciones = 0, totalNeto = 0;
    let totalSalario = 0, totalBonif = 0, totalIgss = 0, totalIsr = 0, totalPrest = 0;
    const porDepto = new Map();
    const porPuesto = new Map();

    for (const r of previewData) {
      const ing  = get(r, 'TOTAL_INGRESOS');
      const ded  = get(r, 'TOTAL_DEDUCCIONES');
      const neto = get(r, 'SALARIO_NETO');
      const sal  = get(r, 'SALARIO_BASE');
      const bonif = get(r, 'BONIFICACION');
      const igss = get(r, 'IGSS');
      const isr  = get(r, 'ISR');
      const prest = get(r, 'DESCUENTO_PRESTAMOS');
      const depto = getS(r, 'DEPARTAMENTO') || 'Sin depto';
      const puesto = getS(r, 'PUESTO') || 'Sin puesto';

      totalIngresos += ing; totalDeducciones += ded; totalNeto += neto;
      totalSalario += sal; totalBonif += bonif;
      totalIgss += igss; totalIsr += isr; totalPrest += prest;

      const d = porDepto.get(depto) || { nombre: depto, cantidad: 0, neto: 0 };
      d.cantidad++; d.neto += neto;
      porDepto.set(depto, d);

      const p = porPuesto.get(puesto) || { nombre: puesto, cantidad: 0, neto: 0 };
      p.cantidad++; p.neto += neto;
      porPuesto.set(puesto, p);
    }

    const totalEmpleados = previewData.length;
    const promedio = totalEmpleados === 0 ? 0 : totalNeto / totalEmpleados;
    const pctDeduc = totalIngresos === 0 ? 0 : (totalDeducciones / totalIngresos) * 100;
    const cargasPatronales = totalSalario * TASA_PATRONAL_TOTAL;
    const costoEmpresa = totalIngresos + cargasPatronales;

    const departamentos = Array.from(porDepto.values())
      .sort((a, b) => b.neto - a.neto)
      .slice(0, 8);
    const puestos = Array.from(porPuesto.values())
      .sort((a, b) => b.neto - a.neto)
      .slice(0, 6);

    const topEmpleados = [...previewData]
      .map((r) => ({
        codigo: getS(r, 'CODIGO_EMPLEADO'),
        nombre: getS(r, 'NOMBRE_EMPLEADO'),
        neto:   get(r, 'SALARIO_NETO'),
      }))
      .sort((a, b) => b.neto - a.neto)
      .slice(0, 5);

    return {
      totalEmpleados, totalIngresos, totalDeducciones, totalNeto,
      totalSalario, totalBonif, totalIgss, totalIsr, totalPrest,
      promedio, pctDeduc, cargasPatronales, costoEmpresa,
      departamentos, puestos, topEmpleados,
    };
  }, [previewData]);

  // Refrescar preview manual
  const refrescarPreview = () => {
    // Forzar re-cargar cambiando un dummy bit
    setPreviewData(null);
    setPreviewLoading(true);
    (async () => {
      try {
        const json = await reportesApi.jsonMensual(anio, mes, tipoPeriodo);
        const rows = json?.data ?? json ?? [];
        setPreviewData(Array.isArray(rows) ? rows : []);
      } catch (e) {
        setPreviewError(getErrorMessage(e));
      } finally { setPreviewLoading(false); }
    })();
  };

  if (!puedeExcel && !puedePdf) {
    return (
      <div className="p-4 md:p-8 max-w-3xl mx-auto">
        <h1 className="text-2xl font-bold text-stone-800 mb-4">Reportes</h1>
        <div className="bg-amber-50 border border-amber-200 rounded-xl p-5 flex items-start gap-3 text-amber-800">
          <AlertCircle size={18} className="flex-shrink-0 mt-0.5" />
          <p className="text-sm">No tienes permisos para exportar reportes.</p>
        </div>
      </div>
    );
  }

  return (
    <div className="p-4 md:p-8 max-w-7xl mx-auto">
      {/* Header */}
      <div className="flex items-end justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-stone-800 flex items-center gap-2">
            <BarChart3 size={22} /> Reportes mensuales
          </h1>
          <p className="text-sm text-stone-500 mt-1">
            Vista previa del periodo seleccionado. Genera Excel o PDF con un click.
          </p>
        </div>
      </div>

      {/* Selector de periodo */}
      <div className="bg-white p-5 rounded-xl border border-stone-200 mb-4">
        <div className="flex items-center justify-between mb-3">
          <p className="text-xs font-semibold text-stone-500 uppercase">Periodo a analizar</p>
          <button
            onClick={refrescarPreview}
            disabled={previewLoading}
            className="text-xs text-stone-500 hover:text-stone-700 flex items-center gap-1 disabled:opacity-50"
          >
            <RefreshCw size={12} className={previewLoading ? 'animate-spin' : ''} /> Actualizar preview
          </button>
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-4 gap-3">
          <div>
            <label className="block text-xs font-medium text-stone-600 mb-1">Año</label>
            <input
              type="number"
              value={anio}
              onChange={(e) => setAnio(parseInt(e.target.value) || anio)}
              min={2020}
              max={2050}
              className="w-full px-3 py-2 text-sm border border-stone-200 rounded-lg"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-stone-600 mb-1">Mes</label>
            <select
              value={mes}
              onChange={(e) => setMes(parseInt(e.target.value))}
              className="w-full px-3 py-2 text-sm border border-stone-200 rounded-lg bg-white"
            >
              {MESES.map((m, i) => <option key={i} value={i + 1}>{m}</option>)}
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-stone-600 mb-1">Tipo</label>
            <select
              value={tipoPeriodo}
              onChange={(e) => setTipoPeriodo(e.target.value)}
              className="w-full px-3 py-2 text-sm border border-stone-200 rounded-lg bg-white"
            >
              <option>MENSUAL</option>
              <option>QUINCENAL</option>
              <option>BONO14</option>
              <option>AGUINALDO</option>
            </select>
          </div>
          <div className="flex items-end gap-2">
            {puedeExcel && (
              <button
                onClick={() => descargar('xlsx')}
                disabled={loading.xlsx || !agg}
                title={!agg ? 'Sin datos en el periodo' : ''}
                className="flex-1 flex items-center justify-center gap-2 bg-emerald-600 hover:bg-emerald-700 disabled:bg-stone-300 text-white font-semibold text-sm py-2 rounded-lg"
              >
                {loading.xlsx ? <Loader2 size={14} className="animate-spin" /> : <FileSpreadsheet size={14} />}
                Excel
              </button>
            )}
            {puedePdf && (
              <button
                onClick={() => descargar('pdf')}
                disabled={loading.pdf || !agg}
                title={!agg ? 'Sin datos en el periodo' : ''}
                className="flex-1 flex items-center justify-center gap-2 bg-red-600 hover:bg-red-700 disabled:bg-stone-300 text-white font-semibold text-sm py-2 rounded-lg"
              >
                {loading.pdf ? <Loader2 size={14} className="animate-spin" /> : <FileText size={14} />}
                PDF
              </button>
            )}
          </div>
        </div>

        {/* Acciones secundarias: enviar por email */}
        {agg && (puedeExcel || puedePdf) && (
          <div className="flex items-center gap-2 mt-3 pt-3 border-t border-stone-100">
            <span className="text-xs text-stone-500">Enviar por correo:</span>
            {puedeExcel && (
              <button
                onClick={() => setEmailModal({ formato: 'excel' })}
                className="text-xs font-medium text-emerald-600 hover:text-emerald-700 hover:underline flex items-center gap-1"
              >
                <Mail size={12} /> Excel por email
              </button>
            )}
            {puedePdf && (
              <button
                onClick={() => setEmailModal({ formato: 'pdf' })}
                className="text-xs font-medium text-red-600 hover:text-red-700 hover:underline flex items-center gap-1"
              >
                <Mail size={12} /> PDF por email
              </button>
            )}
          </div>
        )}
      </div>

      {/* Preview */}
      {previewLoading ? (
        <div className="bg-white rounded-xl border border-stone-200 p-12 text-center text-stone-400">
          <Loader2 className="animate-spin inline mr-2" /> Cargando preview...
        </div>
      ) : previewError ? (
        <div className="flex items-start gap-3 p-4 rounded-xl border border-red-200 bg-red-50 text-red-700">
          <AlertCircle size={18} className="mt-0.5" />
          <p className="text-sm">{previewError}</p>
        </div>
      ) : !agg ? (
        <div className="bg-white rounded-xl border border-stone-200 p-12 text-center">
          <FileSpreadsheet size={32} className="mx-auto text-stone-300 mb-2" />
          <p className="text-stone-500">
            No hay nóminas calculadas para {MESES[mes - 1]} {anio} ({tipoPeriodo}).
          </p>
          <p className="text-xs text-stone-400 mt-1">Crea y calcula el periodo desde la pestaña Nómina.</p>
        </div>
      ) : (
        <PreviewDashboard agg={agg} anio={anio} mes={mes} tipoPeriodo={tipoPeriodo} />
      )}

      {/* Power BI */}
      <div className="bg-white p-6 rounded-xl border border-stone-200 mt-6">
        <div className="flex items-center gap-3 mb-3">
          <div className="w-10 h-10 rounded-xl bg-amber-50 flex items-center justify-center">
            <Database size={20} className="text-amber-600" />
          </div>
          <div>
            <h2 className="font-bold text-stone-800">Conexión directa a Power BI</h2>
            <p className="text-xs text-stone-500">Para dashboards interactivos vía ODBC</p>
          </div>
        </div>
        <div className="bg-stone-50 rounded-lg p-3 text-xs space-y-1 font-mono">
          <div><span className="text-stone-400">Servidor:</span> localhost:1521/xepdb1 &nbsp;
                <span className="text-stone-400">Usuario:</span> nominagt &nbsp;
                <span className="text-stone-400">Password:</span> NominaGT2026</div>
          <div className="text-stone-500 mt-1">
            Vistas: <code className="text-amber-600">vw_pbi_empleados</code>,&nbsp;
            <code className="text-amber-600">vw_pbi_nomina_mensual</code>,&nbsp;
            <code className="text-amber-600">vw_pbi_resumen_mensual</code>,&nbsp;
            <code className="text-amber-600">vw_pbi_historico_empleado</code>
          </div>
        </div>
      </div>

      {emailModal && (
        <EnviarEmailModal
          titulo={`Enviar ${emailModal.formato === 'pdf' ? 'PDF' : 'Excel'} por correo`}
          defaultAsunto={`Planilla de nómina — ${MESES[mes - 1]} ${anio} (${tipoPeriodo})`}
          defaultMensaje={`Adjunto el reporte de nómina correspondiente a ${MESES[mes - 1]} ${anio} (${tipoPeriodo}).`}
          nombreArchivo={`Planilla_${anio}_${String(mes).padStart(2,'0')}_${tipoPeriodo}.${emailModal.formato === 'pdf' ? 'pdf' : 'xlsx'}`}
          onClose={() => setEmailModal(null)}
          onEnviar={async ({ para, asunto, mensaje }) => {
            await reportesApi.enviarPorEmail({
              para, formato: emailModal.formato, anio, mes, tipoPeriodo, asunto, mensaje,
            });
          }}
        />
      )}
    </div>
  );
}

// ════════════════════════════════════════════════════════════════════════
//  Preview dashboard
// ════════════════════════════════════════════════════════════════════════
function PreviewDashboard({ agg, anio, mes, tipoPeriodo }) {
  return (
    <div className="space-y-4">
      <div className="bg-white rounded-xl border border-stone-200 px-5 py-3 flex items-center justify-between">
        <div>
          <p className="text-xs uppercase tracking-wide text-stone-400">Preview del periodo</p>
          <h2 className="text-lg font-bold text-stone-800">
            {MESES[mes - 1]} {anio} · {tipoPeriodo}
          </h2>
        </div>
        <span className="text-xs text-stone-400">
          {agg.totalEmpleados} empleado{agg.totalEmpleados === 1 ? '' : 's'} incluidos
        </span>
      </div>

      {/* Fila de KPIs principales */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <KpiCard icon={Users}     color="emerald" label="Empleados"        value={fmtInt(agg.totalEmpleados)} />
        <KpiCard icon={DollarSign} color="amber"   label="Total ingresos"  value={fmtQ(agg.totalIngresos)} />
        <KpiCard icon={TrendingDown} color="red"   label="Total deducciones" value={fmtQ(agg.totalDeducciones)}
                 hint={fmtPct(agg.pctDeduc) + ' de los ingresos'} />
        <KpiCard icon={Wallet}    color="dark"    label="Neto a pagar"     value={fmtQ(agg.totalNeto)} emphasize />
      </div>

      {/* Fila de KPIs secundarios */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <KpiCard icon={BadgeDollarSign} color="blue"   label="Salario promedio" value={fmtQ(agg.promedio)} />
        <KpiCard icon={TrendingDown} color="violet"   label="IGSS retenido"    value={fmtQ(agg.totalIgss)} hint="4.83% laboral" />
        <KpiCard icon={TrendingDown} color="violet"   label="ISR retenido"     value={fmtQ(agg.totalIsr)} hint="5% / 7% progresivo" />
        <KpiCard icon={Wallet}    color="orange"  label="Costo empresa"   value={fmtQ(agg.costoEmpresa)} hint="Ingresos + aportes patronales" />
      </div>

      {/* Distribución y top */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-3">
        <CardBarChart title="Distribución por departamento" rows={agg.departamentos} color="bg-amber-400" />
        <CardBarChart title="Distribución por puesto"       rows={agg.puestos}       color="bg-blue-400" />
      </div>

      {/* Top 5 + Cargas patronales */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-3">
        <div className="bg-white rounded-xl border border-stone-200 p-5">
          <p className="text-xs font-semibold text-stone-500 uppercase mb-3">Top 5 salarios netos</p>
          <ol className="space-y-1.5">
            {agg.topEmpleados.map((e, i) => (
              <li key={e.codigo + i} className="flex items-center justify-between text-sm">
                <div className="flex items-center gap-3 min-w-0">
                  <span className={`w-6 h-6 rounded-full flex items-center justify-center text-xs font-bold ${
                    i === 0 ? 'bg-amber-100 text-amber-700' : 'bg-stone-100 text-stone-500'
                  }`}>{i + 1}</span>
                  <div className="min-w-0">
                    <p className="font-mono text-xs text-amber-600 font-bold truncate">{e.codigo}</p>
                    <p className="text-stone-700 text-xs truncate">{e.nombre}</p>
                  </div>
                </div>
                <span className="font-bold tabular-nums text-stone-800 flex-shrink-0">{fmtQ(e.neto)}</span>
              </li>
            ))}
          </ol>
        </div>

        <div className="bg-white rounded-xl border border-stone-200 p-5">
          <p className="text-xs font-semibold text-stone-500 uppercase mb-3">Cargas patronales (lo que cuesta al patrono)</p>
          <ul className="space-y-2 text-sm">
            <Linea label="Salario base nómina"        value={agg.totalSalario} muted />
            <Linea label="(+) IGSS patronal (10.67%)" value={agg.totalSalario * 0.1067} />
            <Linea label="(+) IRTRA (1%)"             value={agg.totalSalario * 0.01} />
            <Linea label="(+) INTECAP (1%)"           value={agg.totalSalario * 0.01} />
            <li className="border-t border-stone-100 pt-2 flex items-center justify-between font-bold text-violet-700">
              <span>Total aportes patronales</span>
              <span className="tabular-nums">{fmtQ(agg.cargasPatronales)}</span>
            </li>
            <li className="flex items-center justify-between text-stone-600 mt-3">
              <span>Bruto pagado a empleados</span>
              <span className="tabular-nums">{fmtQ(agg.totalIngresos)}</span>
            </li>
            <li className="border-t border-stone-100 pt-2 flex items-center justify-between font-bold text-amber-700">
              <span>COSTO TOTAL del periodo</span>
              <span className="tabular-nums">{fmtQ(agg.costoEmpresa)}</span>
            </li>
          </ul>
        </div>
      </div>
    </div>
  );
}

function KpiCard({ icon: Icon, color, label, value, hint, emphasize }) {
  const cls = {
    emerald: 'bg-emerald-50 text-emerald-600',
    amber:   'bg-amber-50 text-amber-600',
    red:     'bg-red-50 text-red-600',
    blue:    'bg-blue-50 text-blue-600',
    violet:  'bg-violet-50 text-violet-600',
    orange:  'bg-orange-50 text-orange-600',
    dark:    'bg-stone-100 text-stone-700',
  }[color] || 'bg-stone-100 text-stone-500';

  return (
    <div className={`rounded-xl border border-stone-200 p-4 ${emphasize ? 'bg-stone-900 text-white' : 'bg-white'}`}>
      <div className="flex items-center justify-between mb-2">
        <div className={`w-9 h-9 rounded-lg flex items-center justify-center ${emphasize ? 'bg-amber-500/10' : cls}`}>
          <Icon size={16} className={emphasize ? 'text-amber-400' : ''} />
        </div>
      </div>
      <p className={`text-xs uppercase tracking-wide ${emphasize ? 'text-stone-300' : 'text-stone-400'}`}>{label}</p>
      <p className={`text-xl font-bold mt-1 tabular-nums ${emphasize ? 'text-amber-400' : 'text-stone-800'}`}>{value}</p>
      {hint && <p className={`text-xs mt-1 ${emphasize ? 'text-stone-400' : 'text-stone-400'}`}>{hint}</p>}
    </div>
  );
}

function CardBarChart({ title, rows, color }) {
  const max = rows.length === 0 ? 1 : Math.max(...rows.map((r) => r.neto));
  return (
    <div className="bg-white rounded-xl border border-stone-200 p-5">
      <p className="text-xs font-semibold text-stone-500 uppercase mb-3">{title}</p>
      {rows.length === 0 ? (
        <p className="text-sm text-stone-400 py-4 text-center">Sin datos.</p>
      ) : (
        <ul className="space-y-2">
          {rows.map((r) => {
            const pct = max === 0 ? 0 : (r.neto / max) * 100;
            return (
              <li key={r.nombre}>
                <div className="flex items-center justify-between text-xs mb-1">
                  <span className="text-stone-700 truncate">{r.nombre}</span>
                  <span className="text-stone-500 tabular-nums flex-shrink-0 ml-2">
                    {r.cantidad}× · {fmtQ(r.neto)}
                  </span>
                </div>
                <div className="w-full h-2 bg-stone-100 rounded-full overflow-hidden">
                  <div className={`h-full ${color}`} style={{ width: `${pct}%` }} />
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}

function Linea({ label, value, muted }) {
  return (
    <li className={`flex items-center justify-between ${muted ? 'text-stone-500' : 'text-stone-700'}`}>
      <span>{label}</span>
      <span className="tabular-nums">{fmtQ(value)}</span>
    </li>
  );
}
