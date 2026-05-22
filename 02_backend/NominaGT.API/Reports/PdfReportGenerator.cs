using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NominaGT.API.Reports;

/// <summary>
/// Genera reportes PDF con QuestPDF. Reemplaza el flujo manual de Power BI
/// para casos donde solo se necesita un PDF ejecutivo del periodo.
/// </summary>
public class PdfReportGenerator
{
    static PdfReportGenerator()
    {
        // QuestPDF Community License (gratuita para uso interno y open source).
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static decimal D(IDictionary<string, object> r, string key)
        => r.TryGetValue(key, out var v) && v != null ? Convert.ToDecimal(v) : 0m;
    private static string S(IDictionary<string, object> r, string key)
        => r.TryGetValue(key, out var v) && v != null ? Convert.ToString(v) ?? "" : "";

    private static readonly string[] MESES = {
        "Enero","Febrero","Marzo","Abril","Mayo","Junio",
        "Julio","Agosto","Septiembre","Octubre","Noviembre","Diciembre"
    };

    public byte[] GenerarPlanillaMensual(IEnumerable<dynamic> rows, int anio, int mes, string tipoPeriodo)
    {
        var lista = rows.Cast<IDictionary<string, object>>().ToList();
        var nombreMes = (mes >= 1 && mes <= 12) ? MESES[mes - 1] : mes.ToString();

        decimal totalIngresos    = lista.Sum(r => D(r, "TOTAL_INGRESOS"));
        decimal totalDeducciones = lista.Sum(r => D(r, "TOTAL_DEDUCCIONES"));
        decimal totalNeto        = lista.Sum(r => D(r, "SALARIO_NETO"));
        decimal totalIgss        = lista.Sum(r => D(r, "IGSS"));
        decimal totalIsr         = lista.Sum(r => D(r, "ISR"));

        var porDepto = lista
            .GroupBy(r => string.IsNullOrEmpty(S(r, "DEPARTAMENTO")) ? "Sin departamento" : S(r, "DEPARTAMENTO"))
            .Select(g => new {
                Departamento = g.Key,
                Cantidad     = g.Count(),
                TotalNeto    = g.Sum(x => D(x, "SALARIO_NETO"))
            })
            .OrderByDescending(x => x.TotalNeto)
            .ToList();

        var fmtQ = (decimal v) => $"Q {v:N2}";

        return Document.Create(doc =>
        {
            doc.Page(p =>
            {
                p.Size(PageSizes.Letter);
                p.Margin(28);
                p.DefaultTextStyle(t => t.FontSize(10).FontFamily("Helvetica"));

                // ── Header ──
                p.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("NominaGT v4").FontSize(20).Bold().FontColor("#1F2937");
                            c.Item().Text("Planilla mensual de nomina").FontSize(11).FontColor("#6B7280");
                        });
                        row.ConstantItem(180).AlignRight().Column(c =>
                        {
                            c.Item().AlignRight().Text($"{nombreMes} {anio}").FontSize(14).Bold();
                            c.Item().AlignRight().Text($"Tipo: {tipoPeriodo}").FontSize(10).FontColor("#6B7280");
                            c.Item().AlignRight().Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(9).FontColor("#9CA3AF");
                        });
                    });
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor("#E5E7EB");
                });

                // ── Content ──
                p.Content().PaddingVertical(12).Column(col =>
                {
                    // ----- Resumen tarjetas -----
                    col.Item().PaddingBottom(12).Row(r =>
                    {
                        void Card(string label, string value, string color)
                        {
                            r.RelativeItem().Background("#F9FAFB").Border(1).BorderColor("#E5E7EB").Padding(10).Column(c =>
                            {
                                c.Item().Text(label.ToUpper()).FontSize(8).FontColor("#6B7280").Bold();
                                c.Item().PaddingTop(4).Text(value).FontSize(14).FontColor(color).Bold();
                            });
                        }

                        Card("Empleados",      lista.Count.ToString(), "#10B981");
                        r.ConstantItem(8);
                        Card("Total ingresos", fmtQ(totalIngresos),    "#F59E0B");
                        r.ConstantItem(8);
                        Card("Total deduc.",   fmtQ(totalDeducciones), "#EF4444");
                        r.ConstantItem(8);
                        Card("Neto a pagar",   fmtQ(totalNeto),        "#1F2937");
                    });

                    // ----- Detalle por empleado -----
                    col.Item().PaddingBottom(6).Text("Detalle por empleado").FontSize(12).Bold();

                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(1);  // codigo
                            cd.RelativeColumn(3);  // nombre
                            cd.RelativeColumn(2);  // depto
                            cd.RelativeColumn(1.4f); // ingresos
                            cd.RelativeColumn(1.4f); // deduc
                            cd.RelativeColumn(1.5f); // neto
                        });

                        t.Header(h =>
                        {
                            string[] hdr = { "Codigo", "Nombre", "Departamento", "Ingresos", "Deducc.", "Neto" };
                            foreach (var x in hdr)
                                h.Cell().Background("#1F2937").Padding(5).Text(x).FontColor("#FFFFFF").FontSize(9).Bold();
                        });

                        foreach (var r in lista)
                        {
                            t.Cell().Padding(4).Text(S(r, "CODIGO_EMPLEADO")).FontSize(9);
                            t.Cell().Padding(4).Text(S(r, "NOMBRE_EMPLEADO")).FontSize(9);
                            t.Cell().Padding(4).Text(S(r, "DEPARTAMENTO")).FontSize(9).FontColor("#6B7280");
                            t.Cell().Padding(4).AlignRight().Text(fmtQ(D(r, "TOTAL_INGRESOS"))).FontSize(9);
                            t.Cell().Padding(4).AlignRight().Text(fmtQ(D(r, "TOTAL_DEDUCCIONES"))).FontSize(9);
                            t.Cell().Padding(4).AlignRight().Text(fmtQ(D(r, "SALARIO_NETO"))).FontSize(9).Bold();
                        }

                        // fila total
                        t.Cell().ColumnSpan(3).Padding(4).Background("#F3F4F6").Text("TOTAL").FontSize(9).Bold();
                        t.Cell().Padding(4).Background("#F3F4F6").AlignRight().Text(fmtQ(totalIngresos)).FontSize(9).Bold();
                        t.Cell().Padding(4).Background("#F3F4F6").AlignRight().Text(fmtQ(totalDeducciones)).FontSize(9).Bold();
                        t.Cell().Padding(4).Background("#F3F4F6").AlignRight().Text(fmtQ(totalNeto)).FontSize(9).Bold();
                    });

                    // ----- Por departamento -----
                    if (porDepto.Count > 0)
                    {
                        col.Item().PaddingTop(16).PaddingBottom(6).Text("Resumen por departamento").FontSize(12).Bold();
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(cd =>
                            {
                                cd.RelativeColumn(4);
                                cd.RelativeColumn(1);
                                cd.RelativeColumn(2);
                            });
                            t.Header(h =>
                            {
                                h.Cell().Background("#F3F4F6").Padding(5).Text("Departamento").FontSize(9).Bold();
                                h.Cell().Background("#F3F4F6").Padding(5).AlignCenter().Text("Empleados").FontSize(9).Bold();
                                h.Cell().Background("#F3F4F6").Padding(5).AlignRight().Text("Total Neto").FontSize(9).Bold();
                            });
                            foreach (var g in porDepto)
                            {
                                t.Cell().Padding(4).BorderBottom(0.5f).BorderColor("#E5E7EB").Text(g.Departamento).FontSize(9);
                                t.Cell().Padding(4).BorderBottom(0.5f).BorderColor("#E5E7EB").AlignCenter().Text(g.Cantidad.ToString()).FontSize(9);
                                t.Cell().Padding(4).BorderBottom(0.5f).BorderColor("#E5E7EB").AlignRight().Text(fmtQ(g.TotalNeto)).FontSize(9);
                            }
                        });
                    }

                    // ----- Aportes patronales / fiscales -----
                    if (totalIgss > 0 || totalIsr > 0)
                    {
                        col.Item().PaddingTop(12).Background("#FEF3C7").Padding(8).Row(r =>
                        {
                            r.RelativeItem().Text(t =>
                            {
                                t.Span("Total IGSS retenido: ").FontSize(9).Bold();
                                t.Span(fmtQ(totalIgss)).FontSize(9);
                                t.Span("    Total ISR retenido: ").FontSize(9).Bold();
                                t.Span(fmtQ(totalIsr)).FontSize(9);
                            });
                        });
                    }
                });

                // ── Footer ──
                p.Footer().AlignCenter().Text(t =>
                {
                    t.Span("NominaGT v4 — ").FontSize(8).FontColor("#9CA3AF");
                    t.Span("Reporte generado automaticamente.").FontSize(8).FontColor("#9CA3AF");
                    t.Span(" Pagina ").FontSize(8).FontColor("#9CA3AF");
                    t.CurrentPageNumber().FontSize(8).FontColor("#9CA3AF");
                    t.Span(" de ").FontSize(8).FontColor("#9CA3AF");
                    t.TotalPages().FontSize(8).FontColor("#9CA3AF");
                });
            });
        }).GeneratePdf();
    }

    /// <summary>
    /// Recibo de pago individual: 1 pagina, formato comprobante.
    /// </summary>
    public byte[] GenerarReciboPago(NominaGT.API.DTOs.ReciboPagoDto recibo)
    {
        var fmtQ = (decimal v) => $"Q {v:N2}";

        return Document.Create(doc =>
        {
            doc.Page(p =>
            {
                p.Size(PageSizes.Letter);
                p.Margin(36);
                p.DefaultTextStyle(t => t.FontSize(10).FontFamily("Helvetica"));

                p.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("RECIBO DE PAGO").FontSize(18).Bold().FontColor("#1F2937");
                            c.Item().Text("NominaGT v4").FontSize(10).FontColor("#6B7280");
                        });
                        row.ConstantItem(160).AlignRight().Column(c =>
                        {
                            c.Item().AlignRight().Text(recibo.Periodo).FontSize(13).Bold();
                            c.Item().AlignRight().Text($"Generado {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(9).FontColor("#9CA3AF");
                        });
                    });
                    col.Item().PaddingTop(10).LineHorizontal(1).LineColor("#E5E7EB");
                });

                p.Content().PaddingVertical(16).Column(col =>
                {
                    // Datos del empleado
                    col.Item().Background("#F9FAFB").Border(1).BorderColor("#E5E7EB").Padding(12).Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text("EMPLEADO").FontSize(8).FontColor("#6B7280").Bold();
                            c.Item().PaddingTop(2).Text(recibo.NombreEmpleado ?? "").FontSize(13).Bold();
                            c.Item().Text(recibo.CodigoEmpleado ?? "").FontSize(10).FontColor("#6B7280");
                        });
                        r.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().AlignRight().Text("DEPARTAMENTO").FontSize(8).FontColor("#6B7280").Bold();
                            c.Item().PaddingTop(2).AlignRight().Text(recibo.Departamento ?? "—").FontSize(11);
                            c.Item().PaddingTop(8).AlignRight().Text("SALARIO BASE").FontSize(8).FontColor("#6B7280").Bold();
                            c.Item().AlignRight().Text(fmtQ(recibo.SalarioBase)).FontSize(11).Bold();
                        });
                    });

                    // Ingresos
                    col.Item().PaddingTop(16).Text("INGRESOS").FontSize(10).Bold().FontColor("#10B981");
                    col.Item().PaddingTop(4).Table(t =>
                    {
                        t.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(5);
                            cd.RelativeColumn(2);
                        });
                        foreach (var l in recibo.Ingresos ?? new())
                        {
                            t.Cell().BorderBottom(0.5f).BorderColor("#E5E7EB").Padding(5).Text(l.Concepto ?? "").FontSize(10);
                            t.Cell().BorderBottom(0.5f).BorderColor("#E5E7EB").Padding(5).AlignRight().Text(fmtQ(l.Monto)).FontSize(10).FontColor("#10B981");
                        }
                        t.Cell().Padding(5).Background("#F0FDF4").Text("Total ingresos").FontSize(10).Bold();
                        t.Cell().Padding(5).Background("#F0FDF4").AlignRight().Text(fmtQ(recibo.TotalIngresos)).FontSize(11).Bold().FontColor("#10B981");
                    });

                    // Deducciones
                    col.Item().PaddingTop(16).Text("DEDUCCIONES").FontSize(10).Bold().FontColor("#EF4444");
                    col.Item().PaddingTop(4).Table(t =>
                    {
                        t.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(5);
                            cd.RelativeColumn(2);
                        });
                        if ((recibo.Deducciones?.Count ?? 0) == 0)
                        {
                            t.Cell().ColumnSpan(2).Padding(5).Text("Sin deducciones para este periodo.").FontSize(10).FontColor("#6B7280").Italic();
                        }
                        else
                        {
                            foreach (var l in recibo.Deducciones!)
                            {
                                t.Cell().BorderBottom(0.5f).BorderColor("#E5E7EB").Padding(5).Column(c =>
                                {
                                    c.Item().Text(l.Concepto ?? "").FontSize(10);
                                    if (!string.IsNullOrEmpty(l.Referencia))
                                        c.Item().Text(l.Referencia).FontSize(8).FontColor("#9CA3AF");
                                });
                                t.Cell().BorderBottom(0.5f).BorderColor("#E5E7EB").Padding(5).AlignRight().Text(fmtQ(l.Monto)).FontSize(10).FontColor("#EF4444");
                            }
                            t.Cell().Padding(5).Background("#FEF2F2").Text("Total deducciones").FontSize(10).Bold();
                            t.Cell().Padding(5).Background("#FEF2F2").AlignRight().Text(fmtQ(recibo.TotalDeducciones)).FontSize(11).Bold().FontColor("#EF4444");
                        }
                    });

                    // Total a pagar
                    col.Item().PaddingTop(20).Background("#1F2937").Padding(14).Row(r =>
                    {
                        r.RelativeItem().AlignMiddle().Text("SALARIO NETO A PAGAR").FontSize(11).Bold().FontColor("#FFFFFF");
                        r.ConstantItem(180).AlignRight().AlignMiddle().Text(fmtQ(recibo.SalarioNeto)).FontSize(20).Bold().FontColor("#FBBF24");
                    });

                    // Notas legales
                    col.Item().PaddingTop(20).Text(t =>
                    {
                        t.Span("Bonificacion incentivo: ").FontSize(8).Bold().FontColor("#6B7280");
                        t.Span("exenta de IGSS e ISR (Decreto 78-89). ").FontSize(8).FontColor("#6B7280");
                        t.Span("IGSS Laboral: ").FontSize(8).Bold().FontColor("#6B7280");
                        t.Span("4.83% sobre salario ordinario.").FontSize(8).FontColor("#6B7280");
                    });

                    // Firmas
                    col.Item().PaddingTop(40).Row(r =>
                    {
                        r.RelativeItem().AlignCenter().Column(c =>
                        {
                            c.Item().AlignCenter().LineHorizontal(0.7f).LineColor("#9CA3AF");
                            c.Item().PaddingTop(4).AlignCenter().Text("Recibe conforme").FontSize(9).FontColor("#6B7280");
                            c.Item().AlignCenter().Text(recibo.NombreEmpleado ?? "").FontSize(8).FontColor("#9CA3AF");
                        });
                        r.ConstantItem(40);
                        r.RelativeItem().AlignCenter().Column(c =>
                        {
                            c.Item().AlignCenter().LineHorizontal(0.7f).LineColor("#9CA3AF");
                            c.Item().PaddingTop(4).AlignCenter().Text("Autoriza").FontSize(9).FontColor("#6B7280");
                            c.Item().AlignCenter().Text("NominaGT").FontSize(8).FontColor("#9CA3AF");
                        });
                    });
                });

                p.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Recibo generado por NominaGT v4. ").FontSize(8).FontColor("#9CA3AF");
                    t.Span("Documento informativo, no es comprobante tributario.").FontSize(8).FontColor("#9CA3AF");
                });
            });
        }).GeneratePdf();
    }

    /// <summary>
    /// Genera PDF de finiquito / liquidación laboral con desglose legal.
    /// </summary>
    public byte[] GenerarFiniquito(NominaGT.API.DTOs.LiquidacionDto liq)
    {
        var fmtQ = (decimal v) => $"Q {v:N2}";
        var motivoTxt = liq.Motivo switch
        {
            "RENUNCIA"               => "Renuncia voluntaria",
            "DESPIDO_JUSTIFICADO"    => "Despido justificado",
            "DESPIDO_INJUSTIFICADO"  => "Despido injustificado",
            "MUTUO_ACUERDO"          => "Por mutuo acuerdo",
            "JUBILACION"             => "Jubilación",
            "FALLECIMIENTO"          => "Fallecimiento",
            _                        => "Otro motivo",
        };

        return Document.Create(doc =>
        {
            doc.Page(p =>
            {
                p.Size(PageSizes.Letter);
                p.Margin(32);
                p.DefaultTextStyle(t => t.FontSize(10).FontFamily("Helvetica"));

                p.Header().Column(c =>
                {
                    c.Item().Text("FINIQUITO / LIQUIDACIÓN LABORAL").FontSize(20).Bold().FontColor("#1F2937");
                    c.Item().Text("NominaGT v4 · Conforme al Código de Trabajo de Guatemala").FontSize(10).FontColor("#6B7280");
                    c.Item().PaddingTop(8).LineHorizontal(1).LineColor("#E5E7EB");
                });

                p.Content().PaddingVertical(14).Column(col =>
                {
                    // Datos del empleado
                    col.Item().PaddingBottom(8).Text("Datos del trabajador").FontSize(12).Bold();
                    col.Item().Background("#F9FAFB").Border(0.5f).BorderColor("#E5E7EB").Padding(10).Column(d =>
                    {
                        d.Item().Row(r => { r.RelativeItem().Text("Código:"); r.RelativeItem(2).Text(liq.CodigoEmpleado ?? "—").Bold(); });
                        d.Item().Row(r => { r.RelativeItem().Text("Nombre:"); r.RelativeItem(2).Text(liq.NombreEmpleado ?? "—").Bold(); });
                        d.Item().Row(r => { r.RelativeItem().Text("Fecha de ingreso:"); r.RelativeItem(2).Text(liq.FechaInicioContrato.ToString("dd/MM/yyyy")); });
                        d.Item().Row(r => { r.RelativeItem().Text("Fecha de baja:");    r.RelativeItem(2).Text(liq.FechaBaja.ToString("dd/MM/yyyy")); });
                        d.Item().Row(r => { r.RelativeItem().Text("Motivo:");           r.RelativeItem(2).Text(motivoTxt).Bold(); });
                        d.Item().Row(r => { r.RelativeItem().Text("Antigüedad:");       r.RelativeItem(2).Text($"{liq.AniosServicio:N2} años ({liq.DiasServicioTotal} días)"); });
                        d.Item().Row(r => { r.RelativeItem().Text("Salario base:");     r.RelativeItem(2).Text(fmtQ(liq.SalarioBase)); });
                        d.Item().Row(r => { r.RelativeItem().Text("Salario diario:");   r.RelativeItem(2).Text(fmtQ(liq.SalarioDiario)); });
                    });

                    // Desglose de prestaciones
                    col.Item().PaddingTop(16).PaddingBottom(8).Text("Desglose de prestaciones").FontSize(12).Bold();
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(cd => { cd.RelativeColumn(3); cd.RelativeColumn(2); cd.RelativeColumn(2); });
                        t.Header(h =>
                        {
                            h.Cell().Background("#1F2937").Padding(5).Text("Concepto").FontColor("#FFFFFF").Bold().FontSize(9);
                            h.Cell().Background("#1F2937").Padding(5).AlignRight().Text("Base / Detalle").FontColor("#FFFFFF").Bold().FontSize(9);
                            h.Cell().Background("#1F2937").Padding(5).AlignRight().Text("Monto").FontColor("#FFFFFF").Bold().FontSize(9);
                        });

                        // Indemnizacion
                        t.Cell().Padding(5).BorderBottom(0.5f).BorderColor("#E5E7EB").Text("Indemnización");
                        t.Cell().Padding(5).BorderBottom(0.5f).BorderColor("#E5E7EB").AlignRight()
                          .Text(liq.Indemnizacion > 0
                              ? $"{liq.AniosServicio:N2} años × salario"
                              : "No aplica");
                        t.Cell().Padding(5).BorderBottom(0.5f).BorderColor("#E5E7EB").AlignRight().Text(fmtQ(liq.Indemnizacion)).Bold();

                        // Bono 14
                        t.Cell().Padding(5).BorderBottom(0.5f).BorderColor("#E5E7EB").Text("Bono 14 proporcional");
                        t.Cell().Padding(5).BorderBottom(0.5f).BorderColor("#E5E7EB").AlignRight().Text("Decreto 42-92").FontSize(9).FontColor("#6B7280");
                        t.Cell().Padding(5).BorderBottom(0.5f).BorderColor("#E5E7EB").AlignRight().Text(fmtQ(liq.Bono14Proporcional)).Bold();

                        // Aguinaldo
                        t.Cell().Padding(5).BorderBottom(0.5f).BorderColor("#E5E7EB").Text("Aguinaldo proporcional");
                        t.Cell().Padding(5).BorderBottom(0.5f).BorderColor("#E5E7EB").AlignRight().Text("Código de Trabajo art. 137").FontSize(9).FontColor("#6B7280");
                        t.Cell().Padding(5).BorderBottom(0.5f).BorderColor("#E5E7EB").AlignRight().Text(fmtQ(liq.AguinaldoProporcional)).Bold();

                        // Vacaciones
                        t.Cell().Padding(5).BorderBottom(0.5f).BorderColor("#E5E7EB").Text("Vacaciones no gozadas");
                        t.Cell().Padding(5).BorderBottom(0.5f).BorderColor("#E5E7EB").AlignRight().Text($"{liq.DiasVacacionesPend} días × Q{liq.SalarioDiario:N2}").FontSize(9).FontColor("#6B7280");
                        t.Cell().Padding(5).BorderBottom(0.5f).BorderColor("#E5E7EB").AlignRight().Text(fmtQ(liq.VacacionesNoGozadas)).Bold();

                        // Otros pagos
                        if (liq.OtrosPagos > 0)
                        {
                            t.Cell().Padding(5).BorderBottom(0.5f).BorderColor("#E5E7EB").Text("Otros pagos");
                            t.Cell().Padding(5).BorderBottom(0.5f).BorderColor("#E5E7EB").AlignRight().Text("");
                            t.Cell().Padding(5).BorderBottom(0.5f).BorderColor("#E5E7EB").AlignRight().Text(fmtQ(liq.OtrosPagos)).Bold();
                        }

                        // Descuentos
                        if (liq.Descuentos > 0)
                        {
                            t.Cell().Padding(5).BorderBottom(0.5f).BorderColor("#E5E7EB").Text("(−) Descuentos");
                            t.Cell().Padding(5).BorderBottom(0.5f).BorderColor("#E5E7EB").AlignRight().Text("");
                            t.Cell().Padding(5).BorderBottom(0.5f).BorderColor("#E5E7EB").AlignRight().Text($"− {fmtQ(liq.Descuentos)}").FontColor("#EF4444").Bold();
                        }

                        // Total
                        t.Cell().Background("#F59E0B").Padding(7).Text("TOTAL A LIQUIDAR").FontColor("#FFFFFF").Bold().FontSize(11);
                        t.Cell().Background("#F59E0B").Padding(7).AlignRight().Text("").FontColor("#FFFFFF");
                        t.Cell().Background("#F59E0B").Padding(7).AlignRight().Text(fmtQ(liq.Total)).FontColor("#FFFFFF").Bold().FontSize(13);
                    });

                    if (!string.IsNullOrWhiteSpace(liq.Observaciones))
                    {
                        col.Item().PaddingTop(12).Text("Observaciones:").Bold();
                        col.Item().PaddingTop(4).Text(liq.Observaciones!).FontSize(10).FontColor("#475569");
                    }

                    // Firma
                    col.Item().PaddingTop(60).Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(0.5f).LineColor("#1F2937");
                            c.Item().PaddingTop(4).AlignCenter().Text(liq.NombreEmpleado ?? "Empleado").FontSize(9).Bold();
                            c.Item().AlignCenter().Text("Trabajador").FontSize(8).FontColor("#6B7280");
                        });
                        r.ConstantItem(40);
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(0.5f).LineColor("#1F2937");
                            c.Item().PaddingTop(4).AlignCenter().Text("Representante Legal").FontSize(9).Bold();
                            c.Item().AlignCenter().Text("Empresa").FontSize(8).FontColor("#6B7280");
                        });
                    });
                });

                p.Footer().AlignCenter().Text(t =>
                {
                    t.Span($"Generado por NominaGT v4 el {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8).FontColor("#9CA3AF");
                });
            });
        }).GeneratePdf();
    }
}
