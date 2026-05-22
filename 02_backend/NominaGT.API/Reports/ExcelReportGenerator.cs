using ClosedXML.Excel;
using NominaGT.API.Helpers;

namespace NominaGT.API.Reports;

/// <summary>
/// Contexto enriquecido para generar reportes Excel/PDF.
/// Permite mostrar tendencias y comparativos, no solo el mes "raw".
/// </summary>
public class PlanillaContext
{
    public int Anio { get; set; }
    public int Mes  { get; set; }
    public string TipoPeriodo { get; set; } = "MENSUAL";
    public string EmpresaNombre { get; set; } = "NominaGT v4";
    public IEnumerable<dynamic> Rows         { get; set; } = Array.Empty<dynamic>();
    public IEnumerable<dynamic> RowsPrevMes  { get; set; } = Array.Empty<dynamic>();
    public IEnumerable<dynamic> SerieAnual   { get; set; } = Array.Empty<dynamic>();
}

/// <summary>
/// Genera reportes Excel formateados con ClosedXML.
/// Diseño tipo "dashboard ejecutivo" estilo Plecto:
///   - Tema oscuro slate
///   - KPI cards grandes con flecha de tendencia vs mes anterior
///   - Sparklines anuales
///   - Data bars, color scales e icon sets en las tablas
///   - Hipervínculos entre hojas (botones de navegación)
/// </summary>
public class ExcelReportGenerator
{
    private static readonly string[] MESES = {
        "Enero","Febrero","Marzo","Abril","Mayo","Junio",
        "Julio","Agosto","Septiembre","Octubre","Noviembre","Diciembre"
    };
    private static readonly string[] MESES_ABBR = {
        "Ene","Feb","Mar","Abr","May","Jun","Jul","Ago","Sep","Oct","Nov","Dic"
    };

    // ─── Paleta Plecto (slate dark) ───
    private static readonly XLColor BgDark      = XLColor.FromHtml("#0F172A"); // fondo principal (slate-900)
    private static readonly XLColor BgCard      = XLColor.FromHtml("#1E293B"); // tarjetas (slate-800)
    private static readonly XLColor BgCardHover = XLColor.FromHtml("#334155"); // hover (slate-700)
    private static readonly XLColor TextWhite   = XLColor.FromHtml("#F8FAFC"); // slate-50
    private static readonly XLColor TextMuted   = XLColor.FromHtml("#94A3B8"); // slate-400
    private static readonly XLColor TextDim     = XLColor.FromHtml("#64748B"); // slate-500
    private static readonly XLColor Border      = XLColor.FromHtml("#334155"); // slate-700

    // Acentos vivos
    private static readonly XLColor Emerald     = XLColor.FromHtml("#10B981");
    private static readonly XLColor EmeraldBg   = XLColor.FromHtml("#064E3B");
    private static readonly XLColor Amber       = XLColor.FromHtml("#F59E0B");
    private static readonly XLColor AmberBg     = XLColor.FromHtml("#78350F");
    private static readonly XLColor Red         = XLColor.FromHtml("#EF4444");
    private static readonly XLColor RedBg       = XLColor.FromHtml("#7F1D1D");
    private static readonly XLColor Blue        = XLColor.FromHtml("#3B82F6");
    private static readonly XLColor BlueBg      = XLColor.FromHtml("#1E3A8A");
    private static readonly XLColor Violet      = XLColor.FromHtml("#8B5CF6");
    private static readonly XLColor VioletBg    = XLColor.FromHtml("#4C1D95");

    // ─── Helpers de extracción de DapperRow ───
    private static decimal D(IDictionary<string, object> r, string key)
        => r.TryGetValue(key, out var v) && v != null ? Convert.ToDecimal(v) : 0m;
    private static string S(IDictionary<string, object> r, string key)
        => r.TryGetValue(key, out var v) && v != null ? Convert.ToString(v) ?? "" : "";
    private static int I(IDictionary<string, object> r, string key)
        => r.TryGetValue(key, out var v) && v != null ? Convert.ToInt32(v) : 0;

    /// <summary>Backwards-compat: convierte la firma vieja a la nueva.</summary>
    public byte[] GenerarPlanillaMensual(IEnumerable<dynamic> rows, int anio, int mes, string tipoPeriodo)
        => GenerarPlanillaMensual(new PlanillaContext { Anio = anio, Mes = mes, TipoPeriodo = tipoPeriodo, Rows = rows });

    /// <summary>
    /// Crea un link interno usando la formula nativa =HYPERLINK("#...", "texto")
    /// que SIEMPRE funciona, a diferencia de SetHyperlink que en ClosedXML 0.104
    /// a veces no genera el relationship interno correctamente.
    /// </summary>
    private static void SetSheetLink(IXLCell cell, string targetSheet, string targetCell, string text, XLColor color)
    {
        // Si el nombre de hoja tiene espacios necesita apostrofes
        var sheetRef = targetSheet.Contains(' ') ? $"'{targetSheet}'" : targetSheet;
        cell.FormulaA1 = $"HYPERLINK(\"#{sheetRef}!{targetCell}\",\"{text}\")";
        cell.Style.Font.Bold = true;
        cell.Style.Font.FontColor = color;
        cell.Style.Font.Underline = XLFontUnderlineValues.Single;
    }

    public byte[] GenerarPlanillaMensual(PlanillaContext ctx)
    {
        var lista     = ctx.Rows.Cast<IDictionary<string, object>>().ToList();
        var listaPrev = ctx.RowsPrevMes.Cast<IDictionary<string, object>>().ToList();
        var serie     = ctx.SerieAnual.Cast<IDictionary<string, object>>().ToList();
        var nombreMes = (ctx.Mes >= 1 && ctx.Mes <= 12) ? MESES[ctx.Mes - 1] : ctx.Mes.ToString();

        // Agregados mes actual
        var agg     = Agregar(lista);
        var aggPrev = Agregar(listaPrev);

        using var wb = new XLWorkbook();
        wb.Properties.Title  = $"Planilla {ctx.Anio}-{ctx.Mes:D2} ({ctx.TipoPeriodo})";
        wb.Properties.Author = ctx.EmpresaNombre;

        BuildHojaDashboard(wb, ctx, lista, nombreMes, agg, aggPrev, serie);
        BuildHojaDetalle(wb, lista);
        BuildHojaPorDepartamento(wb, lista, agg.TotalNeto);
        BuildHojaPorPuesto(wb, lista);
        BuildHojaCargasPatronales(wb, agg);
        BuildHojaTendencia(wb, serie, ctx.Anio);

        // Re-ordenar hojas (Dashboard primero)
        wb.Worksheet("Dashboard").Position = 1;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ─── Estructura de agregados ───
    private class Agregados
    {
        public int Empleados;
        public decimal TotalIngresos, TotalDeducciones, TotalNeto;
        public decimal TotalSalarioBase, TotalBonif, TotalIgss, TotalIsr, TotalPrestamos;
        public decimal Promedio, PctDeduccion;
        public decimal CargasPatronales, CostoEmpresa;
    }

    private static Agregados Agregar(List<IDictionary<string, object>> lista)
    {
        var a = new Agregados { Empleados = lista.Count };
        foreach (var r in lista)
        {
            a.TotalIngresos    += D(r, "TOTAL_INGRESOS");
            a.TotalDeducciones += D(r, "TOTAL_DEDUCCIONES");
            a.TotalNeto        += D(r, "SALARIO_NETO");
            a.TotalSalarioBase += D(r, "SALARIO_BASE");
            a.TotalBonif       += D(r, "BONIFICACION");
            a.TotalIgss        += D(r, "IGSS");
            a.TotalIsr         += D(r, "ISR");
            a.TotalPrestamos   += D(r, "DESCUENTO_PRESTAMOS");
        }
        a.Promedio         = a.Empleados == 0 ? 0 : a.TotalNeto / a.Empleados;
        a.PctDeduccion     = a.TotalIngresos == 0 ? 0 : (a.TotalDeducciones / a.TotalIngresos) * 100;
        a.CargasPatronales = a.TotalSalarioBase * (
            GuatemalaValidators.IgssCuotaPatronal +
            GuatemalaValidators.CuotaIrtraPatronal +
            GuatemalaValidators.CuotaIntecapPatronal);
        a.CostoEmpresa     = a.TotalIngresos + a.CargasPatronales;
        return a;
    }

    // ════════════════════════════════════════════════════════════════════
    // HOJA 1 — DASHBOARD (tema oscuro Plecto)
    // ════════════════════════════════════════════════════════════════════
    private static void BuildHojaDashboard(IXLWorkbook wb, PlanillaContext ctx,
        List<IDictionary<string, object>> lista, string nombreMes,
        Agregados a, Agregados prev, List<IDictionary<string, object>> serie)
    {
        var ws = wb.Worksheets.Add("Dashboard");
        ws.ShowGridLines = false;
        ws.TabColor = Amber;

        // Layout 14 columnas, padding lateral
        ws.Column(1).Width = 1.5;
        for (int c = 2; c <= 15; c++) ws.Column(c).Width = 11;
        ws.Column(16).Width = 1.5;

        // Fondo del dashboard: pintar un rango grande con BgDark
        var bgRange = ws.Range(1, 1, 50, 16);
        bgRange.Style.Fill.BackgroundColor = BgDark;

        // === Banner ===
        ws.Range(2, 2, 3, 15).Merge();
        ws.Cell(2, 2).Value = "NOMINAGT v4 · Planilla del periodo";
        ws.Cell(2, 2).Style.Font.Bold = true;
        ws.Cell(2, 2).Style.Font.FontSize = 22;
        ws.Cell(2, 2).Style.Font.FontColor = TextWhite;
        ws.Cell(2, 2).Style.Fill.BackgroundColor = BgDark;
        ws.Cell(2, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Row(2).Height = 30;
        ws.Row(3).Height = 20;

        ws.Range(4, 2, 4, 10).Merge();
        ws.Cell(4, 2).Value = $"{nombreMes} {ctx.Anio}  ·  Tipo: {ctx.TipoPeriodo}";
        ws.Cell(4, 2).Style.Font.FontColor = Amber;
        ws.Cell(4, 2).Style.Font.FontSize = 13;
        ws.Cell(4, 2).Style.Font.Bold = true;
        ws.Cell(4, 2).Style.Fill.BackgroundColor = BgDark;

        ws.Range(4, 11, 4, 15).Merge();
        ws.Cell(4, 11).Value = $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}";
        ws.Cell(4, 11).Style.Font.FontColor = TextMuted;
        ws.Cell(4, 11).Style.Font.FontSize = 10;
        ws.Cell(4, 11).Style.Fill.BackgroundColor = BgDark;
        ws.Cell(4, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        // === Navegacion (hipervinculos a otras hojas) ===
        var navHojas = new[] {
            ("Detalle", "📋 Detalle"),
            ("Por Departamento", "🏢 Departamento"),
            ("Por Puesto", "🎯 Puesto"),
            ("Cargas patronales", "💼 Cargas"),
            ("Tendencia", "📈 Tendencia"),
        };
        for (int i = 0; i < navHojas.Length; i++)
        {
            var col = 2 + i * 2;
            ws.Range(6, col, 6, col + 1).Merge();
            var cell = ws.Cell(6, col);
            // Fórmula HYPERLINK funciona en cualquier versión de Excel
            var sheetName = navHojas[i].Item1;
            var sheetRef = sheetName.Contains(' ') ? $"'{sheetName}'" : sheetName;
            cell.FormulaA1 = $"HYPERLINK(\"#{sheetRef}!A1\",\"{navHojas[i].Item2}\")";
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = TextWhite;
            cell.Style.Font.FontSize = 11;
            cell.Style.Fill.BackgroundColor = BgCard;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = Border;
        }
        ws.Row(6).Height = 24;

        // === KPI cards principales (fila 8-12) ===
        DrawPlectoKpi(ws, 8, 2,  "Empleados",        a.Empleados.ToString("N0"), Emerald,
                      Delta(a.Empleados, prev.Empleados));
        DrawPlectoKpi(ws, 8, 5,  "Total Ingresos",   $"Q {a.TotalIngresos:N2}",    Amber,
                      Delta(a.TotalIngresos, prev.TotalIngresos));
        DrawPlectoKpi(ws, 8, 8,  "Deducciones",      $"Q {a.TotalDeducciones:N2}", Red,
                      Delta(a.TotalDeducciones, prev.TotalDeducciones), invertirSigno: true);
        DrawPlectoKpi(ws, 8, 11, "Neto a Pagar",     $"Q {a.TotalNeto:N2}",        Amber,
                      Delta(a.TotalNeto, prev.TotalNeto), big: true);

        // === KPI secundarios (fila 14-18) ===
        DrawPlectoKpi(ws, 14, 2,  "Salario Promedio", $"Q {a.Promedio:N2}",     Blue,
                      Delta(a.Promedio, prev.Promedio));
        DrawPlectoKpi(ws, 14, 5,  "% Deducción",      $"{a.PctDeduccion:N2} %", Violet,
                      Delta(a.PctDeduccion, prev.PctDeduccion), invertirSigno: true);
        DrawPlectoKpi(ws, 14, 8,  "IGSS Retenido",    $"Q {a.TotalIgss:N2}",    Red,
                      Delta(a.TotalIgss, prev.TotalIgss), invertirSigno: true);
        DrawPlectoKpi(ws, 14, 11, "ISR Retenido",     $"Q {a.TotalIsr:N2}",     Red,
                      Delta(a.TotalIsr, prev.TotalIsr), invertirSigno: true);

        // === Sparkline anual ===
        int rSpark = 20;
        ws.Range(rSpark, 2, rSpark, 15).Merge();
        ws.Cell(rSpark, 2).Value = "  TENDENCIA DEL AÑO";
        ws.Cell(rSpark, 2).Style.Font.Bold = true;
        ws.Cell(rSpark, 2).Style.Font.FontColor = TextMuted;
        ws.Cell(rSpark, 2).Style.Font.FontSize = 11;
        ws.Cell(rSpark, 2).Style.Fill.BackgroundColor = BgDark;
        ws.Row(rSpark).Height = 22;

        // Cuadro de sparkline (data en celdas ocultas + sparkline real)
        int rDataInicio = rSpark + 1;
        int rDataFin    = rDataInicio; // 1 fila con 12 columnas

        // Headers Ene..Dic
        for (int m = 1; m <= 12; m++)
        {
            var col = 2 + (m - 1);
            ws.Cell(rDataInicio + 1, col).Value = MESES_ABBR[m - 1];
            ws.Cell(rDataInicio + 1, col).Style.Font.FontColor = TextMuted;
            ws.Cell(rDataInicio + 1, col).Style.Font.FontSize = 9;
            ws.Cell(rDataInicio + 1, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(rDataInicio + 1, col).Style.Fill.BackgroundColor = BgDark;
        }

        // Datos por mes
        var serieDict = serie.ToDictionary(r => I(r, "MES"), r => D(r, "SALARIO_NETO"));
        for (int m = 1; m <= 12; m++)
        {
            var col = 2 + (m - 1);
            var v = serieDict.TryGetValue(m, out var x) ? x : 0m;
            var cell = ws.Cell(rDataInicio, col);
            cell.Value = v;
            cell.Style.NumberFormat.Format = "Q #,##0";
            cell.Style.Font.FontColor = m == ctx.Mes ? Amber : TextWhite;
            cell.Style.Font.Bold = (m == ctx.Mes);
            cell.Style.Font.FontSize = 9;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Fill.BackgroundColor = BgCard;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = Border;
        }
        ws.Row(rDataInicio).Height = 22;
        ws.Row(rDataInicio + 1).Height = 14;

        // Sparkline real (ClosedXML soporta sparklines desde 0.95)
        try
        {
            var sparkRange = ws.Range(rDataInicio, 2, rDataInicio, 13);
            ws.SparklineGroups.Add(ws.Cell(rDataInicio + 2, 2).Address.ToString(), sparkRange.RangeAddress.ToString());
        }
        catch { /* sparkline opcional */ }

        // === Tabla de distribución por departamento ===
        int rDep = rDataInicio + 4;
        ws.Range(rDep, 2, rDep, 8).Merge();
        ws.Cell(rDep, 2).Value = "  POR DEPARTAMENTO";
        ws.Cell(rDep, 2).Style.Font.Bold = true;
        ws.Cell(rDep, 2).Style.Font.FontColor = TextMuted;
        ws.Cell(rDep, 2).Style.Font.FontSize = 11;
        ws.Cell(rDep, 2).Style.Fill.BackgroundColor = BgDark;

        ws.Range(rDep, 9, rDep, 15).Merge();
        ws.Cell(rDep, 9).Value = "  TOP 5 SALARIOS";
        ws.Cell(rDep, 9).Style.Font.Bold = true;
        ws.Cell(rDep, 9).Style.Font.FontColor = TextMuted;
        ws.Cell(rDep, 9).Style.Font.FontSize = 11;
        ws.Cell(rDep, 9).Style.Fill.BackgroundColor = BgDark;
        ws.Row(rDep).Height = 22;

        var porDep = lista
            .GroupBy(r => string.IsNullOrEmpty(S(r, "DEPARTAMENTO")) ? "Sin depto" : S(r, "DEPARTAMENTO"))
            .Select(g => new { Nombre = g.Key, Total = g.Sum(x => D(x, "SALARIO_NETO")), Cantidad = g.Count() })
            .OrderByDescending(x => x.Total)
            .Take(6)
            .ToList();

        for (int i = 0; i < porDep.Count; i++)
        {
            int r = rDep + 1 + i;
            ws.Row(r).Height = 18;
            ws.Cell(r, 2).Value = porDep[i].Nombre;
            ws.Range(r, 2, r, 4).Merge();
            ws.Cell(r, 5).Value = porDep[i].Cantidad;
            ws.Cell(r, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(r, 5).Style.Font.FontColor = TextMuted;
            ws.Cell(r, 6).Value = porDep[i].Total;
            ws.Range(r, 6, r, 8).Merge();
            ws.Cell(r, 6).Style.NumberFormat.Format = "Q #,##0.00";
            ws.Cell(r, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(r, 6).Style.Font.Bold = true;

            for (int c = 2; c <= 8; c++)
            {
                ws.Cell(r, c).Style.Font.FontColor = TextWhite;
                ws.Cell(r, c).Style.Fill.BackgroundColor = BgCard;
                ws.Cell(r, c).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                ws.Cell(r, c).Style.Border.BottomBorderColor = Border;
            }
        }

        // Aplicamos data bars en la columna de totales del departamento
        if (porDep.Count > 0)
        {
            var rng = ws.Range(rDep + 1, 6, rDep + porDep.Count, 6);
            rng.AddConditionalFormat().DataBar(Amber, false).LowestValue().HighestValue();
        }

        // Top 5 salarios
        var top5 = lista
            .OrderByDescending(r => D(r, "SALARIO_NETO"))
            .Take(5)
            .Select((r, i) => new { Rank = i + 1, Nombre = S(r, "NOMBRE_EMPLEADO"), Total = D(r, "SALARIO_NETO") })
            .ToList();

        for (int i = 0; i < top5.Count; i++)
        {
            int r = rDep + 1 + i;
            ws.Cell(r, 9).Value = $"#{top5[i].Rank}";
            ws.Cell(r, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(r, 9).Style.Font.FontColor = (i == 0) ? Amber : TextMuted;
            ws.Cell(r, 9).Style.Font.Bold = true;
            ws.Cell(r, 10).Value = top5[i].Nombre;
            ws.Range(r, 10, r, 13).Merge();
            ws.Cell(r, 14).Value = top5[i].Total;
            ws.Range(r, 14, r, 15).Merge();
            ws.Cell(r, 14).Style.NumberFormat.Format = "Q #,##0";
            ws.Cell(r, 14).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(r, 14).Style.Font.Bold = true;

            for (int c = 9; c <= 15; c++)
            {
                ws.Cell(r, c).Style.Font.FontColor = TextWhite;
                ws.Cell(r, c).Style.Fill.BackgroundColor = BgCard;
                ws.Cell(r, c).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                ws.Cell(r, c).Style.Border.BottomBorderColor = Border;
            }
        }

        // Footer
        int rFoot = rDep + 8;
        ws.Range(rFoot, 2, rFoot, 15).Merge();
        ws.Cell(rFoot, 2).Value = "NominaGT v4 · Documento informativo, no es comprobante tributario.";
        ws.Cell(rFoot, 2).Style.Font.FontColor = TextDim;
        ws.Cell(rFoot, 2).Style.Font.Italic = true;
        ws.Cell(rFoot, 2).Style.Font.FontSize = 9;
        ws.Cell(rFoot, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Cell(rFoot, 2).Style.Fill.BackgroundColor = BgDark;

        ws.SheetView.FreezeRows(6);
    }

    // ─── KPI card estilo Plecto: label arriba, número gigante, flecha de tendencia ───
    private static void DrawPlectoKpi(IXLWorksheet ws, int row, int colStart, string label, string value,
        XLColor accent, (decimal pct, string flecha, XLColor color) delta, bool big = false, bool invertirSigno = false)
    {
        int spanCols = 3;
        int colEnd = colStart + spanCols - 1;

        // Si invertimos signo (deducciones: subir es malo), invertimos color
        var deltaColor = invertirSigno
            ? (delta.pct > 0 ? Red : (delta.pct < 0 ? Emerald : TextMuted))
            : (delta.pct > 0 ? Emerald : (delta.pct < 0 ? Red : TextMuted));

        // Fila 1: label
        ws.Range(row, colStart, row, colEnd).Merge();
        ws.Cell(row, colStart).Value = label;
        ws.Cell(row, colStart).Style.Font.Bold = true;
        ws.Cell(row, colStart).Style.Font.FontColor = TextMuted;
        ws.Cell(row, colStart).Style.Font.FontSize = 9;
        ws.Cell(row, colStart).Style.Fill.BackgroundColor = BgCard;
        ws.Cell(row, colStart).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Cell(row, colStart).Style.Border.LeftBorder = XLBorderStyleValues.Thin;
        ws.Cell(row, colStart).Style.Border.LeftBorderColor = Border;
        ws.Cell(row, colStart).Style.Border.TopBorder = XLBorderStyleValues.Thin;
        ws.Cell(row, colStart).Style.Border.TopBorderColor = Border;
        ws.Cell(row, colStart).Style.Border.RightBorder = XLBorderStyleValues.Thin;
        ws.Cell(row, colStart).Style.Border.RightBorderColor = Border;
        ws.Cell(row, colStart).Style.Alignment.Indent = 1;
        ws.Row(row).Height = 22;

        // Fila 2-3: valor gigante
        ws.Range(row + 1, colStart, row + 2, colEnd).Merge();
        ws.Cell(row + 1, colStart).Value = value;
        ws.Cell(row + 1, colStart).Style.Font.Bold = true;
        ws.Cell(row + 1, colStart).Style.Font.FontColor = accent;
        ws.Cell(row + 1, colStart).Style.Font.FontSize = big ? 26 : 22;
        ws.Cell(row + 1, colStart).Style.Fill.BackgroundColor = BgCard;
        ws.Cell(row + 1, colStart).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Cell(row + 1, colStart).Style.Alignment.Indent = 1;
        ws.Cell(row + 1, colStart).Style.Border.LeftBorder = XLBorderStyleValues.Thin;
        ws.Cell(row + 1, colStart).Style.Border.LeftBorderColor = Border;
        ws.Cell(row + 1, colStart).Style.Border.RightBorder = XLBorderStyleValues.Thin;
        ws.Cell(row + 1, colStart).Style.Border.RightBorderColor = Border;
        ws.Row(row + 1).Height = 26;
        ws.Row(row + 2).Height = 14;

        // Fila 4: delta + flecha
        ws.Range(row + 3, colStart, row + 3, colEnd).Merge();
        var dCell = ws.Cell(row + 3, colStart);
        dCell.Value = delta.pct == 0 ? "—" : $"{delta.flecha}  {Math.Abs(delta.pct):N2}% vs mes anterior";
        dCell.Style.Font.FontColor = deltaColor;
        dCell.Style.Font.FontSize = 9;
        dCell.Style.Font.Bold = true;
        dCell.Style.Fill.BackgroundColor = BgCard;
        dCell.Style.Alignment.Indent = 1;
        dCell.Style.Border.LeftBorder = XLBorderStyleValues.Thin;
        dCell.Style.Border.LeftBorderColor = Border;
        dCell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        dCell.Style.Border.BottomBorderColor = Border;
        dCell.Style.Border.RightBorder = XLBorderStyleValues.Thin;
        dCell.Style.Border.RightBorderColor = Border;
        ws.Row(row + 3).Height = 20;
    }

    private static (decimal pct, string flecha, XLColor color) Delta(decimal actual, decimal previo)
    {
        if (previo == 0) return (0, "—", TextMuted);
        var diff = actual - previo;
        var pct = (diff / Math.Abs(previo)) * 100;
        return pct > 0 ? (pct, "↑", Emerald)
             : pct < 0 ? (pct, "↓", Red)
             : (0, "—", TextMuted);
    }

    // ════════════════════════════════════════════════════════════════════
    // HOJA 2 — DETALLE con data bars, color scales, icon sets
    // ════════════════════════════════════════════════════════════════════
    private static void BuildHojaDetalle(IXLWorkbook wb, List<IDictionary<string, object>> lista)
    {
        var ws = wb.Worksheets.Add("Detalle");
        ws.TabColor = Blue;
        var headers = new[] { "Código", "Nombre", "Departamento", "Puesto",
            "Salario Base", "Bonificación", "IGSS", "ISR", "Préstamos",
            "Total Ingresos", "Total Deducciones", "% Deducción", "Salario Neto" };

        for (int i = 0; i < headers.Length; i++)
        {
            var c = ws.Cell(1, i + 1);
            c.Value = headers[i];
            c.Style.Font.Bold = true;
            c.Style.Font.FontColor = TextWhite;
            c.Style.Fill.BackgroundColor = BgDark;
            c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        ws.Row(1).Height = 24;

        int row = 2;
        foreach (var r in lista)
        {
            var ing = D(r, "TOTAL_INGRESOS");
            var ded = D(r, "TOTAL_DEDUCCIONES");
            ws.Cell(row, 1).Value  = S(r, "CODIGO_EMPLEADO");
            ws.Cell(row, 2).Value  = S(r, "NOMBRE_EMPLEADO");
            ws.Cell(row, 3).Value  = S(r, "DEPARTAMENTO");
            ws.Cell(row, 4).Value  = S(r, "PUESTO");
            ws.Cell(row, 5).Value  = D(r, "SALARIO_BASE");
            ws.Cell(row, 6).Value  = D(r, "BONIFICACION");
            ws.Cell(row, 7).Value  = D(r, "IGSS");
            ws.Cell(row, 8).Value  = D(r, "ISR");
            ws.Cell(row, 9).Value  = D(r, "DESCUENTO_PRESTAMOS");
            ws.Cell(row, 10).Value = ing;
            ws.Cell(row, 11).Value = ded;
            ws.Cell(row, 12).Value = ing == 0 ? 0d : (double)(ded / ing);
            ws.Cell(row, 13).Value = D(r, "SALARIO_NETO");
            row++;
        }

        // Formatos numericos
        for (int c = 5; c <= 13; c++)
        {
            if (c == 12) ws.Column(c).Style.NumberFormat.Format = "0.00%";
            else        ws.Column(c).Style.NumberFormat.Format = "Q #,##0.00";
        }
        ws.Column(13).Style.Font.Bold = true;

        if (lista.Count > 0)
        {
            int lastDataRow = row - 1;
            var dataRange = ws.Range(2, 1, lastDataRow, headers.Length);

            // === Conditional formatting interactivo ===
            // 1. Data bar en Salario Base (col 5)
            ws.Range(2, 5, lastDataRow, 5).AddConditionalFormat()
                .DataBar(Blue, false).LowestValue().HighestValue();

            // 2. Data bar en Total Ingresos (col 10)
            ws.Range(2, 10, lastDataRow, 10).AddConditionalFormat()
                .DataBar(Emerald, false).LowestValue().HighestValue();

            // 3. Data bar en Salario Neto (col 13)
            ws.Range(2, 13, lastDataRow, 13).AddConditionalFormat()
                .DataBar(Amber, false).LowestValue().HighestValue();

            // 4. Color scale en IGSS+ISR+Prestamos (cols 7,8,9) - rojo intenso = mucha deduccion
            ws.Range(2, 7, lastDataRow, 9).AddConditionalFormat()
                .ColorScale()
                .LowestValue(XLColor.FromHtml("#DCFCE7"))
                .Midpoint(XLCFContentType.Percent, "50", XLColor.FromHtml("#FED7AA"))
                .HighestValue(XLColor.FromHtml("#FECACA"));

            // 5. Icon set en % Deduccion (col 12): 3 flechas
            try
            {
                ws.Range(2, 12, lastDataRow, 12).AddConditionalFormat()
                    .IconSet(XLIconSetStyle.ThreeArrows);
            }
            catch { /* iconset opcional */ }

            // Filas alternas
            for (int i = 2; i <= lastDataRow; i++)
                if (i % 2 == 0)
                    ws.Range(i, 1, i, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");

            // Autofilter y panel congelado
            dataRange.SetAutoFilter();
            ws.SheetView.FreezeRows(1);

            // Fila de totales
            int totRow = row;
            ws.Cell(totRow, 1).Value = "TOTAL";
            ws.Range(totRow, 1, totRow, 4).Merge();
            for (int c = 5; c <= 13; c++)
                if (c != 12) ws.Cell(totRow, c).FormulaA1 = $"SUM({ws.Cell(2, c).Address}:{ws.Cell(lastDataRow, c).Address})";
            ws.Cell(totRow, 12).FormulaA1 = $"IFERROR({ws.Cell(totRow, 11).Address}/{ws.Cell(totRow, 10).Address},0)";
            ws.Range(totRow, 1, totRow, headers.Length).Style.Font.Bold = true;
            ws.Range(totRow, 1, totRow, headers.Length).Style.Fill.BackgroundColor = BgDark;
            ws.Range(totRow, 1, totRow, headers.Length).Style.Font.FontColor = TextWhite;
            ws.Row(totRow).Height = 24;
        }

        // Boton volver al Dashboard (formula HYPERLINK confiable)
        SetSheetLink(ws.Cell(row + 2, 1), "Dashboard", "A1", "← Volver al Dashboard", Blue);
        ws.Range(row + 2, 1, row + 2, 4).Merge();

        ws.Columns().AdjustToContents();
        // Ajustes finos: nombre largo
        ws.Column(2).Width = 28;
        ws.Column(3).Width = 22;
    }

    // ════════════════════════════════════════════════════════════════════
    // HOJA 3 — POR DEPARTAMENTO con data bars
    // ════════════════════════════════════════════════════════════════════
    private static void BuildHojaPorDepartamento(IXLWorkbook wb, List<IDictionary<string, object>> lista, decimal totalNeto)
    {
        var ws = wb.Worksheets.Add("Por Departamento");
        ws.TabColor = Emerald;
        string[] hdr = { "Departamento", "Empleados", "Salario base", "Total ingresos", "Total deducciones", "Total neto", "% del total" };
        for (int i = 0; i < hdr.Length; i++)
        {
            var c = ws.Cell(1, i + 1);
            c.Value = hdr[i];
            c.Style.Font.Bold = true;
            c.Style.Fill.BackgroundColor = BgDark;
            c.Style.Font.FontColor = TextWhite;
        }
        ws.Row(1).Height = 24;

        var grupos = lista
            .GroupBy(r => string.IsNullOrEmpty(S(r, "DEPARTAMENTO")) ? "Sin departamento" : S(r, "DEPARTAMENTO"))
            .Select(g => new {
                Depto       = g.Key,
                Cantidad    = g.Count(),
                Salario     = g.Sum(x => D(x, "SALARIO_BASE")),
                Ingresos    = g.Sum(x => D(x, "TOTAL_INGRESOS")),
                Deducciones = g.Sum(x => D(x, "TOTAL_DEDUCCIONES")),
                Neto        = g.Sum(x => D(x, "SALARIO_NETO"))
            })
            .OrderByDescending(x => x.Neto)
            .ToList();

        int r = 2;
        foreach (var g in grupos)
        {
            ws.Cell(r, 1).Value = g.Depto;
            ws.Cell(r, 2).Value = g.Cantidad;
            ws.Cell(r, 3).Value = g.Salario;
            ws.Cell(r, 4).Value = g.Ingresos;
            ws.Cell(r, 5).Value = g.Deducciones;
            ws.Cell(r, 6).Value = g.Neto;
            ws.Cell(r, 7).Value = totalNeto == 0 ? 0 : (double)(g.Neto / totalNeto);
            ws.Cell(r, 7).Style.NumberFormat.Format = "0.00%";
            r++;
        }
        for (int c = 3; c <= 6; c++)
            ws.Column(c).Style.NumberFormat.Format = "Q #,##0.00";

        if (grupos.Count > 0)
        {
            int last = r - 1;
            // Data bars en cada columna numerica
            ws.Range(2, 2, last, 2).AddConditionalFormat().DataBar(Blue, false).LowestValue().HighestValue();
            ws.Range(2, 6, last, 6).AddConditionalFormat().DataBar(Amber, false).LowestValue().HighestValue();
            ws.Range(2, 7, last, 7).AddConditionalFormat().DataBar(Emerald, false).LowestValue().HighestValue();
            ws.Range(1, 1, last, hdr.Length).SetAutoFilter();
        }

        SetSheetLink(ws.Cell(r + 2, 1), "Dashboard", "A1", "← Volver al Dashboard", Blue);

        ws.Columns().AdjustToContents();
    }

    // ════════════════════════════════════════════════════════════════════
    // HOJA 4 — POR PUESTO
    // ════════════════════════════════════════════════════════════════════
    private static void BuildHojaPorPuesto(IXLWorkbook wb, List<IDictionary<string, object>> lista)
    {
        var ws = wb.Worksheets.Add("Por Puesto");
        ws.TabColor = Violet;
        string[] hdr = { "Puesto", "Empleados", "Salario base prom.", "Salario neto prom.", "Total neto" };
        for (int i = 0; i < hdr.Length; i++)
        {
            var c = ws.Cell(1, i + 1);
            c.Value = hdr[i];
            c.Style.Font.Bold = true;
            c.Style.Fill.BackgroundColor = BgDark;
            c.Style.Font.FontColor = TextWhite;
        }
        ws.Row(1).Height = 24;

        var grupos = lista
            .GroupBy(r => string.IsNullOrEmpty(S(r, "PUESTO")) ? "Sin puesto" : S(r, "PUESTO"))
            .Select(g => new {
                Puesto    = g.Key,
                Cantidad  = g.Count(),
                SalProm   = g.Average(x => D(x, "SALARIO_BASE")),
                NetoProm  = g.Average(x => D(x, "SALARIO_NETO")),
                TotalNeto = g.Sum(x => D(x, "SALARIO_NETO"))
            })
            .OrderByDescending(x => x.TotalNeto)
            .ToList();

        int r = 2;
        foreach (var g in grupos)
        {
            ws.Cell(r, 1).Value = g.Puesto;
            ws.Cell(r, 2).Value = g.Cantidad;
            ws.Cell(r, 3).Value = g.SalProm;
            ws.Cell(r, 4).Value = g.NetoProm;
            ws.Cell(r, 5).Value = g.TotalNeto;
            r++;
        }
        for (int c = 3; c <= 5; c++) ws.Column(c).Style.NumberFormat.Format = "Q #,##0.00";

        if (grupos.Count > 0)
        {
            int last = r - 1;
            ws.Range(2, 3, last, 3).AddConditionalFormat().DataBar(Blue, false).LowestValue().HighestValue();
            ws.Range(2, 5, last, 5).AddConditionalFormat().DataBar(Violet, false).LowestValue().HighestValue();
        }

        SetSheetLink(ws.Cell(r + 2, 1), "Dashboard", "A1", "← Volver al Dashboard", Blue);

        ws.Columns().AdjustToContents();
    }

    // ════════════════════════════════════════════════════════════════════
    // HOJA 5 — CARGAS PATRONALES
    // ════════════════════════════════════════════════════════════════════
    private static void BuildHojaCargasPatronales(IXLWorkbook wb, Agregados a)
    {
        var ws = wb.Worksheets.Add("Cargas patronales");
        ws.TabColor = Red;
        ws.ShowGridLines = false;
        ws.Column(1).Width = 2;
        ws.Column(2).Width = 38;
        ws.Column(3).Width = 12;
        ws.Column(4).Width = 18;
        ws.Column(5).Width = 18;

        ws.Range(2, 2, 2, 5).Merge();
        ws.Cell(2, 2).Value = "CARGAS PATRONALES E IMPUESTOS";
        ws.Cell(2, 2).Style.Font.Bold = true;
        ws.Cell(2, 2).Style.Font.FontSize = 16;
        ws.Cell(2, 2).Style.Font.FontColor = TextWhite;
        ws.Cell(2, 2).Style.Fill.BackgroundColor = BgDark;
        ws.Cell(2, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Row(2).Height = 32;

        ws.Range(3, 2, 3, 5).Merge();
        ws.Cell(3, 2).Value = "Costo TOTAL del periodo para la empresa (sueldos + aportes patronales)";
        ws.Cell(3, 2).Style.Font.FontColor = TextMuted;
        ws.Cell(3, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        decimal igssPat   = a.TotalSalarioBase * GuatemalaValidators.IgssCuotaPatronal;
        decimal irtra     = a.TotalSalarioBase * GuatemalaValidators.CuotaIrtraPatronal;
        decimal intecap   = a.TotalSalarioBase * GuatemalaValidators.CuotaIntecapPatronal;

        int row = 5;
        var hdr = new[] { ("Concepto", 2), ("Tasa", 3), ("Base", 4), ("Monto", 5) };
        foreach (var (h, c) in hdr)
        {
            ws.Cell(row, c).Value = h;
            ws.Cell(row, c).Style.Font.Bold = true;
            ws.Cell(row, c).Style.Fill.BackgroundColor = BgDark;
            ws.Cell(row, c).Style.Font.FontColor = TextWhite;
        }
        ws.Row(row).Height = 24;

        var rows = new[] {
            ("IGSS patronal", GuatemalaValidators.IgssCuotaPatronal, a.TotalSalarioBase, igssPat),
            ("IRTRA",         GuatemalaValidators.CuotaIrtraPatronal, a.TotalSalarioBase, irtra),
            ("INTECAP",       GuatemalaValidators.CuotaIntecapPatronal, a.TotalSalarioBase, intecap),
        };
        row = 6;
        foreach (var x in rows)
        {
            ws.Cell(row, 2).Value = x.Item1;
            ws.Cell(row, 3).Value = (double)x.Item2;
            ws.Cell(row, 3).Style.NumberFormat.Format = "0.00%";
            ws.Cell(row, 4).Value = x.Item3;
            ws.Cell(row, 5).Value = x.Item4;
            ws.Cell(row, 4).Style.NumberFormat.Format = "Q #,##0.00";
            ws.Cell(row, 5).Style.NumberFormat.Format = "Q #,##0.00";
            row++;
        }

        // Data bar en columna Monto
        ws.Range(6, 5, 8, 5).AddConditionalFormat().DataBar(Red, false).LowestValue().HighestValue();

        ws.Cell(row, 2).Value = "TOTAL APORTES PATRONALES";
        ws.Cell(row, 5).Value = a.CargasPatronales;
        ws.Range(row, 2, row, 5).Style.Font.Bold = true;
        ws.Range(row, 2, row, 5).Style.Fill.BackgroundColor = Violet;
        ws.Range(row, 2, row, 5).Style.Font.FontColor = TextWhite;
        ws.Cell(row, 5).Style.NumberFormat.Format = "Q #,##0.00";
        row += 2;

        ws.Cell(row, 2).Value = "RECAP DEL COSTO EMPRESA";
        ws.Range(row, 2, row, 5).Merge();
        ws.Cell(row, 2).Style.Font.Bold = true;
        ws.Cell(row, 2).Style.Fill.BackgroundColor = BgDark;
        ws.Cell(row, 2).Style.Font.FontColor = TextWhite;
        ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Row(row).Height = 24;
        row++;

        var recap = new (string, decimal, XLColor?)[] {
            ("Total bruto pagado a empleados",          a.TotalIngresos,                       null),
            ("(+) Aportes patronales",                  a.CargasPatronales,                    null),
            ("(=) COSTO TOTAL DEL PERIODO",             a.CostoEmpresa,                        Amber),
            ("(-) Retenciones (IGSS+ISR)",              a.TotalIgss + a.TotalIsr,              null),
            ("(=) Neto recibido por empleados",         a.TotalNeto,                           Emerald),
        };
        foreach (var (label, val, color) in recap)
        {
            ws.Cell(row, 2).Value = label;
            ws.Range(row, 2, row, 4).Merge();
            ws.Cell(row, 5).Value = val;
            ws.Cell(row, 5).Style.NumberFormat.Format = "Q #,##0.00";
            ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            if (color != null)
            {
                ws.Range(row, 2, row, 5).Style.Font.Bold = true;
                ws.Range(row, 2, row, 5).Style.Fill.BackgroundColor = color;
                ws.Range(row, 2, row, 5).Style.Font.FontColor = TextWhite;
            }
            row++;
        }

        row += 2;
        ws.Range(row, 2, row, 5).Merge();
        ws.Cell(row, 2).Value = "Base legal: IGSS patronal 10.67% (Acuerdo 1118), IRTRA 1%, INTECAP 1% (Decreto 17-72).";
        ws.Cell(row, 2).Style.Font.FontColor = TextMuted;
        ws.Cell(row, 2).Style.Font.Italic = true;
        ws.Cell(row, 2).Style.Font.FontSize = 9;

        row += 2;
        SetSheetLink(ws.Cell(row, 2), "Dashboard", "A1", "← Volver al Dashboard", Blue);
    }

    // ════════════════════════════════════════════════════════════════════
    // HOJA 6 — TENDENCIA (serie anual con sparklines y data bars)
    // ════════════════════════════════════════════════════════════════════
    private static void BuildHojaTendencia(IXLWorkbook wb, List<IDictionary<string, object>> serie, int anio)
    {
        var ws = wb.Worksheets.Add("Tendencia");
        ws.TabColor = Blue;
        var hdr = new[] { "Mes", "Empleados", "Total ingresos", "Total deducciones", "Total neto" };
        for (int i = 0; i < hdr.Length; i++)
        {
            var c = ws.Cell(1, i + 1);
            c.Value = hdr[i];
            c.Style.Font.Bold = true;
            c.Style.Fill.BackgroundColor = BgDark;
            c.Style.Font.FontColor = TextWhite;
        }
        ws.Row(1).Height = 24;

        var dict = serie.ToDictionary(r => I(r, "MES"));
        for (int m = 1; m <= 12; m++)
        {
            var r = m + 1;
            ws.Cell(r, 1).Value = $"{MESES[m - 1]} {anio}";
            if (dict.TryGetValue(m, out var row))
            {
                ws.Cell(r, 2).Value = I(row, "EMPLEADOS");
                ws.Cell(r, 3).Value = D(row, "TOTAL_INGRESOS");
                ws.Cell(r, 4).Value = D(row, "TOTAL_DEDUCCIONES");
                ws.Cell(r, 5).Value = D(row, "SALARIO_NETO");
            }
            else
            {
                ws.Cell(r, 2).Value = 0;
                ws.Cell(r, 3).Value = 0;
                ws.Cell(r, 4).Value = 0;
                ws.Cell(r, 5).Value = 0;
            }
        }
        for (int c = 3; c <= 5; c++) ws.Column(c).Style.NumberFormat.Format = "Q #,##0.00";

        // Data bars en cada columna
        ws.Range(2, 2, 13, 2).AddConditionalFormat().DataBar(Blue, false).LowestValue().HighestValue();
        ws.Range(2, 3, 13, 3).AddConditionalFormat().DataBar(Emerald, false).LowestValue().HighestValue();
        ws.Range(2, 4, 13, 4).AddConditionalFormat().DataBar(Red, false).LowestValue().HighestValue();
        ws.Range(2, 5, 13, 5).AddConditionalFormat().DataBar(Amber, false).LowestValue().HighestValue();

        // Sparklines: 1 por cada metrica
        try
        {
            ws.SparklineGroups.Add("G15:G15", "C2:C13");
            ws.SparklineGroups.Add("G16:G16", "D2:D13");
            ws.SparklineGroups.Add("G17:G17", "E2:E13");
            ws.Cell(15, 6).Value = "Ingresos →";
            ws.Cell(16, 6).Value = "Deducciones →";
            ws.Cell(17, 6).Value = "Neto →";
            ws.Range(15, 6, 17, 6).Style.Font.FontColor = TextDim;
            ws.Range(15, 6, 17, 6).Style.Font.Italic = true;
            ws.Range(15, 6, 17, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        }
        catch { /* opcional */ }

        SetSheetLink(ws.Cell(19, 1), "Dashboard", "A1", "← Volver al Dashboard", Blue);

        ws.Columns().AdjustToContents();
        ws.Column(6).Width = 20;
    }
}
