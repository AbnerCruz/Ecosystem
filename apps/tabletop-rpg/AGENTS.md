# AGENTS.md — Tabletop RPG

Complementa o `/AGENTS.md`; nunca o substitui.

## Fontes obrigatórias

Antes de alterar este Product, ler:
1. `/MANIFEST.md`, inclusive toda a seção 0.1;
2. `/AGENTS.md`;
3. `/ecosystem.json`;
4. `SPEC.md`;
5. `ROADMAP.md`;
6. ADRs e decisões relacionados.

## Invariantes locais

- **RPG-I01 — Estado é do motor.** Texto de modelo nunca altera diretamente campanha, ficha, inventário, dados, combate ou resultado. Agentes produzem intents; o runtime valida e executa.
- **RPG-I02 — Sem metagame estrutural.** Um Player Agent recebe apenas a visão que sua personagem pode conhecer. Estado verdadeiro do mundo não entra automaticamente no contexto do jogador.
- **RPG-I03 — Verdade, conhecimento e memória são separados.** Um fato verdadeiro, uma crença e uma lembrança não são o mesmo registro.
- **RPG-I04 — Humanos e agentes usam as mesmas operações de jogo.** Não manter dois motores de regras ou ações divergentes.
- **RPG-I05 — Ociosidade custa zero.** Agentes são acordados por eventos/turnos; nenhum polling de LLM é permitido.
- **RPG-I06 — Regras são substituíveis.** O Product não é D&D. Implementações de referência não podem virar dependência conceitual do domínio.
- **RPG-I07 — Solo-first, multiplayer-ready.** O MVP não cria rede, conta ou servidor; decisões internas não podem assumir que sempre existe exatamente um processo no produto final.
- **RPG-I08 — Mobile-first sem contaminar o Core.** UI, lifecycle Android e input não entram em `TabletopRpg.Core`.
- **RPG-I09 — Product independente.** Nenhuma dependência direta de outro Product. Integração futura somente por capability/contract aprovado.
- **RPG-I10 — Dados da campanha pertencem ao usuário.** Persistência futura deve ser local-first, exportável e sem conta obrigatória.

## Primeiro slice

`TabletopRpg.Core` deve permanecer sem PackageReference e sem referência a outro projeto. O objetivo é provar o modelo de domínio antes de escolher UI mobile, persistência ou provedor de IA.

Mudança que introduza provedor de modelo, formato persistente de campanha, UI/runtime mobile, multiplayer, capability pública ou dependência de outro componente exige revisar SPEC/ROADMAP e o processo de decisão aplicável.
