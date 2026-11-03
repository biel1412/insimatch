# insiTalent

Painel para apresentar resultados na tela compartilhada do Microsoft Teams: **resultado individual**, **resultado do time** e **atualização de fila**.

Escrito em **C# com Blazor WebAssembly** e hospedado no **GitHub Pages**: abre direto no navegador, sem instalar nada.

🔗 **Acesse:** https://biel1412.github.io/insimatch/

## Funções (menu lateral)

| Aba | Para quê | Modelos de apresentação |
|---|---|---|
| **Individual** | Feedback mensal de um colaborador | Cartões · Gráficos · Placar · Foco · Mosaico |
| **Time** | Resultado consolidado do time no mês | Cartões · Placar · Foco |
| **Fila** | Atualização das filas Centralizada e Autorizar | Cartões · Placar · Foco |

Na barra do topo:
- **Cor:** Noturno, Oceano, Esmeralda, Aurora ou Claro.
- **Fundo** (papel de parede): Brilho, Aurora, Ondas, Grade, Pontos, Geométrico ou Liso.
- **Modelo:** alterna o formato de apresentação da aba atual.
- **▶ Apresentar** esconde os editores e **⛶** abre em tela cheia.

No modo apresentação, a troca de aba fica no próprio topo. Cor, fundo, modelo e aba ficam salvos no navegador.

## Critérios

### Individual
| Indicador | Como é analisado |
|---|---|
| **Volume / dias trabalhados** | A média diária (volume ÷ dias trabalhados) é comparada com a **Meta Pro** e a **Meta Premium** da pessoa, que são **por dia** (ex.: 150/180 ou 220/250). |
| **Qualidade** | (volume − desvios internos − desvios externos) ÷ volume. Ideal **≥ 99,94%**. |
| **Resultado geral** | **Está bem** com qualidade no ideal e média pelo menos próxima da Meta Pro (≥ 90%). **Excelente** quando, além disso, bate a Premium. |

### Time
| Indicador | Como é analisado |
|---|---|
| **Volume** | Volume atingido ÷ meta de volume. Meta atingida ≥ 100%; próximo da meta ≥ 90%. |
| **Qualidade** | Qualidade do mês comparada com a meta de qualidade (padrão 99,94%, editável). |
| **Resultado geral** | **Está bem** com qualidade na meta e volume pelo menos próximo da meta; **Excelente** com as duas metas atingidas. |

### Fila
Para cada fila (Centralizada e Autorizar) você informa **o total de fluxos na fila** e, **manualmente**, quantos têm **data do dia anterior**, já que os dois ficam misturados na fila. Cada página tem **15 fluxos**.

A situação considera os fluxos do dia anterior:

| Situação | Fluxos do dia anterior |
|---|---|
| Em dia | 0 |
| Sob controle | até 1 página (15) |
| Atenção | até 3 páginas (45) |
| Acúmulo | acima de 3 páginas |

A situação geral é a pior das duas filas.

Em todas as abas, o **parecer** é gerado em linguagem formal a partir dos números, e pode ser editado.

## Como usar

1. Abra o link acima e escolha a aba no menu lateral.
2. Preencha os dados no editor (cada aba tem sua lista ou histórico).
3. Gere o parecer formal e ajuste o texto se quiser.
4. Escolha cor, fundo e modelo, clique em **▶ Apresentar** e compartilhe a tela no Teams.
5. Use **‹ ›** no topo para passar entre colaboradores, times ou atualizações.

### Planilha CSV (individual)

Separador `;` (abre direto no Excel em português):

```
Nome;Equipe;Mes;Volume;MetaPro;MetaPremium;DiasConsiderados;DesviosInternos;DesviosExternos;Parecer
Maria Souza;Atendimento;2026-10;3410;150;180;22;1;0;
```

> 🔒 Os dados ficam salvos **somente no navegador** de quem usa (localStorage). Nada é enviado para servidor algum.

## Desenvolvimento local

Requer o [.NET 8 SDK](https://dotnet.microsoft.com/download).

```bash
cd src/Insimatch
dotnet run
```

## Publicação

Todo `push` na branch `main` dispara `.github/workflows/deploy.yml`, que compila e publica no GitHub Pages.

## Estrutura

```
src/Insimatch/
├── Pages/Home.razor                 # dock, barra de ferramentas, cor, fundo e modelo
├── Components/Individual/           # aba Individual e seus 5 modelos
├── Components/Time/                 # aba Time e seus modelos
├── Components/Fila/                 # aba Fila e seus modelos
├── Components/Comum/                # cabeçalho, parecer, Foco, Placar, medidor, papel de parede
├── Models/                          # dados e critérios (Colaborador, ResultadoTime, AtualizacaoFila)
├── Services/ArmazenamentoService.cs # localStorage e CSV
└── wwwroot/                         # HTML, CSS e JS
```
