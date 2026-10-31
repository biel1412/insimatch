namespace Insimatch.Models;

public enum Status { Excelente, Bom, Atencao, Critico, Neutro }

public enum NivelMeta { SemMeta, Abaixo, Proximo, Pro, Premium }

/// <summary>
/// Critérios de análise:
/// • Volume: a média diária (volume ÷ dias trabalhados) é comparada com a meta Pro e a meta Premium
///   de cada colaborador (as metas são por dia e podem variar de pessoa para pessoa).
/// • Qualidade: calculada pelos desvios internos e externos sobre o volume; o ideal é ≥ 99,94%.
/// • Está bem: qualidade no ideal e média diária pelo menos próxima da meta Pro.
/// </summary>
public static class Avaliacao
{
    public const decimal QualidadeIdeal = 99.94m;

    /// <summary>A partir de quanto da meta Pro a média é considerada "próxima da meta".</summary>
    public const decimal FatorProximo = 0.90m;

    public static bool QualidadeOk(Colaborador c) => c.Volume > 0 && c.Qualidade >= QualidadeIdeal;

    /// <summary>Quantidade máxima de desvios (internos + externos) para manter 99,94%.</summary>
    public static int DesviosPermitidos(Colaborador c) =>
        (int)Math.Floor(c.Volume * (100m - QualidadeIdeal) / 100m);

    public static NivelMeta Nivel(Colaborador c)
    {
        if (c.MetaPro <= 0 && c.MetaPremium <= 0) return NivelMeta.SemMeta;
        var media = c.MediaDiaria;
        if (c.MetaPremium > 0 && media >= c.MetaPremium) return NivelMeta.Premium;
        if (c.MetaPro > 0 && media >= c.MetaPro) return NivelMeta.Pro;
        var referencia = c.MetaPro > 0 ? c.MetaPro : c.MetaPremium;
        if (media >= referencia * FatorProximo) return NivelMeta.Proximo;
        return NivelMeta.Abaixo;
    }

    public static Status StatusVolume(Colaborador c) => Nivel(c) switch
    {
        NivelMeta.Premium => Status.Excelente,
        NivelMeta.Pro => Status.Bom,
        NivelMeta.Proximo => Status.Atencao,
        NivelMeta.Abaixo => Status.Critico,
        _ => Status.Neutro
    };

    public static string RotuloVolume(Colaborador c) => Nivel(c) switch
    {
        NivelMeta.Premium => "Meta Premium atingida",
        NivelMeta.Pro => "Meta Pro atingida",
        NivelMeta.Proximo => "Próximo da meta",
        NivelMeta.Abaixo => "Abaixo da meta",
        _ => "Sem meta"
    };

    public static Status StatusQualidade(Colaborador c) =>
        c.Volume <= 0 ? Status.Neutro : QualidadeOk(c) ? Status.Excelente : Status.Critico;

    public static string RotuloQualidade(Colaborador c) =>
        c.Volume <= 0 ? "Sem volume" : QualidadeOk(c) ? "Ideal" : "Abaixo do ideal";

    public static Status Geral(Colaborador c)
    {
        var nivel = Nivel(c);
        var q = QualidadeOk(c);
        var volumeBem = nivel is NivelMeta.Proximo or NivelMeta.Pro or NivelMeta.Premium or NivelMeta.SemMeta;

        if (q && nivel == NivelMeta.Premium) return Status.Excelente;
        if (q && volumeBem) return Status.Bom;       // qualidade ok e pelo menos próximo da meta
        if (q || volumeBem) return Status.Atencao;
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
        Status.Bom => "Está bem",
        Status.Atencao => "Atenção",
        _ => "Abaixo do esperado"
    };

    /// <summary>Gera um parecer sugerido com base nos números.</summary>
    public static string GerarParecer(Colaborador c)
    {
        var nome = c.Nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Colaborador";
        var mes = c.MesPorExtenso.ToLowerInvariant();
        var media = c.MediaDiaria;
        var partes = new List<string>();

        partes.Add(Geral(c) switch
        {
            Status.Excelente => $"{nome}, seu resultado em {mes} foi excelente.",
            Status.Bom => $"{nome}, você fechou {mes} com um bom resultado.",
            Status.Atencao => $"{nome}, {mes} teve pontos positivos, mas há indicadores que pedem atenção.",
            _ => $"{nome}, precisamos conversar sobre os indicadores de {mes} e montar um plano juntos."
        });

        var prod = $"{c.Volume:N0} itens em {c.DiasConsiderados} dias trabalhados, uma média de {media:N1} por dia";
        partes.Add(Nivel(c) switch
        {
            NivelMeta.Premium => $"Foram {prod}, acima da meta Premium de {c.MetaPremium:N0}/dia.",
            NivelMeta.Pro when c.MetaPremium > 0 =>
                $"Foram {prod}: meta Pro de {c.MetaPro:N0}/dia batida, faltando {c.MetaPremium - media:N1}/dia para a Premium.",
            NivelMeta.Pro => $"Foram {prod}, acima da meta Pro de {c.MetaPro:N0}/dia.",
            NivelMeta.Proximo or NivelMeta.Abaixo when c.MetaPro > 0 =>
                $"Foram {prod}, faltando {c.MetaPro - media:N1}/dia para a meta Pro de {c.MetaPro:N0}/dia.",
            NivelMeta.Proximo or NivelMeta.Abaixo =>
                $"Foram {prod}, faltando {c.MetaPremium - media:N1}/dia para a meta Premium.",
            _ => $"Foram {prod}."
        });

        if (c.Volume > 0)
        {
            var desvios = c.TotalDesvios == 0
                ? "sem nenhum desvio"
                : $"com {c.DesviosInternos} desvio(s) interno(s) e {c.DesviosExternos} externo(s)";
            partes.Add(QualidadeOk(c)
                ? $"A qualidade ficou em {c.Qualidade:N2}%, {desvios}, dentro do ideal de {QualidadeIdeal:N2}%."
                : $"A qualidade ficou em {c.Qualidade:N2}%, {desvios}, abaixo do ideal de {QualidadeIdeal:N2}% — para esse volume, o máximo seria {DesviosPermitidos(c)} desvio(s).");
        }

        partes.Add(Geral(c) switch
        {
            Status.Excelente => "Continue assim!",
            Status.Bom => "Vamos buscar a próxima meta no mês que vem.",
            Status.Atencao => "Com alguns ajustes, o próximo mês pode ser ainda melhor.",
            _ => "Conte comigo para o plano de melhoria."
        });

        return string.Join(" ", partes);
    }
}
