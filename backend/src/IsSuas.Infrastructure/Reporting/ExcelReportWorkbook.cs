using System.Globalization;
using System.IO.Compression;
using System.Text;
using IsSuas.Application.Reporting;

namespace IsSuas.Infrastructure.Reporting;

internal static class ExcelReportWorkbook
{
    public static byte[] Build(ReportDto report)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "[Content_Types].xml", ContentTypes);
            Write(zip, "_rels/.rels", Rels);
            Write(zip, "xl/workbook.xml", Workbook(SheetName(report)));
            Write(zip, "xl/_rels/workbook.xml.rels", WorkbookRels);
            Write(zip, "xl/styles.xml", Styles);
            Write(zip, "xl/worksheets/sheet1.xml", Sheet(report));
        }
        return ms.ToArray();
    }

    private static void Write(ZipArchive zip, string path, string xml)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Fastest);
        using var s = entry.Open();
        using var w = new StreamWriter(s, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        w.Write(xml);
    }

    private static string SheetName(ReportDto report)
    {
        var name = string.IsNullOrWhiteSpace(report.TitleSk) ? report.ReportTypeCode : report.TitleSk.Trim();
        foreach (var c in new[] { ':', '\\', '/', '?', '*', '[', ']' })
            name = name.Replace(c, ' ');
        if (name.Length > 31) name = name[..31];
        return string.IsNullOrWhiteSpace(name) ? "Report" : name;
    }

    private static string Sheet(ReportDto report)
    {
        var sb = new StringBuilder();
        sb.Append("""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <sheetViews><sheetView workbookViewId="0"><pane ySplit="4" topLeftCell="A5" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews>
              <cols>
                <col min="1" max="1" width="48" customWidth="1"/>
                <col min="2" max="2" width="16" customWidth="1"/>
                <col min="3" max="3" width="16" customWidth="1"/>
              </cols>
              <sheetData>
            """);

        var r = 1;
        StartRow(sb, r);
        CellInline(sb, r, 1, report.TitleSk, 2);
        EndRow(sb);
        r++;

        StartRow(sb, r);
        CellInline(sb, r, 1,
            "Údaje k: " + report.DataAsOf.ToLocalTime().ToString("d. M. yyyy HH:mm", CultureInfo.GetCultureInfo("sk-SK"))
            + "  ·  Pravidlá: " + report.RulesVersion);
        EndRow(sb);
        r += 2;

        var headerRow = r;
        StartRow(sb, r);
        CellInline(sb, r, 1, "Ukazovateľ", 1);
        CellInline(sb, r, 2, "Hodnota", 1);
        CellInline(sb, r, 3, "Jednotka", 1);
        EndRow(sb);
        r++;

        foreach (var row in report.Rows)
        {
            StartRow(sb, r);
            CellInline(sb, r, 1, row.Label);
            CellNumber(sb, r, 2, row.Value);
            CellInline(sb, r, 3, row.Unit ?? "");
            EndRow(sb);
            r++;
        }

        if (report.Totals.Count > 0)
        {
            r++;
            StartRow(sb, r);
            CellInline(sb, r, 1, "Súhrn", 1);
            EndRow(sb);
            r++;
            StartRow(sb, r);
            CellInline(sb, r, 1, "Ukazovateľ", 1);
            CellInline(sb, r, 2, "Hodnota", 1);
            EndRow(sb);
            r++;
            foreach (var t in report.Totals)
            {
                StartRow(sb, r);
                CellInline(sb, r, 1, TotalLabel(t.Key));
                CellNumber(sb, r, 2, t.Value);
                EndRow(sb);
                r++;
            }
        }

        var lastData = headerRow + report.Rows.Count;
        sb.Append("</sheetData>");
        if (report.Rows.Count > 0)
            sb.Append($"<autoFilter ref=\"A{headerRow}:C{lastData}\"/>");
        sb.Append("<mergeCells count=\"1\"><mergeCell ref=\"A1:C1\"/></mergeCells>");
        sb.Append("</worksheet>");
        return sb.ToString();
    }

    private static string TotalLabel(string key) => key switch
    {
        "uas_total" => "UAS spolu",
        "pilots_total" => "Piloti spolu",
        "flights_count" => "Lety spolu",
        "flight_minutes" => "Minúty letov",
        "operations_count" => "Operácie spolu",
        "monthly_reports" => "Mesačné výkazy",
        "monthly_hours" => "Nálet (h)",
        "monthly_takeoffs" => "Vzlety",
        _ => key
    };

    private static void StartRow(StringBuilder sb, int row) => sb.Append($"<row r=\"{row}\">");
    private static void EndRow(StringBuilder sb) => sb.Append("</row>");

    private static void CellInline(StringBuilder sb, int row, int col, string text, int style = 0)
    {
        var styleAttr = style > 0 ? $" s=\"{style}\"" : "";
        sb.Append($"<c r=\"{Col(col)}{row}\"{styleAttr} t=\"inlineStr\"><is><t xml:space=\"preserve\">{Esc(text)}</t></is></c>");
    }

    private static void CellNumber(StringBuilder sb, int row, int col, decimal value)
    {
        var v = value == decimal.Truncate(value)
            ? decimal.Truncate(value).ToString(CultureInfo.InvariantCulture)
            : value.ToString(CultureInfo.InvariantCulture);
        sb.Append($"<c r=\"{Col(col)}{row}\"><v>{v}</v></c>");
    }

    private static string Col(int index) => index switch { 1 => "A", 2 => "B", 3 => "C", _ => "A" };

    private static string Esc(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (ch is '&') sb.Append("&amp;");
            else if (ch is '<') sb.Append("&lt;");
            else if (ch is '>') sb.Append("&gt;");
            else if (char.IsControl(ch) && ch is not '\t' and not '\n' and not '\r') continue;
            else sb.Append(ch);
        }
        return sb.ToString();
    }

    private const string ContentTypes = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
          <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
        </Types>
        """;

    private const string Rels = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    private static string Workbook(string sheetName) =>
        $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                  xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets><sheet name="{Esc(sheetName)}" sheetId="1" r:id="rId1"/></sheets>
        </workbook>
        """;

    private const string WorkbookRels = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
        </Relationships>
        """;

    private const string Styles = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <fonts count="3">
            <font><sz val="11"/><name val="Calibri"/></font>
            <font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font>
            <font><b/><sz val="14"/><color rgb="FF0F4C6E"/><name val="Calibri"/></font>
          </fonts>
          <fills count="3">
            <fill><patternFill patternType="none"/></fill>
            <fill><patternFill patternType="gray125"/></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FF0F4C6E"/><bgColor indexed="64"/></patternFill></fill>
          </fills>
          <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
          <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
          <cellXfs count="3">
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
            <xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1"/>
            <xf numFmtId="0" fontId="2" fillId="0" borderId="0" xfId="0" applyFont="1"/>
          </cellXfs>
        </styleSheet>
        """;
}
