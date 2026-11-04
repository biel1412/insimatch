using System.Globalization;
using System.Text.Json;

namespace Insimatch.Models;

public enum TipoCampo { Inteiro, Decimal, Moeda, Percentual, Texto, Data, SimNao }

public enum SentidoMeta { MaiorMelhor, MenorMelhor }

public enum VisualCampo { Numero, Barra, Anel, Medidor, Comparativo }

public enum TipoGrafico { Barras, Colunas, Rosca }

public enum MedidaGrafico { Valor, PercentualMeta }

public enum TomParecer { Formal, Topicos }

/// <summary>Um indicador livre do cliente. O usuário define nome, tipo, formato, meta e visual.</summary>
public class CampoCliente
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public string Nome { get; set; } = "";
    public string Grupo { get; set; } = "";
    public TipoCampo Tipo { get; set; } = TipoCampo.Inteiro;

    /// <summary>Casas decimais (0 = sem vírgula).</summary>
    public int Casas { get; set; }

    /// <summary>Separador de milhar (1.234 x 1234).</summary>
    public bool SeparadorMilhar { get; set; } = true;

    public string Simbolo { get; set; } = "R$";
    public string Unidade { get; set; } = "";

    public decimal? Valor { get; set; }
    public string Texto { get; set; } = "";
    public DateTime? Data { get; set; }
    public bool Booleano { get; set; }

    public decimal? Meta { get; set; }
    public SentidoMeta Sentido { get; set; } = SentidoMeta.MaiorMelhor;

    /// <summary>Percentual da meta a partir do qual o indicador é "próximo da meta".</summary>
    public decimal Tolerancia { get; set; } = 90;

    public VisualCampo Visual { get; set; } = VisualCampo.Numero;
    public bool Destaque { get; set; }

    public bool Numerico => Tipo is TipoCampo.Inteiro or TipoCampo.Decimal or TipoCampo.Moeda or TipoCampo.Percentual;
    public bool TemMeta => Numerico && Meta.HasValue && Valor.HasValue;
}

/// <summary>Um gráfico do painel, comparando vários campos.</summary>
public class GraficoCliente
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public string Titulo { get; set; } = "Novo gráfico";
    public TipoGrafico Tipo { get; set; } = TipoGrafico.Barras;
    public MedidaGrafico Medida { get; set; } = MedidaGrafico.PercentualMeta;
    public List<string> Campos { get; set; } = new();
    public bool MostrarMeta { get; set; } = true;
}

public class Cliente
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Nome { get; set; } = "";
    public string Periodo { get; set; } = Datas.MesPorExtenso(DateTime.Today.ToString("yyyy-MM"));
    public string Descricao { get; set; } = "";
    public List<CampoCliente> Campos { get; set; } = new();
    public List<GraficoCliente> Graficos { get; set; } = new();

    public string Parecer { get; set; } = "";
    public TomParecer Tom { get; set; } = TomParecer.Formal;
    public bool ParecerIncluiSemMeta { get; set; } = true;

    public string Iniciais => Datas.Iniciais(Nome);

    /// <summary>Cópia completa (campos e gráficos incluídos).</summary>
    public Cliente Clonar() => JsonSerializer.Deserialize<Cliente>(JsonSerializer.Serialize(this))!;

    /// <summary>Mesma estrutura de campos e gráficos, com os valores zerados — para o próximo período.</summary>
    public Cliente NovoPeriodo()
    {
        var c = Clonar();
        c.Id = Guid.NewGuid().ToString("N");
        c.Parecer = "";
        foreach (var f in c.Campos)
        {
            f.Valor = null;
            f.Texto = "";
            f.Data = null;
            f.Booleano = false;
        }
        return c;
    }
}

public static class AvaliacaoCliente
{
    private static readonly CultureInfo Br = new("pt-BR");

    public static string NomeTipo(TipoCampo t) => t switch
    {
        TipoCampo.Inteiro => "Número inteiro",
        TipoCampo.Decimal => "Número decimal",
        TipoCampo.Moeda => "Valor (moeda)",
        TipoCampo.Percentual => "Percentual",
        TipoCampo.Texto => "Texto",
        TipoCampo.Data => "Data",
        _ => "Sim / Não"
    };

    public static string IconeTipo(TipoCampo t) => t switch
    {
        TipoCampo.Inteiro => "123",
        TipoCampo.Decimal => "1,5",
        TipoCampo.Moeda => "R$",
        TipoCampo.Percentual => "%",
        TipoCampo.Texto => "Aa",
        TipoCampo.Data => "📅",
        _ => "✓"
    };

    public static string NomeVisual(VisualCampo v) => v switch
    {
        VisualCampo.Numero => "Número em destaque",
        VisualCampo.Barra => "Barra de progresso",
        VisualCampo.Anel => "Anel (% da meta)",
        VisualCampo.Medidor => "Medidor",
        _ => "Realizado × meta"
    };

    public static string NomeGrafico(TipoGrafico t) => t switch
    {
        TipoGrafico.Barras => "Barras horizontais",
        TipoGrafico.Colunas => "Colunas",
        _ => "Rosca (composição)"
    };

    /// <summary>Formata um número conforme o campo (casas, separador, moeda, percentual, unidade).</summary>
    public static string Numero(CampoCliente c, decimal v)
    {
        var casas = c.Tipo == TipoCampo.Inteiro ? 0 : Math.Clamp(c.Casas, 0, 6);
        var fmt = (c.SeparadorMilhar ? "N" : "F") + casas;
        var n = v.ToString(fmt, Br);
        return c.Tipo switch
        {
            // espaço inseparável: "R$ 1.234,50" nunca quebra entre símbolo e número
            TipoCampo.Moeda => $"{(string.IsNullOrWhiteSpace(c.Simbolo) ? "" : c.Simbolo.Trim() + "\u00A0")}{n}",
            TipoCampo.Percentual => n + "%",
            _ => string.IsNullOrWhiteSpace(c.Unidade) ? n : $"{n}\u00A0{c.Unidade.Trim()}"
        };
    }

    public static string Valor(CampoCliente c) => c.Tipo switch
    {
        TipoCampo.Texto => string.IsNullOrWhiteSpace(c.Texto) ? "—" : c.Texto,
        TipoCampo.Data => c.Data?.ToString("dd/MM/yyyy") ?? "—",
        TipoCampo.SimNao => c.Booleano ? "Sim" : "Não",
        _ => c.Valor.HasValue ? Numero(c, c.Valor.Value) : "—"
    };

    public static string MetaTexto(CampoCliente c) =>
        c.Meta.HasValue ? (c.Sentido == SentidoMeta.MenorMelhor ? "≤\u00A0" : "≥\u00A0") + Numero(c, c.Meta.Value) : "—";

    /// <summary>Meta escrita por extenso, para o parecer ("meta de 12.000" / "meta máxima de 6,0 min").</summary>
    public static string MetaExtenso(CampoCliente c) =>
        !c.Meta.HasValue ? "" : (c.Sentido == SentidoMeta.MenorMelhor ? "meta máxima de " : "meta de ") + Numero(c, c.Meta.Value);

    public static string Plural(int n, string singular, string plural) => n == 1 ? $"1 {singular}" : $"{n} {plural}";

    /// <summary>Atingimento em % (maior é melhor: valor ÷ meta; menor é melhor: meta ÷ valor).</summary>
    public static decimal Atingimento(CampoCliente c)
    {
        if (!c.TemMeta) return 0;
        var v = c.Valor!.Value;
        var m = c.Meta!.Value;
        if (c.Sentido == SentidoMeta.MaiorMelhor) return m == 0 ? 100 : v / m * 100;
        if (v <= 0) return m >= 0 ? 100 : 0;
        return m / v * 100;
    }

    public static Status StatusCampo(CampoCliente c)
    {
        if (!c.TemMeta) return Status.Neutro;
        var v = c.Valor!.Value;
        var m = c.Meta!.Value;
        var tol = Math.Clamp(c.Tolerancia, 0, 100) / 100m;
        if (c.Sentido == SentidoMeta.MaiorMelhor)
            return v >= m ? Status.Excelente : v >= m * tol ? Status.Atencao : Status.Critico;
        return v <= m ? Status.Excelente : v <= m * (2 - tol) ? Status.Atencao : Status.Critico;
    }

    public static string RotuloCampo(CampoCliente c) => StatusCampo(c) switch
    {
        Status.Excelente => "Na meta",
        Status.Atencao => "Próximo da meta",
        Status.Critico => "Fora da meta",
        _ => ""
    };

    public static IEnumerable<CampoCliente> ComMeta(Cliente cl) => cl.Campos.Where(c => c.TemMeta);

    public static Status Geral(Cliente cl)
    {
        var lista = ComMeta(cl).Select(StatusCampo).ToList();
        if (lista.Count == 0) return Status.Neutro;
        var media = lista.Average(s => s switch { Status.Excelente => 2.0, Status.Atencao => 1.0, _ => 0.0 });
        return media >= 1.85 ? Status.Excelente
             : media >= 1.4 ? Status.Bom
             : media >= 0.8 ? Status.Atencao
             : Status.Critico;
    }

    public static string RotuloGeral(Cliente cl) => Geral(cl) switch
    {
        Status.Excelente => "Excelente",
        Status.Bom => "Está bem",
        Status.Atencao => "Atenção",
        Status.Critico => "Abaixo do esperado",
        _ => "Sem metas"
    };

    /// <summary>Grupos na ordem em que aparecem; campos sem grupo vão para "Indicadores".</summary>
    public static List<(string Grupo, List<CampoCliente> Campos)> Grupos(Cliente cl) =>
        cl.Campos
          .GroupBy(c => string.IsNullOrWhiteSpace(c.Grupo) ? "Indicadores" : c.Grupo.Trim())
          .Select(g => (g.Key, g.ToList()))
          .ToList();

    public static string GerarParecer(Cliente cl, ContextoPainel? ctx = null)
    {
        ctx ??= ContextoPainel.Cliente;
        // Com espaço inicial; vazio quando uma aba personalizada ainda não tem nome no registro
        var nome = ctx.Generico
            ? (string.IsNullOrWhiteSpace(cl.Nome) ? "" : $" de {cl.Nome.Trim()}")
            : (string.IsNullOrWhiteSpace(cl.Nome) ? " do cliente" : $" do cliente {cl.Nome.Trim()}");
        var periodo = string.IsNullOrWhiteSpace(cl.Periodo) ? "o período" : cl.Periodo.Trim().ToLowerInvariant();
        var comMeta = ComMeta(cl).ToList();
        var semMeta = cl.Campos.Where(c => !c.TemMeta && ValorPreenchido(c)).ToList();

        if (comMeta.Count == 0 && semMeta.Count == 0)
            return $"Em análise aos indicadores{nome} referentes a {periodo}, não constam dados preenchidos para a emissão de parecer.";

        var geral = Geral(cl);
        var naMeta = comMeta.Where(c => StatusCampo(c) == Status.Excelente).ToList();
        var proximos = comMeta.Where(c => StatusCampo(c) == Status.Atencao).ToList();
        var fora = comMeta.Where(c => StatusCampo(c) == Status.Critico).ToList();

        string Linha(CampoCliente c)
        {
            var at = Atingimento(c).ToString("N1", Br);
            var rel = StatusCampo(c) switch
            {
                Status.Excelente => "atendendo à",
                Status.Atencao => "próximo à",
                _ => "fora da"
            };
            return $"{c.Nome} registrou {Valor(c)}, {rel} {MetaExtenso(c)} ({at}% de atingimento)";
        }

        var abertura = $"Em análise aos indicadores{nome} referentes a {periodo}, verifica-se " + geral switch
        {
            Status.Excelente => "resultado excelente",
            Status.Bom => "resultado satisfatório",
            Status.Atencao => "resultado que requer atenção em parte dos indicadores",
            Status.Critico => "resultado abaixo do esperado",
            _ => "o seguinte cenário"
        };
        if (comMeta.Count > 0)
            abertura += comMeta.Count == 1
                ? (naMeta.Count == 1 ? ", com o único indicador com meta atendido" : ", com o único indicador com meta não atendido")
                : $", com {naMeta.Count} de {comMeta.Count} indicadores com meta atendidos";
        abertura += ".";

        var conclusao = geral switch
        {
            Status.Excelente => "Diante do exposto, o desempenho é avaliado como excelente, recomendando-se a manutenção das práticas adotadas.",
            Status.Bom => "Diante do exposto, o desempenho é considerado satisfatório, recomendando-se o acompanhamento dos indicadores próximos à meta.",
            Status.Atencao => "Diante do exposto, recomenda-se a definição de ações direcionadas aos indicadores que não atingiram a meta, com acompanhamento no próximo período.",
            Status.Critico => "Diante do exposto, recomenda-se a elaboração de um plano de ação com acompanhamento periódico dos indicadores fora da meta.",
            _ => "Os indicadores acima foram registrados para acompanhamento, sem metas definidas para o período."
        };

        if (cl.Tom == TomParecer.Topicos)
        {
            var l = new List<string> { abertura, "" };
            if (naMeta.Count > 0) { l.Add("Na meta:"); l.AddRange(naMeta.Select(c => "• " + Linha(c) + ".")); l.Add(""); }
            if (proximos.Count > 0) { l.Add("Próximos da meta:"); l.AddRange(proximos.Select(c => "• " + Linha(c) + ".")); l.Add(""); }
            if (fora.Count > 0) { l.Add("Fora da meta:"); l.AddRange(fora.Select(c => "• " + Linha(c) + ".")); l.Add(""); }
            if (cl.ParecerIncluiSemMeta && semMeta.Count > 0)
            {
                l.Add("Demais indicadores:");
                l.AddRange(semMeta.Select(c => $"• {c.Nome}: {Valor(c)}."));
                l.Add("");
            }
            l.Add(conclusao);
            return string.Join("\n", l);
        }

        var partes = new List<string> { abertura };
        partes.AddRange(comMeta.Select(c => Linha(c) + "."));
        if (cl.ParecerIncluiSemMeta && semMeta.Count > 0)
            partes.Add("Foram registrados, ainda, os seguintes indicadores: " +
                       string.Join("; ", semMeta.Select(c => $"{c.Nome}: {Valor(c)}")) + ".");
        if (fora.Count > 0)
            partes.Add((fora.Count == 1 ? "Requer atenção prioritária: " : "Requerem atenção prioritária: ") +
                       string.Join(", ", fora.Select(c => c.Nome)) + ".");
        partes.Add(conclusao);
        return string.Join(" ", partes);
    }

    public static bool ValorPreenchido(CampoCliente c) => c.Tipo switch
    {
        TipoCampo.Texto => !string.IsNullOrWhiteSpace(c.Texto),
        TipoCampo.Data => c.Data.HasValue,
        TipoCampo.SimNao => true,
        _ => c.Valor.HasValue
    };

    /// <summary>Visual sugerido para cada tipo ao criar o campo.</summary>
    public static VisualCampo VisualPadrao(TipoCampo t) => t switch
    {
        TipoCampo.Percentual => VisualCampo.Medidor,
        TipoCampo.Moeda => VisualCampo.Barra,
        _ => VisualCampo.Numero
    };

    /// <summary>Arredonda o valor para as casas configuradas.</summary>
    public static void Normalizar(CampoCliente c)
    {
        var casas = c.Tipo == TipoCampo.Inteiro ? 0 : Math.Clamp(c.Casas, 0, 6);
        if (c.Valor.HasValue) c.Valor = Math.Round(c.Valor.Value, casas);
        if (c.Meta.HasValue) c.Meta = Math.Round(c.Meta.Value, casas);
        c.Tolerancia = Math.Clamp(c.Tolerancia, 0, 100);
    }

    /// <summary>Exemplo inicial, genérico, para mostrar as possibilidades.</summary>
    public static Cliente Exemplo() => new()
    {
        Nome = "Cliente Exemplo",
        Descricao = "Operação de atendimento",
        Campos = new()
        {
            new() { Id = "vol", Nome = "Volume processado", Grupo = "Operação", Tipo = TipoCampo.Inteiro, Valor = 12480, Meta = 12000, Visual = VisualCampo.Barra, Destaque = true },
            new() { Id = "sla", Nome = "SLA no prazo", Grupo = "Operação", Tipo = TipoCampo.Percentual, Casas = 2, Valor = 97.35m, Meta = 98, Tolerancia = 97, Visual = VisualCampo.Medidor },
            new() { Id = "tma", Nome = "Tempo médio de atendimento", Grupo = "Operação", Tipo = TipoCampo.Decimal, Casas = 1, Unidade = "min", Valor = 5.8m, Meta = 6, Sentido = SentidoMeta.MenorMelhor, Visual = VisualCampo.Comparativo },
            new() { Id = "fat", Nome = "Faturamento", Grupo = "Financeiro", Tipo = TipoCampo.Moeda, Casas = 2, Valor = 148350.90m, Meta = 150000, Visual = VisualCampo.Anel },
            new() { Id = "rec", Nome = "Reclamações", Grupo = "Satisfação", Tipo = TipoCampo.Inteiro, Valor = 4, Meta = 5, Sentido = SentidoMeta.MenorMelhor, Tolerancia = 80 },
            new() { Id = "nps", Nome = "NPS", Grupo = "Satisfação", Tipo = TipoCampo.Inteiro, Valor = 72, Meta = 70, Visual = VisualCampo.Anel },
            new() { Id = "con", Nome = "Contrato renovado", Grupo = "Contrato", Tipo = TipoCampo.SimNao, Booleano = true },
            new() { Id = "rev", Nome = "Próxima revisão", Grupo = "Contrato", Tipo = TipoCampo.Data, Data = DateTime.Today.AddMonths(2) }
        },
        Graficos = new()
        {
            new() { Titulo = "Atingimento das metas", Tipo = TipoGrafico.Barras, Medida = MedidaGrafico.PercentualMeta, Campos = new() { "vol", "sla", "tma", "fat", "rec", "nps" } }
        }
    };
}
