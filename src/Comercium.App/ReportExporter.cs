using System.Globalization;
using System.IO;
using ClosedXML.Excel;
using Comercium.Application;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace Comercium.App;

public static class ReportExporter
{
    public static ReportData Receipt(Receipt receipt)
    {
        var customer = string.IsNullOrEmpty(receipt.Customer) ? "Venta sin cliente registrado" : $"Cliente {receipt.Customer}";
        var rows = receipt.Lines.Select(line => new object?[] { line.Code, line.Name, line.Quantity, line.UnitPrice / 100m,
            line.Quantity * line.UnitPrice / 100m }).ToList();
        rows.Add([null, "Subtotal", null, null, receipt.Subtotal / 100m]);
        rows.Add([null, "Descuento", null, null, receipt.Discount / 100m]);
        rows.Add([null, "Total", null, null, receipt.Total / 100m]);
        if (receipt.Method == Domain.PaymentMethod.Efectivo)
        {
            rows.Add([null, "Efectivo recibido", null, null, receipt.Received / 100m]);
            rows.Add([null, "Cambio", null, null, (receipt.Received - receipt.Total) / 100m]);
        }
        var state = receipt.Voided ? " · ANULADO" : "";
        return new ReportData($"Comprobante interno N.º {receipt.Id}",
            $"{Display.Local(receipt.AtUtc):g} · {customer} · {receipt.Cashier} · {receipt.Method}{state}",
            ["Código", "Producto / concepto", "Cantidad", "Precio S/", "Importe S/"], rows);
    }

    public static void Save(ReportData report, string file, bool excel)
    {
        var temp = file + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = File.Create(temp))
            {
                if (excel) Excel(report, stream);
                else Pdf(report, stream);
            }
            File.Move(temp, file, overwrite: true);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    public static string Cell(object? value) => value switch
    {
        null => "", DateTime at => at.ToString("g", CultureInfo.CurrentCulture), decimal number => number.ToString("N2", CultureInfo.CurrentCulture),
        _ => Convert.ToString(value, CultureInfo.CurrentCulture) ?? ""
    };

    private static void Excel(ReportData report, Stream stream)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Comercium");
        sheet.Cell(1, 1).Value = report.Title;
        sheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(17);
        sheet.Cell(2, 1).Value = report.Subtitle;
        for (var column = 0; column < report.Columns.Count; column++)
            sheet.Cell(4, column + 1).Value = report.Columns[column];
        var header = sheet.Range(4, 1, 4, report.Columns.Count);
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#112640");
        header.Style.Font.SetBold().Font.SetFontColor(XLColor.White);
        for (var row = 0; row < report.Rows.Count; row++)
        {
            for (var column = 0; column < report.Columns.Count; column++)
            {
                var cell = sheet.Cell(row + 5, column + 1);
                var value = report.Rows[row][column];
                if (value == null)
                    continue;
                cell.Value = XLCellValue.FromObject(value, CultureInfo.CurrentCulture);
                if (value is DateTime) cell.Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
                if (value is decimal) cell.Style.NumberFormat.Format = "#,##0.00";
            }
        }
        foreach (var column in sheet.Columns(1, report.Columns.Count))
        {
            column.AdjustToContents(4, Math.Min(250, report.Rows.Count + 4));
            column.Width = Math.Clamp(column.Width + 3, 13, 44);
        }
        sheet.SheetView.FreezeRows(4);
        if (report.Rows.Count > 0)
            sheet.Range(4, 1, report.Rows.Count + 4, report.Columns.Count).SetAutoFilter();
        workbook.SaveAs(stream);
    }

    private static void Pdf(ReportData report, Stream stream)
    {
        var document = new Document();
        document.Info.Title = report.Title;
        document.Styles["Normal"]!.Font.Name = "Arial";
        document.Styles["Normal"]!.Font.Size = 9;
        var section = document.AddSection();
        var landscape = report.Columns.Count > 5;
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.Orientation = landscape ? Orientation.Landscape : Orientation.Portrait;
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.5);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.5);
        var brand = section.AddParagraph("COMERCIUM");
        brand.Format.Font.Bold = true;
        brand.Format.Font.Color = Color.FromRgb(7, 91, 187);
        var heading = section.AddParagraph(report.Title);
        heading.Format.Font.Bold = true;
        heading.Format.Font.Size = 18;
        heading.Format.SpaceBefore = Unit.FromPoint(5);
        var subtitle = section.AddParagraph(report.Subtitle);
        subtitle.Format.SpaceAfter = Unit.FromPoint(12);
        var footer = section.Footers.Primary.AddParagraph("Comercium · Página ");
        footer.AddPageField();
        footer.Format.Alignment = ParagraphAlignment.Right;
        var table = section.AddTable();
        table.Borders.Width = Unit.FromPoint(0.35);
        table.Borders.Color = Color.FromRgb(211, 225, 240);
        var width = landscape ? 26.7 : 18;
        var weights = report.Columns.Select(name => name is "Producto" or "Producto / concepto" or "Concepto" or "Cliente" ? 1.65 : 1).ToArray();
        for (var col = 0; col < report.Columns.Count; col++)
            table.AddColumn(Unit.FromCentimeter(width * weights[col] / weights.Sum()));
        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Shading.Color = Color.FromRgb(17, 38, 64);
        header.Format.Font.Color = Colors.White;
        header.Format.Font.Bold = true;
        for (var col = 0; col < report.Columns.Count; col++)
            header.Cells[col].AddParagraph(report.Columns[col]);
        for (var index = 0; index < report.Rows.Count; index++)
        {
            var row = table.AddRow();
            row.TopPadding = Unit.FromPoint(4);
            row.BottomPadding = Unit.FromPoint(4);
            if (index % 2 == 1) row.Shading.Color = Color.FromRgb(245, 249, 253);
            for (var col = 0; col < report.Columns.Count; col++)
                row.Cells[col].AddParagraph(Cell(report.Rows[index][col]));
        }
        if (report.Rows.Count == 0)
            section.AddParagraph("No hay registros para esta consulta.");
        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(stream, closeStream: false);
    }
}
