using FluentValidation;
using NominaGT.API.DTOs;
using NominaGT.API.Helpers;

namespace NominaGT.API.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.NombreUsuario)
            .NotEmpty().WithMessage("El nombre de usuario es requerido.")
            .MaximumLength(50);
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contrasena es requerida.")
            .MinimumLength(4);
    }
}

public class CrearEmpleadoRequestValidator : AbstractValidator<CrearEmpleadoRequest>
{
    public CrearEmpleadoRequestValidator()
    {
        RuleFor(x => x.EmpresaId).GreaterThan(0);

        RuleFor(x => x.CodigoEmpleado)
            .NotEmpty().WithMessage("Codigo es requerido.")
            .MaximumLength(20)
            .Must(GuatemalaValidators.ValidarCodigoEmpleado)
            .WithMessage("Codigo invalido. Formato esperado: prefijo de 2-5 letras + numero (ej: EMP-001).");

        RuleFor(x => x.PrimerNombre).NotEmpty().MaximumLength(60);
        RuleFor(x => x.PrimerApellido).NotEmpty().MaximumLength(60);

        RuleFor(x => x.Dpi)
            .NotEmpty().WithMessage("DPI es requerido.")
            .Must(GuatemalaValidators.ValidarDPI)
            .WithMessage("DPI invalido. Verifica los 13 digitos, el digito verificador y el codigo de departamento (RENAP).");

        RuleFor(x => x.Nit)
            .Must(nit => string.IsNullOrEmpty(nit) || GuatemalaValidators.ValidarNIT(nit))
            .WithMessage("NIT invalido. Verifica el digito verificador (modulo 11 SAT, soporta 'K').");

        RuleFor(x => x.NumAfiliacionIgss)
            .Must(GuatemalaValidators.ValidarIGSS)
            .WithMessage("Numero de afiliacion IGSS invalido. Debe tener entre 6 y 12 digitos.");

        RuleFor(x => x.Telefono)
            .Must(GuatemalaValidators.ValidarTelefonoGT)
            .WithMessage("Telefono invalido. Debe ser un numero guatemalteco de 8 digitos (puede iniciar con +502).");

        RuleFor(x => x.EmailCorporativo)
            .Must(GuatemalaValidators.ValidarEmail)
            .WithMessage("Correo electronico invalido.");

        RuleFor(x => x.FechaNacimiento)
            .Must(fecha => GuatemalaValidators.CalcularEdad(fecha) >= GuatemalaValidators.EdadMinimaAbsoluta)
            .WithMessage($"El empleado debe ser mayor de {GuatemalaValidators.EdadMinimaAbsoluta} anios. Para menores de {GuatemalaValidators.EdadMinimaLegal} se requiere permiso del MinTrab.")
            .Must(fecha => GuatemalaValidators.CalcularEdad(fecha) <= 80)
            .WithMessage("Fecha de nacimiento sospechosa: edad mayor a 80 anios.");

        RuleFor(x => x.Genero)
            .Must(g => g == "M" || g == "F")
            .WithMessage("Genero debe ser M o F.");

        RuleFor(x => x.SalarioBase)
            .GreaterThanOrEqualTo(GuatemalaValidators.SalarioMinimoNoAgricola2026)
            .WithMessage($"Salario no puede ser menor al minimo vigente (Q{GuatemalaValidators.SalarioMinimoNoAgricola2026:N2}).")
            .LessThan(1_000_000m).WithMessage("Salario fuera de rango razonable.");

        RuleFor(x => x.Bonificacion)
            .GreaterThanOrEqualTo(0).WithMessage("Bonificacion no puede ser negativa.")
            .LessThanOrEqualTo(50_000m).WithMessage("Bonificacion fuera de rango razonable.");

        RuleFor(x => x.TipoContrato)
            .Must(t => new[] { "INDEFINIDO", "TEMPORAL", "APRENDIZAJE", "OBRA" }.Contains(t))
            .WithMessage("Tipo de contrato invalido.");

        RuleFor(x => x.JornadaLaboral)
            .Must(j => new[] { "DIURNA", "NOCTURNA", "MIXTA" }.Contains(j))
            .WithMessage("Jornada laboral invalida.");

        RuleFor(x => x.FormaPago)
            .Must(f => new[] { "MENSUAL", "QUINCENAL", "SEMANAL" }.Contains(f))
            .WithMessage("Forma de pago invalida.");
    }
}

public class CrearPeriodoRequestValidator : AbstractValidator<CrearPeriodoRequest>
{
    public CrearPeriodoRequestValidator()
    {
        RuleFor(x => x.Anio).InclusiveBetween(2020, 2050);
        RuleFor(x => x.Mes).InclusiveBetween(1, 12);
        RuleFor(x => x.TipoPeriodo)
            .Must(t => t == "MENSUAL" || t == "QUINCENAL" || t == "BONO14" || t == "AGUINALDO")
            .WithMessage("Tipo de periodo debe ser MENSUAL, QUINCENAL, BONO14 o AGUINALDO.");
    }
}

public class CrearVacacionRequestValidator : AbstractValidator<CrearVacacionRequest>
{
    public CrearVacacionRequestValidator()
    {
        RuleFor(x => x.EmpleadoId).GreaterThan(0);
        RuleFor(x => x.FechaInicio).NotEmpty()
            .GreaterThanOrEqualTo(DateTime.Today.AddDays(-7))
            .WithMessage("La fecha de inicio no puede ser muy anterior a hoy.");
        RuleFor(x => x.FechaFin).NotEmpty();
        RuleFor(x => x).Must(x => x.FechaFin >= x.FechaInicio)
            .WithMessage("La fecha fin debe ser igual o posterior al inicio.");
        RuleFor(x => x).Must(x => (x.FechaFin - x.FechaInicio).Days <= 60)
            .WithMessage("Periodo demasiado largo. Maximo 60 dias por solicitud.");
    }
}
