# LUNET-511 — busca por nomes e caminhos dos arquivos (17/11/2026)

- Escopo: somente apps/lunet2d, PR #962, branch `feat/lunet-511-versioned-asset-library-20261116`.
- Base consultada: main, AGENTS.md raiz/local, ROADMAP, CHANGELOG, AgentsChat, auditoria Home/Studios/Explorar 15/11 e PRs #959–#962.
- Referências de UX e armazenamento: pesquisa offline-first Android e Godot AssetLib documentadas na auditoria existente; arquivos-fonte e derivados permanecem cópias locais imutáveis.
- Código: `ReusableAssetLibrary.Search` passa a pesquisar também `LibraryAssetVersion.Files[].Path`, além dos metadados/tags. Não modifica manifestos.
- Teste: `Search_FindsSourceAndDerivedFilenamesAfterRestart` verifica busca por nome de fonte, PNG, caminho e termos combinados após reabrir a Library.
- Commits de código/teste: `5ebbacc`, `fdd486e`. Documentação no mesmo PR.
- Verificação: testes .NET e DEVICE Android 13 não executados nesta sessão; confirmar os workflows do head e corrigir falhas antes de merge. Não declarar conclusão da biblioteca ou release.
- Próximo passo: edição global diretamente nos Studios, testes de integração da UI e ciclo de APK; manter PR draft até gates.
