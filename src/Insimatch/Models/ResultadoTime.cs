namespace Insimatch.Models;

/// <summary>Resultado mensal consolidado de um time.</summary>
public class ResultadoTime
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Nome { get; set; } = "";
    public string Mes { get; set; } = DateTime.Today.ToString("yyyy-MM");

    public int MetaVolume { get; set; }
    public int VolumeAtingido { get; set; }

    public decimal MetaQualidade { get; set; } = 99.94m;
    public decimal Qualidade { get; set; }

    public int DesviosInternos { get; set; }
    public int DesviosExternos { get; set; }

    public string Parecer { get; set; } = "";

    /// <summary>Percentual de atingimento da meta de volume.</summary>
    public decimal Atingimento => MetaVolume > 0 ? (decimal)VolumeAtingido / MetaVolume * 100m : 0;

    public int Diferenca => VolumeAtingido - MetaVolume;
    public int TotalDesvios => DesviosInternos + DesviosExternos;
    public string Iniciais => Datas.Iniciais(Nome);
    public string MesPorExtenso => Datas.MesPorExtenso(Mes);

    public ResultadoTime Clonar() => (ResultadoTime)MemberwiseClone();
}

/// <summary>
/// Critérios do time:
/// • Volume: volume atingido ÷ meta de volume (meta atingida ≥ 100%; próximo da meta ≥ 90%).
/// • Qualidade: qualidade do mês comparada com a meta de qualidade.
/// • Está bem: qualidade na meta e volume pelo menos próximo da meta.
/// </summary>
public static class AvaliacaoTime
{
    public const decimal FatorProximo = 90m;

    public static bool TemMetaVolume(ResultadoTime t) => t.MetaVolume > 0;
    public static bool VolumeOk(ResultadoTime t) => TemMetaVolume(t) && t.Atingimento >= 100m;
    public static bool VolumeProximo(ResultadoTime t) => TemMetaVolume(t) && t.Atingimento >= FatorProximo;
    public static bool QualidadeOk(ResultadoTime t) => t.Qualidade >= t.MetaQualidade && t.Qualidade > 0;

    public static Status StatusVolume(ResultadoTime t) =>
        !TemMetaVolume(t) ? Status.Neutro
        : VolumeOk(t) ? Status.Excelente
        : VolumeProximo(t) ? Status.Atencao
        : Status.Critico;

    public static string RotuloVolume(ResultadoTime t) =>
        !TemMetaVolume(t) ? "Sem meta"
        : VolumeOk(t) ? "Meta atingida"
        : VolumeProximo(t) ? "Próximo da meta"
        : "Abaixo da meta";

    public static Status StatusQualidade(ResultadoTime t) =>
        t.Qualidade <= 0 ? Status.Neutro : QualidadeOk(t) ? Status.Excelente : Status.Critico;

    public static string RotuloQualidade(ResultadoTime t) =>
        t.Qualidade <= 0 ? "Não informada" : QualidadeOk(t) ? "Dentro da meta" : "Abaixo da meta";

    public static Status Geral(ResultadoTime t)
    {
        var q = QualidadeOk(t);
        var v = VolumeProximo(t) || !TemMetaVolume(t);
        if (q && VolumeOk(t)) return Status.Excelente;
        if (q && v) return Status.Bom;
        if (q || v) return Status.Atencao;
        return Status.Critico;
    }

    public static string GerarParecer(ResultadoTime t)
    {
        var nome = string.IsNullOrWhiteSpace(t.Nome) ? "do time" : $"do time {t.Nome.Trim()}";
        var periodo = t.MesPorExtenso.ToLowerInvariant();

        if (t.MetaVolume <= 0 && t.VolumeAtingido <= 0 && t.Qualidade <= 0)
            return $"Em análise aos indicadores consolidados {nome} referentes a {periodo}, não constam dados " +
                   "suficientes de volume e qualidade para a emissão de avaliação conclusiva.";

        var geral = Geral(t);
        var partes = new List<string>
        {
            $"Em análise aos indicadores consolidados {nome} referentes a {periodo}, verifica-se " + geral switch
            {
                Status.Excelente => "resultado excelente.",
                Status.Bom => "resultado satisfatório.",
                Status.Atencao => "resultado que requer atenção em parte dos indicadores.",
                _ => "resultado abaixo do esperado."
            }
        };

        if (TemMetaVolume(t))
        {
            var baseTxt = $"O volume atingido foi de {t.VolumeAtingido:N0} itens, frente à meta de {t.MetaVolume:N0}, " +
                          $"o que representa {t.Atingimento:N1}% de atingimento";
            partes.Add(VolumeOk(t)
                ? $"{baseTxt}, com excedente de {t.Diferenca:N0} itens."
                : VolumeProximo(t)
                    ? $"{baseTxt}, resultado próximo à meta, restando {-t.Diferenca:N0} itens para o seu alcance."
                    : $"{baseTxt}, restando {-t.Diferenca:N0} itens para o alcance da meta.");
        }
        else
        {
            partes.Add($"O volume atingido foi de {t.VolumeAtingido:N0} itens, sem meta de volume definida para o período.");
        }

        var desvios = t.TotalDesvios == 0
            ? "sem registro de desvios internos ou externos"
            : $"com {Plural(t.DesviosInternos, "desvio interno", "desvios internos", "nenhum desvio interno")} e " +
              $"{Plural(t.DesviosExternos, "desvio externo", "desvios externos", "nenhum desvio externo")}";

        if (t.Qualidade > 0)
            partes.Add(QualidadeOk(t)
                ? $"O índice de qualidade do mês foi de {t.Qualidade:N2}%, {desvios}, atendendo à meta de {t.MetaQualidade:N2}%."
                : $"O índice de qualidade do mês foi de {t.Qualidade:N2}%, {desvios}, situando-se abaixo da meta de {t.MetaQualidade:N2}%.");
        else
            partes.Add($"A qualidade do mês não foi informada; foram registrados {t.DesviosInternos} desvio(s) interno(s) e {t.DesviosExternos} externo(s).");

        partes.Add(geral switch
        {
            Status.Excelente => "Diante do exposto, o desempenho do time é avaliado como excelente, recomendando-se a manutenção das práticas adotadas.",
            Status.Bom => "Diante do exposto, o desempenho do time é considerado satisfatório, recomendando-se a continuidade das ações para o pleno atingimento das metas.",
            Status.Atencao => "Diante do exposto, recomenda-se o acompanhamento dos indicadores que não atingiram o padrão esperado, com definição de ações de melhoria para o próximo período.",
            _ => "Diante do exposto, recomenda-se a elaboração de um plano de ação para o time, com acompanhamento periódico dos indicadores de volume e qualidade."
        });

        return string.Join(" ", partes);
    }

    private static string Plural(int n, string singular, string plural, string zero) =>
        n == 0 ? zero : n == 1 ? $"1 {singular}" : $"{n:N0} {plural}";
}
