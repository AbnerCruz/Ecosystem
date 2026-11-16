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

## Pop-up contextual (rejeitado pelo proprietário em 15/11/2026) — diagnóstico e redesenho

Captura real antes (`PopupLook_Capture`, 360 px), problemas observados e causas no código:

| Problema | Causa |
|---|---|
| Cobria o título e o texto da nota | Ancorado no topo (`Margin` 64 em cima) |
| Campo de pergunta e Enviar cortados | `ScrollViewer` externo com `MaxHeight` 300 envolvendo um corpo de altura fixa 380 |
| Rolagem dentro de rolagem; título da resposta sumia | O mesmo aninhamento |
| Cartão dentro de cartão | Resposta do agente num balão com fundo dentro do cartão |
| Sem etapas, sem fechar/expandir visíveis | Ações numa linha de links no rodapé, cortada |
| O botão do menu radial cobria o envio | Os dois no mesmo canto |

Desenhos avaliados:
- **A — painel ancorado acima da área de digitação** (escolhido): o pedido do proprietário; o padrão de assistentes contextuais no celular (Notion AI, Apple Writing Tools) — o polegar alcança tudo e a nota fica à vista.
- **B — cartão no topo corrigido**: continua cobrindo o que se edita e longe do polegar.

Entregue (A): metade da altura da tela (260–520 px), uma única rolagem (a conversa), cabeçalho com estado, agente, **Abrir conversa** e **Fechar** sempre visíveis, etapas em palavras, resposta sem balão (só a mensagem da pessoa tem balão), envio redondo igual ao do chat completo, menu radial escondido enquanto o painel está aberto; até 560 px de largura no computador. Capturas depois: 360 e 900 px.

Pendente: validação no aparelho (teclado aberto, rotação, TalkBack) e aprovação do proprietário.
