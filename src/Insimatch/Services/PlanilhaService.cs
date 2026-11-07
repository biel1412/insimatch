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
            var resumo = Resumo(aba);
            if (resumo.Length > 0 && sb.Length + resumo.Length <= limite) sb.Append(resumo);
            sb.Append("Dados:\n");
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

    // ---------- números calculados pelo sistema (a IA só copia, não refaz contas) ----------

    /// <summary>Converte o texto de uma célula em número (aceita 1234.5, 1.234,56, R$ e %).</summary>
    public static double? Numero(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var t = texto.Trim().Replace("R$", "").Replace("%", "").Replace(" ", "").Replace("\u00a0", "");
        if (t.Length == 0) return null;
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"^-?\d+(\.\d+)?([eE]-?\d+)?$"))
            return double.Parse(t, CultureInfo.InvariantCulture);
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"^-?\d{1,3}(\.\d{3})*(,\d+)?$|^-?\d+,\d+$"))
            return double.Parse(t.Replace(".", "").Replace(',', '.'), CultureInfo.InvariantCulture);
        return null;
    }

    private static string Fmt(double v) => Math.Round(v, 4).ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>
    /// Resumo exato de uma aba: soma, média, mínimo e máximo das colunas numéricas,
    /// e os mesmos totais por grupo (ex.: por equipe). Linhas soltas/incompletas ficam de fora.
    /// </summary>
    public static string Resumo(PlanilhaAba aba)
    {
        if (aba.Linhas.Count < 3) return "";
        var cab = aba.Linhas[0];
        var dados = aba.Linhas.Skip(1).ToList();
        var ncol = aba.Colunas;
        string Cel(List<string> l, int c) => c < l.Count ? l[c].Trim() : "";
        string NomeCol(int c) => Cel(cab, c) is { Length: > 0 } n ? n : $"Coluna {c + 1}";

        // Coluna numérica: maioria dos valores preenchidos é número
        var numericas = new List<int>();
        for (var c = 0; c < ncol; c++)
        {
            var preench = dados.Select(l => Cel(l, c)).Where(v => v.Length > 0).ToList();
            if (preench.Count >= 2 && preench.Count(v => Numero(v) is not null) >= Math.Ceiling(preench.Count * 0.6))
                numericas.Add(c);
        }
        if (numericas.Count == 0) return "";

        // Linhas válidas: têm algum número e nenhum texto estranho em coluna numérica
        var validas = dados.Where(l =>
            numericas.Any(c => Numero(Cel(l, c)) is not null) &&
            numericas.All(c => Cel(l, c).Length == 0 || Numero(Cel(l, c)) is not null)).ToList();
        if (validas.Count < 2) return "";
        var ignoradas = dados.Count(l => l.Any(v => v.Trim().Length > 0)) - validas.Count;

        var sb = new StringBuilder();
        sb.Append($"Resumo calculado pelo sistema (números exatos; use-os em vez de refazer contas; {validas.Count} linhas de dados");
        sb.Append(ignoradas > 0 ? $", {ignoradas} linha(s) de total/vazias/soltas ignoradas):\n" : "):\n");
        foreach (var c in numericas)
        {
            var v = validas.Select(l => Numero(Cel(l, c))).Where(x => x is not null).Select(x => x!.Value).ToList();
            if (v.Count == 0) continue;
            sb.Append($"- {NomeCol(c)}: soma {Fmt(v.Sum())}; média {Fmt(v.Average())}; mín {Fmt(v.Min())}; máx {Fmt(v.Max())}; {v.Count} valores\n");
        }

        // Metas: liga cada coluna de meta à coluna numérica que ela mede (a mais próxima à esquerda)
        static bool EhMeta(string n) => System.Text.RegularExpressions.Regex.IsMatch(n.ToLowerInvariant(), @"\b(meta|objetivo|alvo|target|goal)\b");
        var pares = new List<(int Real, int Meta)>();
        foreach (var m in numericas.Where(c => EhMeta(NomeCol(c))))
        {
            var real = numericas.Where(c => c < m && !EhMeta(NomeCol(c))).DefaultIfEmpty(-1).Max();
            if (real < 0) real = numericas.Where(c => c > m && !EhMeta(NomeCol(c))).DefaultIfEmpty(-1).Min();
            if (real >= 0) pares.Add((real, m));
        }
        double Soma(IEnumerable<List<string>> ls, int c) => ls.Select(l => Numero(Cel(l, c)) ?? 0).Sum();
        foreach (var (real, meta) in pares)
        {
            var sm = Soma(validas, meta);
            if (sm == 0) continue;
            sb.Append($"- A coluna \"{NomeCol(meta)}\" é a meta de \"{NomeCol(real)}\": no campo {NomeCol(real)} use meta {Fmt(sm)} (não crie campo separado para a meta). Atingimento: {Fmt(Soma(validas, real) / sm * 100)}%\n");
        }

        // Totais por grupo: colunas de texto com poucas categorias que se repetem
        for (var g = 0; g < ncol; g++)
        {
            if (numericas.Contains(g)) continue;
            var cats = validas.Select(l => Cel(l, g)).Where(x => x.Length > 0).Distinct().ToList();
            if (cats.Count < 2 || cats.Count > 8 || cats.Count >= validas.Count) continue;
            sb.Append($"Por {NomeCol(g)}:\n");
            foreach (var cat in cats)
            {
                var linhas = validas.Where(l => Cel(l, g) == cat).ToList();
                var partes = numericas.Select(c =>
                {
                    var v = linhas.Select(l => Numero(Cel(l, c))).Where(x => x is not null).Select(x => x!.Value).ToList();
                    return v.Count == 0 ? null : $"{NomeCol(c)} soma {Fmt(v.Sum())}, média {Fmt(v.Average())}";
                }).Where(x => x is not null);
                var ating = pares.Select(p => (p, sm: Soma(linhas, p.Meta))).Where(x => x.sm != 0)
                    .Select(x => $"atingimento de {NomeCol(x.p.Real)} {Fmt(Soma(linhas, x.p.Real) / x.sm * 100)}%");
                sb.Append($"- {cat} ({linhas.Count}): {string.Join("; ", partes.Concat(ating))}\n");
            }
        }
        return sb.ToString();
    }
}
