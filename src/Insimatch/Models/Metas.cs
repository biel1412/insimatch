namespace Insimatch.Models;

/// <summary>Metas usadas para colorir os indicadores e gerar o parecer.</summary>
public class Metas
{
    public int MetaVolume { get; set; } = 1200;
    public decimal MetaMediaDiaria { get; set; } = 55;
    public decimal MetaQualidade { get; set; } = 95;
    public int LimiteDesviosInternos { get; set; } = 3;
    public int LimiteDesviosExternos { get; set; } = 1;
    public string NomeEmpresa { get; set; } = "Insimatch";
}

public enum Status { Excelente, Bom, Atencao, Critico }

public static class Avaliacao
{
    /// <summary>Quanto maior, melhor (volume, média, qualidade).</summary>
    public static Status Maior(decimal valor, decimal meta, decimal tolerancia = 0.9m)
    {
        if (meta <= 0) return Status.Bom;
        if (valor >= meta * 1.05m) return Status.Excelente;
        if (valor >= meta) return Status.Bom;
        if (valor >= meta * tolerancia) return Status.Atencao;
        return Status.Critico;
    }

    public static Status Qualidade(decimal valor, decimal meta)
    {
        if (valor >= Math.Min(100, meta + 3)) return Status.Excelente;
        if (valor >= meta) return Status.Bom;
        if (valor >= meta - 5) return Status.Atencao;
        return Status.Critico;
    }

    /// <summary>Quanto menor, melhor (desvios).</summary>
    public static Status Desvios(int valor, int limite)
    {
        if (valor == 0) return Status.Excelente;
        if (valor <= limite) return Status.Bom;
        if (valor <= limite * 2) return Status.Atencao;
        return Status.Critico;
    }

    public static Status Geral(Colaborador c, Metas m)
    {
        var lista = new[]
        {
            Maior(c.Volume, m.MetaVolume),
            Maior(c.MediaDiaria, m.MetaMediaDiaria),
            Qualidade(c.Qualidade, m.MetaQualidade),
            Desvios(c.DesviosInternos, m.LimiteDesviosInternos),
            Desvios(c.DesviosExternos, m.LimiteDesviosExternos),
        };
        var pontos = lista.Average(s => s switch
        {
            Status.Excelente => 3.0,
            Status.Bom => 2.0,
            Status.Atencao => 1.0,
            _ => 0.0
        });
        if (lista.Contains(Status.Critico) && pontos < 2) return Status.Critico;
        return pontos switch
        {
            >= 2.6 => Status.Excelente,
            >= 1.8 => Status.Bom,
            >= 1.0 => Status.Atencao,
            _ => Status.Critico
        };
    }

    public static string Classe(Status s) => s switch
    {
        Status.Excelente => "st-excelente",
        Status.Bom => "st-bom",
        Status.Atencao => "st-atencao",
        _ => "st-critico"
    };

    public static string Rotulo(Status s) => s switch
    {
        Status.Excelente => "Excelente",
        Status.Bom => "Dentro da meta",
        Status.Atencao => "Atenção",
        _ => "Crítico"
    };

    /// <summary>Gera um parecer sugerido com base nos números e nas metas.</summary>
    public static string GerarParecer(Colaborador c, Metas m)
    {
        var nome = c.Nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Colaborador";
        var geral = Geral(c, m);
        var partes = new List<string>();

        partes.Add(geral switch
        {
            Status.Excelente => $"{nome}, seu resultado em {c.MesPorExtenso.ToLowerInvariant()} foi excelente.",
            Status.Bom => $"{nome}, você fechou {c.MesPorExtenso.ToLowerInvariant()} dentro do esperado.",
            Status.Atencao => $"{nome}, {c.MesPorExtenso.ToLowerInvariant()} teve pontos positivos, mas alguns indicadores pedem atenção.",
            _ => $"{nome}, precisamos conversar sobre os indicadores de {c.MesPorExtenso.ToLowerInvariant()} e montar um plano juntos."
        });

        var vol = Maior(c.Volume, m.MetaVolume);
        partes.Add(vol is Status.Excelente or Status.Bom
            ? $"A produção de {c.Volume:N0} itens em {c.DiasConsiderados} dias (média de {c.MediaDiaria:N1}/dia) atingiu a meta."
            : $"A produção ficou em {c.Volume:N0} itens (média de {c.MediaDiaria:N1}/dia), abaixo da meta de {m.MetaVolume:N0}.");

        var q = Qualidade(c.Qualidade, m.MetaQualidade);
        partes.Add(q is Status.Excelente or Status.Bom
            ? $"A qualidade de {c.Qualidade:N1}% está acima da meta de {m.MetaQualidade:N0}%."
            : $"A qualidade de {c.Qualidade:N1}% ficou abaixo da meta de {m.MetaQualidade:N0}%.");

        if (c.TotalDesvios == 0)
            partes.Add("Nenhum desvio registrado no período — parabéns pelo cuidado.");
        else
        {
            var di = Desvios(c.DesviosInternos, m.LimiteDesviosInternos);
            var de = Desvios(c.DesviosExternos, m.LimiteDesviosExternos);
            if (di is Status.Atencao or Status.Critico || de is Status.Atencao or Status.Critico)
                partes.Add($"Foram {c.DesviosInternos} desvio(s) interno(s) e {c.DesviosExternos} externo(s); vamos revisar juntos as causas para reduzir no próximo mês.");
            else
                partes.Add($"Os desvios ({c.DesviosInternos} interno(s) e {c.DesviosExternos} externo(s)) estão dentro do limite.");
        }

        partes.Add(geral switch
        {
            Status.Excelente => "Continue assim!",
            Status.Bom => "Vamos manter a consistência no próximo mês.",
            Status.Atencao => "Com alguns ajustes, o próximo mês pode ser ainda melhor.",
            _ => "Conte comigo para o plano de melhoria."
        });

        return string.Join(" ", partes);
    }
}
