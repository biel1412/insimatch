namespace Insimatch.Models;

/// <summary>Os fatos principais de um resultado, já em frases curtas, usados para montar as sugestões de parecer.</summary>
public class FatosParecer
{
    /// <summary>Quem é avaliado, por extenso (ex.: "Maria Souza", "o time Atendimento").</summary>
    public string Sujeito { get; set; } = "";

    /// <summary>Como chamar na mensagem direta (ex.: "Maria", "Pessoal"). Null = tom institucional.</summary>
    public string? Vocativo { get; set; }

    public string Periodo { get; set; } = "";
    public Status Status { get; set; } = Status.Neutro;

    /// <summary>Números-chave para mensagens curtas (ex.: "155,0/dia", "99,97% de qualidade").</summary>
    public List<string> Chaves { get; } = new();

    public List<string> Positivos { get; } = new();
    public List<string> Atencao { get; } = new();
    public List<string> Neutros { get; } = new();
    public List<string> Acoes { get; } = new();
}

public record Sugestao(string Titulo, string Descricao, string Texto);

/// <summary>Monta 7 estilos de parecer a partir dos fatos principais.</summary>
public static class Sugestoes
{
    public static List<Sugestao> Gerar(FatosParecer f, string formalCompleto) => new()
    {
        new("Formal completo", "Texto corrido, impessoal, com todos os números.", formalCompleto),
        new("Resumo executivo", "Duas ou três frases para quem quer o essencial.", Resumo(f)),
        new("Pontos fortes e de melhoria", "Separado em fortes, melhorias e próximos passos.", FortesMelhorias(f)),
        new(f.Vocativo is null ? "Mensagem cordial" : "Feedback motivacional",
            f.Vocativo is null ? "Tom próximo e respeitoso, para enviar ou ler." : "Conversa direta, em segunda pessoa, para a ligação.",
            Feedback(f)),
        new("Tópicos", "Lista rápida do que está bem e do que pede atenção.", Topicos(f)),
        new("Plano de ação", "Foco no que fazer a seguir, em passos numerados.", Plano(f)),
        new("Mensagem curta", "Uma linha para o chat do Teams.", Curta(f))
    };

    private static string Situacao(Status s) => s switch
    {
        Status.Excelente => "resultado excelente",
        Status.Bom => "resultado satisfatório",
        Status.Atencao => "resultado que requer atenção",
        Status.Critico => "resultado abaixo do esperado",
        _ => "resultado registrado para acompanhamento"
    };

    private static string Lista(IEnumerable<string> itens)
    {
        var l = itens.ToList();
        return l.Count switch
        {
            0 => "",
            1 => l[0],
            _ => string.Join(", ", l.Take(l.Count - 1)) + " e " + l[^1]
        };
    }

    private static string Maiuscula(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];
    private static string Minuscula(string s) => string.IsNullOrEmpty(s) ? s : char.ToLower(s[0]) + s[1..];

    private static string Resumo(FatosParecer f)
    {
        var partes = new List<string> { $"{Maiuscula(f.Sujeito)} apresentou {Situacao(f.Status)} em {f.Periodo}." };
        if (f.Positivos.Count > 0) partes.Add($"Destaca-se {Lista(f.Positivos.Take(3))}.");
        if (f.Atencao.Count > 0) partes.Add($"Requer atenção: {Lista(f.Atencao.Take(3))}.");
        else partes.Add("Não há indicadores fora do esperado.");
        return string.Join(" ", partes);
    }

    private static string FortesMelhorias(FatosParecer f)
    {
        var l = new List<string> { $"{Maiuscula(f.Sujeito)} · {f.Periodo}", "" };
        l.Add("Pontos fortes");
        l.AddRange(f.Positivos.Count > 0 ? f.Positivos.Select(p => "• " + Maiuscula(p) + ".") : new[] { "• Sem destaques acima da meta no período." });
        l.Add("");
        l.Add("Pontos de melhoria");
        l.AddRange(f.Atencao.Count > 0 ? f.Atencao.Select(p => "• " + Maiuscula(p) + ".") : new[] { "• Nenhum ponto crítico identificado." });
        l.Add("");
        l.Add("Próximos passos");
        l.AddRange((f.Acoes.Count > 0 ? f.Acoes : new List<string> { "manter o desempenho atual" }).Select(a => "• " + Maiuscula(a) + "."));
        return string.Join("\n", l);
    }

    private static string Feedback(FatosParecer f)
    {
        var inicio = f.Vocativo is { } v
            ? f.Status switch
            {
                Status.Excelente => $"{v}, parabéns pelo excelente resultado em {f.Periodo}!",
                Status.Bom => $"{v}, bom trabalho em {f.Periodo}!",
                Status.Atencao => $"{v}, obrigado pelo empenho em {f.Periodo}.",
                Status.Critico => $"{v}, vamos olhar juntos para {f.Periodo} e construir o próximo passo.",
                _ => $"{v}, aqui está o resumo de {f.Periodo}."
            }
            : f.Status switch
            {
                Status.Excelente => $"Compartilhamos com satisfação o resultado de {f.Periodo}, que foi excelente.",
                Status.Bom => $"Compartilhamos o resultado de {f.Periodo}, que foi positivo.",
                Status.Atencao => $"Compartilhamos o resultado de {f.Periodo}, com avanços e alguns pontos a ajustar.",
                Status.Critico => $"Compartilhamos o resultado de {f.Periodo} e os pontos que vamos priorizar juntos.",
                _ => $"Segue o resumo de {f.Periodo}."
            };

        var partes = new List<string> { inicio };
        if (f.Positivos.Count > 0)
            partes.Add((f.Vocativo is null ? "Destacamos " : "Destaco ") + Lista(f.Positivos.Take(3)) + ".");
        if (f.Atencao.Count > 0)
            partes.Add((f.Vocativo is null ? "Como oportunidade, vamos trabalhar " : "Como oportunidade, vale olhar para ") + Lista(f.Atencao.Take(2)) + ".");
        if (f.Acoes.Count > 0)
            partes.Add($"Para o próximo período, o foco é {Minuscula(f.Acoes[0])}.");
        partes.Add(f.Vocativo is null ? "Seguimos à disposição." : (f.Status is Status.Excelente or Status.Bom ? "Continue assim!" : "Conte comigo nesse caminho."));
        return string.Join(" ", partes);
    }

    private static string Topicos(FatosParecer f)
    {
        var l = new List<string> { $"Resultado de {f.Periodo}: {Situacao(f.Status)}.", "" };
        l.AddRange(f.Positivos.Select(p => "✓ " + Maiuscula(p)));
        l.AddRange(f.Atencao.Select(p => "! " + Maiuscula(p)));
        l.AddRange(f.Neutros.Select(p => "• " + Maiuscula(p)));
        return string.Join("\n", l).TrimEnd();
    }

    private static string Plano(FatosParecer f)
    {
        var l = new List<string> { $"Situação em {f.Periodo}: {Situacao(f.Status)}." };
        if (f.Atencao.Count > 0) l.Add($"Pontos a tratar: {Lista(f.Atencao)}.");
        l.Add("");
        l.Add("Ações recomendadas:");
        var acoes = f.Acoes.Count > 0 ? f.Acoes : new List<string> { "manter as práticas atuais", "acompanhar os indicadores no próximo período" };
        for (var i = 0; i < acoes.Count; i++) l.Add($"{i + 1}. {Maiuscula(acoes[i])}.");
        l.Add("");
        l.Add("Acompanhamento: revisão dos indicadores no próximo período.");
        return string.Join("\n", l);
    }

    private static string Curta(FatosParecer f)
    {
        var rotulo = f.Status switch
        {
            Status.Excelente => "Excelente",
            Status.Bom => "Está bem",
            Status.Atencao => "Atenção",
            Status.Critico => "Abaixo do esperado",
            _ => "Resumo"
        };
        var chaves = f.Chaves.Count > 0 ? " — " + string.Join(" · ", f.Chaves.Take(3)) : "";
        return $"{Maiuscula(f.Sujeito)} · {f.Periodo}: {rotulo}{chaves}.";
    }
}
