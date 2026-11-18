# LUNET-511 — validação de identidade dos manifestos (16/11/2026)

Escopo: somente `apps/lunet2d`, branch `feat/lunet-511-versioned-asset-library-20261116`, PR #962.

## Estado consultado
- `main`, `AGENTS.md`, `ROADMAP.md` e PRs #961 (integrado), #959/#960 (abertos), #962 (draft).
- Auditoria de Home/Studios/Explorar de 15/11 e orientação de integridade da biblioteca.
- Head anterior `2bde2cd`: quatro workflows concluídos com sucesso, mas DEVICE não comprovado.

## Incremento
- `ReusableAssetLibrary.ReadManifest` agora rejeita `Kind` fora dos valores conhecidos e `CreatedUtc` default, antes de expor a versão na UI ou importar para projeto.
- Dois casos de regressão xUnit: manifesto alterado preservado, importação recusada e `Game.cs` do destino intacto.
- Nenhum dado é reparado ou apagado automaticamente.

## Evidências e limitações
- Commits no PR #962: `b2a55e1`, `12cdc3f` e documentação subsequente.
- CI do novo head não confirmado nesta execução; não declarar testes verdes nem DEVICE.
- Próximo: integrar Studios sem projeto, testar Android 13 real e conferir resultados dos workflows.
- Não forçar merge, não mudar estágio, não inferir APK publicado.
