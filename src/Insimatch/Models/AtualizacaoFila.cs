namespace Insimatch.Models;

/// <summary>Uma atualização das filas Centralizada e Autorizar.</summary>
public class AtualizacaoFila
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Data e hora da atualização.</summary>
    public DateTime DataHora { get; set; } = AgoraSemSegundos();

    /// <summary>Fluxos da fila Centralizada com data do dia anterior.</summary>
    public int Centralizada { get; set; }

    /// <summary>Fluxos da fila Autorizar com data do dia anterior.</summary>
    public int Autorizar { get; set; }

    public string Parecer { get; set; } = "";

    public int Total => Centralizada + Autorizar;
    public DateTime DiaAnterior => DataHora.Date.AddDays(-1);
    public string Rotulo => DataHora.ToString("dd/MM/yyyy 'às' HH:mm");

    public AtualizacaoFila Clonar() => (AtualizacaoFila)MemberwiseClone();

    public static DateTime AgoraSemSegundos()
    {
        var n = DateTime.Now;
        return new DateTime(n.Year, n.Month, n.Day, n.Hour, n.Minute, 0);
    }
}

/// <summary>
/// Critérios da fila (fluxos com data do dia anterior; cada página possui 15 fluxos):
/// 0 fluxos = em dia · até 1 página = sob controle · até 3 páginas = atenção · acima disso = acúmulo.
/// </summary>
public static class AvaliacaoFila
{
    public const int FluxosPorPagina = 15;
    public const int PaginasAtencao = 1;   // até 1 página: sob controle
    public const int PaginasCritico = 3;   // até 3 páginas: atenção; acima: acúmulo

    public static int Paginas(int fluxos) => (int)Math.Ceiling(fluxos / (double)FluxosPorPagina);

    public static string PaginasTexto(int fluxos)
    {
        if (fluxos <= 0) return "nenhuma página";
        var completas = fluxos / FluxosPorPagina;
        var resto = fluxos % FluxosPorPagina;
        var pag = completas == 1 ? "1 página" : $"{completas} páginas";
        if (completas == 0) return $"{resto} fluxo(s) na 1ª página";
        return resto == 0 ? pag : $"{pag} e {resto} fluxo(s)";
    }

    public static Status StatusFila(int fluxos) =>
        fluxos <= 0 ? Status.Excelente
        : fluxos <= PaginasAtencao * FluxosPorPagina ? Status.Bom
        : fluxos <= PaginasCritico * FluxosPorPagina ? Status.Atencao
        : Status.Critico;

    public static string RotuloFila(int fluxos) => StatusFila(fluxos) switch
    {
        Status.Excelente => "Em dia",
        Status.Bom => "Sob controle",
        Status.Atencao => "Atenção",
        _ => "Acúmulo"
    };

    /// <summary>A situação geral é a pior das duas filas.</summary>
    public static Status Geral(AtualizacaoFila f)
    {
        var a = StatusFila(f.Centralizada);
        var b = StatusFila(f.Autorizar);
        return (Status)Math.Max((int)a, (int)b);
    }

    public static string RotuloGeral(AtualizacaoFila f) => Geral(f) switch
    {
        Status.Excelente => "Filas em dia",
        Status.Bom => "Sob controle",
        Status.Atencao => "Atenção",
        _ => "Acúmulo"
    };

    private static string Situacao(int fluxos) => StatusFila(fluxos) switch
    {
        Status.Excelente => "encontrando-se em dia",
        Status.Bom => "situação considerada sob controle",
        Status.Atencao => "situação que requer atenção",
        _ => "caracterizando acúmulo que demanda priorização"
    };

    private static string Fila(string nome, int fluxos, string dia) => fluxos <= 0
        ? $"A fila {nome} não apresenta fluxos com data do dia anterior ({dia}), encontrando-se em dia."
        : $"A fila {nome} apresenta {fluxos:N0} fluxo(s) com data do dia anterior ({dia}), " +
          $"o equivalente a {PaginasTexto(fluxos)}, {Situacao(fluxos)}.";

    public static string GerarParecer(AtualizacaoFila f)
    {
        var dia = f.DiaAnterior.ToString("dd/MM/yyyy");
        var partes = new List<string>
        {
            $"Na atualização de {f.DataHora:dd/MM/yyyy} às {f.DataHora:HH:mm}, verifica-se a seguinte situação das filas.",
            Fila("Centralizada", f.Centralizada, dia),
            Fila("Autorizar", f.Autorizar, dia)
        };

        if (f.Total > 0)
            partes.Add($"No total, são {f.Total:N0} fluxo(s) pendente(s) do dia anterior ({PaginasTexto(f.Total)}).");

        partes.Add(Geral(f) switch
        {
            Status.Excelente => "Diante do exposto, as filas encontram-se em dia, recomendando-se a manutenção do ritmo de tratamento.",
            Status.Bom => "Diante do exposto, as filas encontram-se sob controle, recomendando-se a conclusão dos fluxos remanescentes ao longo do dia.",
            Status.Atencao => "Diante do exposto, recomenda-se priorizar o tratamento dos fluxos do dia anterior, com acompanhamento nas próximas atualizações.",
            _ => "Diante do exposto, recomenda-se a priorização imediata dos fluxos do dia anterior e o reforço na distribuição da equipe até a normalização das filas."
        });

        return string.Join(" ", partes);
    }
}
