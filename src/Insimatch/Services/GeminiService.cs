using System.Net;
using System.Text;
using System.Text.Json;

namespace Insimatch.Services;

public class IAErro(string mensagem) : Exception(mensagem);

/// <summary>
/// Chama a API do Google Gemini direto do navegador.
/// A chave é cadastrada pelo administrador (página admdev) e fica salva só neste navegador.
/// </summary>
public class GeminiService(HttpClient http)
{
    private const string Base = "https://generativelanguage.googleapis.com/v1beta/";

    /// <summary>Usado se o administrador ainda não escolheu um modelo. Se sair do ar, o app escolhe outro sozinho.</summary>
    public const string ModeloPadrao = "gemini-2.5-flash";

    /// <summary>Lista os modelos de texto disponíveis para a chave (mais recentes e "flash" primeiro).</summary>
    public async Task<List<(string Id, string Nome)>> ListarModelosAsync(string chave, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, Base + "models?pageSize=200");
        req.Headers.Add("x-goog-api-key", chave.Trim());
        var (resp, texto) = await EnviarAsync(req, ct);
        if (!resp.IsSuccessStatusCode) throw Erro(resp.StatusCode, texto, "");

        var ignorar = new[] { "embedding", "aqa", "image", "tts", "audio", "live", "vision", "robotics", "computer-use", "veo", "imagen" };
        using var doc = JsonDocument.Parse(texto);
        var lista = new List<(string Id, string Nome)>();
        foreach (var m in doc.RootElement.GetProperty("models").EnumerateArray())
        {
            var id = (m.GetProperty("name").GetString() ?? "").Replace("models/", "");
            var gera = m.TryGetProperty("supportedGenerationMethods", out var g) &&
                       g.EnumerateArray().Any(x => x.GetString() == "generateContent");
            if (!gera || !id.StartsWith("gemini", StringComparison.OrdinalIgnoreCase)) continue;
            if (ignorar.Any(x => id.Contains(x, StringComparison.OrdinalIgnoreCase))) continue;
            var nome = m.TryGetProperty("displayName", out var d) ? d.GetString() ?? id : id;
            lista.Add((id, nome));
        }
        return lista.OrderByDescending(Prioridade).ToList();
    }

    /// <summary>Ordem de preferência: versão mais nova, "flash" antes de "pro" (cota grátis maior), estável antes de preview.</summary>
    private static double Prioridade((string Id, string Nome) m)
    {
        var id = m.Id.ToLowerInvariant();
        var ver = System.Text.RegularExpressions.Regex.Match(id, @"gemini-(\d+(\.\d+)?)");
        var v = ver.Success ? double.Parse(ver.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;
        var p = v * 10;
        if (id.Contains("flash") && !id.Contains("lite")) p += 5;
        else if (id.Contains("lite")) p += 3;
        if (id.Contains("preview") || id.Contains("exp")) p -= 4;
        if (id.Contains("latest")) p -= 1;
        return p;
    }

    /// <summary>
    /// Gera a resposta em JSON. Se o modelo escolhido tiver saído do ar,
    /// tenta automaticamente os modelos disponíveis na conta.
    /// </summary>
    public async Task<string> GerarJsonAsync(string chave, string modelo, string sistema, string usuario, CancellationToken ct = default,
        IReadOnlyList<(string Mime, string Base64)>? imagens = null)
    {
        // Ordem de tentativa: o modelo escolhido (se houver) e depois os melhores disponíveis na chave
        var fila = new List<string>();
        if (!string.IsNullOrWhiteSpace(modelo)) fila.Add(modelo);
        try { fila.AddRange((await ModelosAsync(chave, ct)).Select(m => m.Id)); }
        catch (IAErro) when (fila.Count > 0) { }
        if (fila.Count == 0) fila.Add(ModeloPadrao);
        fila = fila.Distinct().Take(5).ToList();

        IAErro? ultimo = null;
        foreach (var m in fila)
        {
            for (var tentativa = 0; tentativa < 2; tentativa++)
            {
                try { return await GerarComModeloAsync(chave, m, sistema, usuario, imagens, ct); }
                catch (ModeloIndisponivel) { break; }                       // não existe mais: próximo modelo
                catch (Temporario e)
                {
                    ultimo = new IAErro(e.Message);
                    if (e.SemCota) break;                                    // cota do modelo acabou: próximo modelo
                    if (tentativa == 0) await Task.Delay(1500, ct);          // sobrecarga: espera e tenta de novo
                }
            }
        }
        throw ultimo ?? new IAErro("Nenhum modelo do Gemini está disponível para esta chave agora.");
    }

    private List<(string Id, string Nome)>? _modelos;
    private string? _chaveModelos;

    /// <summary>Lista de modelos guardada durante a sessão (evita consultar a cada geração).</summary>
    private async Task<List<(string Id, string Nome)>> ModelosAsync(string chave, CancellationToken ct)
    {
        if (_modelos is null || _chaveModelos != chave)
        {
            _modelos = await ListarModelosAsync(chave, ct);
            _chaveModelos = chave;
        }
        return _modelos;
    }

    /// <summary>Falha passageira (sobrecarga ou cota do modelo): vale tentar de novo ou outro modelo.</summary>
    private sealed class Temporario(string m, bool semCota) : Exception(m) { public bool SemCota { get; } = semCota; }

    /// <summary>Teste rápido da chave e do modelo (usado na página do administrador).</summary>
    public async Task<string> TestarAsync(string chave, string modelo, CancellationToken ct = default)
    {
        var json = await GerarJsonAsync(chave, modelo,
            "Responda somente com JSON.", "Responda exatamente {\"ok\": true, \"mensagem\": \"Olá do Gemini\"}", ct);
        using var d = JsonDocument.Parse(json);
        return d.RootElement.TryGetProperty("mensagem", out var m) ? m.GetString() ?? "ok" : "ok";
    }

    private sealed class ModeloIndisponivel(string m) : Exception(m);

    private async Task<string> GerarComModeloAsync(string chave, string modelo, string sistema, string usuario,
        IReadOnlyList<(string Mime, string Base64)>? imagens, CancellationToken ct)
    {
        var corpo = new
        {
            systemInstruction = new { parts = new[] { new { text = sistema } } },
            contents = new[] { new { role = "user", parts = Partes(usuario, imagens) } },
            generationConfig = new
            {
                temperature = 0.2,
                maxOutputTokens = 32768,
                responseMimeType = "application/json"
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{Base}models/{Uri.EscapeDataString(modelo)}:generateContent");
        req.Headers.Add("x-goog-api-key", chave.Trim());
        req.Content = new StringContent(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json");

        var (resp, texto) = await EnviarAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            if (resp.StatusCode == HttpStatusCode.NotFound ||
                (resp.StatusCode == HttpStatusCode.BadRequest && texto.Contains("model", StringComparison.OrdinalIgnoreCase) && texto.Contains("not found", StringComparison.OrdinalIgnoreCase)))
                throw new ModeloIndisponivel(modelo);
            if (resp.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.InternalServerError or HttpStatusCode.GatewayTimeout)
                throw new Temporario($"O Gemini está sobrecarregado no momento (modelo {modelo}). Tente de novo em instantes.", false);
            if (resp.StatusCode == HttpStatusCode.TooManyRequests)
                throw new Temporario("Limite gratuito do Gemini atingido por agora em todos os modelos disponíveis. Aguarde um minuto e tente de novo.", true);
            throw Erro(resp.StatusCode, texto, modelo);
        }

        using var doc = JsonDocument.Parse(texto);
        if (!doc.RootElement.TryGetProperty("candidates", out var cands) || cands.GetArrayLength() == 0)
        {
            var motivo = doc.RootElement.TryGetProperty("promptFeedback", out var pf) && pf.TryGetProperty("blockReason", out var br) ? br.GetString() : null;
            throw new IAErro(motivo is null ? "O Gemini não devolveu resposta. Tente de novo." : $"O Gemini recusou o pedido ({motivo}).");
        }

        var cand = cands[0];
        var sb = new StringBuilder();
        if (cand.TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts))
        {
            foreach (var p in parts.EnumerateArray())
            {
                if (p.TryGetProperty("thought", out var th) && th.ValueKind == JsonValueKind.True) continue;
                if (p.TryGetProperty("text", out var t)) sb.Append(t.GetString());
            }
        }
        var fim = cand.TryGetProperty("finishReason", out var fr) ? fr.GetString() : null;
        if (sb.Length == 0)
            throw new IAErro(fim == "MAX_TOKENS"
                ? "A resposta do Gemini ficou grande demais. Envie menos dados ou peça um resumo mais curto."
                : $"O Gemini não devolveu texto{(fim is null ? "" : $" ({fim})")}. Tente de novo.");
        return LimparJson(sb.ToString());
    }

    /// <summary>Texto do pedido + imagens anexadas (enviadas junto, em base64).</summary>
    private static object[] Partes(string texto, IReadOnlyList<(string Mime, string Base64)>? imagens)
    {
        var partes = new List<object>();
        foreach (var (mime, b64) in imagens ?? Array.Empty<(string, string)>())
            partes.Add(new { inlineData = new { mimeType = mime, data = b64 } });
        partes.Add(new { text = texto });
        return partes.ToArray();
    }

    private async Task<(HttpResponseMessage, string)> EnviarAsync(HttpRequestMessage req, CancellationToken ct)
    {
        HttpResponseMessage resp;
        try { resp = await http.SendAsync(req, ct); }
        catch (HttpRequestException)
        {
            throw new IAErro("Não foi possível falar com o Gemini. Verifique a internet ou se a rede da empresa bloqueia generativelanguage.googleapis.com.");
        }
        return (resp, await resp.Content.ReadAsStringAsync(ct));
    }

    private static IAErro Erro(HttpStatusCode status, string texto, string modelo)
    {
        var detalhe = ExtrairErro(texto);
        var chaveRuim = texto.Contains("API_KEY_INVALID") || texto.Contains("API key not valid");
        return new IAErro(status switch
        {
            _ when chaveRuim => "Chave do Gemini inválida. Confira a chave em aistudio.google.com/apikey.",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "A chave do Gemini não tem permissão para este uso." + (detalhe is null ? "" : $" ({detalhe})"),
            HttpStatusCode.TooManyRequests => "Limite gratuito do Gemini atingido por agora. Aguarde um minuto e tente de novo, ou envie menos dados.",
            HttpStatusCode.ServiceUnavailable or HttpStatusCode.InternalServerError => "O Gemini está sobrecarregado no momento. Tente de novo em instantes.",
            _ => $"O Gemini respondeu com erro {(int)status}{(modelo == "" ? "" : $" no modelo {modelo}")}." + (detalhe is null ? "" : $" {detalhe}")
        });
    }

    private static string? ExtrairErro(string texto)
    {
        try
        {
            using var d = JsonDocument.Parse(texto);
            return d.RootElement.GetProperty("error").GetProperty("message").GetString();
        }
        catch { return null; }
    }

    /// <summary>Remove cercas de código (```json) e texto em volta do objeto JSON.</summary>
    public static string LimparJson(string s)
    {
        var ini = s.IndexOf('{');
        var fim = s.LastIndexOf('}');
        return ini >= 0 && fim > ini ? s[ini..(fim + 1)] : s;
    }
}
