# Insimatch

Dashboard de **resultado individual do colaborador**, feito para apresentar na tela compartilhada do Microsoft Teams durante o feedback mensal.

Escrito em **C# com Blazor WebAssembly** e hospedado no **GitHub Pages**: abre direto no navegador, sem instalar nada.

🔗 **Acesse:** https://biel1412.github.io/insimatch/

## O que aparece no painel

| Bloco | Indicadores |
|---|---|
| **Produção** | Volume do mês comparado com a **Meta Pro** e a **Meta Premium** · Média diária (volume ÷ dias) · Dias considerados |
| **Qualidade** | Qualidade (%) — ideal **≥ 99,94%** · Desvios internos · Desvios externos (apenas contagem, sem limite) |
| **Parecer** | Texto do gestor (ou sugestão automática gerada a partir dos números) |

O volume mostra se bateu a meta Pro, a Premium, ou quanto faltou. A qualidade fica verde no ideal (≥ 99,94%) e vermelha abaixo dele. Um selo de **resultado geral** resume o mês.

## Como usar

1. Abra o link acima.
2. Na aba **Colaborador**, preencha os dados, incluindo **Meta Pro** e **Meta Premium** (ou importe uma planilha `.csv` na aba **Lista**).
3. Escreva o parecer ou clique em **✨ Gerar sugestão**.
4. Clique em **▶ Modo apresentação** (ou **⛶ Tela cheia**) e compartilhe a tela no Teams.
5. Use **‹ ›** no topo para passar de um colaborador para o próximo.

### Planilha CSV

Separador `;` (abre direto no Excel em português). Cabeçalho:

```
Nome;Equipe;Mes;Volume;MetaPro;MetaPremium;DiasConsiderados;Qualidade;DesviosInternos;DesviosExternos;Parecer
Maria Souza;Atendimento;2026-10;1320;1200;1400;22;99,96;1;0;
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
├── Models/Metas.cs              # avaliação (Pro/Premium, qualidade ideal) e parecer automático
├── Pages/Home.razor             # editor + painel
├── Services/ArmazenamentoService.cs  # localStorage e CSV
└── wwwroot/                     # HTML, CSS e JS
```
