# ROADMAP — Tabletop RPG

> Autoridade de fases do Product. Estado vivo de tarefas fica nas Issues; evidência em handoffs.

Convenção: `RPG-<n>` é ID estável de tarefa.

## R0 — Fundação de domínio

Objetivo: provar o modelo de mesa sem UI e sem provedor real.

- [x] **RPG-001 — Fundação do Product e slice headless.** Registrar Product, SPEC/roadmap, participantes, perspectivas, intents, regras e Agent Turn Runner dirigido por evento, com testes.
- [~] RPG-002 — Persistência local v1: campanha/sessão, versionamento, save atômico, export/import e recuperação.
- [ ] RPG-003 — Modelo de cena, relógio de campanha, quests, inventário, condições e recursos.
- [ ] RPG-004 — Runtime de combate independente de sistema específico.

**Gate G-R0:** Core salva/retoma uma campanha e executa um fluxo solo determinístico sem UI nem rede.

## R1 — Mesa solo com IA

- [ ] RPG-010 — Contrato local de Model Gateway do Product, configuração de provedor e segredo fora dos dados da campanha.
- [ ] RPG-011 — Player Agent: persona, goals, relationships, knowledge, memory e intents estruturadas.
- [ ] RPG-012 — Game Master Agent com visão própria e ferramentas limitadas.
- [ ] RPG-013 — Director: tensão, ameaças, pistas, promessas e consequências sem roteiro rígido.
- [ ] RPG-014 — NPC runtime; NPC não é automaticamente Player Agent.
- [ ] RPG-015 — Orquestração dirigida por eventos, budgets, retry/progress guards e observabilidade.
- [ ] RPG-016 — Criação rápida de campanha sem preparação.

**Gate G-R1:** um humano joga uma sessão solo completa com Mestre IA e companheiros IA, sem vazamento sistemático de informação e sem chamadas ociosas.

## R2 — Produto mobile

- [ ] RPG-020 — Decidir e registrar shell mobile C# e targets iniciais.
- [ ] RPG-021 — Home/campanhas/criação.
- [ ] RPG-022 — Tela de sessão context-driven.
- [ ] RPG-023 — Ficha, grupo, inventário, diário e regras.
- [ ] RPG-024 — Dados e histórico visual de rolagens.
- [ ] RPG-025 — Android lifecycle, autosave, toque, acessibilidade e validação em aparelho.

**Gate G-R2:** MVP solo utilizável no celular do começo ao fim.

## R3 — VTT e ferramentas de mesa

- [ ] RPG-030 — Mapas, tokens e câmera mobile.
- [ ] RPG-031 — Grid quadrado/hex/livre, distância e áreas.
- [ ] RPG-032 — Iniciativa, combate tático e condições.
- [ ] RPG-033 — Fog of war e informação por participante.
- [ ] RPG-034 — Handouts, biblioteca de mídia e áudio/ambiente.
- [ ] RPG-035 — Macros/ações e editor visual de sistema.

## R4 — Sistemas, conteúdo e Ecosystem

- [ ] RPG-040 — Rules packages e homebrew.
- [ ] RPG-041 — Compêndios e import/export com proveniência.
- [ ] RPG-042 — Avaliar integração por capabilities com workspaces de conhecimento do Ecosystem.
- [ ] RPG-043 — Avaliar consumo de runtime de agentes compartilhado apenas quando houver contrato/extração aprovada.
- [ ] RPG-044 — Avaliar Tools do Ecosystem sem dependência direta entre Products.

## R5 — Multiplayer

- [ ] RPG-050 — Modelo de autoridade e sincronização.
- [ ] RPG-051 — Sala, presença, reconexão e permissões.
- [ ] RPG-052 — Jogadores humanos e agentes na mesma sessão remota.
- [ ] RPG-053 — Chat/voz e canais privados.
- [ ] RPG-054 — Segurança, moderação, observabilidade e testes de rede.

**Gate final de multiplayer:** adicionar rede não altera o significado das operações de jogo do Core; transporte sincroniza comandos/eventos em vez de criar um segundo motor.
