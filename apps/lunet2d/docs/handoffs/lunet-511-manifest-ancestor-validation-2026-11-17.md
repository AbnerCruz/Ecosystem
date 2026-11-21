# LUNET-511 — colisão de ancestral em manifesto (17/11/2026)

Escopo: `apps/lunet2d` no repositório canônico Ecosystem; PR #962.

## Implementação

`ReusableAssetLibrary.ReadManifest` recusa entradas nas quais o caminho de um arquivo é ancestral de outro caminho, sem distinguir caixa. Um pacote malformado deixa de passar na leitura para falhar posteriormente durante importação ou extração. Manifesto persistido não é alterado.

`ReusableAssetLibraryTests.ManifestWithFileAsAncestor_IsRejectedBeforeImportOrExport` verifica dois casos de caixa, recusa de Find/Import/Export, preservação de `Game.cs` e ausência de ZIP parcial.

## Gates

Não houve execução local de `dotnet test` nem DEVICE Android 13 nesta sessão. Aguardar checks do novo head e corrigir regressões reais. No head anterior `151d657`, CI Lunet, release e UI-screens estavam verdes, mas consistency falhou em `CHK-HANDOFFS` por `docs/governance/handoffs/HO-20261002-portal-v2.json` (fora do escopo autorizado). Não alterar governança raiz nesta branch; encaminhar ao responsável pelo Ecosystem.

## Próximo incremento

Desacoplar sessão do Studio de `_project` para permitir editar assets da biblioteca diretamente sem projeto. Preservar versões imutáveis e cópia explícita para projetos. Não fechar LUNET-511 sem DEVICE.
