# LUNET-511 — Verificação de integridade sob demanda

Data: 2026-11-16. Escopo: somente apps/lunet2d, branch feat/lunet-511-versioned-asset-library-20261116, PR #962.

## Mudanças
- Core: ReusableAssetLibrary.VerifyIntegrity confere existência, tamanho e SHA-256 de todos os arquivos de uma versão; não modifica nem exclui dados danificados.
- Android: ação Verificar por versão, sucesso por Toast e falha por diálogo preservando dados.
- Testes: VerifyIntegrity_ReportsCorruptionWithoutDeletingStoredVersion cobre sucesso, corrupção e preservação.
- ROADMAP/CHANGELOG atualizados. Nenhuma alteração de estágio de versão.

## Evidências e pendências
Commits: c25cc61, 9dcea79, d91ba91. Verificação xUnit foi adicionada, mas não executada localmente (SDK indisponível); CI do head e DEVICE Android 13 pendentes. Não declarar a funcionalidade validada até checks e teste em aparelho. Não forçar merge.
Referências: auditoria lunet-home-studios-explorar-2026-11-15, AGENTS.md, ROADMAP.md, documentação Android offline-first e modelo de AssetLib Godot já registrados na auditoria.

## Próximo incremento
Concluir edição real dos Studios fora de projetos sem criar projeto temporário, e executar ciclo criar → editar → Library → importar → executar → APK no Android 13.
