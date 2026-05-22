import { useState } from 'react';
import { Loader2, X, Mail, Paperclip } from 'lucide-react';
import { useToast } from '../context/ToastContext';
import { getErrorMessage } from '../services/api';

/**
 * Modal reutilizable para enviar un documento por email.
 *
 * Props:
 *   titulo           - Título del modal ("Enviar planilla por email")
 *   defaultTo        - Email pre-rellenado (opcional)
 *   defaultAsunto    - Asunto sugerido
 *   defaultMensaje   - Mensaje sugerido
 *   nombreArchivo    - Lo que se mostrará en el chip de adjunto (informativo)
 *   onClose          - Cerrar
 *   onEnviar(payload) - async; recibe { para, asunto, mensaje } y debe lanzar
 *                       error si falla. Si retorna, el modal se cierra.
 */
export default function EnviarEmailModal({
  titulo = 'Enviar por correo',
  defaultTo = '',
  defaultAsunto = '',
  defaultMensaje = '',
  nombreArchivo,
  onClose,
  onEnviar,
}) {
  const toast = useToast();
  const [form, setForm] = useState({
    para: defaultTo,
    asunto: defaultAsunto,
    mensaje: defaultMensaje,
  });
  const [sending, setSending] = useState(false);

  const emailValido = /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(form.para.trim());

  const submit = async (e) => {
    e.preventDefault();
    if (!emailValido) {
      toast.warning('Ingresa un correo válido.');
      return;
    }
    setSending(true);
    try {
      await onEnviar({
        para: form.para.trim(),
        asunto: form.asunto.trim() || undefined,
        mensaje: form.mensaje.trim() || undefined,
      });
      toast.success(`Correo enviado a ${form.para.trim()}.`);
      onClose();
    } catch (err) {
      toast.error(getErrorMessage(err), { title: 'No se pudo enviar el correo' });
    } finally {
      setSending(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 bg-stone-900/40 flex items-center justify-center p-4"
         onClick={() => !sending && onClose()}>
      <div className="bg-white rounded-xl shadow-xl max-w-md w-full p-6"
           onClick={(e) => e.stopPropagation()}>
        <div className="flex items-start justify-between mb-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-full bg-blue-50 flex items-center justify-center">
              <Mail size={18} className="text-blue-600" />
            </div>
            <div>
              <h3 className="font-bold text-stone-800">{titulo}</h3>
              <p className="text-xs text-stone-500">Se enviará como adjunto</p>
            </div>
          </div>
          <button onClick={() => !sending && onClose()} className="text-stone-400 hover:text-stone-600 p-1">
            <X size={18} />
          </button>
        </div>

        {nombreArchivo && (
          <div className="mb-4 flex items-center gap-2 text-xs bg-stone-50 border border-stone-200 rounded-lg p-2.5">
            <Paperclip size={14} className="text-stone-500" />
            <span className="text-stone-600 truncate">{nombreArchivo}</span>
          </div>
        )}

        <form onSubmit={submit} className="space-y-3">
          <div>
            <label className="block text-xs font-medium text-stone-600 mb-1">Para *</label>
            <input
              type="email"
              value={form.para}
              onChange={(e) => setForm({ ...form, para: e.target.value })}
              placeholder="correo@empresa.com"
              required
              autoFocus
              className={`w-full px-3 py-2 text-sm border rounded-lg focus:outline-none focus:ring-2 ${
                form.para && !emailValido
                  ? 'border-red-300 focus:ring-red-500/20'
                  : 'border-stone-200 focus:ring-amber-500/20'
              }`}
            />
            <p className="text-xs text-stone-400 mt-1">Puedes separar varios con coma.</p>
          </div>
          <div>
            <label className="block text-xs font-medium text-stone-600 mb-1">Asunto</label>
            <input
              type="text"
              value={form.asunto}
              onChange={(e) => setForm({ ...form, asunto: e.target.value })}
              placeholder={defaultAsunto || 'Opcional, se generará uno automáticamente'}
              className="w-full px-3 py-2 text-sm border border-stone-200 rounded-lg"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-stone-600 mb-1">Mensaje</label>
            <textarea
              value={form.mensaje}
              onChange={(e) => setForm({ ...form, mensaje: e.target.value })}
              rows={3}
              placeholder="Opcional. Si lo dejas vacío se usará una plantilla por defecto."
              className="w-full px-3 py-2 text-sm border border-stone-200 rounded-lg resize-none"
            />
          </div>

          <div className="flex justify-end gap-2 pt-2 border-t border-stone-100">
            <button type="button" onClick={onClose} disabled={sending}
                    className="px-3 py-2 text-sm rounded-lg border border-stone-200 bg-white hover:bg-stone-50 disabled:opacity-50">
              Cancelar
            </button>
            <button type="submit" disabled={sending || !emailValido}
                    className="px-4 py-2 text-sm rounded-lg bg-blue-600 hover:bg-blue-700 disabled:bg-stone-300 text-white font-semibold flex items-center gap-2">
              {sending ? <><Loader2 size={14} className="animate-spin" /> Enviando…</> : <><Mail size={14} /> Enviar</>}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
