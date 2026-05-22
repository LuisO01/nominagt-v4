using Dapper;
using NominaGT.API.Data;
using NominaGT.API.DTOs;
using NominaGT.API.Models;

namespace NominaGT.API.Repositories;

public class NominaRepository : INominaRepository
{
    private readonly DapperContext _db;
    public NominaRepository(DapperContext db) => _db = db;

    public async Task<int> CrearPeriodoAsync(CrearPeriodoRequest req)
    {
        using var conn = _db.CreateConnection();
        var lastDay = DateTime.DaysInMonth(req.Anio, req.Mes);
        var fechaInicio = new DateTime(req.Anio, req.Mes, 1);
        var fechaFin = new DateTime(req.Anio, req.Mes, lastDay);

        await conn.ExecuteAsync(@"
            INSERT INTO periodos_nomina (empresa_id, anio, mes, tipo_periodo,
                fecha_inicio, fecha_fin, fecha_pago, estado)
            VALUES (:EmpresaId, :Anio, :Mes, :TipoPeriodo,
                :FechaInicio, :FechaFin, :FechaFin, 'ABIERTO')",
            new { req.EmpresaId, req.Anio, req.Mes, req.TipoPeriodo, FechaInicio = fechaInicio, FechaFin = fechaFin });

        return await conn.ExecuteScalarAsync<int>(@"
            SELECT periodo_id FROM periodos_nomina
            WHERE empresa_id = :E AND anio = :A AND mes = :M AND tipo_periodo = :T",
            new { E = req.EmpresaId, A = req.Anio, M = req.Mes, T = req.TipoPeriodo });
    }

    public async Task<List<PeriodoDto>> ListarPeriodosAsync(int empresaId, int? anio = null)
    {
        using var conn = _db.CreateConnection();
        var sql = @"
            SELECT periodo_id, anio, mes, tipo_periodo,
                   fecha_inicio, fecha_fin, fecha_pago, estado
            FROM periodos_nomina
            WHERE empresa_id = :EmpresaId
              AND (:Anio IS NULL OR anio = :Anio)
            ORDER BY anio DESC, mes DESC";
        var list = await conn.QueryAsync<PeriodoDto>(sql, new { EmpresaId = empresaId, Anio = anio });
        return list.ToList();
    }

    public async Task<PeriodoDto?> ObtenerPeriodoAsync(int periodoId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<PeriodoDto>(@"
            SELECT periodo_id, anio, mes, tipo_periodo,
                   fecha_inicio, fecha_fin, fecha_pago, estado
            FROM periodos_nomina WHERE periodo_id = :Id", new { Id = periodoId });
    }

    public async Task<int> CalcularNominaAsync(int periodoId)
    {
        using var conn = _db.CreateConnection();
        var p = new DynamicParameters();
        p.Add("p_periodo_id", periodoId);
        p.Add("p_total_empleados", dbType: System.Data.DbType.Int32, direction: System.Data.ParameterDirection.Output);
        await conn.ExecuteAsync("SP_CALCULAR_NOMINA", p, commandType: System.Data.CommandType.StoredProcedure);
        return p.Get<int>("p_total_empleados");
    }

    public async Task<List<NominaResumenDto>> ObtenerResumenAsync(int periodoId)
    {
        using var conn = _db.CreateConnection();
        var rows = await conn.QueryAsync<NominaResumenDto>(@"
            SELECT p.periodo_id,
                   p.anio || '-' || LPAD(p.mes,2,'0') || ' ' || p.tipo_periodo AS periodo,
                   p.estado,
                   COUNT(ne.nomina_enc_id) AS total_empleados,
                   SUM(ne.total_ingresos)    AS total_ingresos,
                   SUM(ne.total_deducciones) AS total_deducciones,
                   SUM(ne.salario_neto)      AS total_neto
            FROM periodos_nomina p
            LEFT JOIN nomina_encabezado ne ON ne.periodo_id = p.periodo_id
            WHERE p.periodo_id = :Id
            GROUP BY p.periodo_id, p.anio, p.mes, p.tipo_periodo, p.estado",
            new { Id = periodoId });
        return rows.ToList();
    }

    public async Task<ReciboPagoDto?> ObtenerReciboAsync(int nominaEncId)
    {
        using var conn = _db.CreateConnection();

        var enc = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
            SELECT ne.nomina_enc_id, ne.salario_base, ne.total_ingresos,
                   ne.total_deducciones, ne.salario_neto,
                   e.codigo_empleado,
                   e.primer_nombre || ' ' || NVL(e.segundo_nombre,'') || ' ' || e.primer_apellido AS nombre_empleado,
                   d.nombre AS departamento,
                   p.anio || '-' || LPAD(p.mes,2,'0') || ' ' || p.tipo_periodo AS periodo
            FROM nomina_encabezado ne
            JOIN empleados e ON e.empleado_id = ne.empleado_id
            JOIN periodos_nomina p ON p.periodo_id = ne.periodo_id
            LEFT JOIN departamentos d ON d.departamento_id = e.departamento_id
            WHERE ne.nomina_enc_id = :Id", new { Id = nominaEncId });

        if (enc == null) return null;

        var detalles = (await conn.QueryAsync<NominaDetalle>(@"
            SELECT nomina_det_id, nomina_enc_id, tipo_movimiento, concepto, monto, es_calculado, referencia
            FROM nomina_detalle WHERE nomina_enc_id = :Id ORDER BY tipo_movimiento DESC, nomina_det_id",
            new { Id = nominaEncId })).ToList();

        return new ReciboPagoDto(
            (int)enc.NOMINA_ENC_ID,
            (string)enc.CODIGO_EMPLEADO,
            (string)enc.NOMBRE_EMPLEADO,
            enc.DEPARTAMENTO as string,
            (string)enc.PERIODO,
            (decimal)enc.SALARIO_BASE,
            detalles.Where(d => d.TipoMovimiento == "I").Select(d => new LineaReciboDto(d.Concepto, d.Monto, d.Referencia)).ToList(),
            detalles.Where(d => d.TipoMovimiento == "D").Select(d => new LineaReciboDto(d.Concepto, d.Monto, d.Referencia)).ToList(),
            (decimal)enc.TOTAL_INGRESOS,
            (decimal)enc.TOTAL_DEDUCCIONES,
            (decimal)enc.SALARIO_NETO);
    }

    public async Task<bool> AprobarPeriodoAsync(int periodoId, string usuarioAprobador)
    {
        using var conn = _db.CreateConnection();
        // NOTA: el bind parameter NO puede llamarse :User porque colisiona con la
        // funcion reservada USER de Oracle (causa ORA-01745).
        var rows = await conn.ExecuteAsync(@"
            UPDATE periodos_nomina
            SET estado = 'APROBADO', aprobado_por = :UsuarioAprob, aprobado_en = SYSTIMESTAMP
            WHERE periodo_id = :Id AND estado = 'CALCULADO'",
            new { Id = periodoId, UsuarioAprob = usuarioAprobador });

        if (rows > 0)
        {
            await conn.ExecuteAsync(
                "UPDATE nomina_encabezado SET estado = 'APROBADO' WHERE periodo_id = :Id",
                new { Id = periodoId });
        }
        return rows > 0;
    }

    public async Task<List<dynamic>> ListarBoletasEmpleadoAsync(int empleadoId)
    {
        using var conn = _db.CreateConnection();
        var rows = await conn.QueryAsync(@"
            SELECT ne.nomina_enc_id    AS NOMINA_ENC_ID,
                   p.anio              AS ANIO,
                   p.mes               AS MES,
                   p.tipo_periodo      AS TIPO_PERIODO,
                   p.fecha_inicio      AS FECHA_INICIO,
                   p.fecha_fin         AS FECHA_FIN,
                   p.fecha_pago        AS FECHA_PAGO,
                   ne.salario_base     AS SALARIO_BASE,
                   ne.total_ingresos   AS TOTAL_INGRESOS,
                   ne.total_deducciones AS TOTAL_DEDUCCIONES,
                   ne.salario_neto     AS SALARIO_NETO,
                   ne.estado           AS ESTADO
              FROM nomina_encabezado ne
              JOIN periodos_nomina p ON p.periodo_id = ne.periodo_id
             WHERE ne.empleado_id = :Id
             ORDER BY p.anio DESC, p.mes DESC",
            new { Id = empleadoId });
        return rows.ToList();
    }
}

public class ReporteRepository : IReporteRepository
{
    private readonly DapperContext _db;
    public ReporteRepository(DapperContext db) => _db = db;

    public async Task<List<dynamic>> ObtenerNominaMensualAsync(int anio, int mes, string tipoPeriodo)
    {
        using var conn = _db.CreateConnection();
        var rows = await conn.QueryAsync(@"
            SELECT codigo_empleado, nombre_empleado, departamento, puesto,
                   salario_base, bonificacion, igss, isr, descuento_prestamos,
                   total_ingresos, total_deducciones, salario_neto
            FROM vw_pbi_nomina_mensual
            WHERE anio = :Anio AND mes = :Mes AND tipo_periodo = :TipoPeriodo
            ORDER BY codigo_empleado",
            new { Anio = anio, Mes = mes, TipoPeriodo = tipoPeriodo });
        return rows.ToList();
    }

    public async Task<List<dynamic>> ObtenerSerieMensualAsync(int empresaId, int anio, string tipoPeriodo)
    {
        using var conn = _db.CreateConnection();
        // Devuelve 12 filas (una por mes); meses sin nomina vienen en 0.
        var rows = await conn.QueryAsync(@"
            WITH meses AS (
                SELECT LEVEL AS mes FROM dual CONNECT BY LEVEL <= 12
            )
            SELECT m.mes AS MES,
                   NVL(SUM(ne.total_ingresos), 0)    AS TOTAL_INGRESOS,
                   NVL(SUM(ne.total_deducciones), 0) AS TOTAL_DEDUCCIONES,
                   NVL(SUM(ne.salario_neto), 0)      AS SALARIO_NETO,
                   NVL(COUNT(DISTINCT ne.empleado_id), 0) AS EMPLEADOS
              FROM meses m
              LEFT JOIN periodos_nomina   p  ON p.mes = m.mes AND p.anio = :Anio
                                            AND p.tipo_periodo = :TipoPeriodo
                                            AND p.empresa_id = :EmpresaId
              LEFT JOIN nomina_encabezado ne ON ne.periodo_id = p.periodo_id
             GROUP BY m.mes
             ORDER BY m.mes",
            new { EmpresaId = empresaId, Anio = anio, TipoPeriodo = tipoPeriodo });
        return rows.ToList();
    }

    public async Task<List<dynamic>> ObtenerResumenMensualAsync(int empresaId, int anio)
    {
        using var conn = _db.CreateConnection();
        var rows = await conn.QueryAsync(@"
            SELECT anio, mes, nombre_mes, tipo_periodo, total_empleados,
                   suma_ingresos, suma_deducciones, suma_neto, promedio_neto
            FROM vw_pbi_resumen_mensual
            WHERE empresa_id = :E AND anio = :A
            ORDER BY mes",
            new { E = empresaId, A = anio });
        return rows.ToList();
    }

    public async Task<List<dynamic>> ObtenerHistoricoEmpleadoAsync(int empleadoId)
    {
        using var conn = _db.CreateConnection();
        var rows = await conn.QueryAsync(@"
            SELECT anio, mes, fecha_periodo, salario_base, total_ingresos,
                   total_deducciones, salario_neto
            FROM vw_pbi_historico_empleado
            WHERE empleado_id = :Id
            ORDER BY anio, mes",
            new { Id = empleadoId });
        return rows.ToList();
    }

    public async Task<DashboardKpisDto> ObtenerKpisDashboardAsync(int empresaId)
    {
        using var conn = _db.CreateConnection();
        var dto = await conn.QueryFirstOrDefaultAsync<DashboardKpisDto>(@"
            SELECT
                (SELECT COUNT(*) FROM empleados WHERE empresa_id = :E AND estado = 'ACTIVO') AS total_empleados_activos,
                (SELECT COUNT(*) FROM empleados WHERE empresa_id = :E AND estado = 'BAJA')   AS total_empleados_baja,
                NVL((SELECT suma_neto FROM vw_pbi_resumen_mensual
                      WHERE empresa_id = :E
                      ORDER BY anio DESC, mes DESC FETCH FIRST 1 ROWS ONLY), 0) AS nomina_ultimo_mes,
                (SELECT COUNT(*) FROM periodos_nomina
                  WHERE empresa_id = :E AND estado = 'APROBADO'
                    AND anio = EXTRACT(YEAR FROM SYSDATE)) AS periodos_aprobados_anio,
                NVL((SELECT SUM(saldo_pendiente) FROM prestamos
                      WHERE estado = 'VIGENTE'), 0) AS saldo_prestamos_vigentes
            FROM dual",
            new { E = empresaId });
        return dto ?? new DashboardKpisDto();
    }
}

public class CatalogoRepository : ICatalogoRepository
{
    private readonly DapperContext _db;
    public CatalogoRepository(DapperContext db) => _db = db;

    public async Task<List<Departamento>> ListarDepartamentosAsync(int empresaId)
    {
        using var conn = _db.CreateConnection();
        var rows = await conn.QueryAsync<Departamento>(
            "SELECT departamento_id, empresa_id, nombre, codigo, activo FROM departamentos WHERE empresa_id = :E AND activo = 1 ORDER BY nombre",
            new { E = empresaId });
        return rows.ToList();
    }

    public async Task<List<Puesto>> ListarPuestosAsync(int empresaId)
    {
        using var conn = _db.CreateConnection();
        var rows = await conn.QueryAsync<Puesto>(
            "SELECT puesto_id, empresa_id, nombre, codigo, salario_minimo, salario_maximo, activo FROM puestos WHERE empresa_id = :E AND activo = 1 ORDER BY nombre",
            new { E = empresaId });
        return rows.ToList();
    }
}

public class AuditoriaRepository : IAuditoriaRepository
{
    private readonly DapperContext _db;
    public AuditoriaRepository(DapperContext db) => _db = db;

    public async Task RegistrarAsync(string usuario, string accion, string? tabla, int? registroId,
                                     string? valorAnterior, string? valorNuevo, string? ip)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(@"
            INSERT INTO auditoria (usuario, accion, tabla, registro_id, valor_anterior, valor_nuevo, ip)
            VALUES (:Usuario, :Accion, :Tabla, :RegistroId, :ValorAnterior, :ValorNuevo, :Ip)",
            new { Usuario = usuario, Accion = accion, Tabla = tabla, RegistroId = registroId,
                  ValorAnterior = valorAnterior, ValorNuevo = valorNuevo, Ip = ip });
    }

    public async Task<(List<Auditoria> Items, int Total)> ListarAsync(int page, int pageSize,
        string? usuario, string? accion, DateTime? desde, DateTime? hasta)
    {
        using var conn = _db.CreateConnection();
        var where = "WHERE 1=1";
        if (!string.IsNullOrEmpty(usuario)) where += " AND LOWER(usuario) = LOWER(:Usuario)";
        if (!string.IsNullOrEmpty(accion))  where += " AND accion = :Accion";
        if (desde.HasValue) where += " AND fecha >= :Desde";
        if (hasta.HasValue) where += " AND fecha <= :Hasta";

        var p = new { Usuario = usuario, Accion = accion, Desde = desde, Hasta = hasta,
                      skipRows = (page - 1) * pageSize, pageSize };

        var total = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM auditoria {where}", p);
        var items = (await conn.QueryAsync<Auditoria>(
            $"SELECT auditoria_id, usuario, accion, tabla, registro_id, valor_anterior, valor_nuevo, ip, fecha FROM auditoria {where} ORDER BY fecha DESC OFFSET :skipRows ROWS FETCH NEXT :pageSize ROWS ONLY", p)).ToList();
        return (items, total);
    }
}

public class VacacionRepository : IVacacionRepository
{
    private readonly DapperContext _db;
    public VacacionRepository(DapperContext db) => _db = db;

    public async Task<List<VacacionDto>> ListarAsync(int? empleadoId, string? estado)
    {
        using var conn = _db.CreateConnection();
        var where = " WHERE 1=1";
        if (empleadoId.HasValue) where += " AND v.empleado_id = :EmpleadoId";
        if (!string.IsNullOrEmpty(estado)) where += " AND v.estado = :Estado";

        var sql = @$"
            SELECT v.vacacion_id     AS VacacionId,
                   v.empleado_id     AS EmpleadoId,
                   e.codigo_empleado AS CodigoEmpleado,
                   e.primer_nombre || ' ' || e.primer_apellido AS NombreEmpleado,
                   v.fecha_inicio    AS FechaInicio,
                   v.fecha_fin       AS FechaFin,
                   v.dias            AS Dias,
                   v.motivo          AS Motivo,
                   v.estado          AS Estado,
                   v.solicitado_por  AS SolicitadoPor,
                   v.solicitado_en   AS SolicitadoEn,
                   v.aprobado_por    AS AprobadoPor,
                   v.aprobado_en     AS AprobadoEn,
                   v.observaciones   AS Observaciones
              FROM vacaciones v
              JOIN empleados  e ON e.empleado_id = v.empleado_id
              {where}
             ORDER BY v.fecha_inicio DESC";
        var rows = await conn.QueryAsync<VacacionDto>(sql, new { EmpleadoId = empleadoId, Estado = estado });
        return rows.ToList();
    }

    public async Task<List<VacacionSaldoDto>> ListarSaldosAsync()
    {
        using var conn = _db.CreateConnection();
        var rows = await conn.QueryAsync<VacacionSaldoDto>(@"
            SELECT empleado_id            AS EmpleadoId,
                   codigo_empleado        AS CodigoEmpleado,
                   nombre                 AS Nombre,
                   fecha_inicio_contrato  AS FechaInicioContrato,
                   anios_trabajados       AS AniosTrabajados,
                   dias_acumulados        AS DiasAcumulados,
                   dias_gozados           AS DiasGozados,
                   dias_pendientes        AS DiasPendientes,
                   dias_en_solicitud      AS DiasEnSolicitud
              FROM vw_vacaciones_saldo
             ORDER BY codigo_empleado");
        return rows.ToList();
    }

    public async Task<VacacionSaldoDto?> ObtenerSaldoAsync(int empleadoId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<VacacionSaldoDto>(@"
            SELECT empleado_id            AS EmpleadoId,
                   codigo_empleado        AS CodigoEmpleado,
                   nombre                 AS Nombre,
                   fecha_inicio_contrato  AS FechaInicioContrato,
                   anios_trabajados       AS AniosTrabajados,
                   dias_acumulados        AS DiasAcumulados,
                   dias_gozados           AS DiasGozados,
                   dias_pendientes        AS DiasPendientes,
                   dias_en_solicitud      AS DiasEnSolicitud
              FROM vw_vacaciones_saldo
             WHERE empleado_id = :Id", new { Id = empleadoId });
    }

    public async Task<int> CrearAsync(CrearVacacionRequest req, string usuario)
    {
        using var conn = _db.CreateConnection();
        var dias = (req.FechaFin.Date - req.FechaInicio.Date).Days + 1;
        if (dias <= 0) throw new InvalidOperationException("La fecha fin debe ser posterior a la fecha inicio.");

        await conn.ExecuteAsync(@"
            INSERT INTO vacaciones (empleado_id, fecha_inicio, fecha_fin, dias, motivo, estado, solicitado_por)
            VALUES (:EmpleadoId, :FechaInicio, :FechaFin, :Dias, :Motivo, 'SOLICITADA', :Usuario)",
            new { req.EmpleadoId, req.FechaInicio, req.FechaFin, Dias = dias, req.Motivo, Usuario = usuario });

        return await conn.ExecuteScalarAsync<int>(@"
            SELECT MAX(vacacion_id) FROM vacaciones
             WHERE empleado_id = :EmpleadoId AND solicitado_por = :Usuario",
            new { req.EmpleadoId, Usuario = usuario });
    }

    public async Task<bool> CambiarEstadoAsync(int vacacionId, string estado, string? observaciones, string usuario)
    {
        using var conn = _db.CreateConnection();
        var rows = await conn.ExecuteAsync(@"
            UPDATE vacaciones
               SET estado        = :Estado,
                   observaciones = NVL(:Obs, observaciones),
                   aprobado_por  = CASE WHEN :Estado IN ('APROBADA','RECHAZADA') THEN :Usuario ELSE aprobado_por END,
                   aprobado_en   = CASE WHEN :Estado IN ('APROBADA','RECHAZADA') THEN SYSTIMESTAMP ELSE aprobado_en END
             WHERE vacacion_id = :Id",
            new { Id = vacacionId, Estado = estado, Obs = observaciones, Usuario = usuario });
        return rows > 0;
    }
}

public class LiquidacionRepository : ILiquidacionRepository
{
    private readonly DapperContext _db;
    public LiquidacionRepository(DapperContext db) => _db = db;

    public async Task<int> CrearAsync(LiquidacionDto d, string usuarioActual)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(@"
            INSERT INTO liquidaciones (
                empleado_id, fecha_inicio_contrato, fecha_baja, motivo, motivo_detalle,
                salario_base, salario_diario, anios_servicio, dias_servicio_total,
                indemnizacion, bono14_proporcional, aguinaldo_proporcional,
                vacaciones_no_gozadas, dias_vacaciones_pend,
                otros_pagos, descuentos, total,
                estado, calculado_por, observaciones)
            VALUES (
                :EmpleadoId, :FechaInicioContrato, :FechaBaja, :Motivo, :MotivoDetalle,
                :SalarioBase, :SalarioDiario, :AniosServicio, :DiasServicioTotal,
                :Indemnizacion, :Bono14Proporcional, :AguinaldoProporcional,
                :VacacionesNoGozadas, :DiasVacacionesPend,
                :OtrosPagos, :Descuentos, :Total,
                'CALCULADA', :CalculadoPor, :Observaciones)",
            new {
                d.EmpleadoId, d.FechaInicioContrato, d.FechaBaja, d.Motivo, d.MotivoDetalle,
                d.SalarioBase, d.SalarioDiario, d.AniosServicio, d.DiasServicioTotal,
                d.Indemnizacion, d.Bono14Proporcional, d.AguinaldoProporcional,
                d.VacacionesNoGozadas, d.DiasVacacionesPend,
                d.OtrosPagos, d.Descuentos, d.Total,
                CalculadoPor = usuarioActual,
                d.Observaciones
            });

        return await conn.ExecuteScalarAsync<int>(
            "SELECT MAX(liquidacion_id) FROM liquidaciones WHERE empleado_id = :EmpleadoId",
            new { d.EmpleadoId });
    }

    public async Task<LiquidacionDto?> ObtenerAsync(int liquidacionId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<LiquidacionDto>(@"
            SELECT l.liquidacion_id        AS LiquidacionId,
                   l.empleado_id            AS EmpleadoId,
                   e.codigo_empleado        AS CodigoEmpleado,
                   e.primer_nombre || ' ' || e.primer_apellido AS NombreEmpleado,
                   l.fecha_inicio_contrato  AS FechaInicioContrato,
                   l.fecha_baja             AS FechaBaja,
                   l.motivo                 AS Motivo,
                   l.motivo_detalle         AS MotivoDetalle,
                   l.salario_base           AS SalarioBase,
                   l.salario_diario         AS SalarioDiario,
                   l.anios_servicio         AS AniosServicio,
                   l.dias_servicio_total    AS DiasServicioTotal,
                   l.indemnizacion          AS Indemnizacion,
                   l.bono14_proporcional    AS Bono14Proporcional,
                   l.aguinaldo_proporcional AS AguinaldoProporcional,
                   l.vacaciones_no_gozadas  AS VacacionesNoGozadas,
                   l.dias_vacaciones_pend   AS DiasVacacionesPend,
                   l.otros_pagos            AS OtrosPagos,
                   l.descuentos             AS Descuentos,
                   l.total                  AS Total,
                   l.estado                 AS Estado,
                   l.calculado_por          AS CalculadoPor,
                   l.calculado_en           AS CalculadoEn,
                   l.observaciones          AS Observaciones
              FROM liquidaciones l
              JOIN empleados e ON e.empleado_id = l.empleado_id
             WHERE l.liquidacion_id = :Id",
            new { Id = liquidacionId });
    }

    public async Task<List<LiquidacionDto>> ListarPorEmpleadoAsync(int empleadoId)
    {
        using var conn = _db.CreateConnection();
        var rows = await conn.QueryAsync<LiquidacionDto>(@"
            SELECT l.liquidacion_id        AS LiquidacionId,
                   l.empleado_id            AS EmpleadoId,
                   l.fecha_inicio_contrato  AS FechaInicioContrato,
                   l.fecha_baja             AS FechaBaja,
                   l.motivo                 AS Motivo,
                   l.total                  AS Total,
                   l.estado                 AS Estado,
                   l.calculado_en           AS CalculadoEn
              FROM liquidaciones l
             WHERE l.empleado_id = :EmpleadoId
             ORDER BY l.calculado_en DESC",
            new { EmpleadoId = empleadoId });
        return rows.ToList();
    }
}
