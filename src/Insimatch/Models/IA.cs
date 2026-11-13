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
/// <summary>Resultado da IA: Formato = "painel", "slides" ou "fluxo".</summary>
public record ResultadoIA(string NomeAba, string Formato, Cliente? Painel, Apresentacao? Deck, Fluxo? Fluxo = null)
{
    public bool Slides => Formato == "slides";
}

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
        - Se os dados vierem de PDF, use os números e textos do documento; em tabelas, respeite os totais que o próprio PDF já traz.
        - Se os dados vierem de imagens, use somente o que está legível nelas; se um número estiver ilegível, deixe-o de fora em vez de adivinhar.
        - Se não houver arquivo (só instruções em texto), os dados são as instruções: use os números citados nelas. Se o usuário não der números, monte a estrutura pedida com "valor": null e "meta": null (e tópicos/textos sem números inventados), para ele preencher depois.
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

    /// <summary>Instrução extra quando há PDF: a IA indica onde estão as figuras para o site recortá-las.</summary>
    public const string ImagensPdf = """

        IMAGENS DO PDF: quando o PDF tiver gráficos, fotos, diagramas, logotipos ou tabelas visuais que ajudem a explicar
        um slide, inclua no slide o campo "imagensPdf" (no máximo 2 por slide):
          "imagensPdf": [{ "pdf": 1, "pagina": 3, "recorte": [ymin, xmin, ymax, xmax], "legenda": "texto curto" }]
        - "pdf": número do PDF na ordem em que foi enviado (1 ou 2); "pagina": começa em 1.
        - "recorte": área da figura na página em coordenadas de 0 a 1000 (0,0 = canto superior esquerdo), cobrindo só a
          figura com uma pequena margem. Use null para usar a página inteira (ex.: um slide que já é uma imagem).
        - Não use para páginas só de texto, nem repita a mesma figura em vários slides. Se não houver figura útil, omita o campo.
        """;

    public static string Sistema(bool slides, bool comPdf = false) => SistemaBase(slides) + (slides && comPdf ? ImagensPdf : "");

    /// <summary>Instruções por formato: "painel", "slides" ou "fluxo".</summary>
    public static string Sistema(string formato, bool comPdf = false) =>
        formato switch
        {
            "fluxo" => SistemaFluxo,
            "excel" => SistemaExcel,
            _ => Sistema(formato == "slides", comPdf)
        };

    private const string SistemaExcel = """
        Você é um especialista em Excel que transforma dados, documentos e pedidos em PLANILHAS prontas para uso
        no trabalho (controles, relatórios, metas, cadastros). Monte uma PLANILHA no formato JSON abaixo.

        {
          "nome": "nome curto do arquivo",
          "abas": [
            {
              "nome": "nome da aba (até 31 caracteres, sem : \ / ? * [ ])",
              "colunas": [
                { "titulo": "Colaborador", "tipo": "texto", "largura": 22 },
                { "titulo": "Volume", "tipo": "inteiro", "largura": 12 },
                { "titulo": "Meta", "tipo": "inteiro", "largura": 12 },
                { "titulo": "Atingimento", "tipo": "percentual", "largura": 14 }
              ],
              "linhas": [
                ["Ana Souza", 1250, 1200, "=IFERROR(B2/C2,0)"],
                ["Bruno Lima", 980, 1200, "=IFERROR(B3/C3,0)"]
              ],
              "total": ["Total", "=SUM(B2:B3)", "=SUM(C2:C3)", "=IFERROR(B4/C4,0)"],
              "destaques": [
                { "coluna": "Atingimento", "se": "<", "valor": 1, "cor": "vermelho" },
                { "coluna": "Atingimento", "se": ">=", "valor": 1, "cor": "verde" }
              ]
            }
          ]
        }

        Posições (essencial para as fórmulas):
        - A linha 1 de cada aba é o cabeçalho (os títulos das colunas). A 1ª coluna é A, a 2ª é B, e assim por diante.
        - O item 0 de "linhas" fica na linha 2 da planilha, o item 1 na linha 3... (linha da planilha = posição + 2).
        - "total" (opcional) fica logo abaixo da última linha de dados: com N linhas de dados, o total fica na linha N + 2.
        - Cada linha tem exatamente um valor por coluna, na mesma ordem das colunas (use "" para célula vazia).

        Tipos de coluna: "texto", "inteiro", "numero" (2 casas), "moeda" (R$), "percentual", "data".
        - Percentual: escreva como fração (99,94% = 0.9994; 150% = 1.5).
        - Data: "AAAA-MM-DD". Moeda e números: só o número, com ponto decimal (1234.5), sem "R$" nem separador de milhar.

        Fórmulas:
        - Comece com "=" e escreva em INGLÊS com vírgula separando argumentos, como o arquivo .xlsx exige:
          SUM, AVERAGE, MIN, MAX, COUNT, COUNTA, COUNTIF, COUNTIFS, SUMIF, SUMIFS, AVERAGEIF, IF, AND, OR,
          IFERROR, ROUND, VLOOKUP, INDEX, MATCH, TODAY, TEXT. Não use XLOOKUP, FILTER, LET, IFS nem funções novas.
        - Use fórmulas para tudo que é calculado (totais, médias, percentuais, diferenças, status), em vez de escrever o resultado.
          Assim o Excel calcula sem erro e atualiza quando o usuário mudar um dado.
        - Divisões sempre com IFERROR(...,0). Textos dentro de fórmula entre aspas duplas: =IF(D2>=1,"Na meta","Abaixo").
        - Para citar outra aba: 'Nome da aba'!B2 (com aspas simples).

        Destaques (formatação condicional, opcional): "coluna" é o título da coluna; "se" é <, <=, >, >=, = ou <>;
        "valor" é número (na mesma escala da coluna, ex.: 0.9994 para 99,94%) ou texto; "cor": vermelho, verde, amarelo ou azul.

        Regras:
        - Responda SOMENTE com um objeto JSON válido, sem texto antes ou depois. Tudo em português do Brasil (exceto nomes de funções).
        - De 1 a 6 abas; até 20 colunas e até 300 linhas por aba. Se os dados forem maiores, priorize o que o pedido pede
          e resuma o restante numa aba de resumo.
        - Use os números dos dados exatamente como estão; não invente nomes nem valores. Se o pedido for um modelo em branco,
          crie as colunas e fórmulas com algumas linhas vazias ("") prontas para preencher.
        - Se houver um "Resumo calculado" nos dados, ele pode virar uma aba de resumo, mas prefira fórmulas sobre a aba de dados.
        - Títulos de coluna curtos e claros; larguras entre 8 e 50 (aproximadamente o número de caracteres).
        """;

    private const string SistemaFluxo = """
        Você é um analista de processos que transforma descrições, documentos e dados em FLUXOGRAMAS claros
        para reuniões no Microsoft Teams. Monte um FLUXOGRAMA no formato JSON abaixo.

        {
          "nome": "nome curto do processo",
          "descricao": "uma linha explicando o objetivo do processo",
          "etapas": [
            {
              "id": "e1",
              "texto": "texto curto da etapa (até 8 palavras, verbo no infinitivo: 'Receber solicitação')",
              "tipo": "inicio | etapa | decisao | documento | dados | fim",
              "proximos": [ { "para": "e2", "rotulo": "" } ]
            }
          ]
        }

        Tipos:
        - "inicio": o gatilho que abre o processo (exatamente 1).
        - "etapa": uma ação ou tarefa.
        - "decisao": uma pergunta com 2 ou mais saídas; escreva o texto como pergunta ("Documentação completa?")
          e dê "rotulo" a cada saída ("Sim", "Não", ou a condição).
        - "documento": geração ou envio de documento, relatório, e-mail, formulário.
        - "dados": registro ou consulta em sistema, planilha ou base de dados.
        - "fim": desfecho do processo (1 ou mais; ex.: "Concluído", "Cancelado").

        Regras:
        - Responda SOMENTE com um objeto JSON válido, sem texto antes ou depois.
        - Escreva tudo em português do Brasil.
        - Use de 4 a 30 etapas; ids únicos ("e1", "e2"...); todo "para" deve apontar para um id existente.
        - Todo caminho deve chegar a uma etapa "fim". Etapas "fim" não têm próximos.
        - Laços (voltar a uma etapa anterior, ex.: corrigir e reenviar) são permitidos quando fizerem parte do processo.
        - Baseie-se no processo descrito nos dados e nas instruções. Complete apenas passos óbvios que ligam duas etapas;
          não invente regras, prazos, áreas ou sistemas que não foram citados.
        - Se os dados forem uma tabela (ex.: etapas, responsáveis, status), use a ordem e as dependências que ela indica.
        - Prefira textos curtos; detalhes longos não cabem nas caixas do fluxograma.
        """;

    private static string SistemaBase(bool slides) => slides
        ? $$"""
            Você é um analista que transforma dados em apresentações claras para reuniões no Microsoft Teams.
            Monte uma APRESENTAÇÃO EM SLIDES no formato JSON abaixo.

            {
              "nome": "título da apresentação",
              "tema": "claro | papel | menta | noite | grafite | executivo | oceano | aurora | vinho | floresta (ou vazio)",
              "slides": [
                {
                  "estrutura": "capa | secao | colunas | destaque | vazio (automática)",
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
            Use de 3 a 32 slides: a quantidade acompanha o volume de conteúdo (sem slides vazios ou repetidos). Se o usuário pedir uma quantidade, siga-a (máximo 32).
            Estruturas: "capa" no primeiro slide (título + subtítulo curtos); "secao" para separar partes;
            "colunas" para listas longas de tópicos; "destaque" para uma mensagem/conclusão curta e forte;
            vazio nos slides com campos. Escolha um "tema" que combine com o assunto (ou o pedido pelo usuário).

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

    /// <summary>Converte o JSON da IA em fluxograma (ids novos, ligações inválidas descartadas).</summary>
    public static Fluxo ParaFluxo(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        var f = new Fluxo { Nome = Txt(r, "nome"), Descricao = Txt(r, "descricao") };
        var mapa = new Dictionary<string, NoFluxo>(StringComparer.OrdinalIgnoreCase);
        var pendentes = new List<(NoFluxo No, JsonElement Proximos)>();
        var i = 0;
        foreach (var e in Lista(r, "etapas").Take(40))
        {
            var texto = Txt(e, "texto").Trim();
            if (texto.Length == 0) continue;
            if (texto.Length > 90) texto = texto[..87].TrimEnd() + "…";
            var no = new NoFluxo { Texto = texto, Tipo = TipoFluxo(Txt(e, "tipo")) };
            var id = Txt(e, "id").Trim();
            if (id.Length == 0 || mapa.ContainsKey(id)) id = $"_auto{i}";
            mapa[id] = no;
            f.Nos.Add(no);
            if (Prop(e, "proximos") is { ValueKind: JsonValueKind.Array } prox) pendentes.Add((no, prox));
            i++;
        }
        foreach (var (no, prox) in pendentes)
        {
            foreach (var p in prox.EnumerateArray())
            {
                string destino, rotulo = "";
                if (p.ValueKind == JsonValueKind.String) destino = p.GetString() ?? "";
                else if (p.ValueKind == JsonValueKind.Object) { destino = Txt(p, "para"); if (destino == "") destino = Txt(p, "destino"); rotulo = Txt(p, "rotulo"); }
                else continue;
                if (!mapa.TryGetValue(destino.Trim(), out var alvo) || alvo == no) continue;
                if (no.Proximos.Any(x => x.Destino == alvo.Id)) continue;
                no.Proximos.Add(new Ligacao { Destino = alvo.Id, Rotulo = rotulo.Length > 30 ? rotulo[..30] : rotulo });
            }
        }
        // Sem nenhuma ligação (IA devolveu só a lista): liga em sequência
        if (f.Nos.Count > 1 && f.Nos.All(n => n.Proximos.Count == 0))
            for (var k = 0; k < f.Nos.Count - 1; k++) f.Nos[k].Proximos.Add(new Ligacao { Destino = f.Nos[k + 1].Id });
        if (f.Nos.Count == 0) throw new JsonException("Fluxograma sem etapas.");
        return f;
    }

    /// <summary>Converte o JSON da IA numa planilha Excel (limites e tipos tolerantes).</summary>
    public static PlanilhaExcel ParaExcel(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        var p = new PlanilhaExcel { Nome = Txt(r, "nome") };
        foreach (var a in Lista(r, "abas").Take(10))
        {
            var aba = new AbaExcel { Nome = Txt(a, "nome") };
            foreach (var c in Lista(a, "colunas").Take(30))
            {
                var titulo = c.ValueKind == JsonValueKind.String ? c.GetString() ?? "" : Txt(c, "titulo");
                aba.Colunas.Add(new ColunaExcel
                {
                    Titulo = titulo.Trim(),
                    Tipo = TipoColuna(c.ValueKind == JsonValueKind.Object ? Txt(c, "tipo") : ""),
                    Largura = c.ValueKind == JsonValueKind.Object ? (double)(Num(c, "largura") ?? 0) : 0
                });
            }
            foreach (var l in Lista(a, "linhas").Take(1000))
                if (l.ValueKind == JsonValueKind.Array) aba.Linhas.Add(LinhaExcel(l, aba.Colunas.Count));
            if (Prop(a, "total") is { ValueKind: JsonValueKind.Array } t) aba.Total = LinhaExcel(t, aba.Colunas.Count);
            if (aba.Colunas.Count == 0)
            {
                // Sem colunas declaradas: usa a primeira linha como cabeçalho
                if (aba.Linhas.Count == 0) continue;
                aba.Colunas = aba.Linhas[0].Select(v => new ColunaExcel { Titulo = Convert.ToString(v, CultureInfo.InvariantCulture) ?? "" }).ToList();
                aba.Linhas.RemoveAt(0);
            }
            foreach (var d in Lista(a, "destaques").Take(20))
            {
                var nomeCol = Txt(d, "coluna");
                var idx = aba.Colunas.FindIndex(c => Igual(c.Titulo, nomeCol));
                if (idx < 0 && Prop(d, "coluna") is { ValueKind: JsonValueKind.Number } n && n.TryGetInt32(out var i)) idx = i;
                if (idx < 0 || idx >= aba.Colunas.Count) continue;
                var op = (Txt(d, "se") is { Length: > 0 } se ? se : Txt(d, "operador")).Trim();
                if (op is not ("<" or "<=" or ">" or ">=" or "=" or "<>")) continue;
                object? valor = Prop(d, "valor") switch
                {
                    { ValueKind: JsonValueKind.Number } v => v.GetDouble(),
                    { ValueKind: JsonValueKind.String } v => v.GetString(),
                    _ => null
                };
                if (valor is null) continue;
                aba.Destaques.Add(new DestaqueExcel { Coluna = idx, Operador = op, Valor = valor, Cor = Normalizar(Txt(d, "cor")) is { Length: > 0 } cor ? cor : "vermelho" });
            }
            p.Abas.Add(aba);
        }
        if (p.Abas.Count == 0) throw new JsonException("Planilha sem abas.");
        return p;
    }

    private static List<object?> LinhaExcel(JsonElement l, int colunas)
    {
        var lista = new List<object?>();
        foreach (var v in l.EnumerateArray().Take(colunas == 0 ? 30 : colunas))
            lista.Add(v.ValueKind switch
            {
                JsonValueKind.Number => v.GetDouble(),
                JsonValueKind.String => v.GetString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            });
        return lista;
    }

    private static string TipoColuna(string t) => Normalizar(t) switch
    {
        "inteiro" or "int" or "integer" or "quantidade" => "inteiro",
        "numero" or "number" or "decimal" or "num" => "numero",
        "moeda" or "currency" or "valor" or "dinheiro" or "r$" => "moeda",
        "percentual" or "percent" or "porcentagem" or "%" => "percentual",
        "data" or "date" => "data",
        _ => "texto"
    };

    private static TipoNo TipoFluxo(string t) => Normalizar(t) switch
    {
        "inicio" or "start" or "comeco" => TipoNo.Inicio,
        "decisao" or "pergunta" or "condicao" or "decision" => TipoNo.Decisao,
        "documento" or "doc" or "relatorio" or "document" => TipoNo.Documento,
        "dados" or "sistema" or "registro" or "data" or "banco" => TipoNo.Dados,
        "fim" or "final" or "termino" or "end" or "encerramento" => TipoNo.Fim,
        _ => TipoNo.Etapa
    };

    public static Apresentacao ParaSlides(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        var tema = Txt(r, "tema").Trim().ToLowerInvariant();
        var ap = new Apresentacao { Nome = Txt(r, "nome"), Tema = TemasSlides.Lista.Any(t => t.Id == tema) ? tema : "" };
        string[] estruturas = { "capa", "secao", "colunas", "destaque" };
        foreach (var s in Lista(r, "slides").Take(32))
        {
            var slide = new Slide
            {
                Titulo = Txt(s, "titulo"),
                Subtitulo = Txt(s, "subtitulo"),
                Estrutura = estruturas.Contains(Txt(s, "estrutura").Trim().ToLowerInvariant()) ? Txt(s, "estrutura").Trim().ToLowerInvariant() : "",
                Texto = Txt(s, "texto").Replace("\\n", "\n"),
                Campos = Lista(s, "campos").Select(ParaCampo).Where(c => c is not null).Select(c => c!).Take(6).ToList(),
                ImagensPdfIA = Lista(s, "imagensPdf").Select(ParaPedidoPdf).Where(x => x is not null).Select(x => x!).Take(2).ToList()
            };
            ap.Slides.Add(slide);
        }
        if (ap.Slides.Count == 0) throw new InvalidOperationException("A IA não retornou nenhum slide. Tente de novo ou dê mais instruções.");
        return ap;
    }

    private static PedidoImagemPdf? ParaPedidoPdf(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object) return null;
        var pagina = (int)(Num(e, "pagina") ?? 0);
        if (pagina < 1) return null;
        int[]? recorte = null;
        if (Prop(e, "recorte") is { ValueKind: JsonValueKind.Array } arr)
        {
            var v = arr.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Number).Select(x => (int)Math.Round(x.GetDouble())).ToArray();
            // Aceita 0–1 (fração) além de 0–1000
            if (v.Length == 4 && v.All(x => x is >= 0 and <= 1) && arr.EnumerateArray().Any(x => x.GetDouble() is > 0 and < 1))
                v = arr.EnumerateArray().Select(x => (int)Math.Round(x.GetDouble() * 1000)).ToArray();
            if (v.Length == 4 && v[2] > v[0] && v[3] > v[1]) recorte = v.Select(x => Math.Clamp(x, 0, 1000)).ToArray();
        }
        return new PedidoImagemPdf
        {
            Pdf = Math.Max(1, (int)(Num(e, "pdf") ?? 1)),
            Pagina = pagina,
            Recorte = recorte,
            Legenda = Txt(e, "legenda")
        };
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
