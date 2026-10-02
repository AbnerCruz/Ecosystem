# ADD-0012 — Automático por padrão; o proprietário só para o que é crítico

> **Tipo:** decisão explícita do proprietário, registrada (MANIFEST §24, nível 1).
> **Autor:** AbnerCruz (proprietário). **Recebido em:** 2026-10-02, em conversa com o agente `claude-code`, persistido aqui conforme NN-009.
> **Contexto da mensagem:** o integrador automático (ADR-0015) estava na `main` com o piso `owner-authorization`: verificava tudo sozinho, mas nenhum PR entrava sem a label `integrar`. DEC-0023 estava pendente no portal com uma pergunta binária ("integrar sozinho" × "um toque por PR"). O proprietário enviou uma instrução longa para transformar a decisão abaixo em mecanismo e corrigir a auditoria do integrador. O texto integral é longo; a frase que decide está transcrita primeiro, seguida dos trechos que fixam a direção.

## Decisão do proprietário (transcrição integral)

> “Quero decidir e aprovar somente coisas críticas, se uma implementação funciona normalmente dentro dos padrões não deve precisar de mim.”

A mesma mensagem cita a frase também assim: «“Quero decidir e aprovar somente coisas críticas. Se uma implementação funciona normalmente dentro dos padrões, não deve precisar de mim.”». As duas formas têm o mesmo sentido.

## Trechos da mesma mensagem que fixam a direção (transcrição)

> «A consequência arquitetural é: AUTOMATIC BY DEFAULT · HUMAN FOR CRITICAL CHANGES. Não: HUMAN APPROVAL BY DEFAULT»

> «Existe atualmente DEC-0023 relacionada ao nível de automação do integrador. A nova declaração do proprietário resolve conceitualmente a questão: trabalho normal → automático; trabalho crítico → proprietário. Não invente um clique no Portal. Não falsifique um evento da DEC-0023. [...] Não marque DEC-0023-A simplesmente por conveniência, porque A não representa perfeitamente esta política.»

> «Uma implementação NÃO é crítica apenas porque: altera muitas linhas; foi feita por IA; é nova; está no Urbe; está no Lunet2D; cria uma feature; refatora internamente; cria testes; corrige bugs; modifica UI; atualiza documentação; mexe em ROADMAP dentro de um plano já aprovado.»

> «Formalize um conjunto pequeno e objetivo de classes críticas.» — Constituição e autoridade; Controle do próprio sistema («Um mecanismo não pode aprovar sozinho uma alteração que reduza sua própria fiscalização.»); Segurança; Dados do usuário; Distribuição crítica («Criar uma release normal usando o mecanismo já aprovado NÃO é uma nova decisão crítica.»); Compatibilidade; Arquitetura transversal; Estratégia de produto.

> «A regra histórica do Urbe exige aprovação para todos os merges. Isso conflita com a nova decisão explícita do proprietário [...] O resultado desejado NÃO é: todo PR Urbe → proprietário. Mas: PR Urbe normal → CI → automatic e: PR Urbe crítico → proprietário. Preserve as decisões críticas específicas do Urbe.»

> «Qualquer PR que altere: merge policy · criticality policy · protected control plane · trusted gate · authorization mechanism é automaticamente: CRITICAL com base na política anterior da "main".»

> «Critical decision: existe uma escolha de direção ainda não tomada → DEC-XXXX → Portal → proprietário escolhe. Critical approval: a direção já foi decidida, mas a execução produz efeito crítico que ainda exige autorização → implementação preparada → testes verdes → proprietário autoriza. Não crie DEC nova para todo critical approval.»

> «O proprietário não é um CI humano. Ele é a autoridade para decisões críticas. [...] SE O SISTEMA JÁ SABE COMO JULGAR → O SISTEMA JULGA. SE É PRECISO ESCOLHER UMA NOVA DIREÇÃO OU AUTORIZAR UM EFEITO CRÍTICO → O PROPRIETÁRIO DECIDE.»

## Interpretação registrada pelo agente (para conferência do proprietário)

| Trecho | Interpretação adotada | Natureza |
|--------|----------------------|----------|
| "decidir e aprovar somente coisas críticas" | Política de integração: **rotina = automática**, **crítico = proprietário**. A autoridade operacional é [`docs/governance/integration-policy.json`](../integration-policy.json), com o mecanismo no [ADR-0015](../../adr/0015-integrador-automatico.md). | Decisão do proprietário. |
| DEC-0023 | A pergunta binária ficou insuficiente: nenhuma alternativa (A, B, C) descreve a política. DEC-0023 passa a `decided`, com esta transcrição como registro e a decisão descrita como "nenhuma das alternativas literais". Nenhum clique no portal foi simulado; o histórico (pergunta e alternativas) continua em `decisions.json`. | Decisão do proprietário; a forma do registro é do agente. |
| classes críticas | Oito classes, com zonas por caminho (falha fechada só dentro delas) e regras semânticas para o que não se resolve por caminho (decisões, ADRs, `ecosystem.json`, contratos, chaves de configuração de distribuição). | Proposta do agente dentro da direção do proprietário. |
| "não pode aprovar sozinho" | Quem classifica é a versão **da `main`** (confiável); um PR que muda a política, o integrador, os checks ou os workflows é crítico pela política anterior. | Decisão do proprietário. |
| Urbe | A regra "nenhum merge sem pedido explícito" vira: mudança rotineira entra sozinha; mudança crítica (dados, segurança, distribuição, licença, decisões consolidadas) espera a autorização do proprietário. | Decisão do proprietário. |
| validação humana (NN-017) | NN-017 não muda e nunca bloqueou a integração: o que depende de aparelho continua `[?]` até a validação humana. Muda só **quando o proprietário é interrompido**: validações críticas (gate de fase, instalação/atualização distribuída, dados do usuário, segurança, release) aparecem em "Precisa de você"; as demais ficam visíveis sem interromper. Sem conflito com o MANIFEST; nenhuma alteração normativa nele é necessária. | Inferência do agente. |
| autorização crítica | A label `integrar` só vale se o evento que a colocou foi feito pelo proprietário do repositório (verificado no GitHub) depois de o head atual ter sido avaliado. Limite da plataforma: se um agente usar a credencial do próprio proprietário, o GitHub não distingue; por isso agentes nunca põem essa label. | Decisão do proprietário + limite registrado. |
