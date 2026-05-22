import { useEffect, useState } from 'react';
import { empleadosApi, nominaApi, getErrorMessage } from '../services/api';
import { useToast } from '../context/ToastContext';
import { FileText, Loader2, AlertCircle, RefreshCw, X, ChevronRight, Download, Mail } from 'lucide-react';
import EnviarEmailModal from '../components/EnviarEmailModal';
import { useAuth } from '../context/AuthContext';

const fmt = (n) =>
  new Intl.NumberFormat('es-GT', { style: 'currency', currency: 'GTQ' }).format(Number(n) || 0);

const MESES = ['Ene','Feb','Mar','Abr','May','Jun','Jul','Ago','Sep','Oct','Nov','Dic'];

export default function MisBoletasPage() {
  const toast = useToast();
  const [boletas, setBoletas] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [recibo, setRecibo] = useState(null);
  const [reciboLoading, setReciboLoading] = useState(false);

  const cargar = async () => {
    setLoading(true);
    setError('');
    try {
      const data = await empleadosApi.misBoletas();
      setBoletas(Array.isArray(data) ? data : []);
    } catch (e) {
      setError(getErrorMessage(e));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { cargar(); }, []);

  const verRecibo = async (nominaEncId) => {
    setReciboLoading(true);
    try {
      const r = await nominaApi.recibo(nominaEncId);
      setRecibo(r);
    } catch (e) {
      toast.error(getErrorMessage(e), { title: 'No se pudo obtener el recibo' });
    } finally {
      setReciboLoading(false);
    }
  };

  return (
    <div className="p-4 md:p-8 max-w-5xl mx-auto">
      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold text-stone-800 flex items-center gap-2">
          <FileText size={22} /> Mis boletas de pago
        </h1>
        <button
          onClick={cargar}
          disabled={loading}
          className="px-3 py-2 text-sm rounded-lg border border-stone-200 bg-white hover:bg-stone-50 disabled:opacity-50 flex items-center gap-1.5"
        >
          <RefreshCw size={14} className={loading ? 'animate-spin' : ''} />
          Actualizar
        </button>
      </div>

      {error && !loading && (
        <div className="mb-4 flex items-start gap-3 p-4 rounded-xl border border-red-200 bg-red-50 text-red-700">
          <AlertCircle size={18} className="mt-0.5 flex-shrink-0" />
          <div className="flex-1 text-sm">
            <p className="font-medium">No se pudieron cargar tus boletas</p>
            <p className="text-red-600/80">{error}</p>
          </div>
          <button onClick={cargar} className="text-sm font-medium px-3 py-1.5 rounded-lg bg-white border border-red-200 hover:bg-red-100 flex items-center gap-1.5">
            <RefreshCw size={14} /> Reintentar
          </button>
        </div>
      )}

      {loading ? (
        <div className="text-stone-400 p-12 text-center">
          <Loader2 className="animate-spin inline mr-2" /> Cargando boletas...
        </div>
      ) : boletas.length === 0 && !error ? (
        <div className="bg-white rounded-xl border border-stone-200 p-12 text-center">
          <FileText size={32} className="mx-auto text-stone-300 mb-2" />
          <p className="text-stone-500">No tienes boletas registradas todavía.</p>
          <p className="text-xs text-stone-400 mt-1">Aparecerán aquí cuando se calcule tu primera nómina.</p>
        </div>
      ) : (
        <div className="bg-white rounded-xl border border-stone-200 overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-stone-50 border-b border-stone-100">
              <tr>
                {['Periodo','Tipo','Pago','Ingresos','Deducciones','Neto','Estado',''].map((h, i) => (
                  <th key={i} className="px-4 py-3 text-left text-xs font-bold text-stone-500 uppercase">{h}</th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-stone-100">
              {boletas.map((b) => {
                const id = b.NOMINA_ENC_ID ?? b.nominaEncId;
                const anio = b.ANIO ?? b.anio;
                const mes = Number(b.MES ?? b.mes);
                const tipo = b.TIPO_PERIODO ?? b.tipoPeriodo;
                const pagoRaw = b.FECHA_PAGO ?? b.fechaPago;
                const pago = typeof pagoRaw === 'string' ? pagoRaw.slice(0, 10) : (pagoRaw ? new Date(pagoRaw).toISOString().slice(0,10) : '—');
                const ing = b.TOTAL_INGRESOS ?? b.totalIngresos;
                const ded = b.TOTAL_DEDUCCIONES ?? b.totalDeducciones;
                const neto = b.SALARIO_NETO ?? b.salarioNeto;
                const estado = b.ESTADO ?? b.estado;
                return (
                  <tr key={id} className="hover:bg-amber-50/30 transition-colors">
                    <td className="px-4 py-3 font-medium text-stone-900">
                      {MESES[mes - 1]} {anio}
                    </td>
                    <td className="px-4 py-3 text-stone-600">{tipo}</td>
                    <td className="px-4 py-3 text-stone-500">{pago}</td>
                    <td className="px-4 py-3 text-right tabular-nums text-emerald-700">{fmt(ing)}</td>
                    <td className="px-4 py-3 text-right tabular-nums text-red-700">{fmt(ded)}</td>
                    <td className="px-4 py-3 text-right tabular-nums font-bold">{fmt(neto)}</td>
                    <td className="px-4 py-3">
                      <span className={`inline-block px-2 py-0.5 rounded-full text-xs font-semibold ${
                        estado === 'PAGADO' ? 'bg-emerald-50 text-emerald-700' :
                        estado === 'APROBADO' ? 'bg-blue-50 text-blue-700' :
                        'bg-amber-50 text-amber-700'
                      }`}>{estado}</span>
                    </td>
                    <td className="px-4 py-3">
                      <button
                        onClick={() => verRecibo(id)}
                        disabled={reciboLoading}
                        className="text-xs font-medium text-amber-600 hover:text-amber-700 hover:underline flex items-center gap-1 disabled:opacity-50"
                      >
                        Ver recibo <ChevronRight size={12} />
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {recibo && <ReciboModal recibo={recibo} onClose={() => setRecibo(null)} />}
    </div>
  );
}

function ReciboModal({ recibo, onClose }) {
  const toast = useToast();
  const { user } = useAuth();
  const [downloading, setDownloading] = useState(false);
  const [showEmail, setShowEmail] = useState(false);

  const descargar = async () => {
    setDownloading(true);
    try {
      const filename = `Recibo_${recibo.codigoEmpleado}_${(recibo.periodo || '').replace(/\s+/g, '_')}.pdf`;
      await nominaApi.descargarReciboPdf(recibo.nominaEncId, filename);
      toast.success('Recibo descargado.');
    } catch (e) {
      toast.error(getErrorMessage(e), { title: 'No se pudo descargar el recibo' });
    } finally {
      setDownloading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 bg-stone-900/40 flex items-center justify-center p-4" onClick={onClose}>
      <div className="bg-white rounded-xl shadow-xl max-w-lg w-full max-h-[90vh] overflow-auto" onClick={(e) => e.stopPropagation()}>
        <div className="sticky top-0 bg-white px-6 py-4 border-b border-stone-100 flex items-start justify-between gap-3 z-10">
          <div className="flex-1">
            <h3 className="font-bold text-stone-800">Recibo de pago</h3>
            <p className="text-xs text-stone-500 mt-0.5">{recibo.codigoEmpleado} — {recibo.nombreEmpleado}</p>
            <p className="text-xs text-stone-500">{recibo.periodo}</p>
          </div>
          <button
            onClick={() => setShowEmail(true)}
            className="flex items-center gap-1.5 text-xs font-semibold px-3 py-2 rounded-lg bg-blue-600 hover:bg-blue-700 text-white"
            title="Enviar por correo"
          >
            <Mail size={14} /> Email
          </button>
          <button
            onClick={descargar}
            disabled={downloading}
            className="flex items-center gap-1.5 text-xs font-semibold px-3 py-2 rounded-lg bg-red-600 hover:bg-red-700 disabled:bg-stone-300 text-white"
          >
            {downloading ? <Loader2 size={14} className="animate-spin" /> : <Download size={14} />}
            PDF
          </button>
          <button onClick={onClose} className="text-stone-400 hover:text-stone-600 p-1" aria-label="Cerrar">
            <X size={18} />
          </button>
        </div>

        {showEmail && (
          <EnviarEmailModal
            titulo="Enviar recibo por correo"
            defaultTo={user?.email || ''}
            defaultAsunto={`Recibo de pago — ${recibo.periodo}`}
            defaultMensaje={`Hola ${recibo.nombreEmpleado},\n\nAdjunto tu recibo de pago correspondiente a ${recibo.periodo}.`}
            nombreArchivo={`Recibo_${recibo.codigoEmpleado}_${(recibo.periodo || '').replace(/\s+/g, '_')}.pdf`}
            onClose={() => setShowEmail(false)}
            onEnviar={async ({ para, asunto, mensaje }) => {
              await nominaApi.enviarReciboPorEmail(recibo.nominaEncId, { para, asunto, mensaje });
            }}
          />
        )}
        <div className="px-6 py-4 space-y-4">
          <div>
            <p className="text-xs font-semibold text-emerald-600 uppercase mb-2">Ingresos</p>
            <ul className="text-sm space-y-1">
              {(recibo.ingresos || []).map((l, i) => (
                <li key={i} className="flex justify-between border-b border-dashed border-stone-100 py-1">
                  <span className="text-stone-600">{l.concepto}</span>
                  <span className="font-mono tabular-nums text-emerald-700">{fmt(l.monto)}</span>
                </li>
              ))}
            </ul>
            <p className="text-right text-sm font-bold text-emerald-700 mt-2">
              Total ingresos: {fmt(recibo.totalIngresos)}
            </p>
          </div>

          <div>
            <p className="text-xs font-semibold text-red-600 uppercase mb-2">Deducciones</p>
            <ul className="text-sm space-y-1">
              {(recibo.deducciones || []).map((l, i) => (
                <li key={i} className="flex justify-between border-b border-dashed border-stone-100 py-1">
                  <span className="text-stone-600">{l.concepto}</span>
                  <span className="font-mono tabular-nums text-red-700">{fmt(l.monto)}</span>
                </li>
              ))}
            </ul>
            <p className="text-right text-sm font-bold text-red-700 mt-2">
              Total deducciones: {fmt(recibo.totalDeducciones)}
            </p>
          </div>

          <div className="bg-stone-50 rounded-lg p-4 border border-stone-200">
            <div className="flex justify-between items-center">
              <span className="font-semibold text-stone-700">Salario neto a pagar</span>
              <span className="text-2xl font-bold text-stone-800 tabular-nums">{fmt(recibo.salarioNeto)}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
