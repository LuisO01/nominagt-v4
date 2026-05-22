import { useEffect, useMemo, useState } from 'react';
import { nominaApi, getErrorMessage } from '../services/api';
import { useToast } from '../context/ToastContext';
import { useAuth } from '../context/AuthContext';
import {
  Calculator, Plus, Loader2, AlertCircle, RefreshCw, X, CheckCircle2, FileText
} from 'lucide-react';

const MESES = [
  'Enero','Febrero','Marzo','Abril','Mayo','Junio',
  'Julio','Agosto','Septiembre','Octubre','Noviembre','Diciembre'
];

const TIPOS = ['MENSUAL', 'QUINCENAL', 'BONO14', 'AGUINALDO'];

const fmtFecha = (s) => (s ? s.slice(0, 10) : '—');

export default function NominaPage() {
  const toast = useToast();
  const { puede } = useAuth();
  const puedeCrearPeriodo = puede('periodo.crear');
  const puedeCalcular     = puede('periodo.calcular');
  const puedeAprobar      = puede('periodo.aprobar');
  const hoy = new Date();
  const [anio, setAnio] = useState(hoy.getFullYear());
  const [periodos, setPeriodos] = useState([]);
  const [loading, setLoading] = useState(true);
  const [errLoad, setErrLoad] = useState('');
  const [calcId, setCalcId] = useState(null);
  const [confirmCalc, setConfirmCalc] = useState(null);
  const [showNuevo, setShowNuevo] = useState(false);
  const [creating, setCreating] = useState(false);
  const [nuevo, setNuevo] = useState({
    anio: hoy.getFullYear(),
    mes: hoy.getMonth() + 1,
    tipoPeriodo: 'MENSUAL',
  });

  const reload = async (a = anio) => {
    setLoading(true);
    setErrLoad('');
    try {
      const data = await nominaApi.listarPeriodos(a);
      setPeriodos(Array.isArray(data) ? data : []);
    } catch (e) {
      setErrLoad(getErrorMessage(e));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { reload(anio); }, [anio]);

  // Detecta colisiones contra los periodos ya cargados (mismo año + mes + tipo)
  const colision = useMemo(() => {
    if (Number(nuevo.anio) !== anio) return false;
    return periodos.some(p =>
      Number(p.anio) === Number(nuevo.anio) &&
      Number(p.mes)  === Number(nuevo.mes) &&
      p.tipoPeriodo  === nuevo.tipoPeriodo
    );
  }, [nuevo, periodos, anio]);

  const crearPeriodo = async () => {
    setCreating(true);
    try {
      const result = await nominaApi.crearPeriodo(
        Number(nuevo.anio), Number(nuevo.mes), nuevo.tipoPeriodo
      );
      const id = result?.periodoId ?? result?.PeriodoId ?? result?.id ?? result;
      setShowNuevo(false);
      // Si crearon en otro año, cambiamos el filtro a ese año
      if (Number(nuevo.anio) !== anio) setAnio(Number(nuevo.anio));
      else await reload();
      toast.success(
        `Periodo ${nuevo.anio}-${String(nuevo.mes).padStart(2,'0')} ${nuevo.tipoPeriodo} creado.${id ? ` (ID ${id})` : ''}`
      );
    } catch (e) {
      toast.error(getErrorMessage(e), { title: 'No se pudo crear el periodo' });
    } finally {
      setCreating(false);
    }
  };

  const calcular = async (id) => {
    setCalcId(id);
    setConfirmCalc(null);
    try {
      const total = await nominaApi.calcular(id);
      const n = Number(total) || 0;
      toast.success(`Nómina calculada para ${n} empleado${n === 1 ? '' : 's'}.`);
      await reload();
    } catch (e) {
      toast.error(getErrorMessage(e), { title: 'Error en el cálculo' });
    } finally {
      setCalcId(null);
    }
  };

  const aprobar = async (id) => {
    try {
      await nominaApi.aprobar(id);
      toast.success('Periodo aprobado.');
      await reload();
    } catch (e) {
      toast.error(getErrorMessage(e), { title: 'No se pudo aprobar' });
    }
  };

  const aniosOpciones = useMemo(() => {
    const y = hoy.getFullYear();
    return [y - 2, y - 1, y, y + 1];
  }, [hoy]);

  return (
    <div className="p-4 md:p-8 max-w-6xl mx-auto">
      <div className="flex flex-wrap items-center justify-between gap-3 mb-6">
        <h1 className="text-2xl font-bold text-stone-800 flex items-center gap-2">
          <Calculator size={22} /> Nómina de Empleados
        </h1>
        <div className="flex items-center gap-2">
          <select
            value={anio}
            onChange={(e) => setAnio(parseInt(e.target.value))}
            className="px-3 py-2 text-sm rounded-lg border border-stone-200 bg-white"
            title="Filtrar por año"
          >
            {aniosOpciones.map((y) => <option key={y} value={y}>{y}</option>)}
          </select>
          <button
            onClick={() => reload()}
            disabled={loading}
            className="px-3 py-2 text-sm rounded-lg border border-stone-200 bg-white hover:bg-stone-50 disabled:opacity-50 flex items-center gap-1.5"
          >
            <RefreshCw size={14} className={loading ? 'animate-spin' : ''} />
            Actualizar
          </button>
          {puedeCrearPeriodo && (
            <button
              onClick={() => setShowNuevo(true)}
              className="flex items-center gap-2 bg-stone-900 text-white text-sm px-4 py-2 rounded-lg hover:bg-stone-800 transition-colors"
            >
              <Plus size={14} /> Nuevo Periodo
            </button>
          )}
        </div>
      </div>

      {errLoad && !loading && (
        <div className="mb-4 flex items-start gap-3 p-4 rounded-xl border border-red-200 bg-red-50 text-red-700">
          <AlertCircle size={18} className="mt-0.5 flex-shrink-0" />
          <div className="flex-1 text-sm">
            <p className="font-medium">No se pudieron cargar los periodos</p>
            <p className="text-red-600/80">{errLoad}</p>
          </div>
          <button onClick={() => reload()} className="text-sm font-medium px-3 py-1.5 rounded-lg bg-white border border-red-200 hover:bg-red-100 transition flex items-center gap-1.5">
            <RefreshCw size={14} /> Reintentar
          </button>
        </div>
      )}

      {loading ? (
        <div className="text-stone-400 p-12 text-center">
          <Loader2 className="animate-spin inline mr-2" /> Cargando periodos...
        </div>
      ) : (
        <div className="bg-white rounded-xl border border-stone-200 shadow-sm overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-stone-50 border-b border-stone-200">
              <tr>
                {['Periodo', 'Tipo', 'Inicio', 'Fin', 'Pago', 'Estado', 'Acciones'].map((h, i) => (
                  <th key={i} className="px-4 py-3 text-left text-xs font-bold text-stone-500 uppercase tracking-wider">{h}</th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-stone-100">
              {periodos.map(p => (
                <tr key={p.periodoId} className="hover:bg-amber-50/20 transition-colors">
                  <td className="px-4 py-3 font-medium text-stone-900">
                    {p.anio}-{String(p.mes).padStart(2, '0')}
                  </td>
                  <td className="px-4 py-3 text-stone-600">{p.tipoPeriodo}</td>
                  <td className="px-4 py-3 text-stone-500">{fmtFecha(p.fechaInicio)}</td>
                  <td className="px-4 py-3 text-stone-500">{fmtFecha(p.fechaFin)}</td>
                  <td className="px-4 py-3 text-stone-500">{fmtFecha(p.fechaPago)}</td>
                  <td className="px-4 py-3">
                    <span className={`inline-block px-2 py-0.5 rounded-full text-xs font-semibold ${
                      p.estado === 'ABIERTO'   ? 'bg-green-50 text-green-700' :
                      p.estado === 'CALCULADO' ? 'bg-amber-50 text-amber-700' :
                      p.estado === 'APROBADO'  ? 'bg-blue-50 text-blue-700' :
                      'bg-stone-100 text-stone-600'
                    }`}>
                      {p.estado}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-3">
                      {p.estado === 'ABIERTO' && puedeCalcular && (
                        <button
                          onClick={() => setConfirmCalc({ id: p.periodoId, label: `${p.anio}-${String(p.mes).padStart(2,'0')} ${p.tipoPeriodo}` })}
                          disabled={calcId === p.periodoId}
                          className="text-xs font-bold text-amber-600 hover:text-amber-700 hover:underline flex items-center gap-1 disabled:opacity-50"
                        >
                          {calcId === p.periodoId
                            ? <><Loader2 size={12} className="animate-spin" /> Calculando...</>
                            : <><Calculator size={12} /> Calcular</>}
                        </button>
                      )}
                      {p.estado === 'CALCULADO' && puedeAprobar && (
                        <button
                          onClick={() => aprobar(p.periodoId)}
                          className="text-xs font-bold text-blue-600 hover:text-blue-700 hover:underline flex items-center gap-1"
                        >
                          <CheckCircle2 size={12} /> Aprobar
                        </button>
                      )}
                      {(p.estado === 'CALCULADO' || p.estado === 'APROBADO') && (
                        <a
                          href={`#/recibos/${p.periodoId}`}
                          className="text-xs font-bold text-stone-500 hover:text-stone-700 hover:underline flex items-center gap-1"
                          onClick={(e) => { e.preventDefault(); toast.info('Vista de recibos próximamente'); }}
                        >
                          <FileText size={12} /> Recibos
                        </a>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
              {periodos.length === 0 && !errLoad && (
                <tr>
                  <td colSpan="7" className="text-center py-16 text-stone-400 italic">
                    No hay periodos registrados para {anio}.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* Modal: nuevo periodo */}
      {showNuevo && (
        <div className="fixed inset-0 z-50 bg-stone-900/40 flex items-center justify-center p-4" onClick={() => !creating && setShowNuevo(false)}>
          <div className="bg-white rounded-xl shadow-xl max-w-md w-full p-6" onClick={(e) => e.stopPropagation()}>
            <div className="flex items-start justify-between mb-4">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-full bg-amber-50 flex items-center justify-center">
                  <Plus size={18} className="text-amber-600" />
                </div>
                <div>
                  <h3 className="font-bold text-stone-800">Nuevo periodo de nómina</h3>
                  <p className="text-xs text-stone-500">Define año, mes y tipo</p>
                </div>
              </div>
              <button
                onClick={() => !creating && setShowNuevo(false)}
                className="text-stone-400 hover:text-stone-600 p-1"
                aria-label="Cerrar"
              >
                <X size={18} />
              </button>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block text-xs font-semibold text-stone-500 uppercase mb-1">Año</label>
                <select
                  value={nuevo.anio}
                  onChange={(e) => setNuevo({ ...nuevo, anio: parseInt(e.target.value) })}
                  className="w-full px-3 py-2 text-sm border border-stone-200 rounded-lg"
                >
                  {aniosOpciones.map((y) => <option key={y} value={y}>{y}</option>)}
                </select>
              </div>
              <div>
                <label className="block text-xs font-semibold text-stone-500 uppercase mb-1">Mes</label>
                <select
                  value={nuevo.mes}
                  onChange={(e) => setNuevo({ ...nuevo, mes: parseInt(e.target.value) })}
                  className="w-full px-3 py-2 text-sm border border-stone-200 rounded-lg"
                >
                  {MESES.map((m, i) => <option key={i} value={i + 1}>{m}</option>)}
                </select>
              </div>
              <div className="col-span-2">
                <label className="block text-xs font-semibold text-stone-500 uppercase mb-1">Tipo</label>
                <select
                  value={nuevo.tipoPeriodo}
                  onChange={(e) => setNuevo({ ...nuevo, tipoPeriodo: e.target.value })}
                  className="w-full px-3 py-2 text-sm border border-stone-200 rounded-lg"
                >
                  {TIPOS.map(t => <option key={t} value={t}>{t}</option>)}
                </select>
              </div>
            </div>

            {colision && (
              <div className="mt-3 flex items-start gap-2 p-3 rounded-lg bg-amber-50 border border-amber-200 text-amber-800 text-xs">
                <AlertCircle size={14} className="mt-0.5 flex-shrink-0" />
                <span>
                  Ya existe un periodo {nuevo.tipoPeriodo} para {MESES[nuevo.mes - 1]} {nuevo.anio}. Crea otro tipo o cambia el mes.
                </span>
              </div>
            )}

            <div className="flex justify-end gap-2 mt-5">
              <button
                onClick={() => setShowNuevo(false)}
                disabled={creating}
                className="px-3 py-2 text-sm rounded-lg border border-stone-200 bg-white hover:bg-stone-50 disabled:opacity-50"
              >
                Cancelar
              </button>
              <button
                onClick={crearPeriodo}
                disabled={creating || colision}
                className="px-3 py-2 text-sm rounded-lg bg-stone-900 hover:bg-stone-800 disabled:bg-stone-300 text-white font-semibold flex items-center gap-2"
              >
                {creating ? <><Loader2 size={14} className="animate-spin" /> Creando...</> : <><Plus size={14} /> Crear periodo</>}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal: confirmar cálculo */}
      {confirmCalc && (
        <div className="fixed inset-0 z-50 bg-stone-900/40 flex items-center justify-center p-4" onClick={() => setConfirmCalc(null)}>
          <div className="bg-white rounded-xl shadow-xl max-w-sm w-full p-6" onClick={(e) => e.stopPropagation()}>
            <div className="flex items-center gap-3 mb-3">
              <div className="w-10 h-10 rounded-full bg-amber-50 flex items-center justify-center">
                <Calculator size={18} className="text-amber-600" />
              </div>
              <div>
                <h3 className="font-bold text-stone-800">Calcular nómina</h3>
                <p className="text-xs text-stone-500">{confirmCalc.label}</p>
              </div>
            </div>
            <p className="text-sm text-stone-600">
              Se ejecutará el cálculo para todos los empleados activos del periodo. ¿Continuar?
            </p>
            <div className="flex justify-end gap-2 mt-5">
              <button
                onClick={() => setConfirmCalc(null)}
                className="px-3 py-2 text-sm rounded-lg border border-stone-200 bg-white hover:bg-stone-50"
              >
                Cancelar
              </button>
              <button
                onClick={() => calcular(confirmCalc.id)}
                className="px-3 py-2 text-sm rounded-lg bg-amber-500 hover:bg-amber-600 text-white font-semibold"
              >
                Calcular
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
