# Urbe em C# — programa de migração

> **Autoridade:** decisão do proprietário DEC-0024-B ([`docs/governance/responses/DEC-0024.md`](../../../../docs/governance/responses/DEC-0024.md)) → Ecosystem ADR-0016 → Urbe ADR-0010 ([`../v2/adr/0010-migracao-para-csharp.md`](../v2/adr/0010-migracao-para-csharp.md)) → este diretório. O roadmap é [`ROADMAP.md`](ROADMAP.md) (itens `UC-n`). **Estado do programa:** ver o ROADMAP (linha `*Estado do gate:*` de cada fase); não é copiado aqui.

## O que foi decidido

Reescrita completa do Urbe em C#: um **novo cliente**, trocando o produto distribuído **só quando houver paridade total** com o atual. Sem big-bang: o Urbe em JavaScript continua sendo o produto (Web/PWA, Electron, Capacitor) até o corte.

## O que ainda NÃO foi decidido (e não pode ser presumido)

- **Pilha de UI e de hosts** por superfície. «Blazor WebAssembly + hosts Windows/Android» era só o exemplo da alternativa. Decide-se em UC-5 (ADR do Ecosystem `Proposto` + decisão no portal).
- **Estratégia de transição** das instalações e dos canais de distribuição (APK, instalador Windows com atualização, Urbe Web/PWA): UC-6.
- **Modelo de plugins** em C# (hoje JS full-trust, Urbe ADR-0002): UC-20.
- **O corte** (trocar o produto distribuído): decisão crítica do proprietário (UC-31).
- ~~O programa de refatoração do Urbe 2.0 em JavaScript durante a migração~~ — **decidido em DEC-0025-C**: o JavaScript fica **congelado, salvo bug crítico**; os itens do 2.0 ficam adiados, nunca removidos (nota no topo de [`../v2/ROADMAP.md`](../v2/ROADMAP.md)).

## Regras do programa

1. **Dados do usuário não mudam de formato.** O vault é o mesmo (Urbe ADR-0004; REQ-007, REQ-035, REQ-038). A paridade é provada contra o código atual, não contra a opinião de ninguém.
2. **A prova de paridade é independente de linguagem** (UC-2): fixtures de vault, goldens e cenários de aceite que o JS e o C# executam.
3. **Zona crítica antes do código.** Dados, segurança e distribuição do código novo precisam estar na política de integração antes do primeiro PR de código C# (UC-7): esse PR e os de persistência pedem a autorização do proprietário.
4. **Sem dependência de outro produto do ecossistema nem do plano de controle** (NN-002, NN-003, NN-023): o Urbe novo funciona e é distribuído sozinho. Extração para o Ecosystem só por Extraction Review (NN-022).
5. **Validação humana por superfície** (Android real, instalação/atualização no Windows, Web offline/PWA, toque e layout) antes do corte (NN-017). CI verde não basta.
6. **Migração e refatoração são tarefas separadas** (NN-013). O que for achado no JS vira correção própria, não vai junto.
7. **Identidade e versão próprias** (NN-014, NN-019): é o mesmo produto `urbe` em `ecosystem.json`; `language` só muda no corte.

## Relação com a Urbe 2.0

A documentação de [`../v2/`](../v2/) descreve o programa de 2.0 em JavaScript, aprovado antes desta decisão; ela **continua válida para o produto atual** e é a fonte dos requisitos (REQ), dos formatos de dados e das fixtures que alimentam a paridade. O que o programa 2.0 ainda tem em aberto fica sujeito à DEC-0025.
