# ADD-0006 — Próxima fase e tolerância à reinstalação dos apps

> **Tipo:** decisão explícita do proprietário, registrada (MANIFEST §24, nível 1).
> **Autor:** AbnerCruz (proprietário). **Recebido em:** 2026-10-01, em conversa com o agente `claude-code`, e persistido aqui conforme NN-009.
> **Contexto da mensagem:** resposta ao resumo do agente que encerrava a importação do Lunet2D e do Urbe (P1-4, P1-5) e deixava a distribuição automatizada (P1-6) como próximo passo.

## Mensagem do proprietário (transcrição integral)

> Vamos prosseguir para a próxima fase. Não tem problema nessa etapa eu precisar desinstalar e instalar os apps dnv, é tudo beta ainda.

*(O texto foi mantido como enviado, inclusive "dnv".)*

## Interpretação registrada pelo agente (para conferência do proprietário)

| Trecho | Interpretação adotada | Natureza |
|--------|----------------------|----------|
| "prosseguir para a próxima fase" | Executar **P1-6** (distribuição automatizada, [`import-plan.md`](../../migration/import-plan.md) §7), já aprovada em DEC-0014-A. Não é mudança de fase do ROADMAP: a Fase 1 só termina com P1-6, P1-7 e P1-8. | Inferência do agente. |
| "não tem problema ... desinstalar e instalar os apps de novo" | Durante a Fase 1 (apps em beta) uma **quebra da atualização in-place** é **aceitável**: mudança de `versionCode`, de chave de assinatura ou do canal de updater pode exigir reinstalação. **Não é um objetivo**: o plano continua sem exigi-la (a origem mantém chave, `versionCode` e `releases/latest`), e a tolerância serve de margem para falhas, não para decidir de modo diferente. Não autoriza perder dados de usuário nem alterar formatos de dados (NN-013). | Decisão do proprietário (tolerância) + inferência (alcance). |
| "nessa etapa" | Vale para a migração (Fase 1). Antes de qualquer release que não seja beta, a regra deve ser revista. | Inferência do agente. |
