# ADR-0045 — Urbe: documentos computacionais e ferramentas interativas

## Status

**Proposto para revisão e integração governada.** O proprietário aprovou a direção do conceito em 28/10/2026 e pediu pesquisa, planejamento e formalização, incluindo **ferramentas interativas**. A aprovação conceitual não dispensa review, contratos de dados/segurança, gates de integração e DEVICE. Nenhum código de runtime ou formato de dados foi alterado.

## Contexto

O Urbe C# é um espaço de trabalho local-first em arquivos reais, com editor Markdown Visual como núcleo de produção de artefatos (DEC-0051/0053; ADR-0032/0034). A expansão solicitada envolve funcionalidades semelhantes às de Jupyter, um editor de código coerente com o produto e ferramentas interativas criadas manualmente ou com IA. O problema central não é ausência de widget: é evitar fragmentação em três experiências, regressão do editor, reatividade intrusiva e execução arbitrária de arquivos externos.

A auditoria da main em 28/10/2026 encontrou `ArtifactModel`, `VisualSurface`, `EditorOverlay`, `FilePreviewPanel`, `ASSISTANT-FORMS` (UC-90) e mecanismos de referência de grafos como bases reais. Encontrou, também, prévia somente leitura para arquivos de código e lacunas na edição direta de formatos como `.cs`, além de defeitos de seleção/rolagem/IME reportados e tratados de forma incremental. A validação Android é obrigatória.

Pesquisa resumida: Jupyter (documento e kernel separados), marimo (reatividade com dependências e lazy), Observable (exploração interativa no documento), Quarto (dashboards e tradeoff de backend), Obsidian Meta Bind/Obsidget (controles, persistência e custos de plugins), AvaloniaEdit (componente potencial para código, condicionado a teste mobile). Exemplos e feedback comunitário estão na especificação.

## Problema

Como entregar código editável, cálculos, visualizações e ferramentas interativas **dentro do fluxo natural de produção de artefatos do Urbe**, sem:
- exigir troca entre editores independentes;
- sacrificar leitura/edição de Markdown comum;
- criar segunda autoridade para dados de blocos, estado de widgets ou código transcluído;
- permitir execução automática/irrestrita de conteúdo importado ou produzido por agentes;
- adicionar dependência direta de Lunet2D/math-authoring ou nova UI WebView.

## Alternativas

**A — Três ambientes autônomos (Documento, Código, Laboratório).** Isolamento conceitual inicial; rejeitado na direção de UX pela navegação, duplicação de editor, conflitos de histórico/foco e custo de aprendizado.

**B — IDE/notebook em WebView embutida.** Acesso rápido a Monaco/CodeMirror/Jupyter; rejeitado como interface primária por ADR-0032 e risco de regressão/motor paralelo no Android. O conteúdo HTML isolado existente não vira host de execução geral.

**C — Documento/artefato único com superfícies contextuais e ferramentas declarativas; execução avançada desacoplada.** **Recomendado.** Preserva modelo de Vault e editor, acrescenta controles nativos, resultados vinculados e edição contextual de arquivos reais. Adoção escalonada e verificável.

**D — Tudo por plugins.** Menor custo inicial de core, mas tende a custo cumulativo de incompatibilidades, sintaxe e UX; rejeitado para controles fundamentais. Plugins continuam úteis como extensões sob permissão.

## Direção aprovada / decisão arquitetural proposta

Adotar a direção de **C** na especificação do produto. A implementação concreta permanece sujeita a revisão.

1. **Uma experiência integrada:** arquivos/artefatos reais, mesmo contexto de navegação e histórico; comandos e ferramentas contextuais, sem nova navegação global `Documento/Laboratório/Código`.
2. **Capacidades separadas:** editar, visualizar/interagir, calcular (puro) e executar código geral têm boundaries e permissões distintas.
3. **Dados sob autoridade existente:** nota em Markdown, código em arquivo de origem, ferramenta declarativa em definição textual versionada, resultados derivados invalidáveis. Nunca dois originais divergentes.
4. **Renderização nativa:** controles e gráficos iniciais em Avalonia; HTML no viewer mantém isolamento e autorização já especificados.
5. **Sem autorun de código:** arquivo importado ou nota nova abre inerte; runner de C# tem gate de segurança e viabilidade Android/AOT, e pode não ser entregue se não houver isolamento aceitável.
6. **Reatividade tipada:** dependências explícitas com invalidação seletiva e stale; efeitos ou cálculo caro sob execução manual. Nenhuma garantia de análise de efeitos de C# arbitrário.
7. **Interatividade de primeira classe:** formulários, sliders, botões, cálculos, gráficos, tabelas, simuladores e visualizações podem ser inseridos e editados no próprio documento. Criação por IA é proposta revisável, não autorização de execução/escrita.
8. **Mobile-first e estável:** foco, caret, posição de leitura, touch, rolagem, teclado, seleção, acessibilidade e latência constituem critérios de aceitação, inclusive com DEVICE.
9. **Sem acoplamento entre produtos:** reuso de capacidades do Ecosystem apenas quando houver segundo consumidor e saldo positivo de complexidade (NN-022); preservar ADR-0029/0030.
10. **Compatibilidade:** Markdown padrão continua legível; extensão de formato é aditiva, versionada e depende de decisão de dados específica.

## Questões técnicas explicitamente não decididas

- Gramática exata de blocos/refs e política de persistência de parâmetros em `.md` versus artefatos textuais versionados.
- Biblioteca de edição de código e sua compatibilidade com Android/Gboard.
- Executor avançado: C# scripting/compilação, isolamento, limites, fallback offline, compatibilidade AOT.
- Formato portátil dos resultados e exportação interativa (não prometer HTML executável).
- Critérios quantitativos de desempenho após baseline em aparelho.
- API pública de extensões; nenhum novo contrato global está autorizado neste ADR.

## Requisitos mínimos para aceitar implementação

- Backlog rastreável CD-00..CD-11 em `COMPUTATIONAL-DOCUMENTS-AND-TOOLS.md` e posterior inclusão formal na sequência do roadmap após revisão.
- Testes de round-trip Visual/Fonte, seleção, caret, teclado, scroll, undo, recuperação e alteração concorrente do arquivo-fonte.
- Caminho de execução nunca concede filesystem/network/IA por mero conteúdo; crash e falha são estados seguros.
- Prova de que a UI de nota comum não ganhou poluição de botões nem trabalho desnecessário.
- Testes Core/Client, CI, avaliação de dependências e Android físico; nenhuma marcação `[x]` apenas por smoke headless.

## Consequências

A entrega mais econômica começa pela edição de arquivos reais e por componentes declarativos nativos, aproveitando `ArtifactModel`, formulários tipados e editor existente. O núcleo do Urbe não assume as obrigações de uma IDE completa. Os primeiros casos ponta a ponta obrigatórios são: gráfico com slider dentro da nota, ferramenta declarativa inserida por usuário/agente e código `.cs` vinculado à documentação sem cópia divergente.

O risco principal passa a ser consistência de UX; qualquer funcionalidade que prejudique a digitação/rolagem da nota comum deve ser bloqueada ou removida, ainda que tecnicamente funcional.

## Fontes normativas e referências

- MANIFEST NN-001/002/005/006/009/010/011/016/017/018/020/022, AGENTS, ADR-0011/0015/0029/0030/0032/0034/0042/0044.
- `apps/urbe/docs/csharp/PRODUCT-DIRECTION.md`; `UX.md`; `ROADMAP.md`; `ASSISTANT-FORMS.md`; `GRAPHS.md`; `EDITOR-INTERACTION-STABILITY-20261026.md`.
- **Pesquisa detalhada, links e plano:** [COMPUTATIONAL-DOCUMENTS-AND-TOOLS.md](../../apps/urbe/docs/csharp/COMPUTATIONAL-DOCUMENTS-AND-TOOLS.md).
