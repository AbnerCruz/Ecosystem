# ADD-0037 — Linguagem visual do Ecosystem

**Data:** 26/11/2026. **Autoridade:** ordem explícita do proprietário nesta conversa. **Registro:** DEC-0069. **Escopo:** a interface de todos os Products e superfícies do Ecosystem. A primeira aplicação é no Lunet. **Complementa** ADD-0026 (experiência de usuário topo de linha no Urbe) e ADD-0036 (casca Avalonia do Lunet, família visual com o Numa).

## Ordem recebida

> "Cara, todo painel e input que tu cria é feio […]. Faz o seguinte, estuda ux design, design, arte, cores, minimalismo, procure referências contemporâneas, procure discussões na web, em comunidades, artigos e wikis. Faz um baita estudo sobre visual, layout, fontes, inputs, painéis, estrutura, teoria das cores, bordas, estilização, animações, efeitos, tudo que for sobre estilização. Estude e estabeleça os padrões que vamos utilizar no ecosystem então registra direto no ecosystem. Eu particularmente gosto das cores preto, azul, branco e roxo mas sem nos limitar a isso. Dps de definir eu quero que implemente no Lunet, ta muito feio mano."

## O que isso significa

1. **Existe uma linguagem visual única do Ecosystem:** [`docs/design/linguagem-visual.md`](../../design/linguagem-visual.md) (v1.0), com os valores em [`docs/design/tokens.json`](../../design/tokens.json) como autoridade única (NN-001). O estudo que a fundamenta, com fontes, está em [`docs/design/estudo-visual-2026-11-26.md`](../../design/estudo-visual-2026-11-26.md).
2. **A base é escura, com preto frio, branco suave, violeta e azul.** O violeta é marca e ação; o azul é informação e seleção. Cada Product tem um acento líder: Lunet usa violeta, Hub usa azul e o Urbe define o seu no próprio roadmap.
3. **Os Products copiam os valores e declaram a versão.** Não há dependência entre Products (NN-002). Um pacote de UI compartilhado continua sujeito à Extraction Review (NN-022).
4. **O Lunet adota agora.** Tokens, estilos globais de todos os controles, fonte Inter embutida e componentes da casca Avalonia seguem a v1.0.
5. **Urbe, Hub e portal adotam ao tocar a fundação visual** de cada um. Tela nova já segue a linguagem.
6. **Mudança de valores ou regras** exige registro (decisão do proprietário ou ADR do Ecosystem) e nova versão.

## O que não muda

- As decisões de produto (o que cada tela faz) e os dados do usuário.
- A automação plena (ADD-0035): sem validação no aparelho; capturas headless não são teste no aparelho e não são apresentadas como tal.
