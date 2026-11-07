using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Insimatch.Services;

public class GroqErro(string mensagem) : Exception(mensagem);

/// <summary>
/// Chama a API do Groq (compatível com OpenAI) direto do navegador.
/// A chave é do próprio usuário e fica salva só no navegador dele.
/// </summary>
public class GroqService(HttpClient http)
{
    private const string Url = "https://api.groq.com/openai/v1/chat/completions";

    public static readonly (string Id, string Nome)[] Modelos =
    {
        ("llama-3.3-70b-versatile", "Llama 3.3 70B (recomendado)"),
        ("openai/gpt-oss-120b", "GPT-OSS 120B"),
        ("llama-3.1-8b-instant", "Llama 3.1 8B (rápido)")
    };

    public async Task<string> GerarJsonAsync(string chave, string modelo, string sistema, string usuario, CancellationToken ct = default)
    {
        var corpo = new
        {
            model = modelo,
            temperature = 0.2,
            max_tokens = 4096,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = sistema },
                new { role = "user", content = usuario }
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, Url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", chave.Trim());
        req.Content = new StringContent(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json");

        HttpResponseMessage resp;
        try
        {
            resp = await http.SendAsync(req, ct);
        }
        catch (HttpRequestException)
        {
            throw new GroqErro("Não foi possível falar com o Groq. Verifique a internet ou se alguma rede/empresa bloqueia api.groq.com.");
        }

        var texto = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            var detalhe = ExtrairErro(texto);
            throw new GroqErro(resp.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Chave do Groq inválida. Confira a chave em console.groq.com/keys.",
                HttpStatusCode.TooManyRequests => "Limite gratuito do Groq atingido por agora. Aguarde um minuto e tente de novo, ou envie uma planilha menor." + (detalhe is null ? "" : $" ({detalhe})"),
                HttpStatusCode.RequestEntityTooLarge => "A planilha ficou grande demais para o limite gratuito. Envie menos linhas ou só as abas necessárias.",
                HttpStatusCode.NotFound or HttpStatusCode.BadRequest when detalhe?.Contains("model", StringComparison.OrdinalIgnoreCase) == true
                    => $"O modelo escolhido não está disponível: {detalhe}. Escolha outro modelo.",
                _ => $"O Groq respondeu com erro {(int)resp.StatusCode}." + (detalhe is null ? "" : $" {detalhe}")
            });
        }

        using var doc = JsonDocument.Parse(texto);
        var conteudo = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        return LimparJson(conteudo);
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
