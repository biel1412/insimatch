namespace Insimatch.Models;

/// <summary>Planilha Excel montada pela IA (abas, colunas, linhas, fórmulas e destaques).</summary>
public class PlanilhaExcel
{
    public string Nome { get; set; } = "";
    public List<AbaExcel> Abas { get; set; } = new();
}

public class AbaExcel
{
    public string Nome { get; set; } = "";
    public List<ColunaExcel> Colunas { get; set; } = new();
    /// <summary>Valores: string (texto; "=" no início = fórmula), double, bool ou null.</summary>
    public List<List<object?>> Linhas { get; set; } = new();
    /// <summary>Linha de totais logo após os dados (opcional).</summary>
    public List<object?>? Total { get; set; }
    public List<DestaqueExcel> Destaques { get; set; } = new();
}

public class ColunaExcel
{
    public string Titulo { get; set; } = "";
    /// <summary>texto, inteiro, numero, moeda, percentual ou data.</summary>
    public string Tipo { get; set; } = "texto";
    public double Largura { get; set; }
}

public class DestaqueExcel
{
    public int Coluna { get; set; }
    /// <summary>&lt;, &lt;=, &gt;, &gt;=, = ou &lt;&gt;</summary>
    public string Operador { get; set; } = "<";
    public object? Valor { get; set; }
    /// <summary>vermelho, verde, amarelo ou azul.</summary>
    public string Cor { get; set; } = "vermelho";
}
