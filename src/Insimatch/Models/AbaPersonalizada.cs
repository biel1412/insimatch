namespace Insimatch.Models;

/// <summary>Uma aba criada pelo usuário. Funciona como a aba Cliente: campos livres, gráficos e parecer.</summary>
public class AbaPersonalizada
{
    public string Id { get; set; } = "aba-" + Guid.NewGuid().ToString("N")[..8];
    public string Nome { get; set; } = "";
    public string Icone { get; set; } = "grafico";

    /// <summary>Como chamar cada registro da aba (ex.: projeto, unidade, contrato).</summary>
    public string Singular { get; set; } = "registro";

    /// <summary>painel (campos flexíveis), slides ou fluxo.</summary>
    public string Tipo { get; set; } = "painel";

    public string ChaveDados => $"insitalent.dados.{Id}";

    public string NomeTipo => Tipo switch { "slides" => "Apresentação em slides", "fluxo" => "Fluxograma", _ => "Painel de indicadores" };
}

/// <summary>Textos que mudam conforme a aba que usa o painel flexível.</summary>
public record ContextoPainel(string RotuloSup, string Singular, bool Generico)
{
    public static readonly ContextoPainel Cliente = new("Resultado do cliente", "cliente", false);

    public string ListaRotulo(int n) => Generico ? $"Lista ({n})" : $"Clientes ({n})";
    public string NomeRotulo => Generico ? "Nome" : "Nome do cliente";
    public string Novo => Generico ? $"+ Adicionar {Singular}" : "+ Novo cliente";
}

/// <summary>Dados do formulário de criar/editar aba.</summary>
public class FormAba
{
    public string Nome { get; set; } = "";
    public string Icone { get; set; } = "grafico";
    public string Singular { get; set; } = "";
    /// <summary>branco, exemplo, slides ou fluxo.</summary>
    public string Inicio { get; set; } = "branco";
}
