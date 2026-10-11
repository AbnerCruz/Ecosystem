# ROADMAP — Tabletop RPG

> Autoridade de fases do Product. Estado vivo de tarefas fica nas Issues; evidência em handoffs.

Convenção: `RPG-<n>` é ID estável de tarefa.

## R0 — Fundação de domínio

Objetivo: provar o modelo de mesa sem UI e sem provedor real.

- [x] **RPG-001 — Fundação do Product e slice headless.** Registrar Product, SPEC/roadmap, participantes, perspectivas, intents, regras e Agent Turn Runner dirigido por evento, com testes.
- [x] RPG-002 — Persistência local v1: campanha/sessão, versionamento, save atômico, export/import e recuperação. Integrado no PR #168 (`46c295a`), com 22 testes e consistency/simulação verdes; Issue #159.
- [x] RPG-003 — Modelo de cena, relógio de campanha, quests, inventário, condições e recursos. PR #310 integrado em 08/10/2026 (Issue #308); schema v2 mantém importação v1.
- [x] RPG-004 — Runtime de combate independente de sistema específico. PR #315 integrado em 10/10/2026 (Issue #314); cursor de turno permanece transitório até RPG-032.

**Gate G-R0:** Core salva/retoma uma campanha e executa um fluxo solo determinístico sem UI nem rede.

## R1 — Mesa solo com IA

- [~] RPG-010 — Contrato local de Model Gateway do Product, configuração de provedor e segredo fora dos dados da campanha. Issue #504: contrato C# isolado e fonte de credencial por variável de ambiente em implementação; integração e CI pendentes.
- [~] RPG-011 — Player Agent: persona, goals, relationships, knowledge, memory e intents estruturadas. Issue #510; adapter C# com perfil, projeção de perspectiva e parser estrito, dependente de RPG-010/#506. CI, integração e definição durável do perfil ainda pendentes.
- [~] RPG-012 — Game Master Agent com visão própria e ferramentas limitadas. Issue #516; visão privada GM, propostas tipadas e aprovação explícita sobre Campaign em revisão, empilhado sobre RPG-011 PR #511; CI e integração pendentes.
- [~] RPG-013 — Director: tensão, ameaças, pistas, promessas e consequências sem roteiro rígido. Issue #518; motor C# headless, pistas/ameaças/promessas, tensão e cues privadas dirigidas por eventos, pendente CI e integração após PR #517.
- [~] RPG-014 — NPC runtime; NPC não é automaticamente Player Agent. Issue #520: NPCs C# headless vinculados a Character/Scene e GM explícito, estado privado, interações determinísticas e diálogos autorizados via SessionEngine; testes, CI e integração pendentes após RPG-013 PR #519.
- [~] RPG-015 — Orquestração dirigida por eventos, budgets, retry/progress guards e observabilidade. Issue #522: orquestrador C# local, deduplicação, anti-loop/retry seguro e recibos opcionais por modelo/provedor. Decisão do proprietário: sem teto financeiro obrigatório, cobrança pay-as-you-go e otimização de chamadas; sem integração direta com outro Product, CI/integração pendentes.
- [~] RPG-016 — Criação rápida de campanha sem preparação. Issue #525: QuickStart C# headless com três temas, defaults, sessão/cena/quest imediatas, humano+companheiro IA+Mestre IA+NPC, mundo secreto isolado e codec v2 existente; CI e integração pendentes após PR #523.

**Gate G-R1:** um humano joga uma sessão solo completa com Mestre IA e companheiros IA, sem vazamento sistemático de informação e sem chamadas ociosas.

## R2 — Produto mobile

- [~] RPG-020 — Decidir e registrar shell mobile C# e targets iniciais. Issue #527: shell Android nativo .NET 10/C# (arm64/API 29+), projeto compilável e workflow de APK prerelease; CI/DEVICE/integração pendentes.
- [~] RPG-021 — Home/campanhas/criação. Issue #527: preview nativo com lista local e criação via RPG-016, tela de sessão com fala/d20/salvar/retomar; faltam UX completa, DEVICE e integração.
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
