# Guia de refatoração dos produtos (passo 10)

> **Autoridade:** operacional, subordinada ao ADR-0009 (`Aceito`, DEC-0018-A) e ao MANIFEST. Vale **depois do gate da Fase 1**. Candidatos e ordem sugerida: [`candidates.md`](candidates.md).

> **Antes do trilho:** uma funcionalidade **nova** nasce no Product que precisa dela (ADR-0011, [`local-first.md`](local-first.md)); estes trilhos valem para reorganizar o que já existe (R) e para promover ao Ecosystem com evidência (X).

## 1. Qual trilho?

```text
A mudança fica toda dentro de apps/<id> e não cria dependência fora do produto?
 ├─ sim → REFATORAÇÃO INTERNA (R): processo do próprio produto + lista do §2
 └─ não (vai para platform/, tools/, workspaces/, Service compartilhado, ou outro produto passa a usar)
        → EXTRAÇÃO (X): ADR do Ecosystem + NN-022 + contrato + fase do §3
```

## 2. Lista de verificação de uma refatoração interna (R)

O `AGENTS.md` do produto (`apps/lunet2d/AGENTS.md`, `apps/urbe/AGENTS.md`) continua valendo dentro do produto; o `AGENTS.md` do Ecosystem acrescenta as regras abaixo (MANIFEST §26).

1. **Escopo:** só arquivos de `apps/<id>/` (mais o handoff). Nada em outro produto.
2. **Processo do produto:** item no ROADMAP do produto; **ADR do produto** se muda a arquitetura dele — Lunet: `apps/lunet2d/docs/adr/`; Urbe: `apps/urbe/docs/v2/adr/` (no Urbe, também REQ/TRACEABILITY e a lista `BOUNDARY-EXCEPTIONS.md`, que deve diminuir).
3. **Referência qualificada:** "Lunet ADR 0007", "Urbe ADR-0010" — nunca um ID nu que colida com os do Ecosystem.
4. **Dados do usuário:** mudou formato, caminho ou migração de dados (vault do Urbe, projetos do Lunet)? **Pare** e leve a decisão ao portal antes (NN-005, NN-011; Urbe REQ-007/ADR-0004).
5. **Testes:** os do produto passam antes e depois (Lunet: `dotnet test --project tests/Lunet.Tests`; Urbe: `npm run check`), com os testes de arquitetura do produto atualizados; checks do Ecosystem verdes (`dotnet run tests/consistency/Check.cs`, inclusive `CHK-ARCH-REFS`).
6. **UI tocada?** Validação em aparelho/navegador pelo proprietário, com registro de validação do build (NN-017; `docs/validation/`).
7. **Merge e distribuição:** PR no Ecosystem; depois do merge, **disparar `sync-from-ecosystem.yml`** na origem (DEC-0017-A). Release: Lunet sai a cada sincronização da `main`; Urbe sai quando se cria a tag `urbe/v<versão>` (com `package.json` e `CHANGELOG.md` atualizados) e se dispara a sincronização.
8. **Handoff** com evidência e, se criou ou reestruturou funcionalidade relevante, `reuse_assessment` (`product-specific`, `possible-candidate` ou `external-consumer-exists`).

## 3. Extração para o Ecosystem (X)

Só depois de uma **Extraction Review** disparada por um segundo consumidor real (`reuse_assessment: external-consumer-exists`; ADR-0011). Exige, nesta ordem: as cinco respostas de NN-022 (complexidade removida, consumidores reais, contrato, custo novo, saldo positivo); **ADR do Ecosystem**; contrato explícito (NN-006) e a fase que o fornece — Capability/Context e Distribution Profile (Fase 2), Host API de Product Shell (Fase 5), Agent Runtime (Fase 6), primeira Tool compartilhada (Fase 7); pelo menos um consumidor real com teste; nenhum `if host == …` (NN-007).

## 4. O que continua proibido

Store; Product Shell compartilhado novo; capabilities improvisadas; packages em massa; reescrita do Urbe em C#; mudança de dados do usuário sem aprovação — salvo ADR específico aprovado (ADD-0002 §17).
