using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Insimatch.Models;

/// <summary>Chave e modelo do Gemini, cadastrados na área do desenvolvedor e salvos só neste navegador.</summary>
public class ConfigIA
{
    public const string ChaveArmazenamento = "insitalent.gemini";
    public string Chave { get; set; } = "";
    /// <summary>Vazio = automático.</summary>
    public string Modelo { get; set; } = "";
}

/// <summary>O que a IA gerou: um painel (lista de campos) ou uma apresentação em slides.</summary>
public record ResultadoIA(string NomeAba, bool Slides, Cliente? Painel, Apresentacao? Deck);

/// <summary>Instruções para a IA montar um painel ou slides no formato da plataforma.</summary>
public static class PromptsIA
{
    private const string Campo = """
        {
          "nome": "texto curto",
          "grupo": "seção do painel (ex.: Operação, Financeiro)",
          "tipo": "inteiro | decimal | moeda | percentual | texto | data | simnao",
          "casas": 0,
          "unidade": "ex.: min, h, itens (só para inteiro/decimal; pode ser vazio)",
          "simbolo": "R$ (só para moeda)",
          "valor": 123.45,
          "texto": "só para tipo texto",
          "data": "aaaa-mm-dd (só para tipo data)",
          "booleano": true,
          "meta": 100,
          "sentido": "maior | menor",
          "visual": "numero | barra | anel | medidor | comparativo",
          "destaque": false
        }
        """;

    private const string Regras = """
        Regras:
        - Responda SOMENTE com um objeto JSON válido, sem texto antes ou depois.
        - Escreva tudo em português do Brasil.
        - Use apenas números que existem nos dados ou que são cálculos diretos deles (soma, média, percentual). Nunca invente valores.
        - Se houver muitas linhas, resuma: totais, médias, maiores e menores, comparação entre grupos ou períodos.
        - "valor" e "meta" são números puros (ponto como separador decimal, sem símbolo, sem separador de milhar).
        - Quando houver "Resumo calculado pelo sistema", use exatamente aqueles totais, médias, totais por grupo e percentuais de atingimento; não refaça contas.
        - Se os dados vierem de imagens, use somente o que está legível nelas; se um número estiver ilegível, deixe-o de fora em vez de adivinhar.
        - Ignore linhas vazias, linhas de "Total" sem números e linhas soltas que pareçam rascunho ou lixo (ex.: uma data com um valor isolado e "n/d"). Não as inclua em somas ou médias.
        - Percentual: use o número em escala 0–100 (ex.: 97.5 para 97,5%). Use o tipo "percentual" só para porcentagens de verdade (coluna com %, taxa, qualidade); notas e índices como NPS, CSAT ou nota 0–10 são "decimal" ou "inteiro".
        - Só preencha "meta" quando ela existir nos dados (ex.: coluna Meta/Objetivo/Alvo) ou for pedida nas instruções. Sem meta, use null.
        - Quando um indicador tiver meta, coloque a meta no "meta" do próprio campo (ex.: Volume com meta 14200). Não crie um campo separado só para a meta.
        - "sentido": "menor" para indicadores em que menos é melhor (tempo, custo, erros, desvios, reclamações); senão "maior".
        - "visual": "medidor" só para percentual; "barra", "anel" ou "comparativo" só quando houver meta; senão "numero".
        - "casas": 0 para contagens; 2 para moeda e percentual; 1 ou 2 para decimais.
        - Marque "destaque": true nos 2 ou 3 indicadores mais importantes.
        - Datas seriais do Excel (números entre 30000 e 60000 numa coluna de data) devem ser tratadas como datas.
        """;

    public static string Sistema(bool slides) => slides
        ? $$"""
            Você é um analista que transforma dados em apresentações claras para reuniões no Microsoft Teams.
            Monte uma APRESENTAÇÃO EM SLIDES no formato JSON abaixo.

            {
              "nome": "título da apresentação",
              "slides": [
                {
                  "titulo": "título do slide",
                  "subtitulo": "uma linha de apoio (opcional)",
                  "texto": "linhas curtas; comece cada tópico com '- ' e separe linhas com \n",
                  "campos": [ CAMPO ]
                }
              ]
            }

            Onde cada CAMPO é:
            {{Campo}}

            Estrutura sugerida: 1) slide de abertura com o resumo; 2 a 5) um slide por tema com até 4 campos cada
            e 2 a 4 tópicos explicando o que os números mostram; último) conclusões e próximos passos (só texto).
            Use de 4 a 8 slides.

            {{Regras}}
            """
        : $$"""
            Você é um analista que transforma dados em painéis de indicadores claros para reuniões no Microsoft Teams.
            Monte um PAINEL DE INDICADORES no formato JSON abaixo.

            {
              "nome": "nome do cliente, time ou assunto",
              "periodo": "período dos dados (ex.: Outubro de 2026), se houver",
              "descricao": "uma linha de contexto",
              "campos": [ CAMPO ],
              "graficos": [
                { "titulo": "texto", "tipo": "barras | colunas | rosca", "medida": "valor | meta", "campos": ["nome exato de campos acima"] }
              ],
              "parecer": "parecer formal e impessoal, de 3 a 6 frases, citando os números principais e recomendações"
            }

            Onde cada CAMPO é:
            {{Campo}}

            Use de 4 a 14 campos, agrupados em 2 a 4 grupos. Inclua 1 ou 2 gráficos:
            "medida": "meta" compara o percentual de atingimento de campos com meta (bom para unidades diferentes);
            "medida": "valor" compara valores da mesma unidade; "rosca" mostra a composição de um total.

            {{Regras}}
            """;

    public static string Usuario(string dados, string origem, string instrucoes, bool cortado)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Origem dos dados: {origem}.");
        if (cortado) sb.AppendLine("Atenção: os dados foram cortados por tamanho; trabalhe com o que foi enviado.");
        if (!string.IsNullOrWhiteSpace(instrucoes)) sb.AppendLine($"Instruções do usuário: {instrucoes.Trim()}");
        sb.AppendLine();
        sb.AppendLine("Dados:");
        sb.AppendLine(dados);
        return sb.ToString();
    }

    /// <summary>"Leitura da tela": transforma o conteúdo da aba aberta (o mesmo do relatório) em texto.</summary>
    public static string TelaParaTexto(RelatorioDados r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{r.Tipo}: {r.Titulo}");
        if (r.Info.Count > 0) sb.AppendLine(string.Join(" · ", r.Info));
        if (!string.IsNullOrWhiteSpace(r.StatusRotulo)) sb.AppendLine($"Resultado geral: {r.StatusRotulo}. {r.Resumo}");
        foreach (var k in r.Kpis) sb.AppendLine($"Indicador-chave: {k.Rotulo} = {k.Valor}{(k.Detalhe is null ? "" : $" ({k.Detalhe})")}");
        foreach (var s in r.Secoes)
        {
            sb.AppendLine();
            sb.AppendLine($"## {s.Titulo}");
            if (!string.IsNullOrWhiteSpace(s.Texto)) sb.AppendLine(s.Texto);
            var cols = s.Colunas ?? new[] { "Indicador", "Realizado", "Referência", "Situação" };
            if (s.Linhas.Count > 0) sb.AppendLine(string.Join(" | ", cols.Where(c => c.Length > 0)));
            foreach (var l in s.Linhas)
                sb.AppendLine($"{l.Indicador} | {l.Realizado} | {l.Referencia}{(l.StatusRotulo is null ? "" : $" | {l.StatusRotulo}")}");
        }
        if (!string.IsNullOrWhiteSpace(r.Parecer)) sb.AppendLine().AppendLine($"Parecer atual: {r.Parecer}");
        return sb.ToString();
    }
}

/// <summary>Converte a resposta da IA nos modelos da plataforma, tolerando pequenas variações.</summary>
public static class ConversorIA
{
    public static Cliente ParaPainel(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        var cl = new Cliente
        {
            Nome = Txt(r, "nome"),
            Periodo = Txt(r, "periodo"),
            Descricao = Txt(r, "descricao"),
            Parecer = Txt(r, "parecer")
        };
        cl.Campos = Lista(r, "campos").Select(ParaCampo).Where(c => c is not null).Select(c => c!).Take(20).ToList();
        foreach (var g in Lista(r, "graficos").Take(4))
        {
            var nomes = Lista(g, "campos").Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : "").ToList();
            var ids = nomes.Select(n => cl.Campos.FirstOrDefault(c => Igual(c.Nome, n))?.Id).Where(id => id is not null).Select(id => id!).ToList();
            var medida = Txt(g, "medida").ToLowerInvariant().Contains("meta") ? MedidaGrafico.PercentualMeta : MedidaGrafico.Valor;
            if (medida == MedidaGrafico.PercentualMeta) ids = ids.Where(id => cl.Campos.First(c => c.Id == id).TemMeta).ToList();
            if (ids.Count == 0) continue;
            cl.Graficos.Add(new GraficoCliente
            {
                Titulo = Txt(g, "titulo") is { Length: > 0 } t ? t : "Gráfico",
                Tipo = Txt(g, "tipo").ToLowerInvariant() switch { var x when x.Contains("col") => TipoGrafico.Colunas, var x when x.Contains("rosca") || x.Contains("pizza") => TipoGrafico.Rosca, _ => TipoGrafico.Barras },
                Medida = medida,
                Campos = ids
            });
        }
        if (cl.Campos.Count == 0) throw new InvalidOperationException("A IA não retornou nenhum indicador. Tente de novo ou dê mais instruções.");
        return cl;
    }

    public static Apresentacao ParaSlides(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        var ap = new Apresentacao { Nome = Txt(r, "nome") };
        foreach (var s in Lista(r, "slides").Take(15))
        {
            var slide = new Slide
            {
                Titulo = Txt(s, "titulo"),
                Subtitulo = Txt(s, "subtitulo"),
                Texto = Txt(s, "texto").Replace("\\n", "\n"),
                Campos = Lista(s, "campos").Select(ParaCampo).Where(c => c is not null).Select(c => c!).Take(6).ToList()
            };
            ap.Slides.Add(slide);
        }
        if (ap.Slides.Count == 0) throw new InvalidOperationException("A IA não retornou nenhum slide. Tente de novo ou dê mais instruções.");
        return ap;
    }

    private static CampoCliente? ParaCampo(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object) return null;
        var nome = Txt(e, "nome");
        if (string.IsNullOrWhiteSpace(nome)) return null;
        var tipo = Txt(e, "tipo").ToLowerInvariant() switch
        {
            var x when x.StartsWith("int") => TipoCampo.Inteiro,
            var x when x.StartsWith("dec") || x == "numero" || x == "número" => TipoCampo.Decimal,
            var x when x.StartsWith("moe") || x.Contains("valor") || x.Contains("r$") => TipoCampo.Moeda,
            var x when x.StartsWith("perc") || x == "%" => TipoCampo.Percentual,
            var x when x.StartsWith("dat") => TipoCampo.Data,
            var x when x.Contains("sim") || x.StartsWith("bool") => TipoCampo.SimNao,
            var x when x.StartsWith("tex") => TipoCampo.Texto,
            _ => Num(e, "valor").HasValue ? TipoCampo.Decimal : TipoCampo.Texto
        };
        var c = new CampoCliente
        {
            Nome = nome.Length > 60 ? nome[..60] : nome,
            Grupo = Txt(e, "grupo"),
            Tipo = tipo,
            Casas = Math.Clamp((int)(Num(e, "casas") ?? (tipo is TipoCampo.Moeda or TipoCampo.Percentual ? 2 : tipo == TipoCampo.Decimal ? 1 : 0)), 0, 4),
            Unidade = Txt(e, "unidade"),
            Simbolo = Txt(e, "simbolo") is { Length: > 0 } sim ? sim : "R$",
            Valor = Num(e, "valor"),
            Texto = Txt(e, "texto"),
            Booleano = Bool(e, "booleano") ?? Bool(e, "valor") ?? false,
            Meta = Num(e, "meta"),
            Sentido = Txt(e, "sentido").ToLowerInvariant().StartsWith("men") ? SentidoMeta.MenorMelhor : SentidoMeta.MaiorMelhor,
            Destaque = Bool(e, "destaque") ?? false
        };
        if (tipo == TipoCampo.Texto && string.IsNullOrWhiteSpace(c.Texto)) c.Texto = Txt(e, "valor");
        if (tipo == TipoCampo.Data && DateTime.TryParse(Txt(e, "data") is { Length: > 0 } d ? d : Txt(e, "valor"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) c.Data = dt;
        // percentual em escala 0–1 → 0–100
        if (tipo == TipoCampo.Percentual && c.Valor is > 0 and <= 1 && (c.Meta is null || c.Meta <= 1))
        {
            c.Valor *= 100;
            if (c.Meta.HasValue) c.Meta *= 100;
        }
        c.Visual = Txt(e, "visual").ToLowerInvariant() switch
        {
            "barra" => VisualCampo.Barra,
            "anel" => VisualCampo.Anel,
            "medidor" when tipo == TipoCampo.Percentual => VisualCampo.Medidor,
            "comparativo" => VisualCampo.Comparativo,
            _ => tipo == TipoCampo.Percentual ? VisualCampo.Medidor : VisualCampo.Numero
        };
        AvaliacaoCliente.Normalizar(c);
        return c;
    }

    // ---------- leitura tolerante ----------

    private static JsonElement? Prop(JsonElement e, string nome)
    {
        if (e.ValueKind != JsonValueKind.Object) return null;
        foreach (var p in e.EnumerateObject())
            if (Igual(p.Name, nome)) return p.Value;
        return null;
    }

    private static string Txt(JsonElement e, string nome) => Prop(e, nome) switch
    {
        { ValueKind: JsonValueKind.String } v => v.GetString()?.Trim() ?? "",
        { ValueKind: JsonValueKind.Number } v => v.GetRawText(),
        { ValueKind: JsonValueKind.True } => "true",
        { ValueKind: JsonValueKind.False } => "false",
        _ => ""
    };

    private static decimal? Num(JsonElement e, string nome)
    {
        var v = Prop(e, nome);
        if (v is null) return null;
        if (v.Value.ValueKind == JsonValueKind.Number && v.Value.TryGetDecimal(out var d)) return d;
        if (v.Value.ValueKind != JsonValueKind.String) return null;
        var s = (v.Value.GetString() ?? "").Replace("R$", "").Replace("%", "").Replace(" ", "").Trim();
        if (s.Length == 0) return null;
        if (s.Contains(',') && s.Contains('.'))
            s = s.LastIndexOf(',') > s.LastIndexOf('.') ? s.Replace(".", "").Replace(',', '.') : s.Replace(",", "");
        else if (s.Contains(','))
            s = s.Replace(',', '.');
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : null;
    }

    private static bool? Bool(JsonElement e, string nome) => Prop(e, nome) switch
    {
        { ValueKind: JsonValueKind.True } => true,
        { ValueKind: JsonValueKind.False } => false,
        { ValueKind: JsonValueKind.String } v => (v.GetString() ?? "").Trim().ToLowerInvariant() switch
        {
            "sim" or "true" or "s" or "yes" or "verdadeiro" => true,
            "não" or "nao" or "false" or "n" or "no" or "falso" => false,
            _ => null
        },
        _ => null
    };

    private static IEnumerable<JsonElement> Lista(JsonElement e, string nome) =>
        Prop(e, nome) is { ValueKind: JsonValueKind.Array } a ? a.EnumerateArray().ToList() : Enumerable.Empty<JsonElement>();

    private static bool Igual(string? a, string? b) =>
        string.Equals(Normalizar(a), Normalizar(b), StringComparison.Ordinal);

    private static string Normalizar(string? s) =>
        new string((s ?? "").Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD)
            .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray());
}
