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
    public int MetaPro { get; set; }
    public int MetaPremium { get; set; }
    public int DiasConsiderados { get; set; }

    // Qualidade
    public decimal Qualidade { get; set; }
    public int DesviosInternos { get; set; }
    public int DesviosExternos { get; set; }

    public string Parecer { get; set; } = "";

    public decimal MediaDiaria => DiasConsiderados > 0
        ? Math.Round((decimal)Volume / DiasConsiderados, 1)
        : 0;

    public int TotalDesvios => DesviosInternos + DesviosExternos;

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
