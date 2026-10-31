namespace Insimatch.Models;

public enum Status { Excelente, Bom, Atencao, Critico, Neutro }

public enum NivelMeta { SemMeta, Abaixo, Pro, Premium }

public static class Avaliacao
{
    /// <summary>Qualidade abaixo deste valor está abaixo do ideal.</summary>
    public const decimal QualidadeIdeal = 99.94m;

    public static bool QualidadeOk(Colaborador c) => c.Qualidade >= QualidadeIdeal;

    public static NivelMeta Nivel(Colaborador c)
    {
        if (c.MetaPro <= 0 && c.MetaPremium <= 0) return NivelMeta.SemMeta;
        if (c.MetaPremium > 0 && c.Volume >= c.MetaPremium) return NivelMeta.Premium;
        if (c.MetaPro > 0 && c.Volume >= c.MetaPro) return NivelMeta.Pro;
        return NivelMeta.Abaixo;
    }

    public static Status StatusVolume(Colaborador c) => Nivel(c) switch
    {
        NivelMeta.Premium => Status.Excelente,
        NivelMeta.Pro => Status.Bom,
        NivelMeta.Abaixo => Status.Critico,
        _ => Status.Neutro
    };

    public static string RotuloVolume(Colaborador c) => Nivel(c) switch
    {
        NivelMeta.Premium => "Meta Premium atingida",
        NivelMeta.Pro => "Meta Pro atingida",
        NivelMeta.Abaixo => "Abaixo da meta",
        _ => "Sem meta"
    };

    public static Status StatusQualidade(Colaborador c) => QualidadeOk(c) ? Status.Excelente : Status.Critico;

    public static string RotuloQualidade(Colaborador c) => QualidadeOk(c) ? "Ideal" : "Abaixo do ideal";

    public static Status Geral(Colaborador c)
    {
        var nivel = Nivel(c);
        var q = QualidadeOk(c);
        var volOk = nivel is NivelMeta.Pro or NivelMeta.Premium or NivelMeta.SemMeta;
        if (q && nivel == NivelMeta.Premium) return Status.Excelente;
        if (q && volOk) return Status.Bom;
        if (q || volOk) return Status.Atencao;
        return Status.Critico;
    }

    public static string Classe(Status s) => s switch
    {
        Status.Excelente => "st-excelente",
        Status.Bom => "st-bom",
        Status.Atencao => "st-atencao",
        Status.Critico => "st-critico",
        _ => "neutro"
    };

    public static string RotuloGeral(Status s) => s switch
    {
        Status.Excelente => "Excelente",
        Status.Bom => "Bom",
        Status.Atencao => "Atenção",
        _ => "Abaixo do esperado"
    };

    /// <summary>Gera um parecer sugerido com base nos números.</summary>
    public static string GerarParecer(Colaborador c)
    {
        var nome = c.Nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Colaborador";
        var mes = c.MesPorExtenso.ToLowerInvariant();
        var partes = new List<string>();

        partes.Add(Geral(c) switch
        {
            Status.Excelente => $"{nome}, seu resultado em {mes} foi excelente.",
            Status.Bom => $"{nome}, você fechou {mes} com um bom resultado.",
            Status.Atencao => $"{nome}, {mes} teve pontos positivos, mas há indicadores que pedem atenção.",
            _ => $"{nome}, precisamos conversar sobre os indicadores de {mes} e montar um plano juntos."
        });

        var prod = $"{c.Volume:N0} itens em {c.DiasConsiderados} dias (média de {c.MediaDiaria:N1}/dia)";
        partes.Add(Nivel(c) switch
        {
            NivelMeta.Premium => $"Você realizou {prod} e bateu a meta Premium de {c.MetaPremium:N0}.",
            NivelMeta.Pro when c.MetaPremium > 0 =>
                $"Você realizou {prod}, bateu a meta Pro de {c.MetaPro:N0} e ficou a {c.MetaPremium - c.Volume:N0} da Premium.",
            NivelMeta.Pro => $"Você realizou {prod} e bateu a meta Pro de {c.MetaPro:N0}.",
            NivelMeta.Abaixo when c.MetaPro > 0 =>
                $"Você realizou {prod}, faltando {c.MetaPro - c.Volume:N0} para a meta Pro de {c.MetaPro:N0}.",
            NivelMeta.Abaixo => $"Você realizou {prod}, faltando {c.MetaPremium - c.Volume:N0} para a meta Premium.",
            _ => $"Você realizou {prod}."
        });

        partes.Add(QualidadeOk(c)
            ? $"A qualidade de {c.Qualidade:N2}% está no ideal (mínimo de {QualidadeIdeal:N2}%)."
            : $"A qualidade de {c.Qualidade:N2}% ficou abaixo do ideal de {QualidadeIdeal:N2}%.");

        partes.Add(c.TotalDesvios == 0
            ? "Nenhum desvio registrado no período — parabéns pelo cuidado."
            : $"Foram registrados {c.DesviosInternos} desvio(s) interno(s) e {c.DesviosExternos} externo(s); vamos olhar juntos as causas.");

        partes.Add(Geral(c) switch
        {
            Status.Excelente => "Continue assim!",
            Status.Bom => "Vamos buscar a meta Premium no próximo mês.",
            Status.Atencao => "Com alguns ajustes, o próximo mês pode ser ainda melhor.",
            _ => "Conte comigo para o plano de melhoria."
        });

        return string.Join(" ", partes);
    }
}
