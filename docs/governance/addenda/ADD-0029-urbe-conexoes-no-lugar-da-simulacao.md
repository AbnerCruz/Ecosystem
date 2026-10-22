# ADD-0029 — Ordem do proprietário: conexões calmas no lugar da simulação

**Data:** 2026-10-22. **Autoridade:** decisão explícita do proprietário nesta conversa, aceitando a proposta do agente. **Registro:** DEC-0052. **Escopo:** o cliente C# nativo do Urbe (`apps/urbe/csharp`). **Complementa** ADD-0028 (DEC-0051): é o substituto que o proprietário ia pensar para a simulação congelada. **Mantém** ADD-0024 (desempenho primeiro), ADD-0026 (experiência de usuário topo de linha) e ADD-0028 (sem a simulação; Explorer como base).

## O que o proprietário disse

> *"Beleza, agora em relação a como vamos substituir aquela simulação. O obsidian tem aqueles gráficos com bolinhas conectadas e tals, aquilo é a identidade do obsidian. Pensando na nossa ideia de ser algo mais tranquilo e relaxante, o que você acha?"*

O agente propôs uma visão de conexões calma, sem copiar o grafo do Obsidian, começando pelo painel de conexões da nota aberta. O proprietário respondeu:

> *"Beleza. Pode ser"*

## Decisões do proprietário

1. **O substituto da simulação é uma visão de conexões calma, não uma cópia do grafo do Obsidian.** A identidade do Urbe é ser tranquilo e relaxante.
2. **Princípios da visão:**
   - **Posições estáveis.** Cada nota fica no mesmo lugar, calculado uma vez, sem layout de forças se mexendo. Você aprende onde as coisas moram, que era o melhor da cidade.
   - **Céu à noite.** Pontos de luz suaves, tamanho pelas ligações, cor pela pasta. As notas recentes brilham um pouco mais.
   - **Quase sem movimento.** Nada de animação contínua. O desenho muda quando a nota ou o tamanho muda.
   - **Foco ao tocar.** Tocar numa nota mostra só ela e as suas ligações, com o resto escurecido.
   - **Leve.** Nada que pese como a simulação pesava.
3. **Ordem:**
   1. **UC-71 — Conexões da nota aberta** (grafo local), dentro do editor. É o passo menor e útil todo dia.
   2. **UC-72 — Constelação do vault** (visão global), como uma tela para passear.
4. **O Explorer continua sendo a base do app** (ADD-0028). A visão de conexões é uma tela ou um painel, nunca a base.
5. **Nada da simulação volta.** Sem moradores, clima, eventos, casas ou ruas.
6. **Dados.** Se a constelação guardar posições, elas ficam num arquivo novo e derivado das notas, que declara a sua fonte (NN-001). Nunca no `.urbe/mapa.json` nem no `.urbe/mundo.json`, que seguem preservados e sem reescrita.
7. **Cada passo é validado no aparelho do proprietário antes de crescer** (NN-017).

## Limites que continuam valendo

- Nativo sem web (ADD-0020), desempenho primeiro (ADD-0024), experiência de usuário topo de linha (ADD-0026, `apps/urbe/docs/csharp/UX.md`).
- Dados primeiro: olhar as conexões nunca grava nada no vault.
