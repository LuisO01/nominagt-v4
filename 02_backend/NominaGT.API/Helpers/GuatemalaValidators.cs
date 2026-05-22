using System.Text.RegularExpressions;

namespace NominaGT.API.Helpers;

/// <summary>
/// Validadores y constantes especificas de Guatemala (DPI/CUI, NIT, IGSS,
/// salario minimo, IGSS%, etc.). Centralizar aqui evita duplicacion de logica
/// fiscal en validators y servicios.
/// </summary>
public static class GuatemalaValidators
{
    // ─── Constantes vigentes (Acuerdo Gubernativo 264-2025 / 2026) ───
    /// <summary>Salario minimo mensual no agricola y maquila (Q3,591.85 + Q250 bonificacion incentivo).</summary>
    public const decimal SalarioMinimoNoAgricola2026 = 3591.85m;
    /// <summary>Bonificacion incentivo (Decreto 78-89), obligatoria adicional al salario.</summary>
    public const decimal BonificacionIncentivo = 250.00m;
    /// <summary>Cuota laboral IGSS sobre salario ordinario.</summary>
    public const decimal IgssCuotaLaboral    = 0.0483m;
    /// <summary>Cuota patronal IGSS sobre salario ordinario.</summary>
    public const decimal IgssCuotaPatronal   = 0.1067m;
    /// <summary>Cuota IRTRA (patronal).</summary>
    public const decimal CuotaIrtraPatronal  = 0.0100m;
    /// <summary>Cuota INTECAP (patronal).</summary>
    public const decimal CuotaIntecapPatronal = 0.0100m;
    /// <summary>Edad minima legal para trabajar sin permiso especial del MinTrab.</summary>
    public const int EdadMinimaLegal = 18;
    /// <summary>Edad minima absoluta con permiso del MinTrab.</summary>
    public const int EdadMinimaAbsoluta = 14;

    // ─── DPI / CUI (Codigo Unico de Identificacion) ───
    /// <summary>
    /// Valida un DPI (CUI) de Guatemala usando el algoritmo del RENAP:
    ///   1. 13 digitos numericos.
    ///   2. Posiciones 10-11 = codigo de departamento (01..22).
    ///   3. Digito 9 = verificador, calculado por suma ponderada modulo 11
    ///      sobre los primeros 8 digitos con factores 2..9.
    /// </summary>
    public static bool ValidarDPI(string? dpi)
    {
        if (string.IsNullOrWhiteSpace(dpi)) return false;
        var limpio = dpi.Replace(" ", "").Replace("-", "");
        if (limpio.Length != 13 || !limpio.All(char.IsDigit)) return false;

        // Codigo de departamento (Guatemala = 01, ..., Peten = 17, ..., total 22)
        var depto = int.Parse(limpio.Substring(9, 2));
        if (depto < 1 || depto > 22) return false;

        // Digito verificador (algoritmo RENAP)
        int sum = 0;
        for (int i = 0; i < 8; i++)
            sum += (limpio[i] - '0') * (i + 2);
        int mod = sum % 11;
        // mod == 10 hace al CUI invalido por convencion.
        if (mod == 10) return false;
        int verificador = limpio[8] - '0';
        return mod == verificador;
    }

    // ─── NIT ───
    /// <summary>
    /// Valida un NIT de Guatemala (SAT): cuerpo numerico + digito verificador
    /// que puede ser 0..9 o 'K'. Algoritmo: multiplicar cada digito por factores
    /// descendientes desde length+1, sumar, mod 11, 11-residuo es el digito.
    /// </summary>
    public static bool ValidarNIT(string? nit)
    {
        if (string.IsNullOrWhiteSpace(nit)) return false;
        var limpio = nit.Replace("-", "").Replace(" ", "").ToUpperInvariant();
        if (limpio.Length < 2) return false;
        if (!Regex.IsMatch(limpio, @"^\d+[0-9K]$")) return false;

        var cuerpo = limpio[..^1];
        var verificador = limpio[^1].ToString();

        int suma = 0, factor = cuerpo.Length + 1;
        foreach (var c in cuerpo)
        {
            suma += (c - '0') * factor;
            factor--;
        }

        var residuo = suma % 11;
        var calc = residuo == 0 ? "0" : (11 - residuo == 10 ? "K" : (11 - residuo).ToString());
        return verificador == calc;
    }

    // ─── IGSS (numero de afiliacion) ───
    /// <summary>
    /// Valida formato de numero de afiliacion IGSS: 8 a 9 digitos, opcionalmente
    /// con guiones, sin caracteres no numericos. No tiene digito verificador
    /// publicado oficialmente.
    /// </summary>
    public static bool ValidarIGSS(string? num)
    {
        if (string.IsNullOrWhiteSpace(num)) return true; // opcional
        var limpio = num.Replace("-", "").Replace(" ", "");
        return limpio.Length is >= 6 and <= 12 && limpio.All(char.IsDigit);
    }

    // ─── Telefono Guatemala ───
    /// <summary>
    /// Valida un telefono guatemalteco: 8 digitos comenzando en 2-7
    /// (fijos: 2; moviles: 3,4,5,6; especiales: 7).
    /// </summary>
    public static bool ValidarTelefonoGT(string? tel)
    {
        if (string.IsNullOrWhiteSpace(tel)) return true; // opcional
        var limpio = Regex.Replace(tel, @"[\s\-()+]", "");
        // Aceptar prefijo internacional 502 opcional
        if (limpio.StartsWith("502")) limpio = limpio[3..];
        return Regex.IsMatch(limpio, @"^[2-7]\d{7}$");
    }

    // ─── Email ───
    public static bool ValidarEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return true; // opcional
        return Regex.IsMatch(email,
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.IgnoreCase);
    }

    // ─── Edad ───
    /// <summary>
    /// Calcula los anios cumplidos a la fecha. Usa anio-mes-dia para precision.
    /// </summary>
    public static int CalcularEdad(DateTime fechaNacimiento, DateTime? a = null)
    {
        var ref_ = a ?? DateTime.Today;
        var edad = ref_.Year - fechaNacimiento.Year;
        if (fechaNacimiento.Date > ref_.AddYears(-edad)) edad--;
        return edad;
    }

    // ─── Codigo de empleado ───
    /// <summary>EMP-### (3+ digitos), case-insensitive.</summary>
    public static bool ValidarCodigoEmpleado(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return false;
        return Regex.IsMatch(codigo, @"^[A-Z]{2,5}-?\d{3,6}$", RegexOptions.IgnoreCase);
    }
}
