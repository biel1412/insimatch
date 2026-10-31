# Insimatch

Dashboard de **resultado individual do colaborador**, feito para apresentar na tela compartilhada do Microsoft Teams durante o feedback mensal.

Escrito em **C# com Blazor WebAssembly** e hospedado no **GitHub Pages**: abre direto no navegador, sem instalar nada.

🔗 **Acesse:** https://biel1412.github.io/insimatch/

## O que aparece no painel

| Bloco | Indicadores |
|---|---|
| **Produção** | Volume do mês · Média diária (calculada: volume ÷ dias) · Dias considerados |
| **Qualidade** | Qualidade (%) · Desvios internos · Desvios externos |
| **Parecer** | Texto do gestor (ou sugestão automática gerada a partir dos números) |

Cada indicador recebe uma cor de acordo com as metas configuradas: **verde** (excelente), **azul** (dentro da meta), **amarelo** (atenção) e **vermelho** (crítico). Um selo de **resultado geral** resume o mês.

## Como usar

1. Abra o link acima.
2. Na aba **Metas**, configure as metas de volume, média diária, qualidade e os limites de desvios.
3. Na aba **Colaborador**, preencha os dados (ou importe uma planilha `.csv` na aba **Lista**).
4. Escreva o parecer ou clique em **✨ Gerar sugestão**.
5. Clique em **▶ Modo apresentação** (ou **⛶ Tela cheia**) e compartilhe a tela no Teams.
6. Use **‹ ›** no topo para passar de um colaborador para o próximo.

### Planilha CSV

Separador `;` (abre direto no Excel em português). Cabeçalho:

```
Nome;Equipe;Mes;Volume;DiasConsiderados;Qualidade;DesviosInternos;DesviosExternos;Parecer
Maria Souza;Atendimento;2026-10;1320;22;97,5;1;0;
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
├── Models/Colaborador.cs        # dados do colaborador
├── Models/Metas.cs              # metas, avaliação e parecer automático
├── Pages/Home.razor             # editor + painel
├── Services/ArmazenamentoService.cs  # localStorage e CSV
└── wwwroot/                     # HTML, CSS e JS
```
