# ADR-0007 — Resposta a decisões pelo portal

## Status

Aceito — mecanismo ratificado pelo proprietário em DEC-0011 (alternativa A, escolhida no próprio portal em 2026-10-01; [registro](../governance/responses/DEC-0011.md)). A capacidade já havia sido decidida em DEC-0010.

## Contexto

O portal (ADR-0005) é um site estático no GitHub Pages: não tem backend e não pode escrever no repositório. O proprietário decidiu (DEC-0007) que decisões pendentes aparecem no portal com o objeto e (DEC-0010) que quer escolher a alternativa clicando nela, com o registro feito no repositório. MANIFEST §47 proíbe criar backend obrigatório; NN-009 exige que a decisão fique em fonte canônica; NN-016 exige acesso explícito e restrito para qualquer escrita automatizada.

## Problema

Como um site estático faz uma escolha chegar ao repositório de forma que seja: restrita ao proprietário, auditável, sem segredo de escrita exposto no navegador, sem backend próprio e sem abrir uma superfície de injeção?

## Opções

1. **Issue pré-preenchida + workflow.** Cada alternativa é um link para `issues/new` já com título e corpo; enviar a Issue é a confirmação (autenticada pelo GitHub). Um workflow acionado por `issues: opened` valida e registra.
2. **Token do GitHub no navegador.** O proprietário cria um token de acesso (fine-grained) e o portal o guarda no navegador para gravar direto pela API: um toque só.
3. **Backend próprio** (função serverless) que recebe o clique e escreve no repositório.
4. **Manter a resposta pelo chat**, com o agente registrando.

## Decisão

Opção 1 (a ser ratificada em DEC-0011).

- **Formato único.** `site/generator/GenerateStatus.cs` emite, para cada alternativa de cada decisão pendente, `id` (letra), `hash` (12 hex de SHA-256 de `option + "\n" + consequences`), `issueTitle` (`Decisão DEC-NNNN: X`) e `issueBody` (marcador `<!-- ecosystem-decision:v1 -->` e as linhas `decision:`, `option:`, `option-hash:`). O portal só monta o link; o aplicador valida o mesmo formato.
- **Workflow `.github/workflows/decision.yml`.** Gatilho `issues: opened`; só roda se o autor é o dono do repositório e o título começa com `Decisão DEC-`; permissões explícitas (`contents`, `issues`, `actions: write`); o texto da Issue chega **apenas por `env`**, nunca interpolado em script.
- **Aplicador `.github/scripts/apply-decision.cs`.** Recusa (sem alterar nada) se: o autor/associação não é o dono; título ou corpo fora do formato ou discordantes; a decisão não existe ou não está `pending`; a alternativa não existe; o hash não confere com o texto atual da alternativa (escolha desatualizada). Se aceita: grava `docs/governance/responses/DEC-NNNN.md` (registro persistido) e marca a decisão `decided` com data, texto e `record`.
- **Consistência antes de gravar.** O workflow roda `tests/consistency/Check.cs` depois do aplicador; se falhar, desfaz tudo, comenta na Issue e não faz commit.
- **Gravação e publicação.** Commit na branch padrão pelo `github-actions[bot]`; como pushes feitos com `GITHUB_TOKEN` não disparam outros workflows, o workflow dispara explicitamente `pages.yml`. A Issue recebe um comentário com o resultado e é fechada (`completed` ou `not planned`).
- **Fiscalização.** `CHK-DECISION-FLOW` (guarda do dono, permissões explícitas, texto não confiável só em `env`, link de resposta no portal) e o self-test, que executa o aplicador em cópia do repositório: caminho feliz mais recusas (autor errado, título inválido, hash adulterado, título/corpo discordantes, decisão já decidida).
- **Nova categoria de componente** `automation` em `ecosystem.json` (componente `decision-recorder`, caminho `.github/scripts`): automação do repositório que escreve em fontes canônicas.

## Consequências

- O proprietário escolhe em **dois toques** (alternativa no portal; "Submit new issue" no GitHub) e a decisão é registrada em ~1–2 minutos, com trilha de auditoria completa (Issue, autor, horário, execução do workflow, commit).
- **Limite conhecido:** agentes que operam com as credenciais do proprietário também *poderiam* criar a Issue. A proteção é a regra (MANIFEST §23.2; `AGENTS.md`: agentes nunca respondem decisão pendente) e a auditoria; não há como o GitHub distinguir. Quando o Hub existir, a identidade pode ser reforçada.
- O registro automático **não aplica** as consequências da decisão nos documentos dependentes nem substitui o ADR de decisões estruturais (NN-011); um agente as aplica depois (`responses/*.md` diz isso).
- Se a branch padrão passar a ter proteção que bloqueie o push do bot, o fluxo precisará de uma exceção explícita.
- O mecanismo foi validado de ponta a ponta com uma decisão sintética (`DEC-9999`, removida depois) e depois **com cliques reais do proprietário** (DEC-0011 e DEC-0012, Issues #4 e #5).

## Alternativas rejeitadas

- **Token no navegador (opção 2):** é um toque só, mas põe um token de escrita no armazenamento de um site público estático (risco de XSS e de vazamento), exige que o proprietário o crie e o renove, e não tem confirmação independente. Continua disponível como alternativa em DEC-0011.
- **Backend próprio (opção 3):** contraria MANIFEST §47 (backend obrigatório) e criaria uma segunda superfície de segurança a manter (NN-020).
- **Só chat (opção 4):** é o que o proprietário pediu para deixar de depender (DEC-0010).

## Referências

DEC-0007, DEC-0010, DEC-0011; ADD-0004; ADR-0005; MANIFEST §23.2, §47; NN-001, NN-009, NN-016, NN-021; [`docs/architecture/portal.md`](../architecture/portal.md) §3.2.
