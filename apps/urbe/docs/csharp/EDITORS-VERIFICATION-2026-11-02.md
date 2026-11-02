# Editor e Publicações — auditoria e verificação, 02/11/2026

Base inspecionada: `d49a05ee84a60f0ef1246c9c5135770efa61d76d`.
Autoridade de escopo: [Numa 2.5](../../../../docs/product-plans/urbe/Numa_Consolidacao_2.5.md), sobretudo 4.3, 4.12, 4.16 e 4.17. Este registro é evidência de execução, não um segundo roadmap.

## Estado observado e confronto

| Código auditado | Observação | Aplicação no incremento |
|---|---|---|
| `EditorOverlay`, `VisualSurface`, `MarkdownReader` | Comandos/alterações visuais atribuíam `TextBox.Text`; isso não representa uma operação de entrada para o histórico nativo. | Substituição mínima por `SelectedText`, mantendo o caminho de autoria, autosave e undo; teste de progresso e inserção/desfazer. Não prova todos os cenários de IME. |
| `EditorOverlay` | Fonte sem identificação lateral das linhas lógicas. | Gutter usa posições do mesmo `TextPresenter`, respeita wrap, fontes, rolagem e espaço visível. Não cria segunda caixa de texto. CST-147 parcial: falta validação humana/plataformas. |
| `MarkdownReader.Pre`, `MarkdownEngine` | Código cercado é o mecanismo extensível já utilizado para gráficos. | `progress`/`progresso` é declarativo, sem script; fonte continua no `.md`. Total de tarefas nunca é persistido como cópia. |
| `PublicationProject`, `PublicationSourceCatalog` | Composição com IDs reais, apenas nota inteira e HTML; `EmbedFragments` já implementa títulos e marcadores `^id`. | Reutilização dos marcadores existentes e títulos únicos, erro por origem ausente/ambígua, sem inventar IDs ou usar offsets como identidade. Peças distintas da mesma nota movem/removem independentemente. |
| `PublicationStudioView`, `MainView.Publications` | Fontes reordenáveis e escrita verificada; sem histórico de composição ou export EPUB. | Undo/redo de layout por sessão, objetivo/público, artigo/livro/slides e temas de saída, EPUB real pelo seletor nativo. A prévia nativa é de conteúdo, explicitamente separada da paginação final. |
| `AssistantPanel.Tools`, `AssistantAgent` | Registro e instruções ainda apontavam a `page_schema`/`write_page`. | Registro vivo usa `publication_schema`/`write_publication`, com diff, revisão exigida ao substituir e writer existente. Host nativo recusa criar `.page.json` por ferramentas genéricas. Oráculos históricos do motor antigo permanecem isolados. |

## Pesquisa consultada

- Código oficial Avalonia `TextBox.cs`: https://github.com/AvaloniaUI/Avalonia/blob/master/src/Avalonia.Controls/TextBox.cs — `SelectedText`, `HandleTextInput`, snapshots e distinção de substituição externa/edição. Aplicado ao histórico dos comandos; não importado editor web nem suprimido IME.
- Issue Avalonia #19178: https://github.com/AvaloniaUI/Avalonia/issues/19178 — relato específico de perda de foco ao fechar conexão IME; rejeitada a sugestão de desativar IME como solução para o Urbe. Não é diagnóstico do Urbe.
- CommonMark: https://spec.commonmark.org/spec — cercas de código e listas. Indicadores não contam exemplos literais e não transformam o total em outra fonte de verdade; casos suportados têm fixtures.
- Código reveal.js: https://github.com/hakimel/reveal.js/blob/master/js/controllers/printview.js — dimensões de página e quebras explícitas. Adaptado para perfil de apresentação HTML sem incorporar runtime web no cliente.
- EPUB 3.3: https://www.w3.org/TR/epub-33/ — pacote OPF, spine, navegação XHTML, recursos e identificação ZIP. Implementado pacote usando BCL, sem novo motor/licença externa; SVG matemático vem do renderer já utilizado.
- EPUBCheck oficial 5.4.0: https://github.com/w3c/epubcheck/releases/tag/v5.4.0 — piloto com acentos, fórmula SVG, tabela, checklist, progresso e imagem PNG: **0 erros, 0 avisos**. O verificador não substitui inspeção em leitores reais.
- Issue reveal.js #711: https://github.com/hakimel/reveal.js/issues/711 — clipping na impressão é um relato histórico específico; não se supõe que slide HTML paginado seja PPTX editável ou PDF editorial profissional.

## Contratos de uso e limites reais

Manual: bloco cercado `progress` com `label`, `value` e `total`. Decimais persistidos usam ponto; `$campo` consulta propriedade numérica da própria nota. Checklist: `source: tasks`, `section: Título` opcional, `note: doc_id` opcional; ID persistente, não caminho. Sem tarefas é 0/0 e **Sem tarefas**, nunca 100%. Cada tarefa aninhada conta uma vez; marcar o resumo não altera checkboxes. Campos inválidos/duplicados, valores negativos, meta zero e excedentes são erros explícitos. A configuração avançada também é editável como Markdown. Localizar pendências pelo resumo e invalidação automática de notas vinculadas fora da fonte aberta continuam pendentes.

EPUB exporta texto semântico, capítulos, navegação, listas/tabelas, autoria, PNG/JPEG/GIF locais e SVG de fórmulas; comentários privados são omitidos. Imagem remota, inválida ou ausente bloqueia a saída com mensagem, sem download implícito. Mudança de revisão durante leituras de mídia cancela a exportação. Recursos interativos, mídia audiovisual, transclusões genéricas e perfis avançados não estão prometidos como equivalentes. Seleção de pasta continua fotografia das fontes presentes, não consulta dinâmica.

## O que NÃO está concluído

O mandato completo dos dois editores permanece aberto no plano canônico. Em particular: matriz física de seleção/IME/toque/ANR e recuperação após morte de processo; edição bidirecional de trecho dentro do estúdio e transações entre múltiplas notas; retirada física de todos os módulos legados; composição livre; packaging completo; motor PDF editorial; DOCX/ODT/PPTX; motion/MP4/áudio; cursos/aplicações; infraestrutura de capacidades/skills, revisão visual multimodal e benchmarks integrais. Não marcar CST-015–026/142–144/147, CST-149–192 ou CST-227–228 fechados por este incremento. Builds, emulação, headless e validação humana têm evidências separadas no handoff.


## Correção adicional do Enter, solicitada durante a execução

Reprodução automatizada anterior à correção: o TextBox do Fonte tinha `AcceptsReturn=true`, mas `TextInputOptions.GetMultiline=false`; os três casos de quebra textual em lista (LF/CR/CRLF) também falharam. **4 testes vermelhos**, sem atribuir diagnóstico de toque físico ao headless.

Código primário examinado: `Avalonia.Android/Platform/Input/AndroidInputMethod.cs` (https://github.com/AvaloniaUI/Avalonia/blob/main/src/Android/Avalonia.Android/Platform/Input/AndroidInputMethod.cs) mapeia Default+single-line para ActionDone; `AvaloniaInputConnection.PerformEditorAction` (https://github.com/AvaloniaUI/Avalonia/blob/main/src/Android/Avalonia.Android/Platform/Input/AvaloniaInputConnection.cs) esconde o IME em Done e depois despacha Enter. Esse mecanismo é compatível com “insere linha e fecha o teclado”. A causa de todos os relatos físicos não foi provada.

Correção em `EditorInput.ConfigureMultiline`: contrato IME explícito (`Multiline=true`, `ReturnKeyType.Return`) no Fonte, no bloco Visual e no compositor da IA. Nenhum input connection paralelo, supressão do IME, timer de reabertura forçada ou troca de foco foi introduzido. Visual trata Enter como tecla ou quebra textual e mantém a mesma instância do bloco; a continuação de lista agora usa a operação registrável no undo. Código cercado é excluído dessa transformação.

**7 regressões headless aprovadas** depois da correção: Fonte com Return/foco; parágrafo Visual com teclado físico e textual; checklists com LF, CR e CRLF; lista literal dentro de código. O host de testes é Linux; esses resultados não substituem Gboard/Android físico nem execução Windows real. Publicação/CI remotos foram bloqueados pelo auto-review do envio público.
