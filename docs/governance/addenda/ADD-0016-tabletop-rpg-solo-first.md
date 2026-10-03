# ADD-0016 — Tabletop RPG: Product C# solo-first com agentes de IA

Decisão explícita do proprietário em 2026-10-03, nesta sessão com ChatGPT.

O proprietário determinou a criação de um aplicativo no Ecosystem com todos os
recursos e ferramentas necessários para RPG de mesa, tratando agentes de IA como
jogadores ou Mestre e jogadores reais como participantes equivalentes. Em seguida
fixou as restrições de implementação: **todo o código novo é C#**, o Ecosystem
funcionará em C#, a primeira experiência é **mobile-first**, e o **MVP é totalmente
solo, sem multiplayer**. Multiplayer continua sendo requisito do produto final.

O proprietário autorizou o próprio agente desta sessão a implementar o Product
depois de verificar as diretrizes do repositório.

Para tornar a direção executável sem congelar um nome comercial, o componente usa
o ID técnico estável `tabletop-rpg` (NN-019) e o nome de exibição provisório
**Tabletop RPG**. Alterar o nome de exibição não muda a identidade do componente.

Direção de produto persistida:

- o Product é independente do Hub, Lunet2D, Urbe e Ecosystem AI;
- o MVP precisa permitir que um único humano jogue uma campanha completa com
  participantes artificiais;
- humanos e agentes usam as mesmas operações fundamentais de jogo;
- modelos não são autoridade de estado nem de regras: produzem intents, e o motor
  C# valida e executa;
- estado verdadeiro do mundo, conhecimento de cada personagem e memória subjetiva
  são domínios distintos;
- agentes são acionados por eventos relevantes; ociosidade não gera polling de LLM;
- sistemas de regras são substituíveis e o Product não nasce preso a D&D ou outro
  sistema comercial;
- dados primários da campanha seguem a direção local-first do MANIFEST;
- multiplayer, rede, voz, contas e backend ficam fora do MVP inicial, mas o Core
  não deve criar um segundo modelo de operações quando a rede chegar.

A especificação canônica desta decisão de produto é
`apps/tabletop-rpg/SPEC.md`; fases e gates pertencem ao
`apps/tabletop-rpg/ROADMAP.md`. A fundação arquitetural é formalizada no
ADR-0022 e registrada como DEC-0033.

Rastreabilidade inicial: RPG-001, Issue #156, branch
`chatgpt/tabletop-rpg-foundation`.
