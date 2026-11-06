namespace Insimatch.Models;

/// <summary>Uma apresentação em slides. As imagens ficam no IndexedDB do navegador (não no localStorage).</summary>
public class Apresentacao
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Nome { get; set; } = "";
    public List<Slide> Slides { get; set; } = new();
}

public class Slide
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public string Titulo { get; set; } = "";
    public string Subtitulo { get; set; } = "";

    /// <summary>Texto livre. Linhas começando com "-" ou "•" viram tópicos.</summary>
    public string Texto { get; set; } = "";

    public List<ImagemSlide> Imagens { get; set; } = new();
    public List<CampoCliente> Campos { get; set; } = new();
}

public class ImagemSlide
{
    public string Id { get; set; } = "img-" + Guid.NewGuid().ToString("N")[..12];
    public string Legenda { get; set; } = "";
    public int Largura { get; set; }
    public int Altura { get; set; }

    /// <summary>Mostrar a imagem inteira (true) ou preencher o espaço recortando as bordas (false).</summary>
    public bool Inteira { get; set; } = true;

    public bool Paisagem => Largura >= Altura * 1.15;
}

public static class SlidesRegras
{
    public const int MaxImagens = 2;

    public static List<(bool Topico, string Texto)> Linhas(string texto) =>
        (texto ?? "").Replace("\r", "").Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .Select(l => l.StartsWith("-") || l.StartsWith("•") || l.StartsWith("*")
                ? (true, l.TrimStart('-', '•', '*').Trim())
                : (false, l))
            .ToList();

    /// <summary>
    /// Escolhe o layout do slide a partir do conteúdo, como a plataforma faz com os campos:
    /// sem imagem → conteúdo ocupa tudo; só imagens → imagem cheia ou galeria;
    /// conteúdo curto + imagem horizontal → imagem em destaque abaixo; demais → imagens ao lado.
    /// </summary>
    public static string Layout(Slide s)
    {
        var linhas = Linhas(s.Texto);
        var temConteudo = s.Campos.Count > 0 || linhas.Count > 0;
        var n = Math.Min(s.Imagens.Count, MaxImagens);
        var curto = s.Campos.Count <= 2 && linhas.Count <= 3 && s.Campos.Count + linhas.Count <= 3;

        if (n == 0) return "so-conteudo";
        if (!temConteudo) return n == 1 ? "imagem-cheia" : "galeria";
        if (n == 1) return s.Imagens[0].Paisagem && curto ? "imagem-baixo" : "imagem-lado";
        return s.Imagens.Take(2).All(i => i.Paisagem) && curto ? "duas-baixo" : "duas-lado";
    }

    public static Apresentacao Exemplo() => new()
    {
        Nome = "Apresentação",
        Slides = new()
        {
            new()
            {
                Titulo = "Resultados do trimestre",
                Subtitulo = "Visão geral para a equipe",
                Texto = "- Crescimento consistente no volume\n- Qualidade acima da meta em dois dos três meses\n- Próximo passo: reduzir o tempo de atendimento"
            },
            new()
            {
                Titulo = "Indicadores principais",
                Subtitulo = "Comparação com as metas",
                Campos = new()
                {
                    new() { Nome = "Volume", Tipo = TipoCampo.Inteiro, Valor = 38420, Meta = 36000, Visual = VisualCampo.Barra },
                    new() { Nome = "Qualidade", Tipo = TipoCampo.Percentual, Casas = 2, Valor = 99.95m, Meta = 99.94m, Tolerancia = 99, Visual = VisualCampo.Medidor },
                    new() { Nome = "Tempo médio", Tipo = TipoCampo.Decimal, Casas = 1, Unidade = "min", Valor = 6.4m, Meta = 6, Sentido = SentidoMeta.MenorMelhor, Visual = VisualCampo.Comparativo }
                }
            },
            new()
            {
                Titulo = "Próximos passos",
                Texto = "Plano para o próximo trimestre:\n- Treinamento sobre os desvios mais frequentes\n- Revisão semanal das filas\n- Acompanhamento individual das metas"
            }
        }
    };
}
