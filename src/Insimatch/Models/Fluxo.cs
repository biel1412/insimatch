namespace Insimatch.Models;

public enum TipoNo { Inicio, Etapa, Decisao, Documento, Dados, Fim }

public class Fluxo
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Nome { get; set; } = "";
    public string Descricao { get; set; } = "";
    public List<NoFluxo> Nos { get; set; } = new();
}

public class NoFluxo
{
    public string Id { get; set; } = "n" + Guid.NewGuid().ToString("N")[..8];
    public string Texto { get; set; } = "";
    public TipoNo Tipo { get; set; } = TipoNo.Etapa;
    public List<Ligacao> Proximos { get; set; } = new();
}

public class Ligacao
{
    public string Destino { get; set; } = "";
    public string Rotulo { get; set; } = "";
}

/// <summary>Posições calculadas para desenhar o fluxograma.</summary>
public record NoPos(NoFluxo No, double X, double Y, double L, double A, int Nivel);
public record SetaPos(string Caminho, string Rotulo, double RX, double RY, bool Retorno);
public record DesenhoFluxo(List<NoPos> Nos, List<SetaPos> Setas, double Largura, double Altura);

public static class FluxoRegras
{
    public const double L = 210, A = 68, LDecisao = 190, ADecisao = 104;
    private const double EspX = 56, EspY = 64, Margem = 34;

    public static string NomeTipo(TipoNo t) => t switch
    {
        TipoNo.Inicio => "Início",
        TipoNo.Etapa => "Etapa",
        TipoNo.Decisao => "Decisão",
        TipoNo.Documento => "Documento",
        TipoNo.Dados => "Dados / sistema",
        _ => "Fim"
    };

    public static string IconeTipo(TipoNo t) => t switch
    {
        TipoNo.Inicio => "▶",
        TipoNo.Etapa => "▭",
        TipoNo.Decisao => "◇",
        TipoNo.Documento => "▤",
        TipoNo.Dados => "▱",
        _ => "■"
    };

    private static (double L, double A) Tamanho(NoFluxo n) => n.Tipo == TipoNo.Decisao ? (LDecisao, ADecisao) : (L, A);

    /// <summary>
    /// Layout automático em camadas: cada etapa fica um nível abaixo da anterior (caminho mais longo),
    /// ligações que voltam para trás são desenhadas pela lateral, e a ordem dentro do nível
    /// segue a média das posições de quem aponta para ela (menos cruzamentos).
    /// </summary>
    public static DesenhoFluxo Desenhar(Fluxo f, bool horizontal)
    {
        var nos = f.Nos;
        var porId = nos.ToDictionary(n => n.Id);
        var arestas = nos.SelectMany(n => n.Proximos.Where(p => porId.ContainsKey(p.Destino)).Select(p => (De: n.Id, Para: p.Destino, p.Rotulo))).ToList();

        // 1) Identifica arestas de retorno (ciclos) com uma busca em profundidade
        var retorno = new HashSet<(string, string)>();
        var estado = new Dictionary<string, int>(); // 0 = novo, 1 = na pilha, 2 = concluído
        void Visitar(string id)
        {
            estado[id] = 1;
            foreach (var a in arestas.Where(a => a.De == id))
            {
                var s = estado.GetValueOrDefault(a.Para);
                if (s == 1) retorno.Add((a.De, a.Para));
                else if (s == 0) Visitar(a.Para);
            }
            estado[id] = 2;
        }
        var raizes = nos.Where(n => !arestas.Any(a => a.Para == n.Id)).Select(n => n.Id).ToList();
        foreach (var r in raizes) if (estado.GetValueOrDefault(r) == 0) Visitar(r);
        foreach (var n in nos) if (estado.GetValueOrDefault(n.Id) == 0) Visitar(n.Id);

        // 2) Nível = caminho mais longo a partir das raízes (ignorando retornos)
        var frente = arestas.Where(a => !retorno.Contains((a.De, a.Para))).ToList();
        var nivel = nos.ToDictionary(n => n.Id, _ => 0);
        for (var it = 0; it < nos.Count; it++)
        {
            var mudou = false;
            foreach (var a in frente)
                if (nivel[a.Para] < nivel[a.De] + 1) { nivel[a.Para] = nivel[a.De] + 1; mudou = true; }
            if (!mudou) break;
        }

        // 3) Ordem dentro de cada nível (baricentro dos pais)
        var niveis = nos.GroupBy(n => nivel[n.Id]).OrderBy(g => g.Key).Select(g => g.ToList()).ToList();
        var ordem = new Dictionary<string, double>();
        foreach (var camada in niveis)
        {
            double Peso(NoFluxo n)
            {
                var pais = frente.Where(a => a.Para == n.Id && ordem.ContainsKey(a.De)).Select(a => ordem[a.De]).ToList();
                return pais.Count > 0 ? pais.Average() : nos.IndexOf(n);
            }
            var ordenada = camada.OrderBy(Peso).ThenBy(n => nos.IndexOf(n)).ToList();
            camada.Clear();
            camada.AddRange(ordenada);
            for (var i = 0; i < camada.Count; i++) ordem[camada[i].Id] = i;
        }

        // 4) Coordenadas (eixo principal = nível; eixo cruzado = ordem, centralizado)
        var pos = new Dictionary<string, NoPos>();
        var cruzMax = niveis.Count == 0 ? 0 : niveis.Max(c => c.Sum(n => Cruz(n, horizontal)) + (c.Count - 1) * EspX);
        double principal = Margem;
        foreach (var camada in niveis)
        {
            var total = camada.Sum(n => Cruz(n, horizontal)) + (camada.Count - 1) * EspX;
            var c = Margem + (cruzMax - total) / 2;
            var passo = camada.Max(n => Principal(n, horizontal));
            foreach (var n in camada)
            {
                var (l, a) = Tamanho(n);
                var x = horizontal ? principal + (passo - l) / 2 : c;
                var y = horizontal ? c : principal + (passo - a) / 2;
                pos[n.Id] = new NoPos(n, x, y, l, a, nivel[n.Id]);
                c += Cruz(n, horizontal) + EspX;
            }
            principal += passo + EspY + (horizontal ? 30 : 0);
        }

        var largura = horizontal ? principal - EspY - 30 + Margem : cruzMax + Margem * 2;
        var altura = horizontal ? cruzMax + Margem * 2 : principal - EspY + Margem;

        // 5) Setas
        var setas = new List<SetaPos>();
        var desvio = 0.0;
        double minX = 0, minY = 0; // retornos pela esquerda/topo podem precisar de espaço extra
        foreach (var n in nos)
        {
            // ordena as saídas pela posição do destino, para as setas não se cruzarem ao sair do nó
            var saidas = n.Proximos.Where(p => pos.ContainsKey(p.Destino))
                .OrderBy(p => horizontal ? pos[p.Destino].Y : pos[p.Destino].X).ToList();
            for (var i = 0; i < saidas.Count; i++)
            {
                var o = pos[n.Id];
                var d = pos[saidas[i].Destino];
                var abre = (i - (saidas.Count - 1) / 2.0) * 26; // espalha várias saídas do mesmo nó
                var ehRetorno = retorno.Contains((n.Id, saidas[i].Destino)) || d.Nivel <= o.Nivel;
                string caminho; double rx, ry;

                if (!ehRetorno && !horizontal)
                {
                    double x1 = o.X + o.L / 2 + abre, y1 = o.Y + o.A, x2 = d.X + d.L / 2, y2 = d.Y - 4;
                    var meio = (y1 + y2) / 2;
                    caminho = $"M{F(x1)} {F(y1)} C{F(x1)} {F(meio)} {F(x2)} {F(meio)} {F(x2)} {F(y2)}";
                    (rx, ry) = ((x1 + x2) / 2, meio);
                }
                else if (!ehRetorno)
                {
                    double x1 = o.X + o.L, y1 = o.Y + o.A / 2 + abre, x2 = d.X - 4, y2 = d.Y + d.A / 2;
                    var meio = (x1 + x2) / 2;
                    caminho = $"M{F(x1)} {F(y1)} C{F(meio)} {F(y1)} {F(meio)} {F(y2)} {F(x2)} {F(y2)}";
                    (rx, ry) = (meio, (y1 + y2) / 2);
                }
                else
                {
                    // Retorno: contorna os nós dos níveis envolvidos pelo lado mais livre
                    desvio += 18;
                    var nMin = Math.Min(o.Nivel, d.Nivel);
                    var nMax = Math.Max(o.Nivel, d.Nivel);
                    var faixa = pos.Values.Where(q => q.Nivel >= nMin && q.Nivel <= nMax).ToList();
                    var mesmoNivel = pos.Values.Where(q => q.Nivel == o.Nivel).OrderBy(q => horizontal ? q.Y : q.X).ToList();
                    var primeiro = mesmoNivel.Count > 1 && mesmoNivel[0].No.Id == o.No.Id;
                    var ultimo = mesmoNivel[^1].No.Id == o.No.Id;

                    if (!horizontal)
                    {
                        double oy = o.Y + o.A / 2, ty = d.Y + d.A / 2;
                        if (primeiro)
                        {
                            var lado = faixa.Min(q => q.X) - 26 - desvio;
                            caminho = $"M{F(o.X)} {F(oy)} H{F(lado + 12)} Q{F(lado)} {F(oy)} {F(lado)} {F(oy - 12)} V{F(ty + 12)} Q{F(lado)} {F(ty)} {F(lado + 12)} {F(ty)} H{F(d.X - 4)}";
                            (rx, ry) = (lado, (oy + ty) / 2);
                            minX = Math.Min(minX, lado - 60); // espaço para o rótulo
                        }
                        else
                        {
                            var lado = faixa.Max(q => q.X + q.L) + 26 + desvio;
                            if (ultimo)
                                caminho = $"M{F(o.X + o.L)} {F(oy)} H{F(lado - 12)} Q{F(lado)} {F(oy)} {F(lado)} {F(oy - 12)} V{F(ty + 12)} Q{F(lado)} {F(ty)} {F(lado - 12)} {F(ty)} H{F(d.X + d.L + 4)}";
                            else
                            {
                                // no meio do nível: desce para o vão entre as linhas antes de contornar
                                double cx = o.X + o.L / 2, vao = o.Y + o.A + EspY / 2;
                                caminho = $"M{F(cx)} {F(o.Y + o.A)} V{F(vao - 10)} Q{F(cx)} {F(vao)} {F(cx + 10)} {F(vao)} H{F(lado - 12)} Q{F(lado)} {F(vao)} {F(lado)} {F(vao - 12)} V{F(ty + 12)} Q{F(lado)} {F(ty)} {F(lado - 12)} {F(ty)} H{F(d.X + d.L + 4)}";
                            }
                            (rx, ry) = (lado, (oy + ty) / 2);
                            largura = Math.Max(largura, lado + Margem + 30);
                        }
                    }
                    else
                    {
                        double ox = o.X + o.L / 2, tx = d.X + d.L / 2;
                        if (primeiro)
                        {
                            var lado = faixa.Min(q => q.Y) - 26 - desvio;
                            caminho = $"M{F(ox)} {F(o.Y)} V{F(lado + 12)} Q{F(ox)} {F(lado)} {F(ox - 12)} {F(lado)} H{F(tx + 12)} Q{F(tx)} {F(lado)} {F(tx)} {F(lado + 12)} V{F(d.Y - 4)}";
                            (rx, ry) = ((ox + tx) / 2, lado);
                            minY = Math.Min(minY, lado - 24);
                        }
                        else
                        {
                            var lado = faixa.Max(q => q.Y + q.A) + 26 + desvio;
                            if (ultimo)
                                caminho = $"M{F(ox)} {F(o.Y + o.A)} V{F(lado - 12)} Q{F(ox)} {F(lado)} {F(ox - 12)} {F(lado)} H{F(tx + 12)} Q{F(tx)} {F(lado)} {F(tx)} {F(lado - 12)} V{F(d.Y + d.A + 4)}";
                            else
                            {
                                // no meio da coluna: sai pela direita, para o vão entre as colunas
                                double cy = o.Y + o.A / 2, vao = o.X + o.L + (EspY + 30) / 2;
                                caminho = $"M{F(o.X + o.L)} {F(cy)} H{F(vao - 10)} Q{F(vao)} {F(cy)} {F(vao)} {F(cy + 10)} V{F(lado - 12)} Q{F(vao)} {F(lado)} {F(vao - 12)} {F(lado)} H{F(tx + 12)} Q{F(tx)} {F(lado)} {F(tx)} {F(lado - 12)} V{F(d.Y + d.A + 4)}";
                            }
                            (rx, ry) = ((ox + tx) / 2, lado);
                            altura = Math.Max(altura, lado + Margem);
                        }
                    }
                }
                setas.Add(new SetaPos(caminho, saidas[i].Rotulo, rx, ry, ehRetorno));
            }
        }

        // Se algum retorno saiu pela esquerda/topo, desloca tudo para dentro do quadro
        if (minX < 0 || minY < 0)
        {
            double dx = minX < 0 ? -minX : 0, dy = minY < 0 ? -minY : 0;
            var movidos = pos.Values.Select(q => q with { X = q.X + dx, Y = q.Y + dy }).ToList();
            var setasMovidas = setas.Select(st => st with { Caminho = Deslocar(st.Caminho, dx, dy), RX = st.RX + dx, RY = st.RY + dy }).ToList();
            return new DesenhoFluxo(movidos, setasMovidas, Math.Max(largura + dx, 300), Math.Max(altura + dy, 160));
        }
        return new DesenhoFluxo(pos.Values.ToList(), setas, Math.Max(largura, 300), Math.Max(altura, 160));
    }

    /// <summary>Desloca um caminho SVG (comandos absolutos M, C, Q, H, V) por dx/dy.</summary>
    private static string Deslocar(string caminho, double dx, double dy)
    {
        var partes = caminho.Split(' ');
        var r = new List<string>();
        var eixo = 'x'; // próximo número é x ou y
        foreach (var p in partes)
        {
            var cmd = char.IsLetter(p[0]) ? p[0] : '\0';
            var num = cmd == '\0' ? p : p[1..];
            if (cmd != '\0') eixo = cmd == 'V' ? 'y' : 'x';
            var v = double.Parse(num, System.Globalization.CultureInfo.InvariantCulture);
            v += eixo == 'x' ? dx : dy;
            r.Add((cmd == '\0' ? "" : cmd.ToString()) + F(v));
            // H e V têm só um eixo; demais comandos alternam x, y
            if (cmd is 'H' or 'V') eixo = 'x';
            else eixo = eixo == 'x' ? 'y' : 'x';
        }
        return string.Join(" ", r);
    }

    private static double Cruz(NoFluxo n, bool horizontal) => horizontal ? Tamanho(n).A : Tamanho(n).L;
    private static double Principal(NoFluxo n, bool horizontal) => horizontal ? Tamanho(n).L : Tamanho(n).A;

    public static string F(double v) => v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Quebra o texto em até 3 linhas para caber na forma.</summary>
    public static List<string> Quebrar(string texto, int max)
    {
        var palavras = (string.IsNullOrWhiteSpace(texto) ? "Sem nome" : texto.Trim()).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var linhas = new List<string>();
        var atual = "";
        foreach (var p in palavras)
        {
            if ((atual + " " + p).Trim().Length > max && atual.Length > 0) { linhas.Add(atual); atual = p; }
            else atual = (atual + " " + p).Trim();
        }
        if (atual.Length > 0) linhas.Add(atual);
        if (linhas.Count > 3) { linhas = linhas.Take(3).ToList(); linhas[2] = linhas[2].TrimEnd('.') + "…"; }
        return linhas;
    }

    public static Fluxo Exemplo()
    {
        var inicio = new NoFluxo { Texto = "Solicitação recebida", Tipo = TipoNo.Inicio };
        var analisar = new NoFluxo { Texto = "Analisar documentos", Tipo = TipoNo.Etapa };
        var decisao = new NoFluxo { Texto = "Dados completos?", Tipo = TipoNo.Decisao };
        var complemento = new NoFluxo { Texto = "Solicitar complemento ao cliente", Tipo = TipoNo.Documento };
        var registrar = new NoFluxo { Texto = "Registrar no sistema", Tipo = TipoNo.Dados };
        var fim = new NoFluxo { Texto = "Concluído", Tipo = TipoNo.Fim };
        inicio.Proximos.Add(new() { Destino = analisar.Id });
        analisar.Proximos.Add(new() { Destino = decisao.Id });
        decisao.Proximos.Add(new() { Destino = registrar.Id, Rotulo = "Sim" });
        decisao.Proximos.Add(new() { Destino = complemento.Id, Rotulo = "Não" });
        complemento.Proximos.Add(new() { Destino = analisar.Id, Rotulo = "Recebido" });
        registrar.Proximos.Add(new() { Destino = fim.Id });
        return new Fluxo
        {
            Nome = "Fluxo de atendimento",
            Descricao = "Da solicitação à conclusão",
            Nos = new() { inicio, analisar, decisao, complemento, registrar, fim }
        };
    }
}
