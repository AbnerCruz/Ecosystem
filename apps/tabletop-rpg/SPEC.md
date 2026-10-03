# SPEC — Tabletop RPG

> Autoridade de produto do componente `tabletop-rpg`, subordinada ao MANIFEST, aos ADRs e às decisões registradas do proprietário.

## 1. Visão

Tabletop RPG é uma plataforma completa para RPG de mesa em que humanos e agentes de IA podem ocupar assentos de jogador e, futuramente, de mestre. A experiência solo deve ser um modo principal e completo, não um tutorial nem uma simulação reduzida.

## 2. Requisitos

### Fundação

- **RPG-REQ-001 — C#.** Todo código novo do Product é C#, salvo exceção estrutural decidida por ADR.
- **RPG-REQ-002 — Mobile-first.** A primeira experiência de usuário é projetada para celular. O Core permanece independente de UI.
- **RPG-REQ-003 — Independência.** O Product funciona sem depender de qualquer outro Product do Ecosystem.
- **RPG-REQ-004 — Local-first.** Campanhas e dados primários pertencem ao usuário; conta e backend não são requisito do domínio solo.

### Participantes e agentes

- **RPG-REQ-010 — Participantes de primeira classe.** Humano e agente são controladores diferentes sobre as mesmas operações de jogo.
- **RPG-REQ-011 — Mestre e jogadores.** A arquitetura admite Mestre humano ou IA e jogadores humanos ou IA. O MVP prioriza um humano com mesa de agentes.
- **RPG-REQ-012 — Intent antes de mutação.** Modelo/agent source só propõe ação estruturada; o motor valida permissões, regras e consequências.
- **RPG-REQ-013 — Execução dirigida por evento.** Agentes só são acionados por gatilho explícito relevante: turno, fala dirigida, mudança de cena, consequência ou outro evento modelado.
- **RPG-REQ-014 — Contexto mínimo pertinente.** Player Agent não recebe estado secreto do mundo por conveniência.

### Estado cognitivo

- **RPG-REQ-020 — World State.** Verdades canônicas do mundo pertencem ao estado da campanha.
- **RPG-REQ-021 — Knowledge.** Cada personagem possui conhecimento próprio com origem e confiança.
- **RPG-REQ-022 — Memory.** Memórias subjetivas são registros distintos de fatos e crenças e podem divergir deles.
- **RPG-REQ-023 — Sem propagação implícita.** Criar ou alterar um fato do mundo não ensina automaticamente esse fato a nenhuma personagem.

### Regras e jogo

- **RPG-REQ-030 — Rules Runtime substituível.** O domínio suporta sistemas diferentes e homebrew; nenhum sistema comercial é requisito estrutural.
- **RPG-REQ-031 — Rolagem real.** Dados e modificadores são executados pelo runtime, não inventados em texto.
- **RPG-REQ-032 — Ação autorizada.** Um participante só age por personagens/entidades que controla ou por autoridade de Mestre explicitamente modelada.
- **RPG-REQ-033 — Histórico observável.** Resoluções relevantes produzem eventos ordenados e auditáveis.

### Campanha

- **RPG-REQ-040 — Persistência.** O MVP deve salvar e retomar campanha localmente quando a fase de persistência começar.
- **RPG-REQ-041 — Sessões.** Campanha e sessão são conceitos distintos; campanha sobrevive a sessões.
- **RPG-REQ-042 — Conteúdo incremental.** Mundo pode crescer durante o jogo sem exigir geração completa antecipada.

### Produto final

- **RPG-REQ-050 — Multiplayer.** O produto final suporta participantes remotos reais, sincronização de sessão e permissões; não faz parte do MVP inicial.
- **RPG-REQ-051 — Ferramentas completas de mesa.** Fichas, dados, iniciativa, combate, inventário, diário, handouts, mapas/battlemaps, grid, tokens, fog of war, áudio e demais ferramentas entram progressivamente no roadmap.
- **RPG-REQ-052 — Sistemas extensíveis.** Sistemas de regras e conteúdo homebrew devem poder ser adicionados sem reescrever o runtime central.

## 3. MVP solo

O MVP é considerado funcional quando um usuário consegue criar/abrir campanha, controlar sua personagem, jogar uma sessão persistente com agentes como companheiros e Mestre, executar ações por regras reais e retomar depois sem perda de estado.

Multiplayer, voz, marketplace, conta e servidor não são critérios do MVP.

## 4. Não objetivos do primeiro slice

O primeiro slice `RPG-001` não escolhe UI mobile, persistência, provedor LLM, formato público de campanha, networking ou sistema comercial de regras. Ele prova as invariantes do domínio de forma headless.
