import { useEffect, useState } from 'react';
import axios from 'axios';
import { ServerOff, CheckCircle2 } from 'lucide-react';

/**
 * Banner sticky que aparece cuando el backend no responde.
 * Hace ping al proxy /api: cualquier respuesta HTTP = backend vivo,
 * solo ECONNREFUSED / network error = caido.
 */
export default function BackendStatusBanner() {
  const [online, setOnline] = useState(null);   // null = sin info, true = ok, false = caido
  const [showRecovered, setShowRecovered] = useState(false);

  useEffect(() => {
    let cancelled = false;
    let lastWasDown = false;

    const ping = async () => {
      let alive = false;
      try {
        // Intentamos llegar al backend via proxy. /api/auth/login con GET
        // tipicamente devuelve 405; lo importante es que RESPONDA algo HTTP.
        await axios.get('/api/auth/login', {
          timeout: 3000,
          validateStatus: () => true, // cualquier status = vivo
        });
        alive = true;
      } catch (e) {
        // Solo error de red si no hay response
        alive = !!e?.response;
      }

      if (cancelled) return;
      if (alive) {
        if (lastWasDown) {
          setShowRecovered(true);
          setTimeout(() => !cancelled && setShowRecovered(false), 4000);
        }
        lastWasDown = false;
        setOnline(true);
      } else {
        lastWasDown = true;
        setOnline(false);
      }
    };

    ping();
    const t = setInterval(ping, 8000);
    return () => { cancelled = true; clearInterval(t); };
  }, []);

  if (online === false) {
    return (
      <div className="sticky top-0 z-40 bg-red-600 text-white text-sm px-4 py-2 flex items-center justify-center gap-2 shadow">
        <ServerOff size={16} className="flex-shrink-0" />
        <span>
          No hay conexión con el servidor. Asegúrate de que el backend esté corriendo en{' '}
          <code className="bg-red-700/40 px-1 rounded">https://localhost:5001</code>.
        </span>
      </div>
    );
  }

  if (showRecovered) {
    return (
      <div className="sticky top-0 z-40 bg-emerald-600 text-white text-sm px-4 py-2 flex items-center justify-center gap-2 shadow">
        <CheckCircle2 size={16} />
        <span>Conexión con el servidor restablecida.</span>
      </div>
    );
  }

  return null;
}
