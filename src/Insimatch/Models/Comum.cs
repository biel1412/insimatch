namespace Insimatch.Models;

public static class Datas
{
    private static readonly string[] Meses =
    {
        "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho",
        "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro"
    };

    /// <summary>"2026-10" → "Outubro de 2026".</summary>
    public static string MesPorExtenso(string mes)
    {
        var p = (mes ?? "").Split('-');
        if (p.Length == 2 && int.TryParse(p[0], out var ano) && int.TryParse(p[1], out var m) && m is >= 1 and <= 12)
            return $"{Meses[m - 1]} de {ano}";
        return mes ?? "";
    }

    public static string Iniciais(string nome)
    {
        var partes = (nome ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length == 0) return "?";
        if (partes.Length == 1) return partes[0][..1].ToUpperInvariant();
        return (partes[0][..1] + partes[^1][..1]).ToUpperInvariant();
    }
}

/// <summary>Um número grande de destaque (usado pelos layouts genéricos).</summary>
public record Indicador(string Rotulo, string Valor, string? Sufixo = null, Status Status = Status.Neutro,
                        string? StatusRotulo = null, string? Detalhe = null);

/// <summary>Uma linha da tabela de placar. Grupo != null indica um subtítulo.</summary>
public record LinhaPlacar(string Indicador, string Realizado = "", string? Sufixo = null, string Referencia = "—",
                          Status? Status = null, string? StatusRotulo = null, string? Grupo = null);

/// <summary>Um critério do quadro de veredito.</summary>
public record Criterio(string Texto, bool? Atendido);

/// <summary>Uma opção de menu (tema, fundo, modelo, aba).</summary>
public record Opcao(string Id, string Nome);
