# Handoff — LUNET-511: exportação ZIP da biblioteca (16/11/2026)

- Fonte canônica: `AbnerCruz/Ecosystem/apps/lunet2d`; branch `feat/lunet-511-versioned-asset-library-20261116`; PR #962; sem alterações no espelho legado.
- Incremento funcional: `ReusableAssetLibrary.ExportZip` cria ZIP autocontido com `asset.json` e `files/`, verifica integridade dos arquivos de origem, rejeita sobrescrita e remove ZIP temporário após falha.
- Testes: `ReusableAssetLibraryTests.ExportZip_ContainsManifestAndImmutableSourceAndDerivedFiles` e `ExportZip_RejectsCorruptedBytesWithoutCreatingDestination` (xUnit); nenhum resultado de execução local declarado.
- Commits de código e testes: `cfeba1fc`, `c05d7e3`; changelog registrado no mesmo PR.
- Fontes normativas: MANIFEST NN-001/NN-005/NN-010; AGENTS raiz/local; ROADMAP LUNET-511; auditoria Home/Studios/Explorar 15/11. Preservar formatos e isolamento de projetos; nenhuma alteração em estágios de versão ou governança.
- Pendente: workflow do novo head, integração de exportação à UI Android/SAF, editor de Studio independente de projeto, teste em Android 13, revisão e integração automática. Não marcar LUNET-511 como concluído.

## Continuação — exportação Android SAF (16/11/2026)
- Integração real no `MainActivity.AssetLibrary.cs`: botão Exportar ZIP por versão, `ACTION_CREATE_DOCUMENT`, resultado roteado em `MainActivity.OnActivityResult`, seleção restaurada em `OnSaveInstanceState` e montagem/verificação do ZIP antes de abrir o documento escolhido. Cache temporário apagado no `finally`.
- Regressão adicional `ExportZip_UsesRequestedVersionAndPreservesLicenseMetadata` cobre versões distintas, fonte imutável e metadados de licença/atribuição.
- Commits: `7d2e05d`, `f9998e6`, `fde717c`. CI/DEVICE do novo head pendentes; o destino SAF pode ficar parcialmente escrito se o provedor falhar durante a cópia, e a interface avisa para verificar o arquivo. Sem alegar transação atômica em provedores externos.
- Próximo incremento: Studio global de edição real e importação para múltiplos projetos; verificar Android 13 e controles de acessibilidade.

## CI — 16/11/2026, compilação xUnit
- O run [lunet2d-ci #43935337968](https://github.com/AbnerCruz/Ecosystem/actions/runs/43935337968) falhou em `ReusableAssetLibraryTests.cs:221` com `CS0579: Duplicate 'Fact' attribute`; release #43935337967 falhou na mesma etapa de teste, antes de gerar APK.
- Corrigido no commit `64e374bc`: removida somente a anotação `[Fact]` redundante; o teste `ExportZip_UsesRequestedVersionAndPreservesLicenseMetadata` continua exercitando ambas as versões.
- CI do head atualizado, DEVICE e publicação de release ainda aguardam confirmação; não liberar integração apenas com a alteração textual.
