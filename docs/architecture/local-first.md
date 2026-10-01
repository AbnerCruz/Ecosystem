# Local-first e promoção por evidência (guia)

> **Autoridade:** operacional, subordinada ao [ADR-0011](../adr/0011-local-first-e-promocao-por-evidencia.md) (decisão do proprietário, ADD-0009), a NN-020 e NN-022.

## 1. O fluxo padrão

```text
necessidade → Product que precisa → feature interna → testes → avaliação de potencial de reuso → handoff (reuse_assessment)
```

e **não**: necessidade → Tool global → Contract → Registry → infraestrutura que talvez ninguém use.

"Quero um painel novo no Urbe" ⇒ implementar em `apps/urbe`. "Quero um sistema X no Lunet" ⇒ implementar em `apps/lunet2d`. Quem decide se é Tool, Service, Library, Workspace, Capability, Product Core ou Product Shell é o agente, **depois** da implementação real e do uso — nunca o proprietário, e nunca antes.

## 2. Três estados do potencial de reutilização

| Estado (`reuse_assessment.status`) | Significa | Ação |
|---|---|---|
| `product-specific` | responsabilidade claramente do Product; nenhum consumidor externo conhecido | nenhuma |
| `possible-candidate` | pode haver utilidade fora; ainda sem evidência | **continua local**; fica registrado para revisão futura |
| `external-consumer-exists` | existe pelo menos um **consumidor concreto** além do dono (`consumers`) | abre **Extraction Review** (`extraction_review: pending`); **não** é extração automática |

Registro: campo opcional `reuse_assessment` no handoff do trabalho (schema `handoff.schema.json`; validado por `CHK-HANDOFFS`). Avalie quando o trabalho **cria ou reestrutura** uma funcionalidade relevante; handoffs sem isso não precisam do campo. `subject` é um slug estável da capacidade (`account-login`, `document-search`…): o mesmo slug em componentes diferentes significa a mesma necessidade.

## 3. Segundo consumidor ⇒ Extraction Review (não extração)

```text
feature local → possible-candidate → segundo consumidor real → EXTRACTION REVIEW → NN-022 → ADR do Ecosystem → contrato (NN-006) → provider/consumers → extração, se aprovada
```

A review responde às cinco perguntas de NN-022 e conclui por **ADR-NNNN** (extrair), **declined** (continua local, com justificativa) ou segue `pending`. Sem saldo positivo, **continua local**, mesmo com duplicação temporária. `CHK-HANDOFFS` falha se o mesmo `subject` for avaliado em dois ou mais componentes sem nenhuma avaliação `external-consumer-exists`: a repetição não pode passar em silêncio.

## 4. Duplicação temporária × abstração errada

Enquanto o domínio não foi compreendido (Editor, Sprite Studio, Agent Workspace, Git, Docs, sistemas de UI, autenticação, Store, Library, serviços futuros), **duplicar é menos prejudicial que congelar um contrato compartilhado errado**. Aprenda com duas implementações reais antes de contratar a terceira.

## 5. Exemplo: sistema de login

**Caso A.** O Urbe precisa de login para uma funcionalidade só dele. Resultado: uma feature local em `apps/urbe/` (por exemplo, `account`), `reuse_assessment: { subject: "account-login", status: "possible-candidate" }`. **Não** se cria `platform/identity/`.

**Caso B.** Depois, o Lunet2D também precisa da mesma identidade e há intenção concreta de uma conta comum (Urbe, Lunet2D, Store, Library, Community). Agora há evidência: o handoff do Lunet2D registra `external-consumer-exists` com `consumers`, `extraction_review: pending`. Então: **Extraction Review → NN-022 → ADR do Ecosystem → Contract → Provider/Consumers** — e só se o saldo for positivo e a fase de contratos (Fase 2) já estiver pronta.

> Arquitetura compartilhada nasce da necessidade comprovada, não de adivinhação.

## 6. Relação com o ADR-0009 (R e X)

| Situação | Trilho |
|---|---|
| Nova feature | nasce no Product (este guia) |
| Reorganizar internamente | **R** ([`refactoring.md`](refactoring.md)) |
| Potencial de reuso | `reuse_assessment` |
| Promoção ao Ecosystem | **X** (Extraction Review → ADR) |

## 7. O que continua proibido

Extrair o que a classificação de `candidates.md` apenas **mapeou** (Editor, Git, IA do Urbe…) sem evidência de segundo consumidor; criar Services especulativos (Identity, Commerce, Catalog, Entitlements, Downloads, Updates, Reviews, Creator Profiles, Notifications, backends de Store/Community); criar `platform/`, `tools/`, `workspaces/`, `services/` para "preparar o futuro" (ADR-0004).
