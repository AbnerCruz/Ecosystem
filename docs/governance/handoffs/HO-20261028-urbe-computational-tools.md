# HO-20261028 — Urbe: documentos computacionais e ferramentas interativas

- **Tipo:** discovery / produto / proposta arquitetural; **nenhum código executável alterado**.
- **Origem:** proprietário (28/10/2026) aprovou estudar integração de funcionalidades semelhantes a Jupyter, edição de código que faça sentido ao Urbe e ferramentas interativas; rejeitou fragmentar UX em três experiências independentes.
- **Escopo:** apenas documentação na branch `docs/urbe-computational-artifacts-20261028`; não aplicar na main automaticamente e não publicar release.
- **Main observada inicialmente:** `65f80115cc65bcfd2bd405323239f64941c11009`; verificar evolução antes de integrar.
- **Docs criados:** `apps/urbe/docs/csharp/COMPUTATIONAL-DOCUMENTS-AND-TOOLS.md`; `docs/adr/0045-urbe-documentos-computacionais-e-ferramentas-interativas.md` (proposto). ROADMAP registra `UC-92` com itens todos pendentes.
- **Fatos examinados:** `ArtifactModel.cs`, `FilePreviewPanel.cs`, `VisualSurface.cs`, `EditorOverlay.cs`, `PRODUCT-DIRECTION.md`, `UX.md`, `ASSISTANT-FORMS.md`, `GRAPHS.md`, `EDITOR-INTERACTION-STABILITY-20261026.md`, `ROADMAP.md`; ADR-0029/0030/0032/0034/0042/0044.
- **Fontes governança:** `MANIFEST.md` seção 0.1; NN-001/002/005/006/008/009/010/011/016/017/018/020/022; `AGENTS.md`; `docs/governance/decisions.json`; padrões em `docs/adr/`.
- **Mercado/comunidades:** Jupyter, marimo, Observable, Quarto, Obsidian+Meta Bind, Obsidget, AvaloniaEdit; links e feedback específico no estudo; relatos qualitativos, sem presumir pesquisa estatística.
- **Direção proposta:** artefato/documento como centro, render nativo e controles contextuais; interatividade declarativa como capacidade base; execução C# separada e condicionada à segurança no Android; agentes propõem ferramentas revisáveis, sem execução implícita.
- **Riscos principais:** rolagem e foco, perda de dados/round-trip do Visual, duplicação de estado, reatividade onerosa, WebView acidental, execução de código importado e acoplamento entre produtos.
- **Testes realizados:** inspeção textual do repositório e documentos de referência. **Não** foram rodados builds, testes, testes de dispositivo ou protótipos; não existe afirmação de aprovação técnica. Documentação apenas.
- **Próximo passo:** review do ADR-0045 e especificação; confirmar ownership do esquema persistido e política do executor; então decompor CD-00..CD-11 em issues/fatias governadas começando por baseline de UX e arquivos reais. Gate humano explícito para mudanças críticas, DEVICE para Android.

## Adendo de UX e estado do PR (28/10/2026)

- O proprietário **não solicitou abertura de PR**. O #844, aberto anteriormente por iniciativa do assistente, foi **fechado sem merge**, por ordem explícita do proprietário. GitHub mantém histórico de PR fechado; não equivale a exclusão física. Nenhum novo PR deve ser aberto sem pedido explícito.
- A direção de UX foi reiterada: **reduzir interfaces, apresentar mínimo necessário, preservar intuição e elegância apesar do crescimento de recursos**. A avaliação é estrutural, não cosmética.
- `apps/urbe/docs/csharp/UX.md` recebeu gate global de minimalismo (§1.1), correção do P11 histórico referente à cidade congelada e critérios de revisão. A especificação, ADR-0045 e UC-92 refletem a mesma prioridade.
- Os documentos permanecem **somente na branch documental** `docs/urbe-computational-artifacts-20261028`, sem integração na main. O usuário não autorizou novo PR, merge, implementação ou release. Revisão e integração são etapas futuras separadas, sujeitas à governança e a pedido explícito.
