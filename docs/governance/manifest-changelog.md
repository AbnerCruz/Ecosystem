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
