namespace Insimatch.Models;

/// <summary>O que aparece na interface. Tudo começa visível.</summary>
public class Configuracoes
{
    // Barra de ferramentas
    public bool MostrarCor { get; set; } = true;
    public bool MostrarFundo { get; set; } = true;
    public bool MostrarMovimento { get; set; } = true;
    public bool MostrarModelo { get; set; } = true;
    public bool MostrarRelatorio { get; set; } = true;
    public bool MostrarTelaCheia { get; set; } = true;
    public bool MostrarIA { get; set; } = true;

    // Menu
    public bool MostrarNovaAba { get; set; } = true;
    public bool MostrarTrocaAbasApresentacao { get; set; } = true;

    // Abas fixas ocultas (ids: individual, time, cliente)
    public List<string> AbasOcultas { get; set; } = new();

    public Configuracoes Copia() => new()
    {
        MostrarCor = MostrarCor, MostrarFundo = MostrarFundo, MostrarMovimento = MostrarMovimento,
        MostrarModelo = MostrarModelo, MostrarRelatorio = MostrarRelatorio, MostrarTelaCheia = MostrarTelaCheia, MostrarIA = MostrarIA,
        MostrarNovaAba = MostrarNovaAba, MostrarTrocaAbasApresentacao = MostrarTrocaAbasApresentacao,
        AbasOcultas = new List<string>(AbasOcultas)
    };
}
