# insiTalent

Painel para apresentar resultados na tela compartilhada do Microsoft Teams: **resultado individual**, **resultado do time**, **resultado do cliente** e abas criadas por você.

Escrito em **C# com Blazor WebAssembly**: abre direto no navegador, sem instalar nada.

🔗 **Acesse:** https://biel1412.github.io/insimatch/


### Abas personalizadas
No menu lateral, **+ Nova** cria uma aba com nome, ícone (26 opções) e nome de cada registro. Há quatro formatos:


| **Em branco** | Painel de indicadores do zero (campos livres, metas, gráficos e parecer, como a aba Cliente). |
| **Exemplo pronto** | O mesmo painel, já com campos de exemplo. |
| **Slides** | Várias páginas com título, texto (linhas com “-” viram tópicos), campos flexíveis e **até 2 imagens por slide**. As imagens são carregadas no editor e a plataforma escolhe a posição: ao lado do conteúdo, em destaque abaixo, em tela cheia ou em galeria. Modelos: Slide (setas ← → do teclado), Rolagem e Visão geral. As imagens ficam no IndexedDB do navegador; exportar (.json) leva os slides e as imagens. |
| **Fluxograma** | Você descreve as etapas (Início, Etapa, Decisão, Documento, Dados/sistema, Fim) e para onde cada uma segue, com rótulos como “Sim/Não”. O desenho é organizado automaticamente em camadas; retornos contornam pelo lado mais livre. Modelos: Vertical e Horizontal. |

O botão **⚙** no topo do editor da aba permite renomear, trocar o ícone ou excluir a aba (com confirmação). O relatório funciona em todos os formatos (nos slides, com as imagens; no fluxograma, com o desenho).

Na barra do topo:
- **Cor:** 13 opções num seletor com amostras. Escuras: Noturno, Oceano, Esmeralda, Aurora, Rubi, Solar, Lavanda, Coral, Meia-noite e Grafite. Claras: Claro, Areia e Ártico.
- **Fundo** (papel de parede): 15 opções com miniaturas. Clássicos: Brilho, Grade, Pontos, Geométrico, Listras e Liso. Formas: Ondas, Topografia, Hexágonos e Malha. Feitos para movimento: Aurora, Fluido, Bokeh, Partículas e Estrelas.
- **Movimento:** liga ou desliga a animação do fundo (desligado, o fundo fica parado).
- **Modelo:** alterna o formato de apresentação da aba atual.
- **Relatório:** gera um relatório da apresentação aberta (capa, resultado geral, indicadores-chave, tabelas, atingimento das metas e parecer) para **imprimir / salvar em PDF** (A4) ou **baixar em .html**.
- **▶ Apresentar** esconde os editores e **⛶** abre em tela cheia.
- **⚙ Configurações** (no menu lateral): oculte opções da barra (cor, fundo, movimento, modelo, relatório, tela cheia), as abas fixas (Individual, Time, Cliente), o botão “+ Nova” e a troca de abas na apresentação. Pelo menos uma aba fica sempre visível; ocultar não apaga dados.

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

### Cliente (configurável)
A aba Cliente não tem indicadores fixos: você cria os **campos** que cada cliente precisar.

| Configuração do campo | Opções |
|---|---|
| **Tipo** | Número inteiro · Número decimal · Valor (moeda) · Percentual · Texto · Data · Sim/Não |
| **Formato** | Casas decimais (0 = sem vírgula, até 4), separador de milhar, símbolo da moeda, unidade (min, h, kg…) |
| **Meta** | Opcional; “maior é melhor” ou “menor é melhor”; a partir de qual % da meta conta como “próximo da meta” |
| **Visual** | Número em destaque · Barra de progresso · Anel (% da meta) · Medidor (percentuais) · Realizado × meta |
| **Organização** | Grupo/seção, ordem (↑ ↓), destaque (cartão maior e modelo Foco) |

- **Gráficos do painel:** barras, colunas ou rosca, comparando os campos escolhidos por **valor** ou por **% da meta** (útil para unidades diferentes).
- **Parecer:** texto formal ou em tópicos por situação, com ou sem os campos sem meta; gerado automaticamente e editável.
- **Clientes:** vários clientes; **Novo período** reaproveita campos, metas e gráficos com os valores em branco; **Exportar/Importar (.json)** leva a configuração para outro computador ou para a equipe; excluir um campo pode ser desfeito.
- **Resultado geral:** considera todos os campos com meta (na meta, próximos e fora).

Em todas as abas, o **parecer** só aparece quando você escolhe um. **✨ Gerar sugestão** mostra 7 modelos montados com os números atuais: Formal completo, Resumo executivo, Pontos fortes e de melhoria, Feedback motivacional (ou Mensagem cordial, no Cliente), Tópicos, Plano de ação e Mensagem curta para o Teams. Depois de escolher, o texto pode ser editado. Sem parecer, a seção não aparece na apresentação.

## Como usar

1. Abra o link acima e escolha a aba no menu lateral.
2. Preencha os dados no editor (cada aba tem sua lista ou histórico).
3. Gere o parecer formal e ajuste o texto se quiser.
4. Escolha cor, fundo e modelo, clique em **▶ Apresentar** e compartilhe a tela no Teams.
5. Use **‹ ›** no topo para passar entre colaboradores, times ou atualizações.



## Desenvolvimento local

Requer o [.NET 8 SDK](https://dotnet.microsoft.com/download).

```bash
cd src/Insimatch
dotnet run
```


