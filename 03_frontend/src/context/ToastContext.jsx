import { createContext, useContext, useState, useCallback, useRef, useEffect } from 'react';
import { CheckCircle2, AlertCircle, Info, X, AlertTriangle } from 'lucide-react';

const ToastContext = createContext(null);

const TYPES = {
  success: { icon: CheckCircle2, ring: 'border-emerald-200', bg: 'bg-emerald-50', text: 'text-emerald-700', iconColor: 'text-emerald-500' },
  error:   { icon: AlertCircle,  ring: 'border-red-200',     bg: 'bg-red-50',     text: 'text-red-700',     iconColor: 'text-red-500' },
  warning: { icon: AlertTriangle,ring: 'border-amber-200',   bg: 'bg-amber-50',   text: 'text-amber-700',   iconColor: 'text-amber-500' },
  info:    { icon: Info,         ring: 'border-blue-200',    bg: 'bg-blue-50',    text: 'text-blue-700',    iconColor: 'text-blue-500' },
};

let idCounter = 0;

export function ToastProvider({ children }) {
  const [toasts, setToasts] = useState([]);
  const timers = useRef(new Map());

  const dismiss = useCallback((id) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
    const tmr = timers.current.get(id);
    if (tmr) {
      clearTimeout(tmr);
      timers.current.delete(id);
    }
  }, []);

  const push = useCallback((type, message, opts = {}) => {
    const id = ++idCounter;
    const duration = opts.duration ?? (type === 'error' ? 6000 : 3500);
    setToasts((prev) => [...prev, { id, type, message, title: opts.title }]);
    if (duration > 0) {
      const tmr = setTimeout(() => dismiss(id), duration);
      timers.current.set(id, tmr);
    }
    return id;
  }, [dismiss]);

  // Limpieza al desmontar
  useEffect(() => {
    const t = timers.current;
    return () => { t.forEach(clearTimeout); t.clear(); };
  }, []);

  const value = {
    success: (msg, opts) => push('success', msg, opts),
    error:   (msg, opts) => push('error', msg, opts),
    warning: (msg, opts) => push('warning', msg, opts),
    info:    (msg, opts) => push('info', msg, opts),
    dismiss,
  };

  return (
    <ToastContext.Provider value={value}>
      {children}
      <div
        className="fixed top-4 right-4 z-50 flex flex-col gap-2 pointer-events-none"
        aria-live="polite"
        aria-atomic="true"
      >
        {toasts.map((t) => {
          const T = TYPES[t.type] || TYPES.info;
          const Icon = T.icon;
          return (
            <div
              key={t.id}
              role="status"
              className={`pointer-events-auto w-80 max-w-[90vw] flex items-start gap-3 p-3 pr-2 rounded-xl border shadow-sm bg-white ${T.ring} animate-[slideIn_.18s_ease-out]`}
            >
              <div className={`mt-0.5 ${T.iconColor}`}>
                <Icon size={18} />
              </div>
              <div className="flex-1 min-w-0">
                {t.title && <p className={`text-sm font-semibold ${T.text}`}>{t.title}</p>}
                <p className="text-sm text-stone-700 break-words">{t.message}</p>
              </div>
              <button
                onClick={() => dismiss(t.id)}
                className="text-stone-400 hover:text-stone-600 p-1 rounded transition"
                aria-label="Cerrar"
              >
                <X size={14} />
              </button>
            </div>
          );
        })}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast() {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error('useToast debe usarse dentro de <ToastProvider>');
  return ctx;
}
