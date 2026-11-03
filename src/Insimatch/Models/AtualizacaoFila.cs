namespace Insimatch.Models;

/// <summary>Uma atualização das filas Centralizada e Autorizar.</summary>
public class AtualizacaoFila
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Data e hora da atualização.</summary>
    public DateTime DataHora { get; set; } = AgoraSemSegundos();

    /// <summary>Total de fluxos na fila Centralizada (do dia e do dia anterior, misturados).</summary>
    public int CentralizadaTotal { get; set; }

    /// <summary>Fluxos da fila Centralizada com data do dia anterior (informado manualmente).</summary>
    public int Centralizada { get; set; }

    /// <summary>Total de fluxos na fila Autorizar (do dia e do dia anterior, misturados).</summary>
    public int AutorizarTotal { get; set; }

    /// <summary>Fluxos da fila Autorizar com data do dia anterior (informado manualmente).</summary>
    public int Autorizar { get; set; }

    public string Parecer { get; set; } = "";

    /// <summary>Total de fluxos com data do dia anterior nas duas filas.</summary>
    public int Total => Centralizada + Autorizar;
    public int TotalNaFila => CentralizadaTotal + AutorizarTotal;
    public string Rotulo => DataHora.ToString("dd/MM/yyyy 'às' HH:mm");

    public AtualizacaoFila Clonar() => (AtualizacaoFila)MemberwiseClone();

    public static DateTime AgoraSemSegundos()
    {
        var n = DateTime.Now;
        return new DateTime(n.Year, n.Month, n.Day, n.Hour, n.Minute, 0);
    }
}

/// <summary>
/// Critérios da fila. Cada fila tem o total de fluxos (do dia e do dia anterior, misturados)
/// e a quantidade com data do dia anterior, ambos informados manualmente. Cada página possui 15 fluxos.
/// A situação considera os fluxos do dia anterior:
/// 0 = em dia · até 15 (1 página) = sob controle · até 45 (3 páginas) = atenção · acima disso = acúmulo.
/// </summary>
public static class AvaliacaoFila
{
    public const int FluxosPorPagina = 15;
    public const int PaginasAtencao = 1;
    public const int PaginasCritico = 3;

    public static int Paginas(int fluxos) => (int)Math.Ceiling(Math.Max(0, fluxos) / (double)FluxosPorPagina);

    public static string Fluxos(int n) => n == 1 ? "1 fluxo" : $"{n:N0} fluxos";

    public static string PaginasTexto(int fluxos)
    {
        var p = Paginas(fluxos);
        return p == 0 ? "nenhuma página" : p == 1 ? "1 página" : $"{p} páginas";
    }

    public static Status StatusFila(int doDiaAnterior) =>
        doDiaAnterior <= 0 ? Status.Excelente
        : doDiaAnterior <= PaginasAtencao * FluxosPorPagina ? Status.Bom
        : doDiaAnterior <= PaginasCritico * FluxosPorPagina ? Status.Atencao
        : Status.Critico;

    public static string RotuloFila(int doDiaAnterior) => StatusFila(doDiaAnterior) switch
    {
        Status.Excelente => "Em dia",
        Status.Bom => "Sob controle",
        Status.Atencao => "Atenção",
        _ => "Acúmulo"
    };

    /// <summary>A situação geral é a pior das duas filas.</summary>
    public static Status Geral(AtualizacaoFila f) =>
        (Status)Math.Max((int)StatusFila(f.Centralizada), (int)StatusFila(f.Autorizar));

    public static string RotuloGeral(AtualizacaoFila f) => Geral(f) switch
    {
        Status.Excelente => "Filas em dia",
        Status.Bom => "Sob controle",
        Status.Atencao => "Atenção",
        _ => "Acúmulo"
    };

    private static string Situacao(int doDiaAnterior) => StatusFila(doDiaAnterior) switch
    {
        Status.Excelente => "encontrando-se em dia",
        Status.Bom => "situação considerada sob controle",
        Status.Atencao => "situação que requer atenção",
        _ => "caracterizando acúmulo que demanda priorização"
    };

    private static string Fila(string nome, int total, int anterior)
    {
        var tamanho = $"A fila {nome} possui {Fluxos(total)} ({PaginasTexto(total)})";
        return anterior <= 0
            ? $"{tamanho}, sem fluxos com data do dia anterior, encontrando-se em dia."
            : $"{tamanho}, dos quais {Fluxos(anterior)} com data do dia anterior, {Situacao(anterior)}.";
    }

    public static string GerarParecer(AtualizacaoFila f)
    {
        var partes = new List<string>
        {
            $"Na atualização de {f.DataHora:dd/MM/yyyy} às {f.DataHora:HH:mm}, verifica-se a seguinte situação das filas.",
            Fila("Centralizada", f.CentralizadaTotal, f.Centralizada),
            Fila("Autorizar", f.AutorizarTotal, f.Autorizar)
        };

        if (f.Total > 0)
            partes.Add(f.Total == 1
                ? "No total, há 1 fluxo pendente com data do dia anterior."
                : $"No total, são {f.Total:N0} fluxos pendentes com data do dia anterior.");

        partes.Add(Geral(f) switch
        {
            Status.Excelente => "Diante do exposto, as filas encontram-se em dia, recomendando-se a manutenção do ritmo de tratamento.",
            Status.Bom => "Diante do exposto, as filas encontram-se sob controle, recomendando-se a conclusão dos fluxos do dia anterior ao longo do dia.",
            Status.Atencao => "Diante do exposto, recomenda-se priorizar o tratamento dos fluxos do dia anterior, com acompanhamento nas próximas atualizações.",
            _ => "Diante do exposto, recomenda-se a priorização imediata dos fluxos do dia anterior e o reforço na distribuição da equipe até a normalização das filas."
        });

        return string.Join(" ", partes);
    }
}
