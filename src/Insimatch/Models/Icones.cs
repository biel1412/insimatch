namespace Insimatch.Models;

/// <summary>Ícones de linha (24×24) usados no menu e nas abas personalizadas.</summary>
public static class Icones
{
    public record Icone(string Id, string Nome, string Svg);

    public static readonly Icone[] Todos =
    {
        new("grafico", "Gráfico", "<path d=\"M4 20V10\"/><path d=\"M10 20V4\"/><path d=\"M16 20v-7\"/><path d=\"M22 20H2\"/>"),
        new("tendencia", "Tendência", "<path d=\"M3 17l6-6 4 4 8-8\"/><path d=\"M15 7h6v6\"/>"),
        new("alvo", "Alvo", "<circle cx=\"12\" cy=\"12\" r=\"8.5\"/><circle cx=\"12\" cy=\"12\" r=\"4.5\"/><circle cx=\"12\" cy=\"12\" r=\"1\"/>"),
        new("trofeu", "Troféu", "<path d=\"M8 4h8v5a4 4 0 0 1-8 0V4z\"/><path d=\"M8 6H5a3 3 0 0 0 3 4\"/><path d=\"M16 6h3a3 3 0 0 1-3 4\"/><path d=\"M12 13v4\"/><path d=\"M8.5 20h7\"/>"),
        new("estrela", "Estrela", "<path d=\"M12 3.5l2.6 5.3 5.9.9-4.3 4.1 1 5.8L12 16.9l-5.2 2.7 1-5.8-4.3-4.1 5.9-.9z\"/>"),
        new("raio", "Raio", "<path d=\"M13 3L5 13.5h6L10 21l8-10.5h-6z\"/>"),
        new("foguete", "Foguete", "<path d=\"M12 3c3.5 2 5 5.5 5 9l-2.5 3.5h-5L7 12c0-3.5 1.5-7 5-9z\"/><circle cx=\"12\" cy=\"10\" r=\"1.6\"/><path d=\"M9.5 15.5L8 20l4-2 4 2-1.5-4.5\"/>"),
        new("coracao", "Coração", "<path d=\"M12 20s-7.5-4.6-7.5-10A4.3 4.3 0 0 1 12 7.3 4.3 4.3 0 0 1 19.5 10c0 5.4-7.5 10-7.5 10z\"/>"),
        new("escudo", "Escudo", "<path d=\"M12 3l7.5 3v5.5c0 4.5-3.2 8-7.5 9.5-4.3-1.5-7.5-5-7.5-9.5V6z\"/><path d=\"M9 12l2.2 2.2L15.5 10\"/>"),
        new("relogio", "Relógio", "<circle cx=\"12\" cy=\"12\" r=\"8.5\"/><path d=\"M12 7.5V12l3 2\"/>"),
        new("calendario", "Calendário", "<rect x=\"3.5\" y=\"5\" width=\"17\" height=\"15.5\" rx=\"2.5\"/><path d=\"M3.5 10h17\"/><path d=\"M8 3v4\"/><path d=\"M16 3v4\"/>"),
        new("dinheiro", "Financeiro", "<rect x=\"2.5\" y=\"6\" width=\"19\" height=\"12\" rx=\"2.5\"/><circle cx=\"12\" cy=\"12\" r=\"2.8\"/><path d=\"M6 9.5v5\"/><path d=\"M18 9.5v5\"/>"),
        new("carrinho", "Vendas", "<path d=\"M3 4h2.5l2.2 10.5h10.6L20.5 7H7\"/><circle cx=\"9\" cy=\"19\" r=\"1.4\"/><circle cx=\"17\" cy=\"19\" r=\"1.4\"/>"),
        new("pacote", "Pacote", "<path d=\"M12 3l8.5 4.5v9L12 21l-8.5-4.5v-9z\"/><path d=\"M3.5 7.5L12 12l8.5-4.5\"/><path d=\"M12 12v9\"/>"),
        new("predio", "Empresa", "<rect x=\"5\" y=\"3.5\" width=\"14\" height=\"17\" rx=\"1.5\"/><path d=\"M9 7.5h1.5M13.5 7.5H15M9 11h1.5M13.5 11H15M9 14.5h1.5M13.5 14.5H15\"/><path d=\"M10.5 20.5v-3h3v3\"/>"),
        new("maleta", "Negócios", "<rect x=\"3.5\" y=\"7\" width=\"17\" height=\"12.5\" rx=\"2.5\"/><path d=\"M8.5 7V5.5A1.5 1.5 0 0 1 10 4h4a1.5 1.5 0 0 1 1.5 1.5V7\"/><path d=\"M3.5 12.5h17\"/><path d=\"M10.5 12.5v1.5h3v-1.5\"/>"),
        new("documento", "Documento", "<path d=\"M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z\"/><path d=\"M14 3v5h5\"/><path d=\"M9 13h6M9 17h4\"/>"),
        new("checklist", "Checklist", "<path d=\"M4 6.5l1.5 1.5L8 5.5\"/><path d=\"M4 12.5L5.5 14 8 11.5\"/><path d=\"M4 18.5L5.5 20 8 17.5\"/><path d=\"M11 7h9M11 13h9M11 19h9\"/>"),
        new("globo", "Global", "<circle cx=\"12\" cy=\"12\" r=\"8.5\"/><path d=\"M3.5 12h17\"/><path d=\"M12 3.5c2.5 2.6 3.7 5.4 3.7 8.5s-1.2 5.9-3.7 8.5c-2.5-2.6-3.7-5.4-3.7-8.5S9.5 6.1 12 3.5z\"/>"),
        new("atendimento", "Atendimento", "<path d=\"M4.5 13v-1a7.5 7.5 0 0 1 15 0v1\"/><rect x=\"3.5\" y=\"13\" width=\"4\" height=\"6\" rx=\"1.5\"/><rect x=\"16.5\" y=\"13\" width=\"4\" height=\"6\" rx=\"1.5\"/><path d=\"M18.5 19c0 1.2-1.5 2-4 2H12\"/>"),
        new("engrenagem", "Processos", "<circle cx=\"12\" cy=\"12\" r=\"3\"/><path d=\"M12 2.8v2.4M12 18.8v2.4M2.8 12h2.4M18.8 12h2.4M5.5 5.5l1.7 1.7M16.8 16.8l1.7 1.7M5.5 18.5l1.7-1.7M16.8 7.2l1.7-1.7\"/>"),
        new("camadas", "Camadas", "<path d=\"M12 3.5l8.5 4.5-8.5 4.5-8.5-4.5z\"/><path d=\"M3.5 12l8.5 4.5 8.5-4.5\"/><path d=\"M3.5 16l8.5 4.5 8.5-4.5\"/>"),
        new("pessoa", "Pessoa", "<circle cx=\"12\" cy=\"8\" r=\"3.6\"/><path d=\"M4.8 20c.8-4 3.7-6.2 7.2-6.2s6.4 2.2 7.2 6.2\"/>"),
        new("pessoas", "Equipe", "<circle cx=\"9\" cy=\"8\" r=\"3.2\"/><circle cx=\"17\" cy=\"9.5\" r=\"2.4\"/><path d=\"M2.8 19c.6-3.3 3.2-5.2 6.2-5.2s5.6 1.9 6.2 5.2\"/><path d=\"M15.2 14.3c2.6-.3 5 1.2 5.8 4.7\"/>")
    };

    public static Icone Obter(string? id) => Todos.FirstOrDefault(i => i.Id == id) ?? Todos[0];
}
