# Chat do Assistente — estudo comparativo e incrementos (15/11/2026)

Pedido do proprietário: estudar os apps de IA (forma da conversa, formatação, etapas e ferramentas), ouvir as comunidades e trazer o melhor para o Urbe. Ele valoriza as **etapas** do Claude (o que o agente faz, à vista) e acha a **formatação de texto** do ChatGPT superior.

## O que as comunidades dizem (evidência anedótica, não pesquisa sistemática)

- Claude é elogiado por respostas em prosa e legibilidade (frases curtas); ChatGPT por estrutura em listas, mas criticado por "listomania". Alguns usuários do r/ClaudeAI acham a formatação do ChatGPT melhor. Fontes: G2, CNET/AOL, r/ClaudeAI.
- Pensamento e ferramentas: usuários reagem mal quando as etapas somem ("não dá para verificar se o raciocínio aconteceu"); pedem bloco recolhido por padrão, **com seta clicável** e opção de deixar expandido. Fontes: issues do Claude Code, The Register, fórum do Cursor.
- Padrões gerais: streaming, botão de parar, renderização tolerante a Markdown incompleto, tabelas/código/fórmulas limpos, transparência de estado e de incerteza. Fontes: guias de UI de chat 2026 (UXPin, Setproduct, Parallel).

## Auditoria do chat do Urbe (capturas a 412 e 1280 px, `ChatLook_Capture`)

- Bom: títulos, listas, tabela, citação, código com Copiar/Quebrar linhas, fórmula renderizada, links de nota, Fontes [S1].
- Problema: as etapas apareciam só como "1 operação · 1 leituras · Detalhes" (contagem técnica, plural errado).
- A fórmula inline é desenhada maior que o texto (escala da matemática; mesma das notas, área do CST-142 — não alterada sem aparelho).

## Entregue

- Cabeçalho da atividade dobrado mostra as **últimas três etapas em palavras** ("✓ Lendo Fotossíntese", ⋯ em andamento, ✗ com erro) e um **resumo humano** ("Leu 1 fonte · 1 etapa · tocar para ver tudo"); tudo continua a um toque.

## Próximos incrementos propostos

1. ~~Rótulo no passado quando a etapa termina~~ — entregue ("✓ Leu Fotossíntese", `StepWords`).
2. Tipografia de leitura: largura de coluna ~70 caracteres no computador, entrelinha e espaçamento entre parágrafos e títulos no padrão do ChatGPT.
3. ~~Opção "manter etapas abertas"~~ — entregue (Custos e consumo → Mostrar as etapas abertas).
4. Citação [S1] com prévia ao tocar (estilo Perplexity).
5. Escala da fórmula inline igual à altura do texto — com validação no aparelho (CST-142).
