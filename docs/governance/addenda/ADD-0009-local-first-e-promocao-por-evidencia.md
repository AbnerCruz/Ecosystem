# ADD-0009 — Local-first e promoção por evidência; abertura da Fase 2

> **Tipo:** decisão explícita do proprietário, registrada (MANIFEST §24, nível 1).
> **Autor:** AbnerCruz (proprietário). **Recebido em:** 2026-10-01, em conversa com o agente `claude-code`, e persistido aqui conforme NN-009.
> **Contexto da mensagem:** delegação posterior à resposta de DEC-0019 (A). O texto integral é longo; abaixo estão **transcritas as passagens que decidem** (princípio, estados, exemplo, limites) e o que o agente interpretou delas. Registros: [ADR-0011](../../adr/0011-local-first-e-promocao-por-evidencia.md), guia [`local-first.md`](../../architecture/local-first.md), ROADMAP (Fase 2).

## Passagens decisórias (transcrição)

> «Uma funcionalidade nova nasce, por padrão, no Product que possui a necessidade. Ela não deve ser transformada preventivamente em infraestrutura compartilhada apenas porque talvez seja reutilizável no futuro.»

> «Extração não é correção de uma implementação que estava “no lugar errado”. É uma promoção baseada em evidência de reutilização.»

> Nome conceitual sugerido: Local-first architecture + Promotion by evidence. «O nome definitivo pode ser refinado para combinar com a terminologia já existente.»

> Estados de potencial de reutilização: **PRODUCT-SPECIFIC** (responsabilidade do Product, nenhum consumidor externo conhecido, nenhuma extração); **POSSIBLE-CANDIDATE** (pode haver utilidade fora, sem evidência suficiente, continua local, fica registrada para revisão futura); **EXTERNAL-CONSUMER-EXISTS** (existe pelo menos outro consumidor concreto; uma Extraction Review deve ser considerada; **não** significa extração automática). «Não precisa usar exatamente esses identificadores se houver terminologia melhor no repositório. Preserve a semântica.»

> «O proprietário deve poder simplesmente dizer: “Quero X no Urbe.” ... Ele NÃO deve precisar decidir antecipadamente: isso é Tool? Service? Library? Workspace? Capability? Product Core? Product Shell? A responsabilidade de analisar; implementar localmente; registrar potencial; detectar repetição; sugerir promoção; é da arquitetura e dos agentes. Só decisões realmente estruturais ou com trade-offs relevantes devem voltar ao proprietário.»

> «Uma feature local NÃO deve ser extraída automaticamente quando ganhar segundo consumidor. O segundo consumidor dispara uma análise.» Fluxo: feature local → possível candidato → segundo consumidor real → EXTRACTION REVIEW → NN-022 → ADR do Ecosystem → contrato explícito → extração, se aprovada. «O saldo precisa ser positivo. Sem isso: continua local, mesmo que exista alguma duplicação temporária.»

> «duplicação temporária pode ser menos prejudicial que abstração compartilhada errada quando o domínio ainda não está compreendido.» (especialmente Editor, Sprite Studio, Agent Workspace, Git, Docs, sistemas de UI, autenticação, Store, Library, serviços futuros.)

> Exemplo do login: o Urbe precisa de login para uma funcionalidade exclusivamente sua → `apps/urbe/` com feature local; **não** criar `platform/identity/`. Depois, o Lunet2D também precisa da mesma identidade e há intenção concreta de conta comum (Urbe, Lunet2D, Store, Library, Community) → agora há evidência para considerar um Identity Service: Extraction Review → NN-022 → ADR → Contract → Provider/Consumers. «Arquitetura compartilhada nasce da necessidade comprovada, não de adivinhação.»

> «Não crie um terceiro sistema paralelo» ao ADR-0009 (R — refatoração interna; X — extração); «não altere MANIFEST.md nem crie nova NN-XXX automaticamente»; «prefira complementar NN-020 e NN-022».

> Limites desta delegação: não refatorar Lunet2D/Urbe; não extrair Editor, Sprite Studio, Git, Agent Workspace nem nenhum outro subsistema; não criar Services imaginários (Identity, Commerce, Catalog, Entitlements, Downloads, Updates, Reviews, Creator Profiles, Notifications, Store/Community backend); não criar `platform/`, `tools/`, `workspaces/`, `services/` apenas para preparar o futuro.

> Fase 2: abrir oficialmente; tarefas `P2-x` reais com objetivo, saída verificável, dependências, evidência e gate; vertical slice mínimo (A provê `capability.test` v1; B exige compatível; C exige v2 incompatível → falha) com fixtures, sem extrair uma Tool real; reavaliar DEC-0008/DEC-0009 sem quebrar releases, Urbe Web, apps instalados nem updates; Portal continua projeção.

## Interpretação registrada pelo agente (para conferência do proprietário)

| Trecho | Interpretação adotada | Natureza |
|--------|----------------------|----------|
| Princípio e complemento | **Decisão do proprietário**, formalizada como ADR-0011 `Aceito` (complementa ADR-0009, NN-020 e NN-022; nenhuma NN nova). Nome adotado: *Local-first e promoção por evidência*. | Decisão do proprietário. |
| Três estados | Adotados com identificadores em inglês `product-specific`, `possible-candidate`, `external-consumer-exists` (mesma semântica). | Decisão (semântica) + proposta (identificadores). |
| Como registrar o potencial | O **mecanismo** foi delegado ao agente ("menor complexidade"): campo **opcional** `reuse_assessment` nos handoffs, validado quando presente; os registros ficam nos próprios handoffs (nenhuma nova fonte de verdade) e a projeção do portal os lista. | Proposta do agente, dentro da delegação. |
| Contratos da Fase 2 (ComponentManifest, Capability, Context, permissões, Registry, Distribution Profile) | O ROADMAP/MANIFEST já exigem esses contratos; o agente os define em **ADR-0012 `Proposto`** e pede ratificação em DEC-0020 (não bloqueante). O vertical slice usa fixtures e roda enquanto a decisão está pendente. | Proposta do agente (NN-011). |
| Nomes dos eixos do Distribution Profile | Provisórios no ADR-0012 (ARCHITECTURE §8.1 já os deixava para ADR); ratificação em DEC-0020. | Proposta. |
