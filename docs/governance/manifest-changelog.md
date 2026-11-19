# Registro de alterações do `MANIFEST.md`

> **Autoridade:** normativa, subordinada ao próprio `MANIFEST.md` (§0). O manifesto "não pode ser silenciosamente reinterpretado, resumido, enfraquecido ou substituído"; toda alteração deve constar aqui com as cinco exigências de MANIFEST §0.

O texto do manifesto incluído no primeiro commit (`912366d`) é o documento original enviado pelo proprietário, sem alterações.

## 1. NN-023 — Produtos distribuíveis não dependem da distribuição do Hub

| Exigência de MANIFEST §0 | Registro |
|--------------------------|----------|
| 1. Contradição identificada | **Nenhuma.** NN-023 é um aditamento. Foram conferidas as cláusulas que poderiam tensionar: NN-003 (complementada, não alterada), §5.3 e §7 (o Hub já não é ponto único de falha), §2 (Product Shell não cria "superaplicativo": cada Product permanece independente), §47 (nada é implementado agora: sem marketplace, contas ou backend). |
| 2. Justificativa | O proprietário decidiu que o Hub pode permanecer privado/interno e que cada Product deve poder ser distribuído como aplicação completa sem ele. NN-003 só garantia independência de *funcionamento*; faltava garantir independência de *distribuição*. |
| 3. Análise de impacto | Nenhum componente existente é afetado: hoje nenhum depende do Hub. Passa a restringir futuros empacotamento, release pipelines, instaladores e Distribution Profiles: o Hub nunca pode ser parte obrigatória de um Product distribuível. Fiscalização: `CHK-BOUNDARIES` (grafo declarado, implementado) e mecanismos planejados por fase em `enforcement-matrix.json`. |
| 4. Decisão registrada do proprietário | [ADD-0002](addenda/ADD-0002-product-shells-distribuicao-independente.md) §5, registrada como **DEC-0006** em [`decisions.json`](decisions.json). |
| 5. Atualização de manifesto e documentos derivados, no mesmo conjunto de mudanças | `MANIFEST.md` (NN-023 inserida após NN-022; nenhuma linha existente removida ou alterada, nenhuma invariante renumerada); `AGENTS.md`; `enforcement-matrix.json`; `CHK-BOUNDARIES`; `ARCHITECTURE.md`; `docs/architecture/`; `ROADMAP.md`; `docs/migration/`; ADR-0006. |

**O que este registro não altera:** as definições de MANIFEST §6 permanecem como estão. Os conceitos Product Shell e Context foram formalizados no nível de arquitetura ([`product-model.md`](../architecture/product-model.md)), conforme o texto do proprietário ("adicionar formalmente à arquitetura"); promovê-los para MANIFEST §6 seria uma alteração adicional e exigiria nova decisão registrada aqui.

## NN-017 — de etapa obrigatória a regra de linguagem (ADD-0035)

| Exigência de MANIFEST §0 | Registro |
|--------------------------|----------|
| 1. Contradição identificada | NN-017 exigia que gates e roadmap esperassem `human validated` quando o requisito dependia de aparelho; o proprietário mandou remover essa etapa. |
| 2. Justificativa | Ordem do proprietário em 19/11/2026: as validações no aparelho e as aprovações atrasam o trabalho; quer automação plena e ser informado do que foi implementado. |
| 3. Análise de impacto | Tarefas concluem com evidência automática; o portal deixa de ter fila de validações; integração automática para tudo exceto a classe `extreme`. Mantido: nunca apresentar CI/emulador como teste no aparelho (honestidade), NN-018 (evidência), o portão protegido. |
| 4. Decisão registrada do proprietário | [ADD-0035](addenda/ADD-0035-automacao-plena-sem-validacao-no-aparelho.md), **DEC-0065** em [`decisions.json`](decisions.json). |
| 5. Atualização de manifesto e documentos derivados | `MANIFEST.md` (nota em NN-017, nenhuma invariante removida ou renumerada), `AGENTS.md`, `definition-of-done.md`, `communication.md` §9, `integration-policy.json` (classe `extreme`), `tests/consistency/Check.cs`, `site/generator/GenerateStatus.cs`. |
