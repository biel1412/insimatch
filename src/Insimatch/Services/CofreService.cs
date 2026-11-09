using Microsoft.JSInterop;

namespace Insimatch.Services;

/// <summary>Estado do cofre (modo sigiloso ou protegido por senha) e ações sobre ele.</summary>
public class CofreService(IJSRuntime js) : IDisposable
{
    public string Modo { get; private set; } = "sigiloso";
    public bool Desbloqueado { get; private set; } = true;
    public bool Legado { get; private set; }
    public bool Carregado { get; private set; }
    public bool Protegido => Modo == "protegido";

    /// <summary>Muda quando o conteúdo precisa ser recarregado do zero (ex.: dados apagados).</summary>
    public int Versao { get; private set; }
    public bool PrivacidadeAberta { get; private set; }

    public event Action? Mudou;
    private DotNetObjectReference<CofreService>? _ref;

    private record EstadoJs(string modo, bool desbloqueado, bool legado);

    public async Task AtualizarAsync()
    {
        var e = await js.InvokeAsync<EstadoJs>("insimatch.cofreEstado");
        Modo = e.modo; Desbloqueado = e.desbloqueado; Legado = e.legado; Carregado = true;
        if (_ref is null)
        {
            _ref = DotNetObjectReference.Create(this);
            await js.InvokeVoidAsync("insimatch.cofreRegistrar", _ref);
        }
        Avisar();
    }

    [JSInvokable]
    public Task CofreBloqueado()
    {
        Desbloqueado = false;
        PrivacidadeAberta = false;
        Avisar();
        return Task.CompletedTask;
    }

    public async Task<bool> AbrirAsync(string senha)
    {
        var ok = await js.InvokeAsync<bool>("insimatch.cofreAbrir", senha);
        if (ok) { Desbloqueado = true; Versao++; Avisar(); }
        return ok;
    }

    public async Task CriarAsync(string senha)
    {
        await js.InvokeAsync<bool>("insimatch.cofreCriar", senha);
        Modo = "protegido"; Desbloqueado = true; Legado = false;
        Avisar();
    }

    public Task BloquearAsync() => js.InvokeVoidAsync("insimatch.cofreBloquear").AsTask();

    public Task<bool> TrocarSenhaAsync(string atual, string nova) =>
        js.InvokeAsync<bool>("insimatch.cofreTrocarSenha", atual, nova).AsTask();

    public async Task DesativarAsync()
    {
        await js.InvokeAsync<bool>("insimatch.cofreDesativar");
        Modo = "sigiloso"; Desbloqueado = true;
        Avisar();
    }

    public async Task LegadoParaMemoriaAsync()
    {
        await js.InvokeAsync<bool>("insimatch.cofreLegadoParaMemoria");
        Legado = false; Versao++;
        Avisar();
    }

    public async Task ApagarTudoAsync()
    {
        await js.InvokeAsync<bool>("insimatch.cofreApagarTudo");
        Modo = "sigiloso"; Desbloqueado = true; Legado = false; Versao++;
        Avisar();
    }

    public void AbrirPrivacidade() { PrivacidadeAberta = true; Avisar(); }
    public void FecharPrivacidade() { PrivacidadeAberta = false; Avisar(); }

    private void Avisar() => Mudou?.Invoke();

    public void Dispose() => _ref?.Dispose();
}
