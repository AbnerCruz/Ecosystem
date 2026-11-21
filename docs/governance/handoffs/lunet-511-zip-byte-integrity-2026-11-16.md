# LUNET-511 — Integridade dos bytes exportados no ZIP (16/11/2026)

- Fonte: `AbnerCruz/Ecosystem/apps/lunet2d`, branch `feat/lunet-511-versioned-asset-library-20261116`, PR #962. Main consultada em `54cdca1d`; PR #961 integrado, #959/#960 abertos.
- Falha: `ExportZip` verificava o hash de um arquivo antes de comprimi-lo. Uma edição concorrente entre a leitura do hash e a cópia poderia produzir ZIP inconsistente com o manifesto, mesmo com preflight aprovado.
- Correção: usar `IncrementalHash(SHA256)` sobre os mesmos blocos gravados na entrada ZIP; validar hash e tamanho reais antes de concluir o arquivo. Falha elimina o temporário, preserva a versão imutável e não substitui destino existente.
- Regressão: `ReusableAssetLibraryTests.ExportZip_ArchivedBytesMatchManifestHashesAndLengths` lê cada entrada do ZIP e compara bytes descompactados, tamanho e SHA-256 com o manifesto.
- Evidência: commits `d4e49b3` e `370a94b`, ROADMAP e CHANGELOG atualizados. `dotnet` indisponível no ambiente desta execução; CI do novo head e DEVICE Android 13 ainda pendentes, sem declaração de conclusão.
- Referências externas: documentação Android Storage Access Framework https://developer.android.com/training/data-storage/shared/documents-files e exemplo oficial https://github.com/android/storage-samples; relatos de limitações SAF https://www.reddit.com/r/androiddev/comments/xqck2j/ . O SAF mantém o controle do destino com o usuário; a verificação do ZIP acontece antes da escrita no provider.
- Próximo: acompanhar workflows do PR #962, corrigir falhas, validar exportação SAF e retomada de Activity no moto g32; avançar edição Studio sem projeto em incremento independente. Não alterar estágio de versão nem forçar merge.
