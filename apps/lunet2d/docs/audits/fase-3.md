# Auditoria da Fase 3 — IDE

- Data da auditoria técnica: 2026-09-30
- Versão testada (release): **aguardando release candidata** (a release da `main` após o PR do Preview isolado)
- Responsável: Claude (continuação do trabalho do Codex; ver `AgentsChat.md`)
- Release candidata (link do APK): informado ao usuário junto com o roteiro
- Estado: **🟡 aguardando validação do usuário no aparelho**

## 1. Gate (§28)

Critério do spec: experiência de IDE real.
Resultado: **pendente.** A implementação e os testes automáticos estão prontos; o gate só é aprovado depois que o usuário executar o roteiro [`fase-3-roteiro.md`](fase-3-roteiro.md) no aparelho e aprovar explicitamente. Nada da Fase 3 além das Fases 1–2 foi validado em aparelho até aqui, exceto a lista de projetos, o editor básico, exportação em ZIP e a versão inicial da documentação (usuário relatou que "funcionou tudo normal" na v0.0.1-dev.63/68).

## 2. Itens do ROADMAP

Legenda: código (projeto), testes automáticos (T), docs (D), integração no app (A). "Aparelho" = ainda precisa do roteiro.

| Item | Código | Testes | Docs | Integração no app | Estado |
| --- | --- | --- | --- | --- | --- |
| Editor: realce, linhas, undo, indentação, autocompletar, definição, referências, dica, localizar, diagnósticos | `Lunet.Editor`, `CodeEditText` | T (Editor) | ADR 0003 | A | aparelho (parcial validado) |
| Multi-cursor | `MultiCursor`, `CodeEditText.Ide` | T | ADR 0003 (adendo) | A | aparelho |
| Code folding | `CodeAnalyzer.GetFoldRegions`, `CodeEditText.Ide` | T (regiões) | ADR 0003 (adendo) | A | aparelho |
| Atalhos de teclado | `Shortcuts` | T | ajuda no app | A | aparelho |
| Minimap | `MinimapView` | — (visual) | Configurações | A | aparelho |
| Documentos grandes / virtualização | `PieceTable` (não ligada) | T (200 mil linhas, 500 edições < 2 s no host) | ADR 0003 | **Medir o editor** no aparelho (J1) | aberto: decidir após a medição |
| Renomear, formatar, quick fixes, inspeção de símbolos | `CodeAnalyzer.*`, `CodeFormatter` | T | roteiro | A | aparelho |
| Compilação incremental | `GameCompiler` | T | — | A | concluído (lógica portátil) |
| Explorer | Fase 1 | T | — | A | concluído |
| Painéis | Preview, Inspector, Console, Problems, Search, Documentation, Explorer | parcial | — | A | aparelho; Assets → Fase 5, Profiler → Fase 4, Agent → Fase 8 (movidos com motivo) |
| Layouts (mover, redimensionar, esconder, salvar) | `WorkspaceLayout`, `SplitterView` | T (modelo) | roteiro | A | aparelho |
| Landscape / seleção acidental | `MainActivity.Layout` | — | — | A | aparelho |
| Busca no projeto | `ProjectSearch` | T | — | A | aparelho |
| Logs exportáveis | `LogExport` | T | — | A | aparelho |
| Settings | `EditorSettings` | T | — | A | aparelho |
| Inspector (reflexão, atributos, `Inspector<T>`, jogo em execução) | `ObjectInspector`, `InspectorPanel` | T (7) | guia `inspector` | A | aparelho |
| Documentação offline e "Explain in Documentation" | `Lunet.Docs`, `DocumentationPanel` | T | guias | A | aparelho (parcial validado) |
| Cada API documentada (parâmetros, retorno, exemplos, versão) | XML docs | T (cobertura + exemplos compilam) | JSON da API | painel | concluído |
| Documentação apontando para os jogos oficiais | — | — | — | — | movido: depende da Fase 11 |
| Classificação de mudanças | `ChangeClassifier` | T | ADR 0006 | status após Run | aparelho |
| Fast × Isolated Preview | `PreviewHost`, `IsolatedPreviewActivity` | — (só CI compila) | ADR 0006 | A | aparelho (mexe no Preview aprovado: regressão A1–A2, G5–G6) |
| Autosave com journal e recuperação | `AutosaveJournal` | T | guia `recuperacao` | A | aparelho |
| Git progressivo | `Lunet.Git`, `GitPanel` | T contra o `git` real | ADR 0005, guia `git` | A | aparelho (push/pull no GitHub real: H7–H8) |

## 3. Requisitos do spec ainda não cobertos

- Profiler, Assets, Agent no workspace: itens criados nas Fases 4, 5 e 8.
- Isolated Preview sem watchdog para laço infinito que trava o aparelho todo (ADR 0006): registrar na Fase 13 (Hardening).
- Git: SSH, submódulos, LFS, rebase, mesclagem de linhas e shallow clone (ADR 0005). Guardar token em Keystore antes da versão estável (Fase 12).

## 4. Princípios (§2)

- [x] Offline first: documentação, Roslyn, Git local e Preview funcionam sem rede; só push/pull usam rede, sob ação explícita.
- [x] Sem conta/telemetria obrigatória: nenhuma; a conta Git é opcional e local.
- [x] Projetos como arquivos normais: o repositório Git é um `.git` padrão; layouts e configurações ficam fora do projeto.
- [x] APIs públicas sem detalhes internos: atributos e `Inspector<T>` só expõem o contrato.
- [ ] Ferramentas oficiais usando as APIs de plugin: não se aplica antes da Fase 6.

## 5. Qualidade (§30)

- [x] Sem TODO crítico escondido: busca por `TODO|FIXME|HACK` em `src` sem ocorrências.
- [x] Sem botão sem comportamento: revisão dos menus (Arquivo, Navegar, Símbolo, Edição, Ferramentas) e dos painéis Git/Inspector/Documentação.
- [x] Sem exceção ignorada: `catch` vazios restantes têm comentário com o motivo (melhor esforço em limpeza, leitura auxiliar, opcional).
- [x] API pública documentada: `DocumentationCoverageTests` e `DocumentedExamplesTests`.
- [ ] Sem código morto nem duplicação de infraestrutura: `PieceTable` existe sem uso no app, mantida de propósito até a medição J1 (registrada no ROADMAP). O Preview rápido e o isolado agora compartilham `PreviewHost`.

## 6. Arquitetura

- [x] `ArchitectureTests`: `Lunet.Docs` e `Lunet.Git` sem dependência de outros projetos Lunet.
- [x] ADRs em dia: 0003 (adendo), 0005 (Git), 0006 (Preview isolado).

## 7. Regressão

- [x] Suíte completa verde: 269 testes.
- [ ] APK do CI instala e abre: aguardando a release candidata.
- [ ] Roteiro manual dos fluxos anteriores no aparelho: blocos A do roteiro.

## 8. Documentação

- [x] README, CHANGELOG, ROADMAP e docs da API atualizados (JSON da API regenerado e conferido por teste de deriva).

## 9. Riscos (§32)

- **EditText como editor (ADR 0003):** multi-cursor, dobrar e minimapa foram feitos por cima; a medição J1 diz se um arquivo de 40 mil linhas continua fluido.
- **Preview em dois modos:** o refactor do Preview toca código já aprovado; a regressão A1–A2 e G5 cobre isso.
- **Git próprio:** interoperabilidade testada contra o `git` de verdade; push/pull ao GitHub real ainda não foi exercitado (H7).
- **Compilação em aparelho:** inalterado desde a Fase 1.

## 10. Pendências herdadas e próxima fase

- Fase 4: Profiler (e painel), Camera2D, animação, tweening, partículas, tilemaps, UI, pathfinding, física.
- Decidir, com os dados de J1, se a estrutura de texto do editor precisa mudar.

## 11. Roteiro e validação do usuário

- Roteiro detalhado entregue: [`docs/audits/fase-3-roteiro.md`](fase-3-roteiro.md) (versão da release candidata, preparo, 40+ passos por funcionalidade, regressão, como relatar).
- Data de envio: (a preencher ao enviar)
- Resultado por item (passou / falhou / não testado): **aguardando**
- Modelo do aparelho e versão do Android: —
- Falhas, correções e versão de reteste: —
- Aprovação explícita do gate pelo usuário: **aguardando validação**

## Decisão

Fase concluída? **Não** — falta a validação do usuário no aparelho e a decisão sobre virtualização do editor (medição J1).
