# ADD-0026 — Ordem do proprietário: experiência de usuário topo de linha no Urbe

**Data:** 2026-10-16. **Autoridade:** instrução explícita do proprietário nesta conversa. **Registro:** DEC-0049. **Escopo:** toda a interface do cliente C# nativo do Urbe (`apps/urbe/csharp/src/Urbe.Client`, `Urbe.Android`, `Urbe.Desktop`). **Complementa** ADD-0024 (desempenho primeiro, DEC-0047), ADD-0022 (essência da 1.8.4, DEC-0045) e ADD-0020 (nativo sem web, DEC-0043).

## O que o proprietário disse

Depois de usar a beta 2.0.0-beta.13, o proprietário mandou observações sobre:

- o editor ("praticamente não dá pra usar, não é eficiente e não é acessível");
- a abertura do app;
- o desempenho ("tem que ser ultra mega blaster performático");
- o fluxo ("esse fluxo é sagrado, acessibilidade, agilidade, atalhos, personalização extrema");
- as páginas, o tutorial, as automações e o widget.

Quando o agente registrou essas observações como itens de funcionalidade, o proprietário respondeu:

> *"Acho que você não entendeu o ponto central das observações: EXPERIÊNCIA DE USUÁRIO. Topo de linha, mais moderno dos modernos, tecnologia e psicologia de ponta."*

## Decisões do proprietário

1. **A experiência de usuário é o critério central do Urbe nativo.** Ela fica ao lado do desempenho (ADD-0024) e do fluxo de produção, e une os dois. Uma funcionalidade que existe mas não é imediata, agradável e óbvia não está pronta.
2. **O nível exigido é topo de linha:** a técnica de interface mais moderna e a psicologia de interação de ponta.

## Como a ordem é cumprida (aplicação, sem direção nova)

- **Critério escrito:** [`apps/urbe/docs/csharp/UX.md`](../../../apps/urbe/docs/csharp/UX.md) registra 13 princípios com base e forma de medir. Também registra a fundação técnica (tokens de design, componentes únicos, transições e orçamento de desempenho na suíte) e a ordem de redesenho tela por tela, começando pelo editor.
- **Roadmap:** o item **UC-69** agrupa o trabalho de experiência. Os itens UC-57 a UC-68 passam a ser entregues sob esse critério. A prioridade de entrega passa a ser:
  1. fundação técnica;
  2. editor;
  3. abertura e primeira vez;
  4. Explorer;
  5. cidade e HUD;
  6. estúdio de páginas;
  7. as demais telas.
- **Limites que continuam valendo:**
  - a essência e os oráculos da 1.8.4 (ADD-0022): a cidade, os sprites e as regras não são redesenhados sem ordem do proprietário; o que muda é a interface ao redor;
  - nativo sem web (ADD-0020);
  - dados primeiro: a gravação continua verificada e nada se perde;
  - CI verde não equivale a validação (NN-017). Cada tela só fecha com validação do proprietário no aparelho.
