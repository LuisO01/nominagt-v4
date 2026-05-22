import { useEffect, useMemo, useState } from 'react';
import {
  empleadosApi, catalogosApi, getErrorMessage,
} from '../services/api';
import { useToast } from '../context/ToastContext';
import { useAuth } from '../context/AuthContext';
import {
  Loader2, Users, AlertCircle, RefreshCw, Plus, Search, X,
  Pencil, UserMinus, UserCheck, Filter, CheckCircle2, KeyRound, ShieldOff, Copy,
  Mail, FileText, Download, FileBarChart, Receipt,
} from 'lucide-react';
import * as gtv from '../utils/gtValidators';

const fmt = (n) =>
  new Intl.NumberFormat('es-GT', { style: 'currency', currency: 'GTQ' }).format(n || 0);

const ESTADOS = ['ACTIVO', 'BAJA', 'SUSPENDIDO'];

// ─── Form helpers ────────────────────────────────────────────────────
const empleadoVacio = {
  empresaId: 1,
  codigoEmpleado: '',
  primerNombre: '',
  segundoNombre: '',
  primerApellido: '',
  segundoApellido: '',
  dpi: '',
  nit: '',
  numAfiliacionIgss: '',
  fechaNacimiento: '',
  genero: 'M',
  estadoCivil: 'SOLTERO',
  telefono: '',
  emailCorporativo: '',
  departamentoId: '',
  puestoId: '',
  salarioBase: '',
  bonificacion: 250,
  tipoContrato: 'INDEFINIDO',
  jornadaLaboral: 'DIURNA',
  formaPago: 'MENSUAL',
  // Acceso al sistema
  crearAcceso: true,
  rolInicial: 'EMPLEADO',
  passwordTemporal: '',
  enviarCredencialesPorEmail: true,
};

// ─── Componente principal ────────────────────────────────────────────
export default function EmpleadosPage() {
  const toast = useToast();
  const { puede } = useAuth();
  const puedeCrear = puede('empleado.crear');
  const puedeEditar = puede('empleado.editar');
  const puedeCambiarEstado = puede('empleado.cambiar-estado');

  const [empleados, setEmpleados] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [busqueda, setBusqueda] = useState('');
  const [filtroEstado, setFiltroEstado] = useState('TODOS');

  // Catalogos
  const [departamentos, setDepartamentos] = useState([]);
  const [puestos, setPuestos] = useState([]);

  // Modales
  const [showCrear, setShowCrear] = useState(false);
  const [editando, setEditando] = useState(null);
  const [confirmEstado, setConfirmEstado] = useState(null); // { empleado, nuevoEstado }
  const [crearAccesoFor, setCrearAccesoFor] = useState(null); // empleado o null
  const [accesoCreado, setAccesoCreado]   = useState(null);    // { empleado, resultado }
  const [liquidarFor, setLiquidarFor]     = useState(null);    // empleado a liquidar
  const [resetPasswordFor, setResetPasswordFor] = useState(null); // empleado para reset password
  const [resetting, setResetting] = useState(false);

  // ─── Carga inicial ───
  const cargar = async () => {
    setLoading(true);
    setError('');
    try {
      const { items } = await empleadosApi.listar({ pageSize: 200 });
      setEmpleados(items);
    } catch (e) {
      setError(getErrorMessage(e));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { cargar(); }, []);

  useEffect(() => {
    // Catalogos en paralelo, no bloquean la lista
    (async () => {
      try {
        const [deps, pst] = await Promise.all([
          catalogosApi.departamentos().catch(() => []),
          catalogosApi.puestos().catch(() => []),
        ]);
        setDepartamentos(deps || []);
        setPuestos(pst || []);
      } catch { /* silencioso */ }
    })();
  }, []);

  // ─── Filtrado en cliente ───
  const filtrados = useMemo(() => {
    const q = busqueda.trim().toLowerCase();
    return empleados.filter((e) => {
      if (filtroEstado !== 'TODOS' && e.estado !== filtroEstado) return false;
      if (!q) return true;
      const txt = [
        e.codigoEmpleado, e.nombreCompleto, e.nombreDepartamento, e.nombrePuesto,
      ].filter(Boolean).join(' ').toLowerCase();
      return txt.includes(q);
    });
  }, [empleados, busqueda, filtroEstado]);

  // ─── Acciones ───
  const handleResetPassword = async () => {
    if (!resetPasswordFor) return;
    setResetting(true);
    try {
      const resultado = await empleadosApi.resetearPassword(resetPasswordFor.empleadoId, true);
      setResetPasswordFor(null);
      // Reusar el modal de credenciales para mostrar
      setAccesoCreado({ empleado: resetPasswordFor, resultado });
    } catch (e) {
      toast.error(getErrorMessage(e), { title: 'No se pudo resetear la contraseña' });
    } finally { setResetting(false); }
  };

  const handleCambioEstado = async () => {
    if (!confirmEstado) return;
    const { empleado, nuevoEstado } = confirmEstado;
    try {
      await empleadosApi.cambiarEstado(empleado.empleadoId, nuevoEstado, '');
      toast.success(`Empleado ${empleado.codigoEmpleado} → ${nuevoEstado}`);
      setConfirmEstado(null);
      await cargar();
    } catch (e) {
      toast.error(getErrorMessage(e), { title: 'No se pudo cambiar el estado' });
    }
  };

  return (
    <div className="p-4 md:p-8 max-w-7xl mx-auto">
      {/* Header */}
      <div className="flex flex-wrap items-center justify-between gap-3 mb-6">
        <h1 className="text-2xl font-bold text-stone-800 flex items-center gap-2">
          <Users size={22} /> Gestión de Empleados
        </h1>
        <div className="flex items-center gap-2">
          <button
            onClick={cargar}
            disabled={loading}
            className="px-3 py-2 text-sm rounded-lg border border-stone-200 bg-white hover:bg-stone-50 disabled:opacity-50 flex items-center gap-1.5"
          >
            <RefreshCw size={14} className={loading ? 'animate-spin' : ''} />
            Actualizar
          </button>
          {puedeCrear && (
            <button
              onClick={() => setShowCrear(true)}
              className="flex items-center gap-2 bg-stone-900 text-white text-sm px-4 py-2 rounded-lg hover:bg-stone-800 transition-colors"
            >
              <Plus size={14} /> Nuevo empleado
            </button>
          )}
        </div>
      </div>

      {/* Barra de filtros */}
      <div className="flex flex-wrap gap-2 mb-4">
        <div className="flex-1 min-w-[240px] relative">
          <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-stone-400" />
          <input
            type="text"
            value={busqueda}
            onChange={(e) => setBusqueda(e.target.value)}
            placeholder="Buscar por nombre, código, departamento..."
            className="w-full pl-9 pr-3 py-2 text-sm border border-stone-200 rounded-lg bg-white focus:outline-none focus:ring-2 focus:ring-amber-500/20"
          />
        </div>
        <div className="relative">
          <Filter size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-stone-400 pointer-events-none" />
          <select
            value={filtroEstado}
            onChange={(e) => setFiltroEstado(e.target.value)}
            className="pl-9 pr-8 py-2 text-sm border border-stone-200 rounded-lg bg-white"
          >
            <option value="TODOS">Todos los estados</option>
            {ESTADOS.map((s) => <option key={s} value={s}>{s}</option>)}
          </select>
        </div>
      </div>

      {/* Error de carga */}
      {error && !loading && (
        <div className="mb-4 flex items-start gap-3 p-4 rounded-xl border border-red-200 bg-red-50 text-red-700">
          <AlertCircle size={18} className="mt-0.5 flex-shrink-0" />
          <div className="flex-1 text-sm">
            <p className="font-medium">No se pudieron cargar los empleados</p>
            <p className="text-red-600/80">{error}</p>
          </div>
          <button onClick={cargar} className="text-sm font-medium px-3 py-1.5 rounded-lg bg-white border border-red-200 hover:bg-red-100 transition flex items-center gap-1.5">
            <RefreshCw size={14} /> Reintentar
          </button>
        </div>
      )}

      {/* Tabla */}
      {loading ? (
        <div className="flex items-center gap-2 text-stone-400 p-12 justify-center">
          <Loader2 className="animate-spin" size={16} /> Cargando desde Oracle...
        </div>
      ) : filtrados.length === 0 && !error ? (
        <div className="text-center py-16 text-stone-400">
          {empleados.length === 0
            ? 'Sin empleados registrados.'
            : 'No hay resultados para los filtros seleccionados.'}
        </div>
      ) : (
        <div className="bg-white rounded-xl border border-stone-200 overflow-x-auto">
          <div className="px-4 py-2 text-xs text-stone-500 bg-stone-50 border-b border-stone-100">
            Mostrando <strong>{filtrados.length}</strong> de {empleados.length} empleados
          </div>
          <table className="w-full text-sm">
            <thead className="bg-stone-50 border-b border-stone-100">
              <tr>
                {['Código','Nombre','Departamento','Puesto','Salario','Estado','Acceso','Acciones'].map((h, i) => (
                  <th key={i} className="px-4 py-3 text-left text-xs font-bold text-stone-400 uppercase">{h}</th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-stone-100">
              {filtrados.map((e) => (
                <tr key={e.empleadoId} className="hover:bg-amber-50/30">
                  <td className="px-4 py-3 font-mono text-xs text-amber-600 font-bold">{e.codigoEmpleado}</td>
                  <td className="px-4 py-3 font-medium">{e.nombreCompleto}</td>
                  <td className="px-4 py-3 text-stone-500">{e.nombreDepartamento || '—'}</td>
                  <td className="px-4 py-3 text-stone-500">{e.nombrePuesto || '—'}</td>
                  <td className="px-4 py-3 text-right font-mono tabular-nums">{fmt(e.salarioBase)}</td>
                  <td className="px-4 py-3">
                    <span className={`inline-block px-2 py-0.5 rounded-full text-xs font-semibold ${
                      e.estado === 'ACTIVO' ? 'bg-emerald-50 text-emerald-700' :
                      e.estado === 'BAJA' ? 'bg-red-50 text-red-700' : 'bg-stone-100 text-stone-500'
                    }`}>{e.estado}</span>
                  </td>
                  <td className="px-4 py-3">
                    {e.tieneAcceso === 1 ? (
                      <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-semibold bg-blue-50 text-blue-700">
                        <KeyRound size={10} /> Activo
                      </span>
                    ) : (
                      <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-semibold bg-stone-100 text-stone-500">
                        <ShieldOff size={10} /> Sin cuenta
                      </span>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-3 flex-wrap">
                      {puedeEditar && (
                        <button
                          onClick={() => setEditando(e)}
                          className="text-xs font-medium text-blue-600 hover:text-blue-700 hover:underline flex items-center gap-1"
                          title="Editar"
                        >
                          <Pencil size={12} /> Editar
                        </button>
                      )}
                      {puedeCrear && e.tieneAcceso !== 1 && e.estado === 'ACTIVO' && (
                        <button
                          onClick={() => setCrearAccesoFor(e)}
                          className="text-xs font-medium text-amber-600 hover:text-amber-700 hover:underline flex items-center gap-1"
                          title="Crear cuenta de acceso"
                        >
                          <KeyRound size={12} /> Crear acceso
                        </button>
                      )}
                      {puedeCrear && e.tieneAcceso === 1 && (
                        <button
                          onClick={() => setResetPasswordFor(e)}
                          className="text-xs font-medium text-blue-600 hover:text-blue-700 hover:underline flex items-center gap-1"
                          title="Reenviar contraseña por email"
                        >
                          <Mail size={12} /> Reenviar
                        </button>
                      )}
                      {puedeCambiarEstado && e.estado === 'ACTIVO' && (
                        <button
                          onClick={() => setLiquidarFor(e)}
                          className="text-xs font-medium text-red-600 hover:text-red-700 hover:underline flex items-center gap-1"
                          title="Liquidar y dar de baja"
                        >
                          <Receipt size={12} /> Liquidar
                        </button>
                      )}
                      {puedeCambiarEstado && e.estado === 'BAJA' && (
                        <button
                          onClick={() => setConfirmEstado({ empleado: e, nuevoEstado: 'ACTIVO' })}
                          className="text-xs font-medium text-emerald-600 hover:text-emerald-700 hover:underline flex items-center gap-1"
                          title="Reactivar"
                        >
                          <UserCheck size={12} /> Reactivar
                        </button>
                      )}
                      {!puedeEditar && !puedeCambiarEstado && (
                        <span className="text-xs text-stone-300">—</span>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {showCrear && (
        <ModalCrearEmpleado
          departamentos={departamentos}
          puestos={puestos}
          onClose={() => setShowCrear(false)}
          onCreated={async () => { setShowCrear(false); await cargar(); }}
        />
      )}

      {editando && (
        <ModalEditarEmpleado
          empleado={editando}
          departamentos={departamentos}
          puestos={puestos}
          onClose={() => setEditando(null)}
          onSaved={async () => { setEditando(null); await cargar(); }}
        />
      )}

      {confirmEstado && (
        <ConfirmModal
          titulo={confirmEstado.nuevoEstado === 'BAJA' ? 'Dar de baja al empleado' : 'Reactivar empleado'}
          mensaje={`¿Confirmas cambiar a ${confirmEstado.empleado.codigoEmpleado} (${confirmEstado.empleado.nombreCompleto}) al estado ${confirmEstado.nuevoEstado}?`}
          variante={confirmEstado.nuevoEstado === 'BAJA' ? 'danger' : 'success'}
          onCancel={() => setConfirmEstado(null)}
          onConfirm={handleCambioEstado}
        />
      )}

      {crearAccesoFor && (
        <ModalCrearAcceso
          empleado={crearAccesoFor}
          onClose={() => setCrearAccesoFor(null)}
          onCreated={async (resultado) => {
            setCrearAccesoFor(null);
            setAccesoCreado({ empleado: crearAccesoFor, resultado });
            await cargar();
          }}
        />
      )}

      {accesoCreado && (
        <ModalCredencialesCreadas
          empleado={accesoCreado.empleado}
          resultado={accesoCreado.resultado}
          onClose={() => setAccesoCreado(null)}
        />
      )}

      {resetPasswordFor && (
        <ConfirmModal
          titulo="Reenviar credenciales"
          mensaje={`Se generará una NUEVA contraseña temporal para ${resetPasswordFor.codigoEmpleado} (${resetPasswordFor.nombreCompleto}) y se enviará al correo registrado del empleado. La contraseña anterior dejará de funcionar.`}
          variante="success"
          onCancel={() => setResetPasswordFor(null)}
          onConfirm={handleResetPassword}
        />
      )}

      {liquidarFor && (
        <ModalLiquidar
          empleado={liquidarFor}
          onClose={() => setLiquidarFor(null)}
          onLiquidado={async (liq) => {
            setLiquidarFor(null);
            toast.success(`Empleado liquidado. Total Q${Number(liq.total).toFixed(2)}`);
            await cargar();
          }}
        />
      )}
    </div>
  );
}

// ─── Modal: Crear empleado ───────────────────────────────────────────
function ModalCrearEmpleado({ departamentos, puestos, onClose, onCreated }) {
  const toast = useToast();
  const [form, setForm] = useState(empleadoVacio);
  const [saving, setSaving] = useState(false);

  const set = (k, v) => setForm((f) => ({ ...f, [k]: v }));

  // ─── Validaciones en vivo ───
  const v = useMemo(() => ({
    codigoEmpleado:    gtv.validarCodigoEmpleado(form.codigoEmpleado),
    dpi:               gtv.validarDPI(form.dpi),
    nit:               gtv.validarNIT(form.nit),
    igss:              gtv.validarIGSS(form.numAfiliacionIgss),
    telefono:          gtv.validarTelefonoGT(form.telefono),
    email:             gtv.validarEmail(form.emailCorporativo),
    fechaNacimiento:   gtv.validarEdad(form.fechaNacimiento),
    salario:           gtv.validarSalario(form.salarioBase),
  }), [form]);

  const camposRequeridos = ['codigoEmpleado','dpi','primerNombre','primerApellido','fechaNacimiento','salario'];
  const formularioValido =
    Object.values(v).every(r => r.ok) &&
    form.primerNombre.trim() &&
    form.primerApellido.trim();

  const submit = async (e) => {
    e.preventDefault();
    if (!formularioValido) {
      toast.warning('Revisa los campos marcados en rojo.');
      return;
    }
    setSaving(true);
    try {
      const payload = {
        ...form,
        departamentoId: form.departamentoId ? Number(form.departamentoId) : null,
        puestoId:       form.puestoId ? Number(form.puestoId) : null,
        salarioBase:    Number(form.salarioBase),
        bonificacion:   Number(form.bonificacion) || gtv.BONIFICACION_INCENTIVO,
        fechaNacimiento: form.fechaNacimiento,
        // El servidor solo crea acceso si "acceso" no es null
        acceso: form.crearAcceso ? {
          rolInicial: form.rolInicial,
          passwordTemporal: form.passwordTemporal?.trim() || null,
          enviarCredencialesPorEmail: form.enviarCredencialesPorEmail,
        } : null,
      };
      // Limpia campos del form que NO van al backend
      delete payload.crearAcceso;
      delete payload.rolInicial;
      delete payload.passwordTemporal;
      delete payload.enviarCredencialesPorEmail;

      await empleadosApi.crear(payload);
      toast.success(`Empleado ${form.codigoEmpleado} creado${form.crearAcceso ? ' con cuenta de acceso' : ''}.`);
      onCreated();
    } catch (err) {
      toast.error(getErrorMessage(err), { title: 'No se pudo crear el empleado' });
    } finally {
      setSaving(false);
    }
  };

  return (
    <ModalShell title="Nuevo empleado" onClose={onClose} disabled={saving}>
      <form onSubmit={submit} className="space-y-4">
        <Section title="Datos personales">
          <Input label="Código *" value={form.codigoEmpleado}
            onChange={(val) => set('codigoEmpleado', val.toUpperCase())}
            required maxLength={20} validation={v.codigoEmpleado} placeholder="EMP-001" />
          <Input label="DPI / CUI *" value={form.dpi}
            onChange={(val) => set('dpi', val.replace(/\D/g, '').slice(0, 13))}
            required maxLength={13} validation={v.dpi} placeholder="13 dígitos del CUI/RENAP" />
          <Input label="Primer nombre *" value={form.primerNombre} onChange={(val) => set('primerNombre', val)} required />
          <Input label="Segundo nombre"  value={form.segundoNombre} onChange={(val) => set('segundoNombre', val)} />
          <Input label="Primer apellido *" value={form.primerApellido} onChange={(val) => set('primerApellido', val)} required />
          <Input label="Segundo apellido"  value={form.segundoApellido} onChange={(val) => set('segundoApellido', val)} />
          <Input label="Fecha de nacimiento *" type="date" value={form.fechaNacimiento}
            onChange={(val) => set('fechaNacimiento', val)} required validation={v.fechaNacimiento} />
          <Select label="Género *" value={form.genero} onChange={(val) => set('genero', val)}
            options={[{v:'M',l:'Masculino'},{v:'F',l:'Femenino'}]} />
          <Select label="Estado civil" value={form.estadoCivil} onChange={(val) => set('estadoCivil', val)}
            options={[
              {v:'SOLTERO',l:'Soltero(a)'}, {v:'CASADO',l:'Casado(a)'},
              {v:'UNIDO',l:'Unido(a)'}, {v:'DIVORCIADO',l:'Divorciado(a)'}, {v:'VIUDO',l:'Viudo(a)'}
            ]} />
        </Section>

        <Section title="Contacto y fiscal">
          <Input label="NIT" value={form.nit} onChange={(val) => set('nit', val.toUpperCase())}
            validation={v.nit} placeholder="Ej: 1234567-8 o 1234567K" />
          <Input label="No. afiliación IGSS" value={form.numAfiliacionIgss}
            onChange={(val) => set('numAfiliacionIgss', val)} validation={v.igss} placeholder="6-12 dígitos" />
          <Input label="Teléfono" value={form.telefono} onChange={(val) => set('telefono', val)}
            validation={v.telefono} placeholder="Ej: 5555-1234" />
          <Input label="Correo corporativo" type="email" value={form.emailCorporativo}
            onChange={(val) => set('emailCorporativo', val)} validation={v.email} />
        </Section>

        <Section title="Puesto y salario">
          <Select label="Departamento" value={form.departamentoId} onChange={(val) => set('departamentoId', val)}
            options={[{ v: '', l: '— Sin asignar —' }, ...departamentos.map(d => ({ v: d.departamentoId, l: d.nombre }))]} />
          <Select label="Puesto" value={form.puestoId} onChange={(val) => set('puestoId', val)}
            options={[{ v: '', l: '— Sin asignar —' }, ...puestos.map(p => ({ v: p.puestoId, l: p.nombre }))]} />
          <Input label={`Salario base * (mín. Q${gtv.SALARIO_MINIMO_NO_AGRICOLA.toFixed(2)})`}
            type="number" step="0.01" min={gtv.SALARIO_MINIMO_NO_AGRICOLA}
            value={form.salarioBase} onChange={(val) => set('salarioBase', val)} required
            validation={v.salario} />
          <Input label={`Bonificación (Q) — incentivo legal Q${gtv.BONIFICACION_INCENTIVO}`}
            type="number" step="0.01"
            value={form.bonificacion} onChange={(val) => set('bonificacion', val)} />
          <Select label="Tipo de contrato" value={form.tipoContrato} onChange={(val) => set('tipoContrato', val)}
            options={['INDEFINIDO','TEMPORAL','APRENDIZAJE','OBRA'].map(o => ({ v:o, l:o }))} />
          <Select label="Forma de pago" value={form.formaPago} onChange={(val) => set('formaPago', val)}
            options={['MENSUAL','QUINCENAL','SEMANAL'].map(o => ({ v:o, l:o }))} />
        </Section>

        {/* Acceso al sistema */}
        <div className="border border-stone-200 rounded-xl p-4">
          <label className="flex items-start gap-3 cursor-pointer">
            <input
              type="checkbox"
              checked={form.crearAcceso}
              onChange={(e) => set('crearAcceso', e.target.checked)}
              className="mt-0.5 w-4 h-4 accent-amber-600"
            />
            <div className="flex-1">
              <p className="text-sm font-semibold text-stone-800">Crear cuenta de acceso al sistema</p>
              <p className="text-xs text-stone-500 mt-0.5">
                Se generará un usuario con el código del empleado y una contraseña temporal.
              </p>
            </div>
          </label>

          {form.crearAcceso && (
            <div className="mt-4 pl-7 space-y-3">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <Select
                  label="Rol inicial"
                  value={form.rolInicial}
                  onChange={(val) => set('rolInicial', val)}
                  options={[
                    { v: 'EMPLEADO', l: 'Empleado (consulta sus boletas)' },
                    { v: 'RRHH',     l: 'RRHH (gestión de empleados)' },
                    { v: 'NOMINA',   l: 'Nómina (cálculo y reportes)' },
                    { v: 'AUDITOR',  l: 'Auditor (solo lectura)' },
                    { v: 'ADMIN',    l: 'Administrador (acceso total)' },
                  ]}
                />
                <Input
                  label="Contraseña temporal (opcional)"
                  value={form.passwordTemporal}
                  onChange={(val) => set('passwordTemporal', val)}
                  placeholder="Se generará automáticamente si la dejas vacía"
                />
              </div>
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  type="checkbox"
                  checked={form.enviarCredencialesPorEmail}
                  onChange={(e) => set('enviarCredencialesPorEmail', e.target.checked)}
                  disabled={!form.emailCorporativo}
                  className="w-4 h-4 accent-amber-600"
                />
                <span className={`text-xs ${form.emailCorporativo ? 'text-stone-700' : 'text-stone-400'}`}>
                  Enviar credenciales al correo corporativo
                  {!form.emailCorporativo && ' (necesitas registrar un email primero)'}
                </span>
              </label>
            </div>
          )}
        </div>

        <div className="flex justify-end gap-2 pt-3 border-t border-stone-100">
          <button type="button" onClick={onClose} disabled={saving}
            className="px-3 py-2 text-sm rounded-lg border border-stone-200 bg-white hover:bg-stone-50 disabled:opacity-50">
            Cancelar
          </button>
          <button type="submit" disabled={saving || !formularioValido}
            title={formularioValido ? '' : 'Hay campos inválidos'}
            className="px-4 py-2 text-sm rounded-lg bg-stone-900 hover:bg-stone-800 disabled:bg-stone-300 text-white font-semibold flex items-center gap-2">
            {saving ? <><Loader2 size={14} className="animate-spin" /> Guardando...</> : <><Plus size={14} /> Crear empleado</>}
          </button>
        </div>
      </form>
    </ModalShell>
  );
}

// ─── Modal: Editar empleado (subset que el endpoint permite) ─────────
function ModalEditarEmpleado({ empleado, departamentos, puestos, onClose, onSaved }) {
  const toast = useToast();
  const [form, setForm] = useState({
    departamentoId: empleado.departamentoId ?? '',
    puestoId:       empleado.puestoId ?? '',
    telefono:       empleado.telefono ?? '',
    emailCorporativo: empleado.emailCorporativo ?? '',
    estadoCivil:    empleado.estadoCivil ?? 'SOLTERO',
  });
  const [saving, setSaving] = useState(false);
  const set = (k, v) => setForm((f) => ({ ...f, [k]: v }));

  const submit = async (e) => {
    e.preventDefault();
    setSaving(true);
    try {
      await empleadosApi.actualizar(empleado.empleadoId, {
        departamentoId: form.departamentoId ? Number(form.departamentoId) : null,
        puestoId:       form.puestoId ? Number(form.puestoId) : null,
        telefono:       form.telefono || null,
        emailCorporativo: form.emailCorporativo || null,
        estadoCivil:    form.estadoCivil || null,
      });
      toast.success(`${empleado.codigoEmpleado} actualizado.`);
      onSaved();
    } catch (err) {
      toast.error(getErrorMessage(err), { title: 'No se pudo guardar' });
    } finally {
      setSaving(false);
    }
  };

  return (
    <ModalShell title={`Editar ${empleado.codigoEmpleado}`} subtitle={empleado.nombreCompleto} onClose={onClose} disabled={saving}>
      <form onSubmit={submit} className="space-y-4">
        <Section title="Puesto">
          <Select label="Departamento" value={form.departamentoId} onChange={(v) => set('departamentoId', v)}
            options={[{ v: '', l: '— Sin asignar —' }, ...departamentos.map(d => ({ v: d.departamentoId, l: d.nombre }))]} />
          <Select label="Puesto" value={form.puestoId} onChange={(v) => set('puestoId', v)}
            options={[{ v: '', l: '— Sin asignar —' }, ...puestos.map(p => ({ v: p.puestoId, l: p.nombre }))]} />
        </Section>
        <Section title="Contacto">
          <Input label="Teléfono" value={form.telefono} onChange={(v) => set('telefono', v)} />
          <Input label="Correo corporativo" type="email" value={form.emailCorporativo} onChange={(v) => set('emailCorporativo', v)} />
          <Select label="Estado civil" value={form.estadoCivil} onChange={(v) => set('estadoCivil', v)}
            options={[
              {v:'SOLTERO',l:'Soltero(a)'}, {v:'CASADO',l:'Casado(a)'},
              {v:'UNIDO',l:'Unido(a)'}, {v:'DIVORCIADO',l:'Divorciado(a)'}, {v:'VIUDO',l:'Viudo(a)'}
            ]} />
        </Section>

        <div className="flex justify-end gap-2 pt-3 border-t border-stone-100">
          <button type="button" onClick={onClose} disabled={saving}
            className="px-3 py-2 text-sm rounded-lg border border-stone-200 bg-white hover:bg-stone-50 disabled:opacity-50">
            Cancelar
          </button>
          <button type="submit" disabled={saving}
            className="px-4 py-2 text-sm rounded-lg bg-blue-600 hover:bg-blue-700 disabled:bg-stone-300 text-white font-semibold flex items-center gap-2">
            {saving ? <><Loader2 size={14} className="animate-spin" /> Guardando...</> : <><Pencil size={14} /> Guardar cambios</>}
          </button>
        </div>
      </form>
    </ModalShell>
  );
}

// ─── Componentes auxiliares ──────────────────────────────────────────
function ModalShell({ title, subtitle, onClose, disabled, children }) {
  return (
    <div
      className="fixed inset-0 z-50 bg-stone-900/40 flex items-center justify-center p-4"
      onClick={() => !disabled && onClose()}
    >
      <div
        className="bg-white rounded-xl shadow-xl max-w-2xl w-full max-h-[90vh] overflow-auto"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="sticky top-0 bg-white px-6 py-4 border-b border-stone-100 flex items-start justify-between z-10">
          <div>
            <h3 className="font-bold text-stone-800">{title}</h3>
            {subtitle && <p className="text-xs text-stone-500 mt-0.5">{subtitle}</p>}
          </div>
          <button onClick={() => !disabled && onClose()} className="text-stone-400 hover:text-stone-600 p-1" aria-label="Cerrar">
            <X size={18} />
          </button>
        </div>
        <div className="px-6 py-4">{children}</div>
      </div>
    </div>
  );
}

function Section({ title, children }) {
  return (
    <div>
      <p className="text-xs font-semibold text-stone-400 uppercase tracking-wide mb-2">{title}</p>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">{children}</div>
    </div>
  );
}

function Input({ label, value, onChange, type = 'text', required, validation, ...rest }) {
  const hasValue = value !== undefined && value !== null && String(value).length > 0;
  const showState = validation && hasValue;
  const isOk = showState && validation.ok;
  const isErr = showState && !validation.ok;
  const isWarn = showState && validation.ok && validation.warning;

  const borderClass =
    isErr  ? 'border-red-300 focus:ring-red-500/20' :
    isOk && !isWarn ? 'border-emerald-300 focus:ring-emerald-500/20' :
    isWarn ? 'border-amber-300 focus:ring-amber-500/20' :
    'border-stone-200 focus:ring-amber-500/20';

  return (
    <div>
      <label className="block text-xs font-medium text-stone-600 mb-1">{label}</label>
      <div className="relative">
        <input
          type={type}
          value={value ?? ''}
          required={required}
          onChange={(e) => onChange(e.target.value)}
          className={`w-full px-3 py-2 text-sm border rounded-lg focus:outline-none focus:ring-2 ${borderClass} ${showState ? 'pr-8' : ''}`}
          {...rest}
        />
        {isOk && !isWarn && (
          <CheckCircle2 size={14} className="absolute right-2 top-1/2 -translate-y-1/2 text-emerald-500 pointer-events-none" />
        )}
        {(isErr || isWarn) && (
          <AlertCircle size={14} className={`absolute right-2 top-1/2 -translate-y-1/2 pointer-events-none ${isErr ? 'text-red-500' : 'text-amber-500'}`} />
        )}
      </div>
      {showState && validation.mensaje && (
        <p className={`mt-1 text-xs ${
          isErr ? 'text-red-600' : isWarn ? 'text-amber-600' : 'text-emerald-600'
        }`}>
          {validation.mensaje}
        </p>
      )}
    </div>
  );
}

function Select({ label, value, onChange, options }) {
  return (
    <div>
      <label className="block text-xs font-medium text-stone-600 mb-1">{label}</label>
      <select
        value={value ?? ''}
        onChange={(e) => onChange(e.target.value)}
        className="w-full px-3 py-2 text-sm border border-stone-200 rounded-lg bg-white"
      >
        {options.map((o, i) => <option key={i} value={o.v}>{o.l}</option>)}
      </select>
    </div>
  );
}

function ConfirmModal({ titulo, mensaje, variante = 'danger', onCancel, onConfirm }) {
  const styles = variante === 'danger'
    ? { btn: 'bg-red-600 hover:bg-red-700', icon: <UserMinus size={18} className="text-red-600" />, iconBg: 'bg-red-50' }
    : { btn: 'bg-emerald-600 hover:bg-emerald-700', icon: <UserCheck size={18} className="text-emerald-600" />, iconBg: 'bg-emerald-50' };

  return (
    <div className="fixed inset-0 z-50 bg-stone-900/40 flex items-center justify-center p-4" onClick={onCancel}>
      <div className="bg-white rounded-xl shadow-xl max-w-sm w-full p-6" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-center gap-3 mb-3">
          <div className={`w-10 h-10 rounded-full flex items-center justify-center ${styles.iconBg}`}>
            {styles.icon}
          </div>
          <h3 className="font-bold text-stone-800">{titulo}</h3>
        </div>
        <p className="text-sm text-stone-600">{mensaje}</p>
        <div className="flex justify-end gap-2 mt-5">
          <button onClick={onCancel} className="px-3 py-2 text-sm rounded-lg border border-stone-200 bg-white hover:bg-stone-50">
            Cancelar
          </button>
          <button onClick={onConfirm} className={`px-3 py-2 text-sm rounded-lg text-white font-semibold ${styles.btn}`}>
            Confirmar
          </button>
        </div>
      </div>
    </div>
  );
}

// ─── Modal: Crear acceso retroactivo ────────────────────────────────
function ModalCrearAcceso({ empleado, onClose, onCreated }) {
  const toast = useToast();
  const [form, setForm] = useState({
    nombreUsuario: empleado.codigoEmpleado?.toLowerCase() || '',
    passwordTemporal: '',
    rolInicial: 'EMPLEADO',
    enviarCredencialesPorEmail: true,
  });
  const [saving, setSaving] = useState(false);

  const submit = async (e) => {
    e.preventDefault();
    setSaving(true);
    try {
      const resultado = await empleadosApi.crearAcceso(empleado.empleadoId, {
        nombreUsuario: form.nombreUsuario.trim().toLowerCase() || null,
        passwordTemporal: form.passwordTemporal.trim() || null,
        rolInicial: form.rolInicial,
        enviarCredencialesPorEmail: form.enviarCredencialesPorEmail,
      });
      toast.success(`Acceso creado para ${empleado.codigoEmpleado}.`);
      onCreated(resultado);
    } catch (err) {
      toast.error(getErrorMessage(err), { title: 'No se pudo crear el acceso' });
    } finally { setSaving(false); }
  };

  return (
    <ModalShell title={`Crear acceso para ${empleado.codigoEmpleado}`}
                subtitle={empleado.nombreCompleto} onClose={onClose} disabled={saving}>
      <form onSubmit={submit} className="space-y-4">
        <div className="bg-blue-50 border border-blue-200 rounded-lg p-3 text-xs text-blue-800">
          <KeyRound size={14} className="inline mr-1" />
          Se generará un usuario nuevo para que este empleado pueda iniciar sesión y ver sus boletas.
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <Input label="Nombre de usuario" value={form.nombreUsuario}
                 onChange={(v) => setForm({ ...form, nombreUsuario: v })}
                 placeholder="Ej: emp-007" />
          <Input label="Contraseña temporal (opcional)" value={form.passwordTemporal}
                 onChange={(v) => setForm({ ...form, passwordTemporal: v })}
                 placeholder="Se genera automáticamente si lo dejas vacío" />
        </div>
        <Select label="Rol inicial" value={form.rolInicial}
                onChange={(v) => setForm({ ...form, rolInicial: v })}
                options={[
                  { v: 'EMPLEADO', l: 'Empleado (consulta sus boletas)' },
                  { v: 'RRHH',     l: 'RRHH (gestión de empleados)' },
                  { v: 'NOMINA',   l: 'Nómina (cálculo y reportes)' },
                  { v: 'AUDITOR',  l: 'Auditor (solo lectura)' },
                  { v: 'ADMIN',    l: 'Administrador' },
                ]} />
        <label className="flex items-center gap-2 cursor-pointer">
          <input type="checkbox"
                 checked={form.enviarCredencialesPorEmail}
                 onChange={(e) => setForm({ ...form, enviarCredencialesPorEmail: e.target.checked })}
                 className="w-4 h-4 accent-amber-600" />
          <span className="text-xs text-stone-700">Enviar credenciales por correo al empleado</span>
        </label>

        <div className="flex justify-end gap-2 pt-3 border-t border-stone-100">
          <button type="button" onClick={onClose} disabled={saving}
                  className="px-3 py-2 text-sm rounded-lg border border-stone-200 bg-white hover:bg-stone-50">
            Cancelar
          </button>
          <button type="submit" disabled={saving}
                  className="px-4 py-2 text-sm rounded-lg bg-stone-900 hover:bg-stone-800 disabled:bg-stone-300 text-white font-semibold flex items-center gap-2">
            {saving ? <><Loader2 size={14} className="animate-spin" /> Creando…</> : <><KeyRound size={14} /> Crear acceso</>}
          </button>
        </div>
      </form>
    </ModalShell>
  );
}

// ─── Modal: Credenciales generadas ──────────────────────────────────
function ModalCredencialesCreadas({ empleado, resultado, onClose }) {
  const toast = useToast();
  const copiar = (txt) => {
    navigator.clipboard.writeText(txt).then(() => toast.success('Copiado al portapapeles.'));
  };

  return (
    <ModalShell title="Cuenta creada" subtitle={`${empleado.codigoEmpleado} — ${empleado.nombreCompleto}`}
                onClose={onClose}>
      {resultado.correoEnviado ? (
        <div className="bg-emerald-50 border border-emerald-200 rounded-lg p-4 mb-4 text-sm text-emerald-800">
          <CheckCircle2 size={16} className="inline mr-1" />
          Las credenciales fueron enviadas al correo del empleado.
        </div>
      ) : (
        <div className="bg-amber-50 border border-amber-200 rounded-lg p-4 mb-4 text-sm text-amber-900">
          <AlertCircle size={16} className="inline mr-1" />
          Guarda estas credenciales: no se enviaron por correo y no podrán recuperarse después.
        </div>
      )}

      <div className="space-y-2">
        <CredItem label="Usuario"  value={resultado.nombreUsuario} onCopy={copiar} />
        {resultado.passwordTemporal && (
          <CredItem label="Contraseña temporal" value={resultado.passwordTemporal} onCopy={copiar} mono />
        )}
        <CredItem label="Rol"     value={resultado.rol}   onCopy={copiar} />
        {resultado.email && <CredItem label="Correo" value={resultado.email} onCopy={copiar} />}
      </div>

      <div className="flex justify-end mt-5 pt-3 border-t border-stone-100">
        <button onClick={onClose}
                className="px-4 py-2 text-sm rounded-lg bg-stone-900 hover:bg-stone-800 text-white font-semibold">
          Listo
        </button>
      </div>
    </ModalShell>
  );
}

function CredItem({ label, value, onCopy, mono }) {
  return (
    <div className="flex items-center justify-between gap-2 bg-stone-50 border border-stone-200 rounded-lg px-3 py-2">
      <div className="min-w-0">
        <p className="text-xs text-stone-500">{label}</p>
        <p className={`text-sm text-stone-800 ${mono ? 'font-mono' : 'font-semibold'} truncate`}>{value}</p>
      </div>
      <button onClick={() => onCopy(value)} className="text-stone-400 hover:text-stone-700 p-1 flex-shrink-0">
        <Copy size={14} />
      </button>
    </div>
  );
}

// ─── Modal: Liquidar empleado ──────────────────────────────────────
function ModalLiquidar({ empleado, onClose, onLiquidado }) {
  const toast = useToast();
  const [form, setForm] = useState({
    fechaBaja: new Date().toISOString().slice(0, 10),
    motivo: 'DESPIDO_INJUSTIFICADO',
    motivoDetalle: '',
    otrosPagos: 0,
    descuentos: 0,
    observaciones: '',
    enviarFiniquitoPorEmail: false,
  });
  const [preview, setPreview] = useState(null);
  const [loadingPreview, setLoadingPreview] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');

  // Recalcula preview cuando cambian campos relevantes
  useEffect(() => {
    let cancelled = false;
    if (!form.fechaBaja || !form.motivo) return;
    (async () => {
      setLoadingPreview(true);
      setError('');
      try {
        const dto = await empleadosApi.liquidacionPreview(empleado.empleadoId, {
          fechaBaja: form.fechaBaja,
          motivo: form.motivo,
          motivoDetalle: form.motivoDetalle || null,
          otrosPagos: Number(form.otrosPagos) || 0,
          descuentos: Number(form.descuentos) || 0,
          observaciones: form.observaciones || null,
        });
        if (!cancelled) setPreview(dto);
      } catch (e) {
        if (!cancelled) { setError(getErrorMessage(e)); setPreview(null); }
      } finally { if (!cancelled) setLoadingPreview(false); }
    })();
    return () => { cancelled = true; };
  }, [empleado.empleadoId, form.fechaBaja, form.motivo, form.otrosPagos, form.descuentos]);

  const liquidar = async () => {
    if (!preview) return;
    setSubmitting(true);
    try {
      const liq = await empleadosApi.liquidar(empleado.empleadoId, {
        fechaBaja: form.fechaBaja,
        motivo: form.motivo,
        motivoDetalle: form.motivoDetalle || null,
        otrosPagos: Number(form.otrosPagos) || 0,
        descuentos: Number(form.descuentos) || 0,
        observaciones: form.observaciones || null,
        enviarFiniquitoPorEmail: form.enviarFiniquitoPorEmail,
      });
      onLiquidado(liq);
      // descargar PDF directo
      try {
        await empleadosApi.descargarFiniquito(liq.liquidacionId,
          `Finiquito_${empleado.codigoEmpleado}_${form.fechaBaja}.pdf`);
      } catch { /* opcional */ }
    } catch (e) {
      toast.error(getErrorMessage(e), { title: 'No se pudo liquidar' });
    } finally { setSubmitting(false); }
  };

  const fmt = (n) =>
    new Intl.NumberFormat('es-GT', { style: 'currency', currency: 'GTQ' }).format(Number(n) || 0);

  return (
    <ModalShell title="Liquidar empleado"
                subtitle={`${empleado.codigoEmpleado} — ${empleado.nombreCompleto}`}
                onClose={onClose} disabled={submitting}>
      <div className="space-y-4">
        {/* Form */}
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <Input label="Fecha de baja *" type="date"
                 value={form.fechaBaja}
                 onChange={(v) => setForm({ ...form, fechaBaja: v })} required />
          <Select label="Motivo *" value={form.motivo}
                  onChange={(v) => setForm({ ...form, motivo: v })}
                  options={[
                    { v: 'RENUNCIA',              l: 'Renuncia voluntaria' },
                    { v: 'DESPIDO_JUSTIFICADO',   l: 'Despido justificado' },
                    { v: 'DESPIDO_INJUSTIFICADO', l: 'Despido injustificado' },
                    { v: 'MUTUO_ACUERDO',         l: 'Mutuo acuerdo' },
                    { v: 'JUBILACION',            l: 'Jubilación' },
                    { v: 'FALLECIMIENTO',         l: 'Fallecimiento' },
                    { v: 'OTRO',                  l: 'Otro' },
                  ]} />
          <Input label="Otros pagos (Q)" type="number" step="0.01"
                 value={form.otrosPagos}
                 onChange={(v) => setForm({ ...form, otrosPagos: v })} />
          <Input label="Descuentos (Q)" type="number" step="0.01"
                 value={form.descuentos}
                 onChange={(v) => setForm({ ...form, descuentos: v })} />
          <div className="sm:col-span-2">
            <label className="block text-xs font-medium text-stone-600 mb-1">Detalle del motivo (opcional)</label>
            <input
              type="text"
              value={form.motivoDetalle}
              onChange={(e) => setForm({ ...form, motivoDetalle: e.target.value })}
              className="w-full px-3 py-2 text-sm border border-stone-200 rounded-lg"
            />
          </div>
          <div className="sm:col-span-2">
            <label className="block text-xs font-medium text-stone-600 mb-1">Observaciones</label>
            <textarea
              value={form.observaciones}
              onChange={(e) => setForm({ ...form, observaciones: e.target.value })}
              rows={2}
              className="w-full px-3 py-2 text-sm border border-stone-200 rounded-lg resize-none"
            />
          </div>
        </div>

        {/* Preview */}
        <div>
          <p className="text-xs font-semibold text-stone-400 uppercase tracking-wide mb-2">
            Cálculo de prestaciones (en vivo)
          </p>

          {loadingPreview ? (
            <div className="p-6 text-center text-stone-400">
              <Loader2 className="animate-spin inline mr-2" size={14} /> Calculando...
            </div>
          ) : error ? (
            <div className="p-4 bg-red-50 border border-red-200 rounded-lg text-sm text-red-700">
              <AlertCircle size={14} className="inline mr-1" /> {error}
            </div>
          ) : preview ? (
            <div className="bg-stone-50 border border-stone-200 rounded-lg overflow-hidden">
              <div className="px-4 py-2 text-xs text-stone-500 bg-white border-b border-stone-100">
                Antigüedad: <strong>{Number(preview.aniosServicio).toFixed(2)} años</strong>
                {' · '}salario diario <strong>{fmt(preview.salarioDiario)}</strong>
                {' · '}vacaciones pendientes <strong>{preview.diasVacacionesPend} días</strong>
              </div>
              <table className="w-full text-sm">
                <tbody className="divide-y divide-stone-200">
                  <FilaPrest label="Indemnización"             monto={preview.indemnizacion}
                             hint={preview.indemnizacion === 0 ? 'No aplica para este motivo' : `${Number(preview.aniosServicio).toFixed(2)} años × salario`} />
                  <FilaPrest label="Bono 14 proporcional"       monto={preview.bono14Proporcional}
                             hint="Decreto 42-92 (jul–jun)" />
                  <FilaPrest label="Aguinaldo proporcional"     monto={preview.aguinaldoProporcional}
                             hint="Art. 137 CT (dic–nov)" />
                  <FilaPrest label="Vacaciones no gozadas"      monto={preview.vacacionesNoGozadas}
                             hint={`${preview.diasVacacionesPend} días × ${fmt(preview.salarioDiario)}`} />
                  {Number(preview.otrosPagos) > 0 && (
                    <FilaPrest label="(+) Otros pagos"          monto={preview.otrosPagos} />
                  )}
                  {Number(preview.descuentos) > 0 && (
                    <FilaPrest label="(−) Descuentos"           monto={-preview.descuentos} negative />
                  )}
                </tbody>
                <tfoot>
                  <tr className="bg-amber-500 text-white">
                    <td className="px-4 py-3 font-bold">TOTAL A LIQUIDAR</td>
                    <td className="px-4 py-3 text-right font-bold text-lg tabular-nums">{fmt(preview.total)}</td>
                  </tr>
                </tfoot>
              </table>
            </div>
          ) : null}
        </div>

        {empleado && (
          <label className="flex items-center gap-2 cursor-pointer">
            <input type="checkbox"
                   checked={form.enviarFiniquitoPorEmail}
                   onChange={(e) => setForm({ ...form, enviarFiniquitoPorEmail: e.target.checked })}
                   className="w-4 h-4 accent-amber-600" />
            <span className="text-xs text-stone-700">
              Enviar el finiquito en PDF al correo del empleado
            </span>
          </label>
        )}

        <div className="bg-amber-50 border border-amber-200 rounded-lg p-3 text-xs text-amber-900">
          <AlertCircle size={14} className="inline mr-1" />
          Al confirmar: el empleado pasa a estado <strong>BAJA</strong>, se registra la liquidación
          y se descargará el PDF del finiquito.
        </div>

        <div className="flex justify-end gap-2 pt-3 border-t border-stone-100">
          <button onClick={onClose} disabled={submitting}
                  className="px-3 py-2 text-sm rounded-lg border border-stone-200 bg-white hover:bg-stone-50 disabled:opacity-50">
            Cancelar
          </button>
          <button onClick={liquidar} disabled={submitting || !preview}
                  className="px-4 py-2 text-sm rounded-lg bg-red-600 hover:bg-red-700 disabled:bg-stone-300 text-white font-semibold flex items-center gap-2">
            {submitting ? <><Loader2 size={14} className="animate-spin" /> Liquidando…</> : <><Receipt size={14} /> Confirmar liquidación</>}
          </button>
        </div>
      </div>
    </ModalShell>
  );
}

function FilaPrest({ label, monto, hint, negative }) {
  const fmt = (n) =>
    new Intl.NumberFormat('es-GT', { style: 'currency', currency: 'GTQ' }).format(Number(n) || 0);
  return (
    <tr>
      <td className="px-4 py-2.5">
        <div className="text-sm text-stone-700">{label}</div>
        {hint && <div className="text-xs text-stone-400 mt-0.5">{hint}</div>}
      </td>
      <td className={`px-4 py-2.5 text-right tabular-nums font-mono ${negative ? 'text-red-600' : 'text-stone-800'}`}>
        {fmt(monto)}
      </td>
    </tr>
  );
}
