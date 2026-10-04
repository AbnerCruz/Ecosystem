# Auditoria da Fase 3 — IDE

- Data da auditoria técnica: 2026-09-30
- Versão testada (release): **0.0.1-dev.1000008** (`1ff4386bae93edf027db81c8de6579640d2e58ed`)
- Responsável: Claude (continuação do trabalho do Codex; ver `AgentsChat.md`)
- Release candidata (link do APK): informado ao usuário junto com o roteiro
- Estado: **✅ concluída e aprovada no aparelho — LUNET-303 / Issue #204**

## 1. Gate (§28)

Critério do spec: experiência de IDE real.
Resultado: **aprovado.** O proprietário aprovou explicitamente o gate integral LUNET-303 pelo portal na Issue #204, sobre o objeto `fase-3-candidata.md`, que referencia o roteiro A–J e J1. A automação registrou o resultado no handoff canônico. Os tempos brutos de J1 não foram persistidos no formulário; como o gate foi aprovado e nenhuma falha bloqueante foi registrada, a auditoria não inventa números nem ativa a `PieceTable` por antecipação.

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

- [x] Suíte completa verde: 286 testes, incluindo `Fase3RoteiroTests` (17), a versão automatizada dos passos do roteiro que não dependem de tela: A1, A4–A5, B1–B8, C1–C5, D1–D4 (lógica), E1–E6, F1–F5, G1–G2, G4, H1–H6 e I1, executados sobre um projeto real do modelo Coletor.
- Achado da automação: o Inspector ficava **vazio** no modelo Coletor de moedas (todos os campos eram privados). Corrigido marcando `score`, `best` e `lives` com `[Inspect]` (e `Range`/`Tooltip`).
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
- Data de envio: 2026-10-04
- Resultado por item (passou / falhou / não testado): **gate integral aprovado via portal; relatório bruto por passo não foi persistido**
- Modelo do aparelho e versão do Android: não persistidos no formulário canônico de validação
- Falhas, correções e versão de reteste: nenhuma falha bloqueante registrada na aprovação
- Aprovação explícita do gate pelo usuário: **aprovada — Issue #204 / commit `ed8bfe36c4d076ea20a2c9229805f83c27f10ff6`**

## Decisão

Fase concluída? **Sim.** O gate integral foi aprovado pelo proprietário e registrado canonicamente. J1 não deixou tempos brutos persistidos, portanto não há base para uma troca estrutural do editor nesta fase; `PieceTable` permanece implementação testada disponível, e qualquer ativação futura exige evidência própria.


## Auditoria incremental — LUNET-302 (2026-10-03)

Achado real: remover using apagava linhas inteiras (inclusive classe inline);
ordenar usings reconstruía texto e perdia `global`, comentários e CRLF.
Correção na camada Editor: remoção pelo Span da diretiva e ordenação de linhas
standalone preservadas; recusa de condicionais/conteúdo misto. Regressões cobrem
código inline, remoções múltiplas, escopo global em outro arquivo, CRLF, EOF sem
newline e blocos ambíguos. C6 do roteiro cobre integração Android.

ADR 0007 formaliza Framework/IDE/Studio; boundary atual preservado. O gate
continua pendente; aprovação da importação ZIP (#121) não aprova a Fase 3 inteira.
Resultados automáticos e CI são registrados no handoff LUNET-302; não substituir
com essa auditoria a evidência humana existente nem a reconciliação de #111.

## Entrega canônica — LUNET-303 (2026-10-04)

Candidato fixado: `lunet2d-v0.0.1-dev.1000008`, commit `1ff4386bae93edf027db81c8de6579640d2e58ed`. [APK, digest, instruções e resposta por bloco](fase-3-candidata.md). O roteiro A–J e a medição J1 compõem o objeto integral aprovado pelo proprietário no portal pela Issue #204; o resultado foi registrado no handoff `HO-20261004-lunet-f3-candidate`. Aprovações específicas #121 (ZIP), #155 (C6) e #163 (P4-4) permanecem rastreáveis e distintas. Os tempos brutos de J1 não foram preservados no formulário, portanto esta auditoria registra a lacuna sem fabricar métricas.


## Fechamento canônico — 2026-10-04

- Candidato: `lunet2d-v0.0.1-dev.1000008`, commit `1ff4386bae93edf027db81c8de6579640d2e58ed`.
- Gate humano integral: **passed**, Issue #204; commit de registro `ed8bfe36c4d076ea20a2c9229805f83c27f10ff6`.
- Evidência automatizada anterior permanece válida: 321 testes Lunet, build portátil sem warnings/erros e CI da candidata verde.
- J1: a aprovação cobre o passo, mas o texto bruto com tempos não foi persistido. Nenhuma métrica é inferida.
- Decisão sobre `PieceTable`: **não ativar sem necessidade demonstrada**. A estrutura permanece pronta/testada e pode ser retomada se o Profiler/Fase 4 ou uso real revelar regressão.
- A Fase 4 do Lunet fica liberada pela governança local; isso não autoriza automaticamente IPC/Host API do Ecosystem, que possuem decisão e gates próprios.
