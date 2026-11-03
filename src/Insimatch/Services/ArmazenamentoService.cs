using System.Globalization;
using System.Text;
using System.Text.Json;
using Insimatch.Models;
using Microsoft.JSInterop;

namespace Insimatch.Services;

/// <summary>Guarda os dados no navegador (localStorage) e cuida de importação/exportação.</summary>
public class ArmazenamentoService(IJSRuntime js)
{
    private const string ChaveColaboradores = "insimatch.colaboradores";
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public async Task<List<Colaborador>> CarregarColaboradoresAsync()
    {
        var txt = await js.InvokeAsync<string?>("insimatch.ler", ChaveColaboradores);
        if (string.IsNullOrWhiteSpace(txt)) return new();
        try { return JsonSerializer.Deserialize<List<Colaborador>>(txt, Json) ?? new(); }
        catch { return new(); }
    }

    public Task SalvarColaboradoresAsync(List<Colaborador> lista) =>
        js.InvokeVoidAsync("insimatch.gravar", ChaveColaboradores, JsonSerializer.Serialize(lista, Json)).AsTask();

    /// <summary>Lê qualquer objeto salvo em JSON no navegador.</summary>
    public async Task<T?> CarregarAsync<T>(string chave)
    {
        var txt = await js.InvokeAsync<string?>("insimatch.ler", chave);
        if (string.IsNullOrWhiteSpace(txt)) return default;
        try { return JsonSerializer.Deserialize<T>(txt, Json); }
        catch { return default; }
    }

    public Task SalvarAsync<T>(string chave, T valor) =>
        js.InvokeVoidAsync("insimatch.gravar", chave, JsonSerializer.Serialize(valor, Json)).AsTask();

    public async Task<string?> LerTextoAsync(string chave) => await js.InvokeAsync<string?>("insimatch.ler", chave);

    public Task GravarTextoAsync(string chave, string valor) => js.InvokeVoidAsync("insimatch.gravar", chave, valor).AsTask();

    // ---------- CSV (separador ";" para abrir direto no Excel em pt-BR) ----------

    private static readonly string[] Cabecalho =
        { "Nome", "Equipe", "Mes", "Volume", "MetaPro", "MetaPremium", "DiasConsiderados", "DesviosInternos", "DesviosExternos", "Parecer" };

    public static string GerarCsv(IEnumerable<Colaborador> lista)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';', Cabecalho));
        foreach (var c in lista)
        {
            sb.AppendLine(string.Join(';', new[]
            {
                Esc(c.Nome), Esc(c.Equipe), c.Mes, c.Volume.ToString(), c.MetaPro.ToString("0.##", new CultureInfo("pt-BR")), c.MetaPremium.ToString("0.##", new CultureInfo("pt-BR")),
                c.DiasConsiderados.ToString(),
                c.DesviosInternos.ToString(), c.DesviosExternos.ToString(), Esc(c.Parecer)
            }));
        }
        return sb.ToString();
    }

    public static string ModeloCsv() =>
        string.Join(';', Cabecalho) + "\n" +
        "Maria Souza;Atendimento;2026-10;3410;150;180;22;1;0;\n" +
        "João Lima;Atendimento;2026-10;4400;220;250;21;2;1;\n";

    public static List<Colaborador> LerCsv(string conteudo)
    {
        var linhas = conteudo.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var resultado = new List<Colaborador>();
        if (linhas.Length == 0) return resultado;

        var sep = linhas[0].Count(ch => ch == ';') >= linhas[0].Count(ch => ch == ',') ? ';' : ',';
        var cab = Dividir(linhas[0].TrimStart('﻿'), sep).Select(Normalizar).ToList();
        int Idx(params string[] nomes) => cab.FindIndex(h => nomes.Contains(h));

        int iNome = Idx("nome", "colaborador"), iEquipe = Idx("equipe", "time", "setor"),
            iMes = Idx("mes", "mesreferencia", "referencia"), iVol = Idx("volume", "volumedomes", "volumerealizado"),
            iPro = Idx("metapro", "pro"), iPrem = Idx("metapremium", "premium"),
            iDias = Idx("diasconsiderados", "diastrabalhados", "dias"),
            iDi = Idx("desviosinternos", "internos"), iDe = Idx("desviosexternos", "externos"),
            iPar = Idx("parecer", "observacao");

        foreach (var linha in linhas.Skip(1))
        {
            var col = Dividir(linha, sep);
            string Get(int i) => i >= 0 && i < col.Count ? col[i].Trim() : "";
            var nome = Get(iNome);
            if (string.IsNullOrWhiteSpace(nome)) continue;

            resultado.Add(new Colaborador
            {
                Nome = nome,
                Equipe = Get(iEquipe),
                Mes = string.IsNullOrWhiteSpace(Get(iMes)) ? DateTime.Today.ToString("yyyy-MM") : Get(iMes),
                Volume = (int)Num(Get(iVol)),
                MetaPro = Num(Get(iPro)),
                MetaPremium = Num(Get(iPrem)),
                DiasConsiderados = (int)Num(Get(iDias)),
                DesviosInternos = (int)Num(Get(iDi)),
                DesviosExternos = (int)Num(Get(iDe)),
                Parecer = Get(iPar)
            });
        }
        return resultado;
    }

    private static string Normalizar(string s)
    {
        var semAcento = new string(s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD)
            .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray());
        return semAcento.Replace(" ", "").Replace("_", "").Replace("-", "");
    }

    private static decimal Num(string s)
    {
        s = s.Trim();
        if (s.Length == 0) return 0;
        // aceita "97,5", "97.5", "1.320" e "1,320.5"
        if (s.Contains(',') && s.Contains('.'))
            s = s.LastIndexOf(',') > s.LastIndexOf('.') ? s.Replace(".", "").Replace(',', '.') : s.Replace(",", "");
        else if (s.Contains(','))
            s = s.Replace(',', '.');
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static string Esc(string s) =>
        s.Contains(';') || s.Contains('"') || s.Contains('\n') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

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
            else if (ch == sep) { campos.Add(atual.ToString()); atual.Clear(); }
            else atual.Append(ch);
        }
        campos.Add(atual.ToString());
        return campos;
    }
}
