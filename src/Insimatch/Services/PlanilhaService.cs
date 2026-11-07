using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Insimatch.Services;

/// <summary>Uma aba de planilha lida (linhas × colunas, como texto).</summary>
public record PlanilhaAba(string Nome, List<List<string>> Linhas)
{
    public int Colunas => Linhas.Count == 0 ? 0 : Linhas.Max(l => l.Count);
}

/// <summary>
/// Lê .xlsx (todas as abas) e .csv direto no navegador, sem bibliotecas externas.
/// O .xlsx é um arquivo zip com XML dentro.
/// </summary>
public static class PlanilhaService
{
    public static List<PlanilhaAba> Ler(string nomeArquivo, byte[] dados)
    {
        var ext = Path.GetExtension(nomeArquivo).ToLowerInvariant();
        return ext switch
        {
            ".xlsx" or ".xlsm" => LerXlsx(dados),
            ".csv" or ".txt" or ".tsv" => new List<PlanilhaAba> { new(Path.GetFileNameWithoutExtension(nomeArquivo), LerCsv(DecodificarTexto(dados))) },
            ".xls" => throw new InvalidOperationException("O formato .xls (Excel antigo) não é suportado. No Excel, use “Salvar como” → .xlsx ou .csv."),
            _ => throw new InvalidOperationException("Envie uma planilha .xlsx ou .csv.")
        };
    }

    // ---------- CSV ----------

    private static string DecodificarTexto(byte[] dados)
    {
        // UTF-8 (com ou sem BOM); se houver bytes inválidos, tenta Latin-1 (CSV salvo pelo Excel em português)
        var utf8 = new UTF8Encoding(false, true);
        try { return utf8.GetString(dados).TrimStart('﻿'); }
        catch (DecoderFallbackException) { return Encoding.Latin1.GetString(dados); }
    }

    public static List<List<string>> LerCsv(string texto)
    {
        var linhas = texto.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var primeira = linhas.FirstOrDefault(l => l.Trim().Length > 0) ?? "";
        var sep = new[] { ';', ',', '\t' }.OrderByDescending(c => primeira.Count(ch => ch == c)).First();
        var r = new List<List<string>>();
        foreach (var l in linhas)
        {
            if (l.Trim().Length == 0) continue;
            r.Add(Dividir(l, sep));
        }
        return r;
    }

    private static List<string> Dividir(string linha, char sep)
    {
        var campos = new List<string>();
        var atual = new StringBuilder();
        var aspas = false;
        for (var i = 0; i < linha.Length; i++)
        {
            var ch = linha[i];
            if (aspas)
            {
                if (ch == '"' && i + 1 < linha.Length && linha[i + 1] == '"') { atual.Append('"'); i++; }
                else if (ch == '"') aspas = false;
                else atual.Append(ch);
            }
            else if (ch == '"') aspas = true;
            else if (ch == sep) { campos.Add(atual.ToString().Trim()); atual.Clear(); }
            else atual.Append(ch);
        }
        campos.Add(atual.ToString().Trim());
        return campos;
    }

    // ---------- XLSX ----------

    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace NsRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace NsPkg = "http://schemas.openxmlformats.org/package/2006/relationships";

    private static List<PlanilhaAba> LerXlsx(byte[] dados)
    {
        using var zip = new ZipArchive(new MemoryStream(dados), ZipArchiveMode.Read);
        XDocument? Xml(string caminho)
        {
            var e = zip.GetEntry(caminho);
            if (e is null) return null;
            using var s = e.Open();
            return XDocument.Load(s);
        }

        // textos compartilhados
        var compartilhados = new List<string>();
        var ss = Xml("xl/sharedStrings.xml");
        if (ss is not null)
            foreach (var si in ss.Root!.Elements(Ns + "si"))
                compartilhados.Add(string.Concat(si.Descendants(Ns + "t").Select(t => t.Value)));

        // estilos que são datas (para converter o número serial do Excel em dd/mm/aaaa)
        var estilosData = EstilosDeData(Xml("xl/styles.xml"));

        // abas na ordem do arquivo
        var wb = Xml("xl/workbook.xml") ?? throw new InvalidOperationException("Arquivo .xlsx inválido.");
        var rels = Xml("xl/_rels/workbook.xml.rels");
        var alvos = rels?.Root?.Elements(NsPkg + "Relationship")
            .ToDictionary(r => (string?)r.Attribute("Id") ?? "", r => (string?)r.Attribute("Target") ?? "") ?? new();

        var abas = new List<PlanilhaAba>();
        var i = 0;
        foreach (var sheet in wb.Root!.Descendants(Ns + "sheet"))
        {
            i++;
            var nome = (string?)sheet.Attribute("name") ?? $"Planilha {i}";
            var rid = (string?)sheet.Attribute(NsRel + "id") ?? "";
            var alvo = alvos.TryGetValue(rid, out var t) ? t : $"worksheets/sheet{i}.xml";
            var caminho = alvo.StartsWith("/") ? alvo.TrimStart('/') : "xl/" + alvo;
            var doc = Xml(caminho);
            if (doc is null) continue;
            abas.Add(new PlanilhaAba(nome, LerAba(doc, compartilhados, estilosData)));
        }
        return abas;
    }

    /// <summary>Índices de estilo (cellXfs) cujo formato numérico é de data.</summary>
    private static HashSet<int> EstilosDeData(XDocument? estilos)
    {
        var r = new HashSet<int>();
        if (estilos?.Root is null) return r;
        var formatos = estilos.Root.Element(Ns + "numFmts")?.Elements(Ns + "numFmt")
            .ToDictionary(f => (int?)f.Attribute("numFmtId") ?? -1, f => (string?)f.Attribute("formatCode") ?? "") ?? new();
        var i = 0;
        foreach (var xf in estilos.Root.Element(Ns + "cellXfs")?.Elements(Ns + "xf") ?? Enumerable.Empty<XElement>())
        {
            var id = (int?)xf.Attribute("numFmtId") ?? 0;
            var ehData = id is (>= 14 and <= 22) or (>= 45 and <= 47)
                || (formatos.TryGetValue(id, out var cod) && FormatoEhData(cod));
            if (ehData) r.Add(i);
            i++;
        }
        return r;
    }

    private static bool FormatoEhData(string codigo)
    {
        // ignora trechos entre aspas e colchetes ([Red], [$-416]) e procura d, m ou y
        var limpo = System.Text.RegularExpressions.Regex.Replace(codigo, "\"[^\"]*\"|\\[[^\\]]*\\]", "").ToLowerInvariant();
        return limpo.IndexOfAny(new[] { 'd', 'y' }) >= 0 || (limpo.Contains('m') && !limpo.Contains('0') && !limpo.Contains('#'));
    }

    private static List<List<string>> LerAba(XDocument doc, List<string> compartilhados, HashSet<int> estilosData)
    {
        var linhas = new SortedDictionary<int, SortedDictionary<int, string>>();
        foreach (var row in doc.Descendants(Ns + "row"))
        {
            var nLinha = int.TryParse((string?)row.Attribute("r"), out var rr) ? rr : linhas.Count + 1;
            var celulas = new SortedDictionary<int, string>();
            var proxima = 0;
            foreach (var c in row.Elements(Ns + "c"))
            {
                var col = ColunaDe((string?)c.Attribute("r")) ?? proxima;
                proxima = col + 1;
                var tipo = (string?)c.Attribute("t");
                var v = c.Element(Ns + "v")?.Value;
                string texto = tipo switch
                {
                    "s" => int.TryParse(v, out var idx) && idx >= 0 && idx < compartilhados.Count ? compartilhados[idx] : "",
                    "inlineStr" => string.Concat(c.Descendants(Ns + "t").Select(t => t.Value)),
                    "b" => v == "1" ? "VERDADEIRO" : "FALSO",
                    "str" or "e" => v ?? "",
                    _ => estilosData.Contains((int?)c.Attribute("s") ?? -1) ? DataSerial(v) : FormatarNumero(v)
                };
                if (!string.IsNullOrWhiteSpace(texto)) celulas[col] = texto.Trim();
            }
            if (celulas.Count > 0) linhas[nLinha] = celulas;
        }

        var maxCol = linhas.Values.SelectMany(c => c.Keys).DefaultIfEmpty(-1).Max();
        return linhas.Values
            .Select(c => Enumerable.Range(0, maxCol + 1).Select(k => c.TryGetValue(k, out var s) ? s : "").ToList())
            .ToList();
    }

    private static string FormatarNumero(string? v)
    {
        if (string.IsNullOrEmpty(v)) return "";
        // números do Excel vêm com ponto; arredonda o ruído de ponto flutuante (0,1+0,2 = 0,30000000000000004)
        return double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? Math.Round(d, 6).ToString("0.######", CultureInfo.InvariantCulture)
            : v;
    }

    private static string DataSerial(string? v)
    {
        if (!double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return v ?? "";
        try
        {
            var data = DateTime.FromOADate(d);
            return data.TimeOfDay.TotalSeconds < 1 ? data.ToString("dd/MM/yyyy") : data.ToString("dd/MM/yyyy HH:mm");
        }
        catch (ArgumentException) { return v ?? ""; }
    }

    /// <summary>"C12" → 2 (base zero).</summary>
    private static int? ColunaDe(string? referencia)
    {
        if (string.IsNullOrEmpty(referencia)) return null;
        var n = 0;
        foreach (var ch in referencia)
        {
            if (!char.IsLetter(ch)) break;
            n = n * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
        }
        return n == 0 ? null : n - 1;
    }

    // ---------- Texto para a IA ----------

    /// <summary>Converte as abas em texto tabular (separado por " | "), respeitando um limite de caracteres.</summary>
    public static (string Texto, bool Cortado) ParaTexto(IEnumerable<PlanilhaAba> abas, int limite)
    {
        var sb = new StringBuilder();
        var cortado = false;
        foreach (var aba in abas)
        {
            var cab = $"### Planilha: {aba.Nome} ({aba.Linhas.Count} linhas × {aba.Colunas} colunas)\n";
            if (sb.Length + cab.Length > limite) { cortado = true; break; }
            sb.Append(cab);
            foreach (var linha in aba.Linhas)
            {
                // remove colunas vazias do fim
                var fim = linha.FindLastIndex(c => c.Length > 0);
                var txt = string.Join(" | ", linha.Take(fim + 1)) + "\n";
                if (sb.Length + txt.Length > limite) { cortado = true; break; }
                sb.Append(txt);
            }
            if (cortado) break;
            sb.Append('\n');
        }
        return (sb.ToString(), cortado);
    }
}
