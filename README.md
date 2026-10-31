# Insimatch

Dashboard de **resultado individual do colaborador**, feito para apresentar na tela compartilhada do Microsoft Teams durante o feedback mensal.

Escrito em **C# com Blazor WebAssembly** e hospedado no **GitHub Pages**: abre direto no navegador, sem instalar nada.

🔗 **Acesse:** https://biel1412.github.io/insimatch/

## Critérios de análise

| Indicador | Como é analisado |
|---|---|
| **Volume / dias trabalhados** | A **média diária** (volume ÷ dias trabalhados) é comparada com a **Meta Pro** e a **Meta Premium** da pessoa. As metas são **por dia** e preenchidas por colaborador (ex.: Pro 150 / Premium 180, ou Pro 220 / Premium 250). |
| **Qualidade** | Calculada pelos desvios: **(volume − desvios internos − desvios externos) ÷ volume**. O ideal é **≥ 99,94%**. |
| **Resultado geral** | **Está bem** quando a qualidade está no ideal e a média diária está pelo menos próxima da Meta Pro (≥ 90%). **Excelente** quando, além disso, bate a Premium. |

O painel também mostra o volume do mês, os dias trabalhados, os desvios internos e externos, o máximo de desvios para manter 99,94% com aquele volume, e o **parecer** (texto do gestor ou sugestão automática).

## Como usar

1. Abra o link acima.
2. Na aba **Colaborador**, preencha volume, dias trabalhados, **Meta Pro** e **Meta Premium** (por dia) e os desvios (ou importe uma planilha `.csv` na aba **Lista**).
3. Escreva o parecer ou clique em **✨ Gerar sugestão**.
4. Clique em **▶ Modo apresentação** (ou **⛶ Tela cheia**) e compartilhe a tela no Teams.
5. Use **‹ ›** no topo para passar de um colaborador para o próximo.

### Planilha CSV

Separador `;` (abre direto no Excel em português). Cabeçalho:

```
Nome;Equipe;Mes;Volume;MetaPro;MetaPremium;DiasConsiderados;DesviosInternos;DesviosExternos;Parecer
Maria Souza;Atendimento;2026-10;3410;150;180;22;1;0;
```

Use o botão **Baixar modelo** no sistema para obter um arquivo pronto.

> 🔒 Os dados ficam salvos **somente no navegador** de quem usa (localStorage). Nada é enviado para servidor algum.

## Desenvolvimento local

Requer o [.NET 8 SDK](https://dotnet.microsoft.com/download).

```bash
cd src/Insimatch
dotnet run
```

## Publicação

Todo `push` na branch `main` dispara o workflow `.github/workflows/deploy.yml`, que compila o projeto e publica no GitHub Pages.

Na primeira vez, confirme em **Settings → Pages** que a origem (*Source*) está como **GitHub Actions**.

## Estrutura

```
src/Insimatch/
├── Components/Dashboard.razor   # painel de apresentação
├── Components/Medidor.razor     # medidor circular de qualidade
├── Components/LinhaMeta.razor   # comparação meta x realizado
├── Models/Colaborador.cs        # dados do colaborador
├── Models/Metas.cs              # critérios de análise e parecer automático
├── Pages/Home.razor             # editor + painel
├── Services/ArmazenamentoService.cs  # localStorage e CSV
└── wwwroot/                     # HTML, CSS e JS
```
