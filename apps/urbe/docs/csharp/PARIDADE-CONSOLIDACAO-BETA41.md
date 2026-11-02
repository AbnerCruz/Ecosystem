# Urbe — paridade observável com Numa Consolidação 2.5 (beta.41)

**Base:** `main` e código/CI da beta.40, auditados em 01/11/2026. Fonte normativa: `docs/product-plans/urbe/Numa_Consolidacao_2.5.md`.
**Método:** distinguir evidência do JSON exportado da conversa, implementação atual, testes de CI, relato do usuário e validação Android física. Os checkboxes normativos permanecem **abertos** até aceites exigidos. Isto é um relatório de rastreabilidade, não um segundo roadmap.

## Resultado e classificação

| CST | Fato no código ou na sessão | Ação beta.41 | Aceite ainda necessário |
| --- | --- | --- | --- |
| 078; 213; 217; 218 | Na beta.40, `update_plan` inicial foi estruturado, mas as atualizações sem `goal` caíram silenciosamente no contrato legado; final_plan_review/final_user_review não foram validados. Evidência: conversa de teste beta.40 enviada pelo proprietário; `AssistantLayeredPlan.cs`. | `NormalizeUpdate` preserva contexto no mesmo RunHost, sem estado compartilhado entre chats; atualizações com campos de grafo e sem contexto são recusadas; placeholders `Ainda pendente` não aprovam nós. Testes TDD em `AssistantLayeredPlanTests`. | Repetir cenário real, nova execução/resume, erros de ferramenta e encerramento; não há recibos independentes de evidência. |
| Abas do editor; CST-015/017 | A beta.40 usa `EditorOverlay.cs`: chip de 48dp, fechamento de 48dp, rótulo até 180dp, margens verticais de 6dp — visual grande para telefone. | Cápsula visual de 40dp, texto 12dp limitado a 124dp, margens menores; alvo de seleção externo preservado em 48dp; teste headless de geometria. Sem mudar semântica de aba explicitamente aberta. | Conferir toque e acessibilidade no moto g32, múltiplas abas e fontes escaladas. |
| CST-142 | `MathLayoutUiTests` testa altura intrínseca, overflow e fórmulas extensas; beta.39 alterou render. O usuário ainda relata sobreposição/corte. | Sem alterar renderer sem reprodução adicional; conferir CSS, Avalonia e vídeo/dispositivo. | Variantes de frações, fonte, Visual e edição, IME aberta e fechada em aparelho. **Bloqueador aberto**. |
| CST-143 | `RadialNavUiTests.RadialNav_DoesNotCoverReadingEditingControlOnCompactEditor` exercita 320/360/412 dp, com botão no dock esquerdo. O relato em aparelho não foi invalidado. | Preservar layout e teste; sem fingir que headless prova toque físico. | Testar orientações, acessibilidade, menu expandido e botão de modo no aparelho. **Bloqueador aberto**. |
| CST-144; 004–007; 129 | `EditorOverlay` tem autosave com pausa de 900ms, máximo 3s, pipeline de escrita; existem `NotesSaveSchedulerTests`, `InterruptedWritesTests` e regressões de navegação. O usuário reportou perda de texto. | Não enfraquecer salvamento nem fazer reescrita sem reprodução mínima; exigir rastreamento de draft/IME. | Interromper digitação, alternar nota/aba e modo, fechar forçadamente, falha de armazenamento e recuperação no Android. **Bloqueador aberto**. |
| CST-016 | Relato de Enter que fecha o teclado e foco intermitente no editor e chat. Avalonia tem histórico de bugs Android IME. | Sem aplicar workaround que remova entrada de texto; inspeção de `KeyboardFocusPolicy`, `VisualSurface` e `EditorOverlay` indica necessidade de teste físico. | Reproduzir com Gboard e IME alternativo após Enter, selecionar linha e voltar do segundo plano. **Bloqueador aberto**. |
| CST-112; 136; 148 | Usuário relatou expansão de ferramenta e aviso ANR; existem testes headless de expansão, não medição de ANR no dispositivo. | Nenhuma correção anunciada sem traces/perf antes/depois. | Perfetto/ANR e longas conversas no dispositivo. |
| CST-035 | HTML de teste exibiu a autoria de IA após cada bloco como texto visível; preservação de origem existe, discrição editorial não foi aprovada. | Preservar marcas; estudar ajuste de visualização sem adulterar dado canônico. | Conferir visual e exportações PDF/HTML, acessibilidade e texto extraído. |
| CST-063; 149–164 | Exportação HTML de três capítulos fornecida, estruturalmente coerente. Exportação PDF, edição contextual de fontes e retomada não foram demonstradas pelo material recebido. | Sem alegar entrega dos demais formatos. | Testar abrir e conferir PDF salvo, origem viva, recuperação e conflito. |
| CST-227–228 | Os componentes nativos de progresso e checklists são somente decisão documental em 2.5, não resultam da beta.40. | Não simulados nem marcados prontos nesta correção. | Implementação e testes de componentes Avalonia e fonte de verdade no vault. |

## Pesquisa externa e confronto

- [Avalonia TabControl](https://docs.avaloniaui.net/docs/how-to/tabcontrol-how-to) e [template Fluent TabItem](https://github.com/AvaloniaUI/Avalonia/blob/main/src/Avalonia.Themes.Fluent/Controls/TabItem.xaml): geometria interna, padding e `MinHeight` afetam tamanho de abas; manter alvo de toque distinto da cápsula visual.
- [VS Code — Modern UI e densidade](https://github.com/microsoft/vscode/issues/334285) e [código real do workbench](https://github.com/microsoft/vscode/blob/main/src/vs/workbench/browser/parts/editor/editor.ts): comunidade quer abas compactas sem sacrificar legibilidade; adotado apenas princípio de densidade e truncamento, não o layout desktop.
- [Avalonia issue #19178](https://github.com/AvaloniaUI/Avalonia/issues/19178): regressão de conexão de IME que zerava foco no Android; encerrada upstream, não é diagnóstico suficiente de falha do Urbe. Evitado workaround que desativa IME.
- [Avalonia 12.1.2, Enter e caret](https://github.com/AvaloniaUI/Avalonia/discussions/22131): bug corrigido upstream com entrada multiline; comparar versão atual antes de atribuir causas.
- [LangGraph checkpointers](https://github.com/langchain-ai/docs/blob/main/src/oss/langgraph/checkpointers.mdx): checkpoints por thread impedem perda de contexto, porém exigem estado durável e ownership. Adotada só projeção efêmera por execução; armazenamento durável segue pendente e não foi fingido.
- Feedback de tab sizing e UI compacta é **relato de comunidade**, não prova de bug no Urbe. Os bloqueadores Android do plano são **relatos do proprietário**; testes headless existentes não encerram o gate.

## Limites explícitos

- Nenhuma prova de teste em aparelho físico foi produzida por este PR.
- O novo plano preserva `goal/current_state/target_state` dentro de uma execução do agente; não entrega retomada durável entre reinícios/conversas.
- Evidência textual permanece declaração do agente, não comprovante criptográfico de ferramentas, CI ou validação por outro verificador.
- Os demais CST-001–228 permanecem sob o checklist canônico; este incremento não afirma paridade integral do produto com todas as capacidades previstas.
