## Origem

- Issue:
- Item do ROADMAP (`RM-Fn-nn`):
- Requisito(s) `REQ-...`:
- Gate (`G0`–`G6`):
- ADR, se aplicável:

## Problema

Que necessidade concreta este PR resolve?

## Mudança

O que foi alterado?

## Fora de escopo

O que deliberadamente não foi alterado?

## Arquitetura / legacy

- [ ] Não cria nova autoridade concorrente.
- [ ] Não adiciona nova camada histórica em `app.js` (`node tools/check-debt.mjs`).
- **Dívida criada:** (nenhuma / descrever) · **Dívida removida:** (descrever; tetos em `tools/debt-ceiling.json` só descem)
- [ ] Se toca legacy, identifica substituto/consumidores/condição de remoção.
- [ ] ADR criado/atualizado quando necessário.

Detalhes:

## Dados e compatibilidade

- Formatos afetados:
- Compatibilidade com vaults antigos:
- Migração/rollback:
- [ ] Não altera persistência.
- [ ] Ou possui testes/fixtures de migração.

## Plataformas

Impacto:

- [ ] Web
- [ ] Windows/Electron
- [ ] Android
- [ ] Nenhum específico

## Segurança e privacidade

Há mudança em plugins, IA/chaves, filesystem, HTML ativo, links externos ou bridges?

## Performance

Cenário/medição afetado:

## Testes executados

```
npm run check      # versão única, módulos/boundaries, dívida, rastreabilidade, workflows, tutorial e testes
```

Outros testes reproduzíveis:

## Validação manual

Pré-condições, passos e resultados esperados, quando necessária.

## Documentação

- [ ] README/CHANGELOG quando aplicável.
- [ ] Arquitetura/ADR quando aplicável.
- [ ] Requirement Ledger/SPEC/ROADMAP quando aplicável.
- [ ] Tutorial quando comportamento do usuário mudou.

## Definition of Done

- [ ] Implementação real e integrada (código antigo substituído removido no mesmo PR).
- [ ] Testes relevantes passam (`npm run check` verde) e cobrem o comportamento (não só strings de código).
- [ ] Critérios de aceite do item do ROADMAP atendidos; estado do item atualizado (`[x]` só com DoD completo).
- [ ] `TRACEABILITY.md` regenerada se REQ/SPEC/ROADMAP mudaram (`node tools/gen-traceability.mjs`).
- [ ] Sem mudança de formato persistido sem catálogo, migração, fixture e teste.
- [ ] Sem placeholder relevante.
- [ ] Sem regressão conhecida.
- [ ] Documentação coerente.
