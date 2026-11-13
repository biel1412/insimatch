using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using Insimatch.Models;

namespace Insimatch.Services;

/// <summary>
/// Gera um arquivo .xlsx (Office Open XML) direto no navegador, sem bibliotecas externas:
/// cabeçalho formatado e congelado, filtro, larguras, formatos (moeda, %, data), fórmulas,
/// linha de totais e formatação condicional. O Excel recalcula as fórmulas ao abrir.
/// </summary>
public static class ExcelService
{
    public const string Mime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly string[] Tipos = { "texto", "inteiro", "numero", "moeda", "percentual", "data" };
    private static readonly string[] Cores = { "vermelho", "verde", "amarelo", "azul" };
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly CultureInfo PtBr = new("pt-BR");

    // Índices de estilo (cellXfs): 0 padrão, 1 cabeçalho, 2..7 tipos, 8..13 tipos na linha de total
    [ThreadStatic] private static List<(string De, string Para)>? Trocas;

    private static int Estilo(string tipo, bool total) => (total ? 8 : 2) + Math.Max(0, Array.IndexOf(Tipos, tipo));

    public static byte[] Gerar(PlanilhaExcel p)
    {
        var originais = p.Abas.Select(a => a.Nome ?? "").ToList();
        var nomes = NomesAbas(p.Abas);
        // Nomes de abas ajustados: atualiza as referências nas fórmulas ('Nome antigo'!A1 → 'Nome novo'!A1)
        Trocas = originais.Select((o, i) => (o.Trim(), nomes[i])).Where(t => t.Item1.Length > 0 && t.Item1 != t.Item2).ToList();
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            Escrever(zip, "[Content_Types].xml", ContentTypes(p.Abas.Count));
            Escrever(zip, "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                "</Relationships>");
            Escrever(zip, "xl/workbook.xml", Workbook(p, nomes));
            Escrever(zip, "xl/_rels/workbook.xml.rels", WorkbookRels(p.Abas.Count));
            Escrever(zip, "xl/styles.xml", Estilos());
            for (var i = 0; i < p.Abas.Count; i++)
                Escrever(zip, $"xl/worksheets/sheet{i + 1}.xml", Aba(p.Abas[i]));
        }
        return ms.ToArray();
    }

    /// <summary>Nome de arquivo seguro, terminando em .xlsx.</summary>
    public static string NomeArquivo(string nome)
    {
        var n = new string((nome ?? "").Where(c => !Path.GetInvalidFileNameChars().Contains(c) && c is not ('/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|')).ToArray()).Trim();
        if (n.Length == 0) n = "Planilha";
        if (n.Length > 60) n = n[..60].Trim();
        return n + ".xlsx";
    }

    // ---------- Partes do pacote ----------

    private static void Escrever(ZipArchive zip, string caminho, string xml)
    {
        var e = zip.CreateEntry(caminho, CompressionLevel.Optimal);
        using var s = new StreamWriter(e.Open(), new UTF8Encoding(false));
        s.Write(xml);
    }

    private static string ContentTypes(int abas)
    {
        var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
        for (var i = 1; i <= abas; i++)
            sb.Append($"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
        return sb.Append("</Types>").ToString();
    }

    private static string Workbook(PlanilhaExcel p, List<string> nomes)
    {
        var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            "<bookViews><workbookView/></bookViews><sheets>");
        for (var i = 0; i < nomes.Count; i++)
            sb.Append($"<sheet name=\"{X(nomes[i])}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
        sb.Append("</sheets>");
        var filtros = new StringBuilder();
        for (var i = 0; i < p.Abas.Count; i++)
        {
            var a = p.Abas[i];
            if (a.Colunas.Count == 0) continue;
            var ultima = Math.Max(2, a.Linhas.Count + 1);
            filtros.Append($"<definedName name=\"_xlnm._FilterDatabase\" localSheetId=\"{i}\" hidden=\"1\">'{X(nomes[i].Replace("'", "''"))}'!$A$1:${Col(a.Colunas.Count - 1)}${ultima}</definedName>");
        }
        if (filtros.Length > 0) sb.Append("<definedNames>").Append(filtros).Append("</definedNames>");
        return sb.Append("<calcPr calcId=\"191029\" fullCalcOnLoad=\"1\"/></workbook>").ToString();
    }

    private static string WorkbookRels(int abas)
    {
        var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        for (var i = 1; i <= abas; i++)
            sb.Append($"<Relationship Id=\"rId{i}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>");
        sb.Append($"<Relationship Id=\"rId{abas + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
        return sb.Append("</Relationships>").ToString();
    }

    private static string Estilos()
    {
        // numFmt: 3 = #,##0 · 4 = #,##0.00 · 164 moeda · 165 percentual · 166 data
        int[] fmt = { 0, 3, 4, 164, 165, 166 };
        var xfs = new StringBuilder();
        xfs.Append("<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>");
        xfs.Append("<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf>");
        foreach (var total in new[] { false, true })
            foreach (var f in fmt)
                xfs.Append($"<xf numFmtId=\"{f}\" fontId=\"{(total ? 2 : 0)}\" fillId=\"{(total ? 3 : 0)}\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment vertical=\"center\"/></xf>");

        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<numFmts count=\"3\">" +
            "<numFmt numFmtId=\"164\" formatCode=\"&quot;R$&quot;\\ #,##0.00\"/>" +
            "<numFmt numFmtId=\"165\" formatCode=\"0.00%\"/>" +
            "<numFmt numFmtId=\"166\" formatCode=\"dd/mm/yyyy\"/>" +
            "</numFmts>" +
            "<fonts count=\"3\">" +
            "<font><sz val=\"11\"/><color theme=\"1\"/><name val=\"Calibri\"/><family val=\"2\"/><scheme val=\"minor\"/></font>" +
            "<font><b/><sz val=\"11\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/><family val=\"2\"/><scheme val=\"minor\"/></font>" +
            "<font><b/><sz val=\"11\"/><color theme=\"1\"/><name val=\"Calibri\"/><family val=\"2\"/><scheme val=\"minor\"/></font>" +
            "</fonts>" +
            "<fills count=\"4\">" +
            "<fill><patternFill patternType=\"none\"/></fill>" +
            "<fill><patternFill patternType=\"gray125\"/></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF3F3D9E\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFE8EAF6\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
            "</fills>" +
            "<borders count=\"2\">" +
            "<border><left/><right/><top/><bottom/><diagonal/></border>" +
            "<border><left style=\"thin\"><color rgb=\"FFD0D5E2\"/></left><right style=\"thin\"><color rgb=\"FFD0D5E2\"/></right><top style=\"thin\"><color rgb=\"FFD0D5E2\"/></top><bottom style=\"thin\"><color rgb=\"FFD0D5E2\"/></bottom><diagonal/></border>" +
            "</borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            $"<cellXfs count=\"14\">{xfs}</cellXfs>" +
            "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
            "<dxfs count=\"4\">" +
            Dxf("FF9C0006", "FFFFC7CE") + Dxf("FF006100", "FFC6EFCE") + Dxf("FF9C5700", "FFFFEB9C") + Dxf("FF1F4E79", "FFDDEBF7") +
            "</dxfs>" +
            "<tableStyles count=\"0\" defaultTableStyle=\"TableStyleMedium2\" defaultPivotStyle=\"PivotStyleLight16\"/>" +
            "</styleSheet>";
    }

    private static string Dxf(string fonte, string fundo) =>
        $"<dxf><font><color rgb=\"{fonte}\"/></font><fill><patternFill><bgColor rgb=\"{fundo}\"/></patternFill></fill></dxf>";

    private static string Aba(AbaExcel a)
    {
        var nCol = Math.Max(1, a.Colunas.Count);
        var ultimaLinha = a.Linhas.Count + 1 + (a.Total is null ? 0 : 1);
        var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
        sb.Append($"<dimension ref=\"A1:{Col(nCol - 1)}{ultimaLinha}\"/>");
        sb.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
        sb.Append("<sheetFormatPr defaultRowHeight=\"15\"/>");

        sb.Append("<cols>");
        for (var c = 0; c < a.Colunas.Count; c++)
        {
            var col = a.Colunas[c];
            var largura = col.Largura >= 4 ? Math.Min(col.Largura, 80) : Math.Clamp(Math.Max(col.Titulo.Length + 4, LarguraDados(a, c)), 8, 50);
            sb.Append($"<col min=\"{c + 1}\" max=\"{c + 1}\" width=\"{largura.ToString("0.##", Inv)}\" customWidth=\"1\"/>");
        }
        sb.Append("</cols><sheetData>");

        sb.Append("<row r=\"1\" ht=\"22\" customHeight=\"1\">");
        for (var c = 0; c < a.Colunas.Count; c++)
            sb.Append($"<c r=\"{Col(c)}1\" s=\"1\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{X(a.Colunas[c].Titulo)}</t></is></c>");
        sb.Append("</row>");

        for (var i = 0; i < a.Linhas.Count; i++) Linha(sb, a, a.Linhas[i], i + 2, false);
        if (a.Total is not null) Linha(sb, a, a.Total, a.Linhas.Count + 2, true);
        sb.Append("</sheetData>");

        if (a.Colunas.Count > 0)
            sb.Append($"<autoFilter ref=\"A1:{Col(a.Colunas.Count - 1)}{Math.Max(2, a.Linhas.Count + 1)}\"/>");

        var prioridade = 1;
        foreach (var grupo in a.Destaques.GroupBy(d => d.Coluna))
        {
            var faixa = $"{Col(grupo.Key)}2:{Col(grupo.Key)}{Math.Max(2, a.Linhas.Count + 1)}";
            sb.Append($"<conditionalFormatting sqref=\"{faixa}\">");
            foreach (var d in grupo)
            {
                var dxf = Math.Max(0, Array.IndexOf(Cores, d.Cor));
                var valor = d.Valor is double n ? n.ToString("R", Inv) : "\"" + Convert.ToString(d.Valor, Inv)?.Replace("\"", "\"\"") + "\"";
                sb.Append($"<cfRule type=\"cellIs\" dxfId=\"{dxf}\" priority=\"{prioridade++}\" operator=\"{Operador(d.Operador)}\"><formula>{X(valor)}</formula></cfRule>");
            }
            sb.Append("</conditionalFormatting>");
        }

        sb.Append("<pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/>");
        return sb.Append("</worksheet>").ToString();
    }

    private static void Linha(StringBuilder sb, AbaExcel a, List<object?> valores, int r, bool total)
    {
        sb.Append($"<row r=\"{r}\">");
        for (var c = 0; c < a.Colunas.Count; c++)
        {
            var tipo = a.Colunas[c].Tipo;
            var s = Estilo(tipo, total);
            var refc = $"{Col(c)}{r}";
            var v = c < valores.Count ? valores[c] : null;
            switch (v)
            {
                case null:
                    sb.Append($"<c r=\"{refc}\" s=\"{s}\"/>");
                    break;
                case bool b:
                    sb.Append($"<c r=\"{refc}\" s=\"{s}\" t=\"b\"><v>{(b ? 1 : 0)}</v></c>");
                    break;
                case double d:
                    sb.Append($"<c r=\"{refc}\" s=\"{s}\"><v>{d.ToString("R", Inv)}</v></c>");
                    break;
                case string txt:
                    var t = txt.Trim();
                    if (t.Length > 1 && t[0] == '=')
                        sb.Append($"<c r=\"{refc}\" s=\"{s}\"><f>{X(Formula(t[1..]))}</f></c>");
                    else if (t.Length == 0)
                        sb.Append($"<c r=\"{refc}\" s=\"{s}\"/>");
                    else if (Numero(t, tipo) is double n)
                        sb.Append($"<c r=\"{refc}\" s=\"{s}\"><v>{n.ToString("R", Inv)}</v></c>");
                    else
                        sb.Append($"<c r=\"{refc}\" s=\"{s}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{X(txt)}</t></is></c>");
                    break;
                default:
                    sb.Append($"<c r=\"{refc}\" s=\"{s}\" t=\"inlineStr\"><is><t>{X(Convert.ToString(v, Inv) ?? "")}</t></is></c>");
                    break;
            }
        }
        sb.Append("</row>");
    }

    /// <summary>Texto que representa número/data numa coluna numérica vira número (ex.: "1.234,50", "99,94%", "2026-10-31").</summary>
    private static double? Numero(string t, string tipo)
    {
        if (tipo == "texto") return null;
        if (tipo == "data")
        {
            if (DateTime.TryParseExact(t, new[] { "yyyy-MM-dd", "dd/MM/yyyy", "yyyy-MM-ddTHH:mm:ss" }, Inv, DateTimeStyles.None, out var dt))
                return (dt.Date - new DateTime(1899, 12, 30)).TotalDays;
            return null;
        }
        var s = t.Replace("R$", "").Replace(" ", "").Replace(" ", "");
        var pct = s.EndsWith('%');
        if (pct) s = s[..^1];
        double n;
        if (s.Contains(',') && double.TryParse(s, NumberStyles.Number, PtBr, out n)) { }
        else if (!double.TryParse(s, NumberStyles.Number, Inv, out n)) return null;
        return pct ? n / 100 : n;
    }

    /// <summary>Fórmula do .xlsx: sem "=", argumentos separados por vírgula (troca ";" fora de aspas).</summary>
    private static string Formula(string f)
    {
        foreach (var (de, para) in Trocas ?? new())
        {
            f = f.Replace($"'{de}'!", $"'{para.Replace("'", "''")}'!", StringComparison.OrdinalIgnoreCase);
            if (!de.Any(ch => ch is ' ' or '-' or '\'' )) f = f.Replace($"{de}!", $"'{para}'!", StringComparison.OrdinalIgnoreCase);
        }
        var sb = new StringBuilder(f.Length);
        var aspas = false;
        foreach (var ch in f)
        {
            if (ch == '"') aspas = !aspas;
            sb.Append(!aspas && ch == ';' ? ',' : ch);
        }
        return sb.ToString();
    }

    private static string Operador(string op) => op switch
    {
        "<" => "lessThan",
        "<=" => "lessThanOrEqual",
        ">" => "greaterThan",
        ">=" => "greaterThanOrEqual",
        "=" => "equal",
        _ => "notEqual"
    };

    private static int LarguraDados(AbaExcel a, int c) =>
        a.Linhas.Take(50).Select(l => c < l.Count && l[c] is string s && !s.StartsWith('=') ? s.Length + 2 : 10).DefaultIfEmpty(10).Max();

    private static List<string> NomesAbas(List<AbaExcel> abas)
    {
        var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lista = new List<string>();
        for (var i = 0; i < abas.Count; i++)
        {
            var n = new string((abas[i].Nome ?? "").Where(ch => ch is not (':' or '\\' or '/' or '?' or '*' or '[' or ']')).ToArray()).Trim().Trim('\'');
            if (n.Length == 0 || n.Equals("History", StringComparison.OrdinalIgnoreCase)) n = $"Aba {i + 1}";
            if (n.Length > 31) n = n[..31].Trim();
            var baseNome = n; var k = 2;
            while (!usados.Add(n)) { var suf = $" ({k++})"; n = (baseNome.Length + suf.Length > 31 ? baseNome[..(31 - suf.Length)] : baseNome) + suf; }
            lista.Add(n);
            abas[i].Nome = n;
        }
        return lista;
    }

    private static string Col(int i)
    {
        var s = "";
        for (i++; i > 0; i = (i - 1) / 26) s = (char)('A' + (i - 1) % 26) + s;
        return s;
    }

    private static string X(string s) => SecurityElement.Escape(new string(s.Where(ch => ch == '\t' || ch == '\n' || ch == '\r' || ch >= ' ').ToArray())) ?? "";
}
