// Validadores guatemaltecos (replica de Helpers/GuatemalaValidators.cs).
// Mantener sincronizado con el backend.

// ─── Constantes ───
export const SALARIO_MINIMO_NO_AGRICOLA = 3591.85;
export const BONIFICACION_INCENTIVO = 250.00;
export const EDAD_MINIMA_LEGAL = 18;
export const EDAD_MINIMA_ABSOLUTA = 14;

// ─── DPI / CUI ───
/**
 * Valida un DPI/CUI guatemalteco con algoritmo RENAP.
 * @returns { ok: boolean, mensaje: string }
 */
export function validarDPI(dpi) {
  if (!dpi) return { ok: false, mensaje: 'DPI requerido' };
  const limpio = String(dpi).replace(/[\s-]/g, '');

  if (limpio.length !== 13) return { ok: false, mensaje: 'DPI debe tener 13 dígitos' };
  if (!/^\d{13}$/.test(limpio)) return { ok: false, mensaje: 'DPI solo acepta números' };

  const depto = parseInt(limpio.slice(9, 11), 10);
  if (depto < 1 || depto > 22) return { ok: false, mensaje: 'Código de departamento inválido (debe ser 01–22)' };

  let sum = 0;
  for (let i = 0; i < 8; i++) sum += parseInt(limpio[i], 10) * (i + 2);
  const mod = sum % 11;
  if (mod === 10) return { ok: false, mensaje: 'DPI inválido (dígito verificador imposible)' };

  const verificador = parseInt(limpio[8], 10);
  if (mod !== verificador) return { ok: false, mensaje: 'Dígito verificador no coincide (RENAP)' };

  return { ok: true, mensaje: 'DPI válido' };
}

// ─── NIT ───
export function validarNIT(nit) {
  if (!nit) return { ok: true, mensaje: '' }; // opcional
  const limpio = String(nit).replace(/[\s-]/g, '').toUpperCase();
  if (limpio.length < 2) return { ok: false, mensaje: 'NIT demasiado corto' };
  if (!/^\d+[0-9K]$/.test(limpio)) return { ok: false, mensaje: 'NIT debe ser dígitos + verificador (puede ser K)' };

  const cuerpo = limpio.slice(0, -1);
  const verificador = limpio.slice(-1);

  let suma = 0, factor = cuerpo.length + 1;
  for (const c of cuerpo) {
    suma += parseInt(c, 10) * factor;
    factor--;
  }
  const residuo = suma % 11;
  const calc = residuo === 0 ? '0' : (11 - residuo === 10 ? 'K' : String(11 - residuo));
  if (verificador !== calc) return { ok: false, mensaje: `Dígito verificador SAT no coincide (esperado: ${calc})` };
  return { ok: true, mensaje: 'NIT válido' };
}

// ─── IGSS ───
export function validarIGSS(num) {
  if (!num) return { ok: true, mensaje: '' };
  const limpio = String(num).replace(/[\s-]/g, '');
  if (!/^\d{6,12}$/.test(limpio)) return { ok: false, mensaje: 'IGSS debe tener 6 a 12 dígitos' };
  return { ok: true, mensaje: 'IGSS válido' };
}

// ─── Teléfono GT ───
export function validarTelefonoGT(tel) {
  if (!tel) return { ok: true, mensaje: '' };
  let limpio = String(tel).replace(/[\s\-()+]/g, '');
  if (limpio.startsWith('502')) limpio = limpio.slice(3);
  if (!/^[2-7]\d{7}$/.test(limpio)) return { ok: false, mensaje: 'Teléfono GT debe tener 8 dígitos y empezar con 2-7' };
  return { ok: true, mensaje: 'Teléfono válido' };
}

// ─── Email ───
export function validarEmail(email) {
  if (!email) return { ok: true, mensaje: '' };
  if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(email)) return { ok: false, mensaje: 'Correo electrónico inválido' };
  return { ok: true, mensaje: 'Correo válido' };
}

// ─── Edad ───
export function calcularEdad(fechaNacimiento, ref = new Date()) {
  const f = new Date(fechaNacimiento);
  let edad = ref.getFullYear() - f.getFullYear();
  const m = ref.getMonth() - f.getMonth();
  if (m < 0 || (m === 0 && ref.getDate() < f.getDate())) edad--;
  return edad;
}

export function validarEdad(fechaNacimiento) {
  if (!fechaNacimiento) return { ok: false, mensaje: 'Fecha de nacimiento requerida' };
  const edad = calcularEdad(fechaNacimiento);
  if (edad < EDAD_MINIMA_ABSOLUTA)
    return { ok: false, mensaje: `Edad mínima absoluta: ${EDAD_MINIMA_ABSOLUTA} años (con permiso del MinTrab).` };
  if (edad > 80)
    return { ok: false, mensaje: 'Edad fuera de rango razonable.' };
  if (edad < EDAD_MINIMA_LEGAL)
    return { ok: true, mensaje: `Menor de edad (${edad}). Requiere permiso del MinTrab.` , warning: true };
  return { ok: true, mensaje: `${edad} años` };
}

// ─── Salario ───
export function validarSalario(salario) {
  const v = Number(salario);
  if (!salario || isNaN(v)) return { ok: false, mensaje: 'Salario requerido' };
  if (v < SALARIO_MINIMO_NO_AGRICOLA)
    return { ok: false, mensaje: `Mínimo legal vigente: Q${SALARIO_MINIMO_NO_AGRICOLA.toFixed(2)}` };
  if (v > 1_000_000)
    return { ok: false, mensaje: 'Salario fuera de rango razonable' };
  return { ok: true, mensaje: 'Salario válido' };
}

// ─── Codigo de empleado ───
export function validarCodigoEmpleado(codigo) {
  if (!codigo) return { ok: false, mensaje: 'Código requerido' };
  if (!/^[A-Z]{2,5}-?\d{3,6}$/i.test(codigo))
    return { ok: false, mensaje: 'Formato esperado: 2-5 letras + número (ej: EMP-001)' };
  return { ok: true, mensaje: '' };
}
