# LUNET-511 — conflito em diretório ancestral (17/11/2026)

Escopo: apenas `apps/lunet2d`, PR #962. Main, AGENTS, ROADMAP, PRs #959/#960/#961/#962 e CI anterior consultados. A auditoria de 15/11 e o guia da biblioteca permanecem as referências do produto.

## Implementação
- `ReusableAssetLibrary.FindImportConflicts` passa a listar arquivos de destino cuja pasta ancestral já é um arquivo, sem gravar nada no projeto.
- `ReusableAssetLibraryTests.FindImportConflicts_ReportsAncestorFileWithoutChangingDestination` cobre duas entradas bloqueadas pela mesma pasta ocupada e preservação do destino.
- Commits: `98484b2`, `8c09c87`; CHANGELOG e ROADMAP atualizados.

## Gates
- Não foi possível executar dotnet test localmente nesta sessão; aguardar workflows do novo head e corrigir falhas antes de integrar.
- CI anterior de `a44e58a` tinha quatro workflows verdes; não equivale a validação deste incremento.
- DEVICE Android 13, Studios independentes e integração final continuam pendentes.
- Sem merge forçado, release presumida ou alteração de estágio.
