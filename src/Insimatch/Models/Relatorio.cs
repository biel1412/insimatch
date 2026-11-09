namespace Insimatch.Models;

public record RelKpi(string Rotulo, string Valor, string? Detalhe = null, Status Status = Status.Neutro);

public record RelLinha(string Indicador, string Realizado, string Referencia = "—", Status? Status = null,
                       string? StatusRotulo = null, decimal? Atingimento = null);

public record RelSecao(string Titulo, List<RelLinha> Linhas, string? Texto = null, List<string>? Imagens = null, string[]? Colunas = null);

public record RelBarra(string Nome, decimal Percentual, Status Status);

/// <summary>Conteúdo de um relatório, montado a partir da apresentação aberta.</summary>
public class RelatorioDados
{
    public string Tipo { get; set; } = "";
    public string Titulo { get; set; } = "";
    public string Sigla { get; set; } = "";
    public List<string> Info { get; set; } = new();
    public Status Status { get; set; } = Status.Neutro;
    public string StatusRotulo { get; set; } = "";
    public string Resumo { get; set; } = "";
    public List<RelKpi> Kpis { get; set; } = new();
    public List<RelSecao> Secoes { get; set; } = new();
    public List<RelBarra> Barras { get; set; } = new();
    public string Parecer { get; set; } = "";
    /// <summary>Fluxograma a desenhar no relatório (abas de fluxograma).</summary>
    public Fluxo? Fluxo { get; set; }
    public bool FluxoHorizontal { get; set; }
    public DateTime GeradoEm { get; set; } = DateTime.Now;

    public string NomeArquivo
    {
        get
        {
            var baseNome = string.IsNullOrWhiteSpace(Titulo) ? Tipo : Titulo;
            var limpo = new string(baseNome.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
            return $"relatorio-{(limpo.Length == 0 ? "insitalent" : limpo)}-{GeradoEm:yyyy-MM-dd}.html";
        }
    }
}

/// <summary>Cada aba informa aqui como montar o relatório do que está aberto.</summary>
public class RelatorioHost
{
    public Func<RelatorioDados?>? Gerar { get; set; }
}

public static class Relatorios
{
    // ---------- Individual ----------
    public static RelatorioDados De(Colaborador c)
    {
        var media = c.MediaDiaria;
        var sVol = Avaliacao.StatusVolume(c);
        var sQual = Avaliacao.StatusQualidade(c);
        var geral = Avaliacao.Geral(c);
        var permitidos = Avaliacao.DesviosPermitidos(c);
        var r = new RelatorioDados
        {
            Tipo = "Resultado individual",
            Titulo = string.IsNullOrWhiteSpace(c.Nome) ? "Colaborador" : c.Nome,
            Sigla = c.Iniciais,
            Info = new[] { c.Equipe, c.MesPorExtenso }.Where(x => !string.IsNullOrWhiteSpace(x)).ToList(),
            Status = geral,
            StatusRotulo = Avaliacao.RotuloGeral(geral),
            Resumo = $"Volume: {Avaliacao.RotuloVolume(c).ToLowerInvariant()} · Qualidade: {Avaliacao.RotuloQualidade(c).ToLowerInvariant()}",
            Parecer = c.Parecer
        };
        r.Kpis.Add(new("Média diária", $"{media:N1}/dia", MetasTexto(c.MetaPro, c.MetaPremium, "/dia"), sVol));
        r.Kpis.Add(new("Qualidade", $"{c.Qualidade:N2}%", $"meta ≥ {Avaliacao.MetaQualTexto(c)}", sQual));
        r.Kpis.Add(new("Volume do mês", c.Volume.ToString("N0"), $"{c.DiasConsiderados} dias trabalhados"));
        r.Kpis.Add(new("Desvios", c.TotalDesvios.ToString(), $"máx. {permitidos} para {Avaliacao.MetaQualTexto(c)}",
            c.Volume > 0 ? (c.TotalDesvios <= permitidos ? Status.Excelente : Status.Critico) : Status.Neutro));

        var prod = new List<RelLinha>
        {
            new("Média diária", $"{media:N1}/dia", MetasTexto(c.MetaPro, c.MetaPremium, "/dia") ?? "—", sVol, Avaliacao.RotuloVolume(c),
                c.MetaPro > 0 ? media / c.MetaPro * 100 : null),
            new("Volume do mês", c.Volume.ToString("N0"),
                MetasTexto(c.MetaPro * c.DiasConsiderados, c.MetaPremium * c.DiasConsiderados, "") ?? "—"),
            new("Dias trabalhados", c.DiasConsiderados.ToString())
        };
        var qual = new List<RelLinha>
        {
            new("Qualidade", $"{c.Qualidade:N2}%", $"≥ {Avaliacao.MetaQualTexto(c)}", sQual, Avaliacao.RotuloQualidade(c)),
            new("Desvios internos", c.DesviosInternos.ToString()),
            new("Desvios externos", c.DesviosExternos.ToString()),
            new("Total de desvios", c.TotalDesvios.ToString(), $"máx. {permitidos}",
                c.Volume > 0 ? (c.TotalDesvios <= permitidos ? Status.Excelente : Status.Critico) : null,
                c.Volume > 0 ? (c.TotalDesvios <= permitidos ? "Dentro do limite" : "Acima do limite") : null)
        };
        r.Secoes.Add(new("Produção", prod));
        r.Secoes.Add(new("Qualidade", qual));

        if (c.MetaPro > 0) r.Barras.Add(new("Média × meta Pro", media / c.MetaPro * 100, media >= c.MetaPro ? Status.Excelente : Status.Atencao));
        if (c.MetaPremium > 0) r.Barras.Add(new("Média × meta Premium", media / c.MetaPremium * 100, media >= c.MetaPremium ? Status.Excelente : Status.Atencao));
        return r;
    }

    // ---------- Cliente e abas personalizadas ----------
    public static RelatorioDados De(Cliente cl, ContextoPainel? ctx = null)
    {
        ctx ??= ContextoPainel.Cliente;
        var geral = AvaliacaoCliente.Geral(cl);
        var comMeta = AvaliacaoCliente.ComMeta(cl).ToList();
        var naMeta = comMeta.Count(c => AvaliacaoCliente.StatusCampo(c) == Status.Excelente);
        var r = new RelatorioDados
        {
            Tipo = ctx.RotuloSup,
            Titulo = string.IsNullOrWhiteSpace(cl.Nome) ? ctx.RotuloSup : cl.Nome,
            Sigla = cl.Iniciais,
            Info = new[] { cl.Periodo, cl.Descricao }.Where(x => !string.IsNullOrWhiteSpace(x)).ToList(),
            Status = geral,
            StatusRotulo = AvaliacaoCliente.RotuloGeral(cl),
            Resumo = comMeta.Count == 0 ? $"{cl.Campos.Count} indicador(es) registrado(s), sem metas definidas" : $"{naMeta} de {comMeta.Count} indicadores com meta atendidos",
            Parecer = cl.Parecer
        };

        // Destaques primeiro; completa até 4 com os campos com meta e depois os numéricos
        var kpis = cl.Campos.Where(c => c.Destaque)
            .Concat(comMeta)
            .Concat(cl.Campos.Where(c => c.Numerico && AvaliacaoCliente.ValorPreenchido(c)))
            .DistinctBy(c => c.Id).ToList();
        foreach (var c in kpis.Take(4))
            r.Kpis.Add(new(c.Nome, AvaliacaoCliente.Valor(c),
                c.TemMeta ? $"meta {AvaliacaoCliente.MetaTexto(c)} · {AvaliacaoCliente.Atingimento(c):N0}%" : (string.IsNullOrWhiteSpace(c.Grupo) ? null : c.Grupo),
                AvaliacaoCliente.StatusCampo(c)));

        foreach (var (grupo, campos) in AvaliacaoCliente.Grupos(cl))
            r.Secoes.Add(new(grupo, campos.Select(c => new RelLinha(
                c.Nome, AvaliacaoCliente.Valor(c),
                c.Meta.HasValue && c.Numerico ? AvaliacaoCliente.MetaTexto(c) : "—",
                c.TemMeta ? AvaliacaoCliente.StatusCampo(c) : null,
                c.TemMeta ? AvaliacaoCliente.RotuloCampo(c) : null,
                c.TemMeta ? AvaliacaoCliente.Atingimento(c) : null)).ToList()));

        foreach (var c in comMeta)
            r.Barras.Add(new(c.Nome, AvaliacaoCliente.Atingimento(c), AvaliacaoCliente.StatusCampo(c)));
        return r;
    }

    // ---------- Slides ----------
    public static RelatorioDados De(Apresentacao ap, ContextoPainel ctx, IReadOnlyDictionary<string, string> imagens)
    {
        var campos = ap.Slides.SelectMany(s => s.Campos).ToList();
        var comMeta = campos.Where(c => c.TemMeta).ToList();
        var naMeta = comMeta.Count(c => AvaliacaoCliente.StatusCampo(c) == Status.Excelente);
        var r = new RelatorioDados
        {
            Tipo = ctx.RotuloSup,
            Titulo = string.IsNullOrWhiteSpace(ap.Nome) ? ctx.RotuloSup : ap.Nome,
            Sigla = Datas.Iniciais(string.IsNullOrWhiteSpace(ap.Nome) ? ctx.RotuloSup : ap.Nome),
            Info = new() { $"{ap.Slides.Count} slide(s)" },
            Status = Status.Neutro,
            StatusRotulo = "Apresentação",
            Resumo = comMeta.Count > 0
                ? $"{naMeta} de {comMeta.Count} indicadores com meta atendidos ao longo dos slides"
                : $"{ap.Slides.Count} slide(s) com {campos.Count} campo(s)"
        };
        if (comMeta.Count > 0)
        {
            r.Status = comMeta.All(c => AvaliacaoCliente.StatusCampo(c) == Status.Excelente) ? Status.Excelente
                : comMeta.Any(c => AvaliacaoCliente.StatusCampo(c) == Status.Critico) ? Status.Atencao : Status.Bom;
            r.StatusRotulo = r.Status switch { Status.Excelente => "Todas as metas atendidas", Status.Bom => "Está bem", _ => "Atenção" };
        }
        for (var i = 0; i < ap.Slides.Count; i++)
        {
            var s = ap.Slides[i];
            var titulo = $"{i + 1}. {(string.IsNullOrWhiteSpace(s.Titulo) ? "Slide sem título" : s.Titulo)}";
            var texto = string.Join("\n", new[] { s.Subtitulo }.Where(x => !string.IsNullOrWhiteSpace(x))
                .Concat(SlidesRegras.Linhas(s.Texto).Select(l => l.Topico ? "• " + l.Texto : l.Texto)));
            var linhas = s.Campos.Select(c => new RelLinha(c.Nome, AvaliacaoCliente.Valor(c),
                c.Meta.HasValue && c.Numerico ? AvaliacaoCliente.MetaTexto(c) : "—",
                c.TemMeta ? AvaliacaoCliente.StatusCampo(c) : null,
                c.TemMeta ? AvaliacaoCliente.RotuloCampo(c) : null,
                c.TemMeta ? AvaliacaoCliente.Atingimento(c) : null)).ToList();
            var imgs = s.Imagens.Take(SlidesRegras.MaxImagens).Where(im => imagens.ContainsKey(im.Id)).Select(im => imagens[im.Id]).ToList();
            r.Secoes.Add(new RelSecao(titulo, linhas, texto.Length > 0 ? texto : null, imgs.Count > 0 ? imgs : null));
        }
        foreach (var c in comMeta.Take(12))
            r.Barras.Add(new(c.Nome, AvaliacaoCliente.Atingimento(c), AvaliacaoCliente.StatusCampo(c)));
        return r;
    }

    // ---------- Fluxograma ----------
    public static RelatorioDados De(Fluxo f, ContextoPainel ctx, bool horizontal)
    {
        string NomeNo(string id) => f.Nos.FirstOrDefault(n => n.Id == id)?.Texto is { Length: > 0 } t ? t : "Etapa sem nome";
        var r = new RelatorioDados
        {
            Tipo = ctx.RotuloSup,
            Titulo = string.IsNullOrWhiteSpace(f.Nome) ? "Fluxograma" : f.Nome,
            Sigla = Datas.Iniciais(string.IsNullOrWhiteSpace(f.Nome) ? ctx.RotuloSup : f.Nome),
            Info = new[] { f.Descricao }.Where(x => !string.IsNullOrWhiteSpace(x)).ToList(),
            Status = Status.Neutro,
            StatusRotulo = "Fluxograma",
            Resumo = $"{f.Nos.Count} etapa(s) · {f.Nos.Count(n => n.Tipo == TipoNo.Decisao)} decisão(ões)",
            Fluxo = f,
            FluxoHorizontal = horizontal
        };
        r.Secoes.Add(new RelSecao("Etapas",
            f.Nos.Select((n, i) => new RelLinha($"{i + 1}. {(string.IsNullOrWhiteSpace(n.Texto) ? "Etapa sem nome" : n.Texto)}",
                FluxoRegras.NomeTipo(n.Tipo),
                n.Proximos.Count == 0 ? "—" : string.Join("; ", n.Proximos.Select(p => (string.IsNullOrWhiteSpace(p.Rotulo) ? "" : p.Rotulo + ": ") + NomeNo(p.Destino))))).ToList(),
            Colunas: new[] { "Etapa", "Tipo", "Segue para", "" }));
        return r;
    }

    private static string? MetasTexto(decimal pro, decimal premium, string sufixo)
    {
        var fmt = sufixo.Length > 0 ? "0.#" : "N0";
        var l = new List<string>();
        if (pro > 0) l.Add($"Pro {pro.ToString(fmt)}{sufixo}");
        if (premium > 0) l.Add($"Premium {premium.ToString(fmt)}{sufixo}");
        return l.Count == 0 ? null : string.Join(" · ", l);
    }
}
