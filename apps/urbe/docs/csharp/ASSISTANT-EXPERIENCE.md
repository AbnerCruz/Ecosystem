# Assistente: conteúdo, atividade e intervenção

Pedido do proprietário de 24/10/2026, UC-63/UC-81. Implementação C# Avalonia sobre o runtime existente, mantendo arquivo no vault como autoridade. Pesquisa e decisão de implementação registradas aqui para sobreviver à conversa.

## Critérios e implementação

| Área | Comportamento |
|---|---|
| Conversa | Resposta no fluxo, corpo a 15 dp, ações Copiar e Selecionar texto; texto original acessível em campo somente leitura para selecionar entre parágrafos |
| Atividade | Uma linha expandível por pedido; lista plana de operações, alvo com quebra de linha e estado em linha própria; parâmetros/resultados carregados sob demanda |
| Estados | Recusada distinta de falha; operação de atualizar plano é Registrado, não conclusão do plano; plano pendente não implica trabalho concluído |
| Auditoria | Raciocínio recebido/resumo do provedor, rascunhos, parâmetros e retornos continuam acessíveis; duração observada em sessão quando disponível, sem inventar duração histórica |
| Código | Qualquer linguagem ou nenhuma, Copiar com confirmação após sucesso, fonte exata atual preservada, quebra de linhas opcional e scroll horizontal |
| Seleção | Respostas selecionáveis; menus de texto para campos editáveis; sem capturar gestos quando já há seleção; re-render do transcript e autoscroll adiados enquanto a seleção existe |
| Custo | Preço de entrada/saída e unidade em todo seletor de texto, inclusive agentes e ID manual; imagens mantêm preço/unidade/fonte do serviço existente; desconhecido explicitamente indisponível |
| Consumo | Tokens acumulados separados da janela de contexto; painel por chamada/modelo; valores retornados pelo provedor identificados como potencialmente parciais; geração de imagens sem recibo não vira custo zero |
| Fontes | Painel com busca/filtro/paginação, fonte encontrada distinta de trechos consultados; falhas de leitura acessíveis; URLs validadas; citações [S1] interativas sem alterar código |
| Imagens | Prévia proporcional, carregamento por viewport e reutilização de controles, Abrir/Exportar/Copiar caminho; conjunto em ZIP; visualizador nativo com zoom por botões, Ctrl+roda e pinch |
| Intervenções | Perguntas e propostas editadas preservadas em memória ao minimizar/alternar apresentações; controles respondem somente à aprovação que os originou |
| Acessibilidade | Alvos primários de 48 dp, texto/status com organização independente, animação respeita movimento reduzido, ações com rótulos |

A renderização de fórmulas/notas mantém seu caminho de edição anterior. Texto selecionável de conversa é opt-in no MarkdownReader; não se transforma um toque em fórmula em seleção de parágrafo. Menus Copiar/Recortar/Colar/Selecionar tudo usam as operações do controle Avalonia. Nenhuma chave é copiada para o vault e campos de senha não recebem menu adicional.

## Referências pesquisadas

- Claude Artifacts: https://support.claude.com/en/articles/17153992-what-are-artifacts-and-how-do-i-use-them
- Cursor revisão/checkpoints: https://cursor.com/docs/agent/overview
- Pesquisa com citações: https://learn.chatgpt.com/docs/web-search e https://www.perplexity.ai/help-center/en/articles/10352895-how-does-perplexity-work
- Open WebUI: https://docs.openwebui.com/features/chat-conversations/chat-features/
- OpenCode, compactação e fork experimental: https://github.com/anomalyco/opencode/issues/40096
- OpenCode, grupos concluídos: https://github.com/anomalyco/opencode/issues/54515
- Pergunta minimizável: https://github.com/anomalyco/opencode/issues/32791
- Clipboard copiando versão antiga: https://github.com/open-webui/open-webui/issues/16745
- Cursor Android caret: https://forum.cursor.com/t/mobile-web-app-caret-does-not-move-on-tap/154937

Relatos de comunidade são evidência de casos concretos, não prevalência estatística. Nenhum fork web foi incorporado como dependência. Não há WebView nova nem extração transversal (ADR-0032/0033, NN-022).

## Verificação e limites reais

Regressões automatizadas cobrem conteúdo exato de clipboard (acentos, Unicode, tabs e linhas vazias), código sem linguagem, preços pequenos/desconhecidos, 77 operações com detalhes lazy, preservação de seleção ao atualizar, perguntas e propostas editadas ao reabrir, e o editor de fórmulas. Capturas Skia em 412×900 e desktop amplo ficam nos artifacts do CI.

Android físico continua necessário: IME, alças de seleção, menus do sistema, pinch, fontes ampliadas, retomada e atualização por cima. Não declarar desempenho de aparelho medido com base em captura headless. A pesquisa usa o serviço existente; orientação de consultas mais específicas não garante relevância nem veracidade de resultados ou imagens. O painel identifica trechos consultados, sem afirmar leitura integral. Não há recibo financeiro de imagens/cache quando o provedor não o fornece. O ZIP usa limite operacional de memória e orienta exportação individual em conjuntos grandes, sem truncar silenciosamente arquivos.
