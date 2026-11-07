namespace Insimatch.Models;

/// <summary>Uma apresentação em slides. As imagens ficam no IndexedDB do navegador (não no localStorage).</summary>
public class Apresentacao
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Nome { get; set; } = "";
    public List<Slide> Slides { get; set; } = new();

    /// <summary>Tema visual da apresentação (vazio = cores do site).</summary>
    public string Tema { get; set; } = "";
    /// <summary>Estilo dos títulos: "" (padrão), "elegante", "impacto", "suave".</summary>
    public string Fonte { get; set; } = "";
    public bool MostrarRodape { get; set; } = true;
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

    // ---------- Aparência (vazio = segue o tema da apresentação) ----------
    /// <summary>Estrutura: "" (automática), capa, secao, colunas, destaque.</summary>
    public string Estrutura { get; set; } = "";
    /// <summary>Fundo da página: cor (#rrggbb) ou degradê (linear-gradient...).</summary>
    public string Fundo { get; set; } = "";
    public string CorTexto { get; set; } = "";
    public string CorDestaque { get; set; } = "";
    /// <summary>"" ou "p"/"g": tamanho do texto.</summary>
    public string TamanhoTexto { get; set; } = "";
    /// <summary>"" (esquerda) ou "centro".</summary>
    public string Alinhamento { get; set; } = "";
    /// <summary>Coloca as imagens do outro lado (esquerda / acima).</summary>
    public bool InverterImagem { get; set; }

    public bool AparenciaPersonalizada => Estrutura != "" || Fundo != "" || CorTexto != "" || CorDestaque != ""
                                          || TamanhoTexto != "" || Alinhamento != "" || InverterImagem;
}

/// <summary>Tema de cores de uma apresentação.</summary>
public record TemaSlide(string Id, string Nome, string Fundo, string Texto, string Destaque, string Destaque2);

public static class TemasSlides
{
    public static readonly TemaSlide[] Lista =
    {
        new("", "Do site", "", "", "", ""),
        new("claro", "Claro corporativo", "#ffffff", "#0f172a", "#4f46e5", "#06b6d4"),
        new("papel", "Papel", "#faf7f0", "#292524", "#b45309", "#d97706"),
        new("menta", "Menta", "#ecfdf5", "#064e3b", "#059669", "#10b981"),
        new("noite", "Noite", "#0b1020", "#f1f5f9", "#38bdf8", "#818cf8"),
        new("grafite", "Grafite", "#1f2937", "#f9fafb", "#f97316", "#fbbf24"),
        new("executivo", "Executivo", "linear-gradient(135deg, #0f2a5c 0%, #1e3a8a 100%)", "#ffffff", "#fbbf24", "#f59e0b"),
        new("oceano", "Oceano", "linear-gradient(135deg, #0c4a6e 0%, #0e7490 100%)", "#f0f9ff", "#5eead4", "#a5f3fc"),
        new("aurora", "Aurora", "linear-gradient(135deg, #4c1d95 0%, #be185d 100%)", "#ffffff", "#fde68a", "#f9a8d4"),
        new("vinho", "Vinho", "linear-gradient(135deg, #3b0a1a 0%, #7f1d1d 100%)", "#fff1f2", "#fda4af", "#fecdd3"),
        new("floresta", "Floresta", "linear-gradient(135deg, #052e16 0%, #166534 100%)", "#f0fdf4", "#bef264", "#86efac"),
    };

    public static TemaSlide Achar(string? id) => Lista.FirstOrDefault(t => t.Id == (id ?? "")) ?? Lista[0];

    /// <summary>Classes e variáveis CSS do slide: o que o slide definir vale sobre o tema da apresentação.</summary>
    public static (string Classes, string Estilo) Visual(Apresentacao? deck, Slide s)
    {
        var t = Achar(deck?.Tema);
        var fundo = s.Fundo != "" ? s.Fundo : t.Fundo;
        var texto = s.CorTexto != "" ? s.CorTexto : t.Texto != "" && s.Fundo == "" ? t.Texto
                  : fundo != "" ? (Luminancia(fundo) > 0.55 ? "#0f172a" : "#ffffff") : "";
        var destaque = s.CorDestaque != "" ? s.CorDestaque : t.Destaque;
        var destaque2 = s.CorDestaque != "" ? s.CorDestaque : t.Destaque2;

        var cls = new List<string>();
        var est = new System.Text.StringBuilder();
        if (fundo != "") { cls.Add("tem-fundo"); est.Append($"--slide-fundo:{fundo};"); }
        if (texto != "") { cls.Add("tem-texto"); est.Append($"--slide-texto:{texto};"); }
        if (destaque != "") { cls.Add("tem-destaque"); est.Append($"--slide-destaque:{destaque};--slide-destaque-2:{(destaque2 == "" ? destaque : destaque2)};"); }
        if (s.Estrutura != "") cls.Add("est-" + s.Estrutura);
        if (s.TamanhoTexto != "") cls.Add("txt-" + s.TamanhoTexto);
        if (s.Alinhamento == "centro") cls.Add("alinha-centro");
        if (s.InverterImagem) cls.Add("img-inverter");
        if (!string.IsNullOrEmpty(deck?.Fonte)) cls.Add("fonte-" + deck!.Fonte);
        return (string.Join(' ', cls), est.ToString());
    }

    /// <summary>Luminância (0–1) da primeira cor #rrggbb encontrada (serve para degradês também).</summary>
    public static double Luminancia(string cor)
    {
        var m = System.Text.RegularExpressions.Regex.Match(cor ?? "", "#([0-9a-fA-F]{6})");
        if (!m.Success) return 0;
        var n = Convert.ToInt32(m.Groups[1].Value, 16);
        return (0.299 * ((n >> 16) & 255) + 0.587 * ((n >> 8) & 255) + 0.114 * (n & 255)) / 255;
    }
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
                Estrutura = "capa",
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
