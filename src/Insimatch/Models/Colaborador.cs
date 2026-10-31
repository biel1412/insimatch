namespace Insimatch.Models;

/// <summary>Resultado mensal individual de um colaborador.</summary>
public class Colaborador
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Nome { get; set; } = "";
    public string Equipe { get; set; } = "";

    /// <summary>Mês de referência no formato yyyy-MM.</summary>
    public string Mes { get; set; } = DateTime.Today.ToString("yyyy-MM");

    // Produção
    public int Volume { get; set; }
    /// <summary>Meta Pro por dia trabalhado.</summary>
    public decimal MetaPro { get; set; }
    /// <summary>Meta Premium por dia trabalhado.</summary>
    public decimal MetaPremium { get; set; }
    public int DiasConsiderados { get; set; }

    // Qualidade (calculada a partir dos desvios)
    public int DesviosInternos { get; set; }
    public int DesviosExternos { get; set; }

    public string Parecer { get; set; } = "";

    /// <summary>Média por dia trabalhado (sem arredondar; a tela formata).</summary>
    public decimal MediaDiaria => DiasConsiderados > 0
        ? (decimal)Volume / DiasConsiderados
        : 0;

    public int TotalDesvios => DesviosInternos + DesviosExternos;

    /// <summary>
    /// Qualidade = (volume − desvios internos − desvios externos) ÷ volume.
    /// Truncada em 2 casas para que o valor exibido nunca pareça maior que o real.
    /// </summary>
    public decimal Qualidade => Volume > 0
        ? Math.Max(0, Math.Floor((decimal)(Volume - TotalDesvios) / Volume * 10000m) / 100m)
        : 0;

    public string Iniciais
    {
        get
        {
            var partes = Nome.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length == 0) return "?";
            if (partes.Length == 1) return partes[0][..1].ToUpperInvariant();
            return (partes[0][..1] + partes[^1][..1]).ToUpperInvariant();
        }
    }

    public string MesPorExtenso
    {
        get
        {
            string[] nomes = { "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho",
                               "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro" };
            var p = Mes.Split('-');
            if (p.Length == 2 && int.TryParse(p[0], out var ano) && int.TryParse(p[1], out var m) && m is >= 1 and <= 12)
                return $"{nomes[m - 1]} de {ano}";
            return Mes;
        }
    }

    public Colaborador Clonar() => (Colaborador)MemberwiseClone();
}
