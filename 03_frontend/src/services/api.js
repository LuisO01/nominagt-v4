// ============================================================================
// NominaGT v4 - Cliente HTTP con axios + JWT + refresh tokens
// ============================================================================
import axios from 'axios';

const api = axios.create({
  baseURL: '/api',
  headers: {
    'Content-Type': 'application/json',
    // Evita que el navegador devuelva respuestas cacheadas para datos vivos
    // (KPIs, listas de empleados, periodos, etc.).
    'Cache-Control': 'no-cache',
    'Pragma': 'no-cache',
  },
});

// ─── Inyectar JWT ───
api.interceptors.request.use((config) => {
  const stored = localStorage.getItem('nominagt_session');
  if (stored) {
    try {
      const session = JSON.parse(stored);
      if (session?.token) config.headers.Authorization = `Bearer ${session.token}`;
    } catch {}
  }
  return config;
});

// ─── Normalizacion de errores ───
// Convierte cualquier error de axios en un mensaje claro para el usuario,
// y lo adjunta como `error.userMessage` para que las paginas lo lean directo.
export function getErrorMessage(error) {
  if (!error) return 'Error desconocido.';
  if (error.userMessage) return error.userMessage;

  // Sin respuesta = problema de red / backend caido / CORS
  if (!error.response) {
    if (error.code === 'ERR_NETWORK' || error.message?.includes('Network Error')) {
      return 'No se pudo conectar con el servidor. Verifica que el backend esté corriendo en https://localhost:5001.';
    }
    if (error.code === 'ECONNABORTED' || error.message?.includes('timeout')) {
      return 'El servidor tardó demasiado en responder. Intenta de nuevo.';
    }
    return error.message || 'No se pudo contactar al servidor.';
  }

  const { status, data } = error.response;
  // Soporta camelCase y PascalCase (mensaje/Mensaje, message/Message, etc.)
  const backendMsg = data?.mensaje || data?.Mensaje
                  || data?.message || data?.Message
                  || data?.title;

  switch (status) {
    case 400: return backendMsg || 'Datos invalidos en la solicitud.';
    case 401: return 'Tu sesión expiró. Vuelve a iniciar sesión.';
    case 403: return 'No tienes permisos para realizar esta acción.';
    case 404: return backendMsg || 'El recurso solicitado no existe.';
    case 409: return backendMsg || 'Conflicto: el recurso ya existe o está en uso.';
    case 422: return backendMsg || 'Los datos no cumplen las validaciones.';
    case 500: return backendMsg || 'Error interno del servidor. Revisa los logs del backend.';
    case 502:
    case 503:
    case 504: return 'El servidor no está disponible en este momento.';
    default:  return backendMsg || `Error inesperado (HTTP ${status}).`;
  }
}

// ─── Manejo automatico de 401 + normalizacion ───
let isRefreshing = false;
let pendingRequests = [];

api.interceptors.response.use(
  (r) => r,
  async (error) => {
    const original = error.config;
    if (error.response?.status === 401 && !original._retry) {
      const stored = localStorage.getItem('nominagt_session');
      if (!stored) {
        window.location.href = '/login';
        return Promise.reject(error);
      }

      const session = JSON.parse(stored);
      if (!session.refreshToken) {
        localStorage.removeItem('nominagt_session');
        window.location.href = '/login';
        return Promise.reject(error);
      }

      if (isRefreshing) {
        return new Promise(resolve => pendingRequests.push(resolve))
          .then(token => { original.headers.Authorization = `Bearer ${token}`; return api(original); });
      }

      original._retry = true;
      isRefreshing = true;

      try {
        const res = await axios.post('/api/auth/refresh', { refreshToken: session.refreshToken });
        const newToken = res.data.data.token;
        const newRefresh = res.data.data.refreshToken;
        const newSession = { ...session, token: newToken, refreshToken: newRefresh };
        localStorage.setItem('nominagt_session', JSON.stringify(newSession));

        pendingRequests.forEach(cb => cb(newToken));
        pendingRequests = [];
        original.headers.Authorization = `Bearer ${newToken}`;
        return api(original);
      } catch (e) {
        localStorage.removeItem('nominagt_session');
        window.location.href = '/login';
        e.userMessage = 'Tu sesión expiró. Vuelve a iniciar sesión.';
        return Promise.reject(e);
      } finally {
        isRefreshing = false;
      }
    }

    // Adjuntamos el mensaje listo-para-mostrar a TODOS los errores
    error.userMessage = getErrorMessage(error);
    return Promise.reject(error);
  }
);

// ─── Auth ───
export const authApi = {
  login: async (nombreUsuario, password) => {
    const res = await api.post('/auth/login', { nombreUsuario, password });
    if (!res.data?.ok) throw new Error(res.data?.mensaje || 'Login fallido');
    return res.data.data;
  },
};

// ─── Empleados ───
export const empleadosApi = {
  listar: async (params = {}) => {
    const res = await api.get('/empleados', { params: { empresaId: 1, page: 1, pageSize: 100, ...params } });
    return { items: res.data.data || [], total: res.data.total || 0 };
  },
  obtener: async (id) => (await api.get(`/empleados/${id}`)).data?.data ?? null,
  crear: async (data) => {
    const res = await api.post('/empleados', data);
    if (!res.data?.ok) throw new Error(res.data?.mensaje);
    return res.data.data;
  },
  actualizar: async (id, data) => (await api.put(`/empleados/${id}`, data)).data,
  cambiarEstado: async (id, estado, motivo) =>
    (await api.patch(`/empleados/${id}/estado`, { estado, motivo })).data,
  crearAcceso: async (empleadoId, payload) => {
    // payload: { nombreUsuario?, passwordTemporal?, rolInicial, enviarCredencialesPorEmail }
    const res = await api.post(`/empleados/${empleadoId}/crear-acceso`, payload);
    if (!res.data?.ok) throw new Error(res.data?.mensaje);
    return res.data.data; // { usuarioId, nombreUsuario, rol, email, passwordTemporal, correoEnviado }
  },
  resetearPassword: async (empleadoId, enviarPorEmail = true) => {
    const res = await api.post(`/empleados/${empleadoId}/resetear-password`, null, {
      params: { enviarPorEmail }
    });
    if (!res.data?.ok) throw new Error(res.data?.mensaje);
    return res.data.data;
  },
  liquidacionPreview: async (empleadoId, payload) => {
    // payload: { fechaBaja, motivo, motivoDetalle?, otrosPagos?, descuentos?, observaciones? }
    const res = await api.post(`/empleados/${empleadoId}/liquidacion-preview`, payload);
    if (!res.data?.ok) throw new Error(res.data?.mensaje);
    return res.data.data;
  },
  liquidar: async (empleadoId, payload) => {
    const res = await api.post(`/empleados/${empleadoId}/liquidar`, payload);
    if (!res.data?.ok) throw new Error(res.data?.mensaje);
    return res.data.data;
  },
  descargarFiniquito: async (liquidacionId, filename) => {
    const res = await api.get(`/empleados/liquidaciones/${liquidacionId}/pdf`, { responseType: 'blob' });
    const blob = new Blob([res.data], { type: 'application/pdf' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename || `Finiquito_${liquidacionId}.pdf`;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  },
  misBoletas: async () => {
    const res = await api.get('/empleados/me/boletas');
    return res.data?.data || [];
  },
};

// ─── Nomina ───
export const nominaApi = {
  listarPeriodos: async (anio) => {
    const res = await api.get('/nomina/periodos', { params: { empresaId: 1, anio } });
    return res.data.data || [];
  },
  crearPeriodo: async (anio, mes, tipoPeriodo = 'MENSUAL') => {
    const res = await api.post('/nomina/periodos', { empresaId: 1, anio, mes, tipoPeriodo });
    return res.data.data;
  },
  calcular: async (periodoId) => (await api.post(`/nomina/periodos/${periodoId}/calcular`)).data.data,
  resumen: async (periodoId) => (await api.get(`/nomina/periodos/${periodoId}/resumen`)).data.data,
  aprobar: async (periodoId) => (await api.post(`/nomina/periodos/${periodoId}/aprobar`)).data.data,
  recibo: async (nominaEncId) => (await api.get(`/nomina/recibo/${nominaEncId}`)).data.data,
  enviarReciboPorEmail: async (nominaEncId, payload) => {
    const res = await api.post(`/nomina/recibo/${nominaEncId}/email`, payload);
    if (!res.data?.ok) throw new Error(res.data?.mensaje);
    return res.data.data;
  },
  descargarReciboPdf: async (nominaEncId, filename) => {
    const res = await api.get(`/nomina/recibo/${nominaEncId}/pdf`, { responseType: 'blob' });
    const blob = new Blob([res.data], { type: 'application/pdf' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename || `Recibo_${nominaEncId}.pdf`;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  },
};

// ─── Reportes ───
async function descargarBinario(url, params, filename, mime) {
  const res = await api.get(url, { params, responseType: 'blob' });
  const blob = new Blob([res.data], { type: mime });
  const dlUrl = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = dlUrl;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(dlUrl);
}

export const reportesApi = {
  descargarExcelMensual: async (anio, mes, tipoPeriodo = 'MENSUAL') =>
    descargarBinario(
      '/reportes/excel-mensual',
      { anio, mes, tipoPeriodo },
      `Planilla_${anio}_${String(mes).padStart(2,'0')}_${tipoPeriodo}.xlsx`,
      'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
    ),
  descargarPdfMensual: async (anio, mes, tipoPeriodo = 'MENSUAL') =>
    descargarBinario(
      '/reportes/pdf-mensual',
      { anio, mes, tipoPeriodo },
      `Planilla_${anio}_${String(mes).padStart(2,'0')}_${tipoPeriodo}.pdf`,
      'application/pdf'
    ),
  jsonMensual: async (anio, mes, tipoPeriodo = 'MENSUAL') => {
    const res = await api.get('/reportes/nomina-mensual', { params: { anio, mes, tipoPeriodo } });
    return res.data.data;
  },
  enviarPorEmail: async (payload) => {
    // payload: { para, formato:'excel'|'pdf', anio, mes, tipoPeriodo, asunto?, mensaje? }
    const res = await api.post('/reportes/enviar-email', payload);
    if (!res.data?.ok) throw new Error(res.data?.mensaje);
    return res.data.data;
  },
};

// ─── Vacaciones ───
export const vacacionesApi = {
  listar: async (params = {}) =>
    (await api.get('/vacaciones', { params })).data?.data || [],
  saldos: async () =>
    (await api.get('/vacaciones/saldos')).data?.data || [],
  miSaldo: async (empleadoId) =>
    (await api.get('/vacaciones/saldo', { params: empleadoId ? { empleadoId } : {} })).data?.data,
  crear: async (data) => {
    const res = await api.post('/vacaciones', data);
    if (!res.data?.ok) throw new Error(res.data?.mensaje);
    return res.data.data;
  },
  cambiarEstado: async (id, estado, observaciones) =>
    (await api.patch(`/vacaciones/${id}/estado`, { estado, observaciones })).data,
};

// ─── Dashboard ───
export const dashboardApi = {
  kpis: async () => (await api.get('/dashboard/kpis', { params: { empresaId: 1 } })).data.data,
  resumenAnual: async (anio) =>
    (await api.get('/dashboard/resumen-anual', { params: { empresaId: 1, anio } })).data.data,
};

// ─── Catalogos ───
export const catalogosApi = {
  departamentos: async () => (await api.get('/catalogos/departamentos')).data.data,
  puestos: async () => (await api.get('/catalogos/puestos')).data.data,
};

export default api;
