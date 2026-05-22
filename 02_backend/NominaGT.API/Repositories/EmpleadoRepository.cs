using Dapper;
using NominaGT.API.Data;
using NominaGT.API.DTOs;

namespace NominaGT.API.Repositories;

public class EmpleadoRepository : IEmpleadoRepository
{
    private readonly DapperContext _db;
    public EmpleadoRepository(DapperContext db) => _db = db;

    public async Task<(List<EmpleadoListDto> Items, int Total)> ListarAsync(
        int empresaId, int page, int pageSize, string? estado, string? busqueda)
    {
        using var conn = _db.CreateConnection();

        var where = "WHERE e.empresa_id = :empresaId";
        if (!string.IsNullOrEmpty(estado))   where += " AND e.estado = :estado";
        if (!string.IsNullOrEmpty(busqueda)) where += " AND (LOWER(e.primer_nombre) LIKE :busq OR LOWER(e.primer_apellido) LIKE :busq OR LOWER(e.codigo_empleado) LIKE :busq)";

        var countSql = $"SELECT COUNT(*) FROM empleados e {where}";

        var dataSql = $@"
            SELECT e.empleado_id,
                   e.codigo_empleado,
                   e.primer_nombre || ' ' || NVL(e.segundo_nombre,'') || ' ' || e.primer_apellido || ' ' || NVL(e.segundo_apellido,'') AS nombre_completo,
                   d.nombre AS nombre_departamento,
                   p.nombre AS nombre_puesto,
                   c.salario_base,
                   e.estado,
                   (SELECT COUNT(*) FROM usuarios u WHERE u.empleado_id = e.empleado_id AND u.activo = 1) AS tiene_acceso
            FROM empleados e
            LEFT JOIN departamentos d ON d.departamento_id = e.departamento_id
            LEFT JOIN puestos p ON p.puesto_id = e.puesto_id
            LEFT JOIN contratos_laborales c ON c.empleado_id = e.empleado_id AND c.activo = 1
            {where}
            ORDER BY e.codigo_empleado
            OFFSET :skipRows ROWS FETCH NEXT :pageSize ROWS ONLY";

        var p = new
        {
            empresaId, estado, busq = $"%{busqueda?.ToLower()}%",
            skipRows = (page - 1) * pageSize, pageSize
        };

        var total = await conn.ExecuteScalarAsync<int>(countSql, p);
        var items = (await conn.QueryAsync<EmpleadoListDto>(dataSql, p)).ToList();
        return (items, total);
    }

    public async Task<EmpleadoDetalleDto?> ObtenerAsync(int empleadoId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<EmpleadoDetalleDto>(@"
            SELECT e.empleado_id, e.empresa_id, e.codigo_empleado,
                   e.primer_nombre, e.segundo_nombre,
                   e.primer_apellido, e.segundo_apellido,
                   e.dpi, e.nit, e.num_afiliacion_igss,
                   e.fecha_nacimiento, e.genero, e.estado_civil,
                   e.telefono, e.email_corporativo, e.estado,
                   e.departamento_id, d.nombre AS nombre_departamento,
                   e.puesto_id, p.nombre AS nombre_puesto,
                   c.salario_base, c.bonificacion, c.tipo_contrato, c.forma_pago
            FROM empleados e
            LEFT JOIN departamentos d ON d.departamento_id = e.departamento_id
            LEFT JOIN puestos p ON p.puesto_id = e.puesto_id
            LEFT JOIN contratos_laborales c ON c.empleado_id = e.empleado_id AND c.activo = 1
            WHERE e.empleado_id = :empleadoId",
            new { empleadoId });
    }

    public async Task<int> CrearAsync(CrearEmpleadoRequest req)
    {
        using var conn = _db.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            // 1. Insert empleado (sin RETURNING - en Oracle+Dapper requiere
            //    OracleParameter OUT que Dapper no maneja con records).
            await conn.ExecuteAsync(@"
                INSERT INTO empleados (empresa_id, sucursal_id, departamento_id, puesto_id, codigo_empleado,
                    primer_nombre, segundo_nombre, primer_apellido, segundo_apellido,
                    dpi, nit, num_afiliacion_igss, fecha_nacimiento, genero, estado_civil,
                    telefono, email_corporativo)
                VALUES (:EmpresaId, :SucursalId, :DepartamentoId, :PuestoId, :CodigoEmpleado,
                    :PrimerNombre, :SegundoNombre, :PrimerApellido, :SegundoApellido,
                    :Dpi, :Nit, :NumAfiliacionIgss, :FechaNacimiento, :Genero, :EstadoCivil,
                    :Telefono, :EmailCorporativo)", req, tx);

            // 2. Recuperar el id generado. dpi tiene UNIQUE constraint asi que es seguro.
            var empId = await conn.ExecuteScalarAsync<int>(
                "SELECT empleado_id FROM empleados WHERE dpi = :Dpi", new { req.Dpi }, tx);

            // 2. Insert contrato
            await conn.ExecuteAsync(@"
                INSERT INTO contratos_laborales (empleado_id, tipo_contrato, fecha_inicio,
                    salario_base, bonificacion, jornada_laboral, forma_pago, activo)
                VALUES (:EmpleadoId, :TipoContrato, :FechaInicio,
                    :SalarioBase, :Bonificacion, :JornadaLaboral, :FormaPago, 1)",
                new
                {
                    EmpleadoId = empId,
                    req.TipoContrato,
                    FechaInicio = req.FechaInicioContrato ?? DateTime.Today,
                    req.SalarioBase, req.Bonificacion,
                    req.JornadaLaboral, req.FormaPago
                }, tx);

            // 3. Cuenta bancaria opcional
            if (!string.IsNullOrEmpty(req.BancoNombre) && !string.IsNullOrEmpty(req.NumeroCuenta))
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO cuentas_bancarias (empleado_id, banco_nombre, numero_cuenta, tipo_cuenta, es_principal)
                    VALUES (:EmpleadoId, :BancoNombre, :NumeroCuenta, :TipoCuenta, 1)",
                    new { EmpleadoId = empId, req.BancoNombre, req.NumeroCuenta, req.TipoCuenta }, tx);
            }

            tx.Commit();
            return empId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<bool> ActualizarAsync(int empleadoId, ActualizarEmpleadoRequest req)
    {
        using var conn = _db.CreateConnection();
        var rows = await conn.ExecuteAsync(@"
            UPDATE empleados
            SET departamento_id   = NVL(:DepartamentoId, departamento_id),
                puesto_id         = NVL(:PuestoId, puesto_id),
                telefono          = NVL(:Telefono, telefono),
                email_corporativo = NVL(:EmailCorporativo, email_corporativo),
                estado_civil      = NVL(:EstadoCivil, estado_civil),
                actualizado_en    = SYSTIMESTAMP
            WHERE empleado_id = :EmpleadoId",
            new { req.DepartamentoId, req.PuestoId, req.Telefono, req.EmailCorporativo, req.EstadoCivil, EmpleadoId = empleadoId });
        return rows > 0;
    }

    public async Task<bool> CambiarEstadoAsync(int empleadoId, string estado, string? motivo)
    {
        using var conn = _db.CreateConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            var rows = await conn.ExecuteAsync(@"
                UPDATE empleados
                SET estado = :Estado,
                    motivo_baja = CASE WHEN :Estado = 'BAJA' THEN :Motivo ELSE motivo_baja END,
                    fecha_baja = CASE WHEN :Estado = 'BAJA' THEN SYSDATE ELSE fecha_baja END
                WHERE empleado_id = :Id",
                new { Estado = estado, Motivo = motivo, Id = empleadoId }, tx);

            if (estado == "BAJA")
            {
                await conn.ExecuteAsync(
                    "UPDATE contratos_laborales SET activo = 0 WHERE empleado_id = :Id AND activo = 1",
                    new { Id = empleadoId }, tx);
            }

            tx.Commit();
            return rows > 0;
        }
        catch { tx.Rollback(); throw; }
    }

    public async Task<bool> ExisteDpiAsync(string dpi, int? excluirEmpleadoId = null)
    {
        using var conn = _db.CreateConnection();
        var sql = "SELECT COUNT(*) FROM empleados WHERE dpi = :Dpi";
        if (excluirEmpleadoId.HasValue) sql += " AND empleado_id != :Id";
        var count = await conn.ExecuteScalarAsync<int>(sql, new { Dpi = dpi, Id = excluirEmpleadoId });
        return count > 0;
    }

    public async Task<bool> ExisteCodigoAsync(int empresaId, string codigo)
    {
        using var conn = _db.CreateConnection();
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM empleados WHERE empresa_id = :EmpresaId AND codigo_empleado = :Codigo",
            new { EmpresaId = empresaId, Codigo = codigo });
        return count > 0;
    }

    /// <summary>
    /// Obtiene fecha de inicio del contrato activo y salario base actual.
    /// Si no hay contrato activo, devuelve null.
    /// </summary>
    public async Task<(DateTime fechaInicio, decimal salario)?> ObtenerInicioContratoYSalarioAsync(int empleadoId)
    {
        using var conn = _db.CreateConnection();
        var row = await conn.QueryFirstOrDefaultAsync(@"
            SELECT c.fecha_inicio AS FECHA_INICIO,
                   c.salario_base AS SALARIO_BASE
              FROM contratos_laborales c
             WHERE c.empleado_id = :EmpleadoId AND c.activo = 1
             ORDER BY c.fecha_inicio DESC FETCH FIRST 1 ROWS ONLY",
            new { EmpleadoId = empleadoId });
        if (row == null) return null;
        var dict = (IDictionary<string, object>)row;
        var fecha = Convert.ToDateTime(dict["FECHA_INICIO"]);
        var salario = Convert.ToDecimal(dict["SALARIO_BASE"]);
        return (fecha, salario);
    }
}
