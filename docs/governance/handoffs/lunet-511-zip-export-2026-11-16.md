# Handoff — LUNET-511: exportação ZIP da biblioteca (16/11/2026)

- Fonte canônica: `AbnerCruz/Ecosystem/apps/lunet2d`; branch `feat/lunet-511-versioned-asset-library-20261116`; PR #962; sem alterações no espelho legado.
- Incremento funcional: `ReusableAssetLibrary.ExportZip` cria ZIP autocontido com `asset.json` e `files/`, verifica integridade dos arquivos de origem, rejeita sobrescrita e remove ZIP temporário após falha.
- Testes: `ReusableAssetLibraryTests.ExportZip_ContainsManifestAndImmutableSourceAndDerivedFiles` e `ExportZip_RejectsCorruptedBytesWithoutCreatingDestination` (xUnit); nenhum resultado de execução local declarado.
- Commits de código e testes: `cfeba1fc`, `c05d7e3`; changelog registrado no mesmo PR.
- Fontes normativas: MANIFEST NN-001/NN-005/NN-010; AGENTS raiz/local; ROADMAP LUNET-511; auditoria Home/Studios/Explorar 15/11. Preservar formatos e isolamento de projetos; nenhuma alteração em estágios de versão ou governança.
- Pendente: workflow do novo head, integração de exportação à UI Android/SAF, editor de Studio independente de projeto, teste em Android 13, revisão e integração automática. Não marcar LUNET-511 como concluído.
