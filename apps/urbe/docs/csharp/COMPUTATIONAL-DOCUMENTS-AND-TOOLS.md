# Urbe — Documentos Computacionais, Código e Ferramentas Interativas

> **Estado:** conceito de produto solicitado e aprovado pelo proprietário em conversa de 28/10/2026; **especificação e arquitetura propostas para revisão**, não implementação concluída. Este documento complementa `PRODUCT-DIRECTION.md`, `UX.md` e `ROADMAP.md`. A formalização estrutural está em `docs/adr/0045-urbe-documentos-computacionais-e-ferramentas-interativas.md` (proposto). Nenhuma mudança em runtime, vault ou release foi realizada neste estudo.

## 1. Formulação do produto

**O Urbe é um ambiente de produção de artefatos sobre arquivos reais, onde texto, matemática, código, dados, visualizações e ferramentas interativas podem conviver no mesmo fluxo de trabalho.**

Não criar um clone de VS Code/Jupyter nem uma IDE com outro nome. A unidade de trabalho do usuário continua sendo **o arquivo/artefato e seu contexto no Vault**. Um texto continua simples, legível e editável sem depender de kernel, serviço, plugin ou conexão.

Três capacidades, sem três interfaces concorrentes:

1. **Editar** — notas Markdown e arquivos-fonte reais, com interação consistente.
2. **Experimentar** — cálculos, consultas, simulações e resultados ancorados ao contexto.
3. **Interagir** — ferramentas com botões, formulários, controles, gráficos, mapas e visualizações dentro de documentos, ou em superfície ampliada somente quando necessário.

### Não objetivos

Não competir com VS Code em depuração, terminal, Git, extensões, LSPs multi-language, build completo ou gestão de toolchains. Não reproduzir literalmente `.ipynb` como formato canônico obrigatório; não reintroduzir WebView/HTML como interface nativa; não criar outro runtime de IA; não acoplar Urbe a Lunet2D, Atlas Forge ou math-authoring; não executar código automaticamente ao abrir notas.

## 2. Evidências — Urbe auditado

**Fonte:** `AbnerCruz/Ecosystem`, main consultada em 28/10/2026 (SHA `65f80115cc65bcfd2bd405323239f64941c11009`, sujeito a evolução por outros agentes).

- `apps/urbe/docs/csharp/PRODUCT-DIRECTION.md`: C# nativo, Avalonia, Android primeiro; Vault em arquivos reais, editor Visual central, produção de artefatos; o mundo/cidade estão congelados.
- `apps/urbe/csharp/src/Urbe.Core/ArtifactModel.cs`: extensão e editabilidade já classificadas para Markdown, HTML, JS, CSS, JSON, YAML etc.; `.cs`, `.py` e `.ts` não aparecem no conjunto textual consultado. É um ponto de evolução, não suporte comprovado para edição de código.
- `apps/urbe/csharp/src/Urbe.Client/FilePreviewPanel.cs`: arquivo de texto e código já pode ser exibido em prévia, atualmente **somente leitura**, distinguindo pré-visualização de editor real.
- `apps/urbe/csharp/src/Urbe.Client/VisualSurface.cs` e `EditorOverlay.cs`: editor Visual usa edição por bloco e editor Fonte; manipulação de seleção, foco e rolagem ainda é sensível.
- `apps/urbe/docs/csharp/EDITOR-INTERACTION-STABILITY-20261026.md`: defeitos concretos de salto de rolagem, foco, reconstrução de blocos, estatísticas durante digitação; teste headless não certifica dispositivo.
- `apps/urbe/docs/csharp/ASSISTANT-FORMS.md` / ADR-0042: `FormDefinition`, campos tipados, controles Avalonia, validação, formulário de chat e templates já existem; não reinventar o esquema antes de avaliar reuso. Formulários em chat e ferramentas persistidas **não possuem a mesma autoridade de estado**.
- `apps/urbe/docs/csharp/GRAPHS.md`: grafos autorais como artefatos reutilizáveis, inclusive por embed; gestos do canvas não devem deslocar o editor.
- ADR-0044: Atlas Map é conteúdo HTML especial, sob ponte estritamente delimitada; não o transformar por acidente em executor HTML genérico do Vault.
- ADR-0029/0030: `math-authoring` é produto separado com documento semântico próprio; **não** reimplementar seu motor/timeline no Urbe.
- `apps/urbe/docs/csharp/UX.md`: resposta imediata, fluxo sem interrupção, controles alcançáveis, poucas escolhas e previsibilidade. Preservar a direção; trechos históricos sobre cidade não reabrem a simulação.

## 3. Pesquisa de referência — capacidades e limites

| Sistema | Capacidade relevante | Lição/limitação |
| --- | --- | --- |
| Jupyter | Markdown + células de código + resultados + kernels | Execução e documento são conceitos diferentes; kernel e estado não devem ser condição para ler/editar |
| marimo | DAG de dependências, estados stale, interface reativa, exportabilidade textual | Reexecutar apenas dependentes puros; optar por lazy para cálculos caros/efeitos colaterais |
| Observable | Explicações interativas, inspeção de valores e estado próximo ao código | Ferramenta incorporada deve exigir zero navegação extra para uso normal |
| Quarto | Documentos e dashboards interativos com controles e visualizações | Diferenciar ferramentas locais de interfaces que dependem de servidor |
| Obsidian + Meta Bind | Campos ligados a propriedades, toggles e botões nas notas | Evitar scripts/estilos/plugins encadeados para tarefas simples |
| Obsidian Mobile UX | Ferramentas contextuais e ações próximas ao polegar | Não encolher interface desktop para o Android |
| AvaloniaEdit | Syntax highlighting, folding, linha, scroll virtualizado, multicaret | Candidato técnico, **não** adoção aprovada; validar IME/Gboard/seleção/toque e carga no APK |
| Acode | Experiência mobile de arquivos-fonte | Aproveitar atalhos de símbolos e navegação, sem copiar IDE inteira |

### Voz da comunidade (evidência qualitativa, não amostra representativa)

- Relatos sobre Obsidian Android como desktop comprimido e entrada lenta em contexto: https://www.reddit.com/r/ObsidianMD/comments/1o74hpk/
- Embeds não editáveis no próprio local causam navegação e perdem a fonte única: https://www.reddit.com/r/ObsidianMD/comments/1qw13bu/
- Meta Bind: mudança em inputs ocasionando salto de scroll: https://www.reddit.com/r/ObsidianMD/comments/1lxgi1m/
- Meta Bind: complexidade de criação/organização de campos: https://www.reddit.com/r/ObsidianMD/comments/1jalhl4/
- Dataview/Meta Bind: plugins podem reconstruir edição e roubar foco: https://www.reddit.com/r/ObsidianMD/comments/1l6zbla/
- Calculadora reativa: usuário quer slider sem fechar e reabrir nota: https://www.reddit.com/r/ObsidianMD/comments/1swxxmd/
- Obsidget: demonstra demanda por contadores, trackers, calculadoras e estado persistido diretamente na nota: https://www.reddit.com/r/ObsidianMD/comments/1qeiuuf/
- Meta Bind oficial: https://community.obsidian.md/plugins/obsidian-meta-bind-plugin
- marimo: https://docs.marimo.io/guides/reactivity/
- Jupyter e formato: https://jupyter.org/
- Observable: https://observablehq.com/
- Quarto: https://quarto.org/docs/dashboards/interactivity/
- AvaloniaEdit: https://github.com/AvaloniaUI/AvaloniaEdit

**Síntese:** interatividade sem edição no contexto é só visualização. Interatividade que rouba foco, reposiciona a nota ou exige plugins encadeados prejudica mais do que ajuda.

## 4. Um modelo mental para o usuário

- Abrir `Pesquisa.md`: escrever texto. Inserir `Gráfico` mostra um gráfico manipulável; tocar em `Editar` abre sua configuração na posição atual. Nenhum modo global obrigatório.
- Abrir `Modelo.cs`: o mesmo cabeçalho, abas/histórico/busca e gestos do Urbe; ferramentas de código aparecem contextualizadas. O arquivo permanece `.cs` real.
- Incorporar um trecho de `Modelo.cs` em `Pesquisa.md`: mostrar a fonte vinculada, com origem visível, abrir/editar fonte no contexto, sem duplicar bytes nem sobrescrever o documento de origem em silêncio.
- Adicionar um slider a um cálculo: o resultado muda no mesmo lugar; só dependentes baratos e puros atualizam automaticamente.
- Pedir ao agente `crie uma calculadora de juros aqui`: proposta em diff/preview, controles tipados, usuário aprova inserção. Uso da calculadora não autoriza o agente a executar ferramentas privilegiadas.
- Em ferramenta complexa (mapa/grafo/simulador), um único comando `Expandir` abre superfície dedicada e `Voltar` restaura foco e posição exata no documento.

## 5. Taxonomia de ferramentas interativas (não esconder estas diferenças)

**A — Controles nativos declarativos (primeira classe, fase inicial).** Texto, número, seletor, toggle, escala, data, botão, progress, lista, formulário, tabela filtrável, gráfico 2D. Descrições tipadas; render Avalonia; expressões puras e operações conhecidas; sem código arbitrário.

**B — Blocos computacionais seguros.** Calculadora, gráficos de funções, agregações, transformações CSV, filtros e expressões matemáticas. Runtime puro, limites de custo e tipagem; resultados/erros visíveis, identificáveis e reproduzíveis. Persistir definição; evitar gravar cache como fonte.

**C — Ferramentas programáveis avançadas.** C#/outros scripts, simulações e módulos autorizados, com executor/capacidades separadas. Segurança, compatibilidade .NET AOT/Android, limites de recursos e recuperação exigem prova real antes de oferecer. Não confundir linguagem de configuração declarativa com código livre.

**D — Conteúdo rico especial.** Atlas Map/HTML independente e outros viewers continuam sujeitos aos seus contratos restritos; não herdam acesso universal a arquivos, rede ou IA.

Cada bloco define identidade estável, tipo/versão, entradas, saídas, vínculo de dados, efeitos/permissões, estado de execução, mensagens de erro, acessibilidade e modo de fallback.

## 6. UX: invariantes irrenunciáveis

1. **Um fluxo, não três aplicativos** — menus e controles contextuais, não abas principais `Laboratório/Código/Documento`.
2. **Escrever continua simples** — notas antigas sem ferramenta não mudam de aparência ou comportamento.
3. **Rolar é rolar** — gesto vertical não seleciona texto, não inicia slider e não abre teclado; desambiguação clara de gestos.
4. **A tela não se mexe sozinha** — tocar num controle não altera âncora de leitura; atualização de gráfico ou resultado preserva posição, foco e seleção.
5. **Editar no contexto** — incorporações têm origem clara, comando de edição na fonte e retorno estável; nada de cópia divergente.
6. **Zero janelas impostas no fluxo** — inserção orientada, mini-edição no próprio bloco, expansão só por escolha.
7. **Ferramentas progressivas** — leitura apresenta resultado; interação apresenta controles; programação avançada só ao solicitar.
8. **Android verdadeiro** — teclado virtual, safe areas, alças de seleção, toque longo, scroll com dedo e orientação horizontal possuem testes específicos.
9. **Autorização observável** — leitura e cálculo não pedem permissão redundante; escrita, rede, execução de código e ações com efeitos exigem política explícita apropriada.
10. **Custo proporcional** — edição jamais aguarda cálculo, IA, render grande ou acesso a disco; orçamento de reatividade e cancelamento.
11. **Sem dependência compulsória** — documento continua legível em editor Markdown externo (fences/links visíveis com fallback textual).
12. **Undo confiável** — gesto de controle não deve criar uma entrada de histórico por frame; mudanças gravadas são transações coerentes e reversíveis.
13. **Acessibilidade** — controles e gráficos têm nomes, teclado/semântica, contraste, alternativas textuais e alvos compatíveis com UX.md.
14. **Artefato em primeiro lugar** — visualizações e operações têm saída útil e exportável, não são decoração.

### Layout mobile de referência (comportamento, não mock definitivo)

- Cabeçalho único: voltar, título/local do arquivo, ações contextuais discretas.
- Corpo ocupa espaço quase integral: o texto é dominante; blocos computacionais são componentes da leitura, não painéis paralelos permanentes.
- Ao focar código: toolbar compacta de símbolos/indentação junto ao teclado (quando necessário), sem bar de IDE fixada.
- Controle interativo assume gesto **somente em sua região**; scroll do documento prevalece fora dela.
- Resultados grandes têm preview compacto e `Expandir` opcional; nunca ocupam tela inteira automaticamente.
- Navegar para fonte e voltar mantém âncora, caret e estado sem reflow agressivo.

## 7. Arquitetura candidata (dependências para baixo)

```text
Urbe.Client / Avalonia (renderizador nativo + host contextual)
  ├─ ArtifactEditor (Fonte/Visual/Arquivo, mesmo contexto de navegação)
  ├─ InteractiveBlockHost (controles, visualização, foco, acessibilidade)
  └─ ResultPresenter (texto, tabela, gráfico, erro, estado stale)
        ↓ contratos locais
Urbe.Core
  ├─ ArtifactModel + DocumentStore/NoteLibrary (autoridades atuais)
  ├─ BlockDefinitionParser (fences/refs e validadores)
  ├─ Bindings + DependencyGraph (ciclos/refs/versões)
  ├─ PureEvaluator (math/CSV/agregações determinísticas)
  ├─ ResultCache (derivado e invalidável, não autoridade)
  └─ ExecutionContracts (capabilities, erro, efeitos, autorização)
        ↓ adaptadores opt-in sujeitos a gate
Executor avançado por linguagem/host (não implementado neste plano)
```

- **Fonte**: o `.md` guarda conteúdo autoral, fórmulas e referências. Um arquivo de código continua sendo arquivo-fonte; especificações reutilizáveis podem morar em artefatos textuais separados e versionados, com link/ID. Não introduzir escrita duplicada em Markdown + JSON sem contrato.
- **Valores**: distinguir `entrada temporária`, `valor salvo explicitamente` e `resultado derivado`. Slider contínuo não reescreve o Vault a cada movimento; `Salvar valores` ou conclusão de formulário produz uma operação atômica autorizada.
- **Reatividade**: grafo acíclico de dependências de dados **declarados**, invalidação por alteração de versão, execução pura seletiva, debounce/coalescing, cache por entrada/versionamento, stale explícito e cancelamento. Código mutável/arbitrário não recebe automaticamente essas garantias.
- **Segurança**: parse/visualização inertes; execute-capability negada por padrão para código, plugins e conteúdo importado; efeitos separados em `read`, `write`, `network`, `execute`. Nunca considerar `trust` como permissão irrestrita sobre o Vault. Plano de ameaça obrigatório para executor avançado.
- **Erros**: sem resultado falso no sucesso; fonte alterada torna saída stale. Avisos locais e reutilizáveis; travamento do executor não encerra editor.
- **Render**: nativo Avalonia para UI, controle e gráfico inicial. HTML/WebView permanece só conteúdo/viewer isolado conforme ADR-0032/0044, sem transformá-lo em nova casca de UI.
- **Integração com agentes**: propostas estruturadas (criar/editar definição, código e note references), diffs revisáveis e ferramentas existentes. IA não inventa novo protocolo UI nem recebe autorização pelo fato de mostrar um botão.
- **Integração entre Products**: Urbe não referencia Lunet/math-authoring; futuro reuso por capability contratada, se segundo consumidor real comprovar redução de complexidade (NN-002/006/022).

### Contrato de exemplo (ILUSTRATIVO, NÃO FORMATO APROVADO)

````md
# Estudo da função
Observe o efeito do coeficiente a.

```urbe-tool
{"version":1,"kind":"function-plot","inputs":[{"id":"a","type":"range","min":-5,"max":5,"value":1}],"expression":"a*x*x - 4*x + 3"}
```
````

A sintaxe final depende de protótipo lossless, política de versões, migrador e revisão de dados. Não colocar este JSON em produção por analogia.

## 8. Riscos, tradeoffs e contenções

| Risco | Gravidade | Controle e critério de bloqueio |
| --- | --- | --- |
| Colisão de gestos em bloco e documento | Crítica | Teste de arrastar/rolar sem seleção/teclado e sem mover página ao operar controle |
| Editor Visual reserializar/destruir bloco de ferramenta | Crítica | Round-trip byte-sem perda de conteúdo desconhecido; casos com fences e edição simultânea |
| Código importado/gerado por IA executar em Android | Crítica | Sem autorun; capabilities e isolamento comprovados; se não houver isolamento suficiente, não habilitar executor |
| Dois estados canônicos para um valor | Alta | Ownership explícito, transações e invalidadores; fallback legível |
| Recomputação custosa/bateria | Alta | Lazy/stale, DAG, cancelamento e métricas de CPU/memória; sem execução durante digitação não relacionada |
| Poluição visual e curva de aprendizagem | Alta | Modo básico sem controles extras; mini-edição e contextualização com testes de usabilidade |
| AvaloniaEdit não funcionar bem com IME | Alta | Spike comparativo com edição existente; aceitar/substituir somente após DEVICE |
| Cópia divergente de código transcluído | Alta | Referência ao original com confirmação de gravação e aviso de conflito |
| Licença/footprint de bibliotecas | Média | Revisão legal, APK size, memória de inicialização e startup |
| Escopo virar IDE completa | Alta | Não objetivos e revisão de cada feature por utilidade à produção de artefatos |

## 9. Sequência de prova e implementação (não iniciar execução sem roadmap/gates)

- [ ] **CD-00 — Baseline/UX**: medir teclado, scroll, seleção, abertura de notas, consumo, e identificar regressões do editor existente em aparelho. Definir tarefas de teste e cenários negativos antes de código.
- [ ] **CD-01 — Arquivos-fonte**: ampliar ArtifactModel conforme casos, abrir/editar/salvar arquivos reais com história/undo/conflitos; testes de byte-preservation, quebras LF/CRLF, encoding e arquivos grandes. Prova Android física.
- [ ] **CD-02 — Editor contextual**: realce, indentação, fechamento de pares, busca, navegação, barra de símbolos junto do teclado; spike comparativo AvaloniaEdit vs motor existente com custo de memória/IME, sem terceira UI.
- [ ] **CD-03 — Bloco declarativo/round-trip**: render inerte, inserção via menu de bloco, edição inline de definição, fallback Markdown, export legível; nenhuma execução automática.
- [ ] **CD-04 — Ferramentas interativas nativas**: formulário, sliders, botões, tabela, seletor, gráfico e calculadora de expressões; bindings locais e persistência explícita; analisar reutilização do UC-90 em vez de duplicar.
- [ ] **CD-05 — Dependências e resultados**: DAG tipada, invalidadores, cache, estados idle/running/stale/error, lazy para caro e efeitos, cancelamento; armazenamento derivado não canônico.
- [ ] **CD-06 — Referências de código e dados**: abrir/editar arquivo de origem a partir do documento, retornar ao ponto exato, CSV/table source, conflitos e provenance.
- [ ] **CD-07 — IA como autora de ferramentas**: agente cria bloco declarativo com preview/diff/aprovação, usa permissões existentes, não executa nem persiste silenciosamente.
- [ ] **CD-08 — Executor avançado**: apenas após threat model, viabilidade Android/AOT, processo/isolamento, limites, API versionada e validação; se inviável, permanecer com motor puro.
- [ ] **CD-09 — Exportação de artefatos**: texto, gráficos, tabela e fallback estático em formatos acessíveis; interatividade portátil é opção posterior, não promessa implícita de HTML executável.
- [ ] **CD-10 — Piloto completo**: nota de matemática com slider/gráfico; uma ferramenta criada pelo agente; arquivo `.cs` ligado à nota; cada caso passa em Android e reabre offline, incluindo falhas de processo.
- [ ] **CD-11 — Auditoria, feedback e iteração**: comparar resultados com baseline, coletar feedback de uso, ajustar visibilidade/performance antes de ampliar categorias.

**Gates por fatia:** testes Unit/Core + Client, consistência e boundaries do Ecosystem, CI integrado, Android físico com toque/Gboard/restart, revisão de permissão/persistência, e autorização humana para mudanças críticas. Sem porcentagens de performance inventadas; usar baseline real.

## 10. Critérios de aceitação ponta a ponta

1. `nota.md` sem ferramenta abre/edita como hoje, sem qualquer botão extra e sem atraso mensurável atribuído ao recurso desligado.
2. Inserir gráfico, alterar slider e voltar a escrever não muda a posição visual arbitrariamente, não abre teclado inesperadamente e preserva caret.
3. Valores de um gráfico não entram no histórico por frame; `Salvar` (se aplicável) produz transação recuperável.
4. Arquivo `Modelo.cs` abre e grava em pasta real SAF; editar, fechar, reabrir e abrir externamente preserva conteúdo e quebras de linha declaradas.
5. Nota mostra trecho de `Modelo.cs` sem cópia; `Editar fonte` e `Voltar` preservam origem e posição, detectando revisão externa conflitante.
6. Mudança em dado invalida somente resultados dependentes; cálculo caro permanece `Desatualizado — executar`.
7. Arquivo baixado de fora, ferramenta gerada por IA ou nota importada **não executa código** ao ser exibida.
8. Falha no parse, erro no cálculo, ausência de executor ou processo morto não destrói texto nem bloqueia leitura.
9. Exportação mantém explicação e imagem/tabela estáticas quando destinatário não possui Urbe.
10. O usuário comum cria ferramenta básica pelo menu sem escrever JSON, código, nomes de kernel ou escolher ambiente.

## 11. Decisão de produto e estado da formalização

**Direção aprovada pelo proprietário:** incluir capacidades semelhantes a Jupyter, edição útil de código e ferramentas interativas sem criar três aplicativos nem concorrer com IDE.

**Propostas ainda sujeitas a validação:** esquema exato de blocos, mecanismo do editor, regra de persistência, runner C#, distribuição de ferramentas reutilizáveis, formatos de exportação e critérios quantitativos finais.

**Próximo passo normativo:** revisar ADR-0045 e esta especificação; registrar etapas no ROADMAP do Urbe depois da revisão estrutural, sem marcar implementação como concluída. Mudanças de formato/permissões exigem ADR e gates aplicáveis. Nunca mesclar automaticamente uma proposta crítica porque foi documentada.

## 12. Links internos de autoridade

- `MANIFEST.md` NN-001/002/005/009/010/011/016/017/018/020/022.
- `docs/adr/0032-urbe-ui-nativa-sem-webview.md`.
- `docs/adr/0030-documento-canonico-de-autoria-matematica.md`.
- `docs/adr/0042-urbe-formularios-nativos.md`.
- `docs/adr/0044-urbe-atlas-map-host.md`.
- `apps/urbe/docs/csharp/{PRODUCT-DIRECTION,UX,ROADMAP,ASSISTANT-FORMS,GRAPHS,EDITOR-INTERACTION-STABILITY-20261026}.md`.
