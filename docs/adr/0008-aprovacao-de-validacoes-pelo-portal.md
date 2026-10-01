# ADR-0008 — Aprovação de validações humanas pelo portal

## Status

Proposto — o proprietário pediu a capacidade (ADD-0005); o **mecanismo** aguarda ratificação em DEC-0015. Estende o [ADR-0007](0007-resposta-a-decisoes-pelo-portal.md), que continua válido.

## Contexto

Validações humanas (NN-017) aparecem no portal com o objeto a validar (DEC-0007), mas só podiam ser concluídas pelo chat, com o agente editando o handoff. O proprietário pediu (ADD-0005) poder **aprovar pelo próprio portal**, como já faz com decisões (DEC-0010). As restrições são as do ADR-0007: site estático sem backend (MANIFEST §47), registro em fonte canônica (NN-009), acesso restrito e explícito (NN-016), sem token no navegador.

## Problema

Como o proprietário registra o resultado de uma validação humana (aprovada ou reprovada) clicando no portal, sem que um agente possa fazê-lo por ele nem se criar uma segunda fonte para o mesmo fato (NN-001)?

## Opções

1. **Reusar o mecanismo do ADR-0007**: botões **Aprovar** e **Reprovar** abrem uma Issue pré-preenchida; o dono a envia; o mesmo workflow valida e **atualiza a própria entrada de verificação no handoff** (única fonte da validação), mais um registro em `docs/governance/responses/`.
2. **Registro paralelo** (arquivo de respostas) que o gerador cruza com os handoffs: cria duas fontes para o mesmo fato (viola NN-001).
3. **Chat** com o agente editando o handoff (o que se quer deixar de depender).

## Decisão

Opção 1 (a ser ratificada em DEC-0015).

- **Formato único.** `site/generator/GenerateStatus.cs` emite, para cada validação humana pendente, `handoff` (message_id), `checkHash` (12 hex de SHA-256 de `check + "\n" + object`) e as duas respostas (`approve`, `reject`) com `issueTitle` (`Validação <tarefa>: aprovada|reprovada`) e `issueBody` (marcador `<!-- ecosystem-validation:v1 -->`, linhas `validation:`, `handoff:`, `check-hash:`, `result: passed|failed`). O texto livre depois da linha `comment:` é **dado** (registrado como citação, nunca interpretado).
- **Mesmo workflow e mesmo aplicador** (`decision.yml`, `apply-decision.cs`), com o filtro de título estendido para `Validação `. Mesma guarda do dono, mesmas permissões, texto da Issue só por `env`, checks antes de gravar, commit na branch padrão, portal republicado.
- **Recusas.** Autor ou associação que não é o dono; título/corpo fora do formato ou discordantes; handoff inexistente; handoff que **não é o mais recente da sua tarefa** ou está `done`/`cancelled`/`failed`; verificação inexistente, não humana ou que **não está `pending`**; `check-hash` que não confere (validação mudou depois de exibida).
- **O que é gravado.** Na entrada de verificação do handoff: `result` (`passed` ou `failed`) e `evidence` (quem, quando, Issue e registro). Nada mais do handoff muda: **o `state` e as demais entradas continuam do agente**. Registro persistido: `docs/governance/responses/VAL-<message_id>-<check-hash>.md`.
- **Reprovação.** O resultado `failed` aparece nos checks e o agente trata como bloqueio/rework; o comentário opcional do proprietário vai para o registro.
- **Fiscalização.** `CHK-DECISION-FLOW` passa a exigir o filtro `Validação ` e o link de resposta de validação no portal; o self-test do aplicador cobre aprovar, recusa de hash adulterado, de handoff antigo, de validação já respondida e de autor que não é o dono.

## Consequências

- O proprietário aprova ou reprova em **dois toques**, com a mesma trilha de auditoria das decisões.
- O **mesmo limite conhecido** do ADR-0007 vale: um agente com as credenciais do proprietário *poderia* criar a Issue; a proteção é a regra (MANIFEST §23.2; AGENTS.md: agentes nunca respondem validação humana pendente) e a auditoria.
- A automação escreve em um handoff de **outro** autor, mas só o campo de resultado de uma verificação humana pendente, e deixa o registro da origem (Issue e arquivo de resposta). O agente nunca escreve o resultado de uma validação humana.
- Depois de aprovada, um agente atualiza o `state` do handoff e o ROADMAP.

## Alternativas rejeitadas

- **Registro paralelo (opção 2):** duas fontes para o mesmo fato (NN-001).
- **Chat (opção 3):** é o que o proprietário pediu para deixar de depender.
- **Token no navegador:** mesmos motivos do ADR-0007.

## Referências

ADD-0005; ADR-0007; DEC-0007, DEC-0010, DEC-0015; MANIFEST §23.2, §47; NN-001, NN-009, NN-016, NN-017, NN-021; [`docs/governance/communication.md`](../governance/communication.md) §9.
