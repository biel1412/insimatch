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

    /// <summary>Os 7 modelos de sugestão de parecer para o colaborador.</summary>
    public static List<Sugestao> ListarSugestoes(Colaborador c) => Sugestoes.Gerar(Fatos(c), GerarParecer(c));

    public static FatosParecer Fatos(Colaborador c)
    {
        var nome = c.Nome?.Trim() ?? "";
        var f = new FatosParecer
        {
            Sujeito = string.IsNullOrWhiteSpace(nome) ? "o(a) colaborador(a)" : nome,
            Nome = nome,
            Vocativo = string.IsNullOrWhiteSpace(nome) ? "Olá" : nome.Split(' ')[0],
            Periodo = c.MesPorExtenso.ToLowerInvariant(),
            Status = Geral(c)
        };
        var media = c.MediaDiaria;
        f.Chaves.Add($"{media:N1}/dia");
        if (c.Volume > 0) f.Chaves.Add($"{c.Qualidade:N2}% de qualidade");

        switch (Nivel(c))
        {
            case NivelMeta.Premium:
                f.Positivos.Add($"a média de {media:N1} itens/dia, acima da meta Premium ({c.MetaPremium:0.#}/dia)");
                break;
            case NivelMeta.Pro:
                f.Positivos.Add($"a média de {media:N1} itens/dia, acima da meta Pro ({c.MetaPro:0.#}/dia)");
                if (c.MetaPremium > 0)
                {
                    f.Atencao.Add($"a diferença de {c.MetaPremium - media:N1} itens/dia para a meta Premium");
                    f.Acoes.Add($"buscar mais {c.MetaPremium - media:N1} itens por dia para alcançar a meta Premium");
                }
                break;
            case NivelMeta.Proximo or NivelMeta.Abaixo:
                var meta = c.MetaPro > 0 ? c.MetaPro : c.MetaPremium;
                var nomeMeta = c.MetaPro > 0 ? "Pro" : "Premium";
                f.Atencao.Add($"a média de {media:N1} itens/dia, {meta - media:N1} abaixo da meta {nomeMeta} ({meta:0.#}/dia)");
                f.Acoes.Add($"elevar a média diária em {meta - media:N1} itens para atingir a meta {nomeMeta}");
                break;
            default:
                f.Neutros.Add($"média de {media:N1} itens/dia");
                break;
        }
        f.Neutros.Add($"{c.Volume:N0} itens em {c.DiasConsiderados} dias trabalhados");

        if (c.Volume > 0)
        {
            if (QualidadeOk(c))
                f.Positivos.Add($"a qualidade de {c.Qualidade:N2}%, dentro do ideal de {QualidadeIdeal:N2}%");
            else
            {
                f.Atencao.Add($"a qualidade de {c.Qualidade:N2}%, abaixo do ideal de {QualidadeIdeal:N2}%");
                f.Acoes.Add($"revisar as causas dos desvios para manter no máximo {Plural(DesviosPermitidos(c), "desvio", "desvios", "nenhum desvio")} no período");
            }
            if (c.TotalDesvios == 0) f.Positivos.Add("nenhum desvio registrado");
            else f.Neutros.Add($"{Plural(c.DesviosInternos, "desvio interno", "desvios internos", "nenhum desvio interno")} e {Plural(c.DesviosExternos, "desvio externo", "desvios externos", "nenhum desvio externo")}");
        }
        if (f.Acoes.Count == 0) f.Acoes.Add("manter o ritmo e a qualidade atuais");
        return f;
    }

    /// <summary>Gera um parecer formal com base nos números.</summary>
    public static string GerarParecer(Colaborador c)
    {
        var nome = string.IsNullOrWhiteSpace(c.Nome) ? "o(a) colaborador(a)" : c.Nome.Trim();
        var periodo = c.MesPorExtenso.ToLowerInvariant();

        if (c.Volume <= 0 || c.DiasConsiderados <= 0)
            return $"Em análise aos indicadores de desempenho referentes a {periodo}, não constam registros " +
                   $"suficientes de produção para {nome}, razão pela qual não é possível emitir avaliação conclusiva " +
                   "para o período.";

        var media = c.MediaDiaria;
        var geral = Geral(c);
        var partes = new List<string>();

        partes.Add($"Em análise aos indicadores de desempenho referentes a {periodo}, verifica-se que {nome} " + geral switch
        {
            Status.Excelente => "apresentou resultado excelente.",
            Status.Bom => "apresentou resultado satisfatório.",
            Status.Atencao => "apresentou resultado que requer atenção em parte dos indicadores.",
            _ => "apresentou resultado abaixo do esperado."
        });

        var prod = $"Foram realizados {c.Volume:N0} itens em {Plural(c.DiasConsiderados, "dia trabalhado", "dias trabalhados")}, " +
                   $"o que corresponde a uma média de {media:N1} itens por dia";
        partes.Add(Nivel(c) switch
        {
            NivelMeta.Premium => $"{prod}, superando a meta Premium estabelecida ({c.MetaPremium:0.#}/dia).",
            NivelMeta.Pro when c.MetaPremium > 0 =>
                $"{prod}, superando a meta Pro estabelecida ({c.MetaPro:0.#}/dia) e permanecendo {c.MetaPremium - media:N1} itens por dia abaixo da meta Premium ({c.MetaPremium:0.#}/dia).",
            NivelMeta.Pro => $"{prod}, superando a meta Pro estabelecida ({c.MetaPro:0.#}/dia).",
            NivelMeta.Proximo when c.MetaPro > 0 =>
                $"{prod}, resultado próximo à meta Pro ({c.MetaPro:0.#}/dia), com diferença de {c.MetaPro - media:N1} itens por dia.",
            NivelMeta.Abaixo when c.MetaPro > 0 =>
                $"{prod}, inferior à meta Pro estabelecida ({c.MetaPro:0.#}/dia) em {c.MetaPro - media:N1} itens por dia.",
            NivelMeta.Proximo or NivelMeta.Abaixo =>
                $"{prod}, inferior à meta Premium estabelecida ({c.MetaPremium:0.#}/dia) em {c.MetaPremium - media:N1} itens por dia.",
            _ => $"{prod}."
        });

        var desvios = c.TotalDesvios == 0
            ? "sem registro de desvios internos ou externos"
            : $"com {Plural(c.DesviosInternos, "desvio interno", "desvios internos", "nenhum desvio interno")} e " +
              $"{Plural(c.DesviosExternos, "desvio externo", "desvios externos", "nenhum desvio externo")}";
        partes.Add(QualidadeOk(c)
            ? $"O índice de qualidade apurado foi de {c.Qualidade:N2}%, {desvios}, atendendo ao padrão mínimo de {QualidadeIdeal:N2}%."
            : $"O índice de qualidade apurado foi de {c.Qualidade:N2}%, {desvios}, situando-se abaixo do padrão mínimo de {QualidadeIdeal:N2}%. " +
              $"Para o volume realizado, o limite seria de {Plural(DesviosPermitidos(c), "desvio", "desvios", "nenhum desvio")}.");

        partes.Add(geral switch
        {
            Status.Excelente => "Diante do exposto, o desempenho é avaliado como excelente, recomendando-se a manutenção das práticas adotadas.",
            Status.Bom => "Diante do exposto, o desempenho é considerado satisfatório, recomendando-se a continuidade do desenvolvimento com foco no alcance da meta Premium.",
            Status.Atencao => "Diante do exposto, recomenda-se o acompanhamento dos indicadores que não atingiram o padrão esperado, com definição de ações de melhoria para o próximo período.",
            _ => "Diante do exposto, recomenda-se a elaboração de um plano de ação, com acompanhamento periódico dos indicadores de produção e qualidade."
        });

        return string.Join(" ", partes);
    }

    private static string Plural(int n, string singular, string plural, string? zero = null) =>
        n == 0 && zero is not null ? zero : n == 1 ? $"1 {singular}" : $"{n:N0} {plural}";

    /// <summary>Reconhece pareceres gerados automaticamente pelas versões anteriores (informais).</summary>
    public static bool ParecerAntigoAutomatico(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return false;
        string[] marcas =
        {
            ", seu resultado em ", ", você fechou ", "teve pontos positivos, mas", "precisamos conversar sobre os indicadores",
            "Continue assim!", "Conte comigo para o plano", "Vamos manter a consistência", "Vamos buscar a "
        };
        return marcas.Any(m => texto.Contains(m, StringComparison.Ordinal));
    }
}
