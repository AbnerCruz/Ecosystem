# LUNET-511 — case-insensitive import collisions — 2026-11-17

- Escopo: apenas `apps/lunet2d`, PR #962, branch `feat/lunet-511-versioned-asset-library-20261116`.
- Problema: em Android/Linux, `Content/Art/wing.png` e `Content/Art/WING.PNG` podem coexistir, mas tornam a cópia não portátil para sistemas de arquivos sem distinção de caixa. O mesmo vale para `Content/Art` e `Content/ART`.
- Implementação: prévia `FindImportConflicts` e importação `ImportToProject` verificam cada componente de caminho já existente sem sobrescrever dados.
- Regressão: `ImportRejectsCaseInsensitiveFileAndDirectoryCollisions` cobre colisão de arquivo e diretório, preservação de dados e do `Game.cs`.
- Validação: commits gravados no GitHub; execução local .NET e DEVICE Android 13 não realizada. Conferir CI do head após publicação; não declarar gate concluído.
- Próximo: testar fluxos na Home/Studios, editar assets sem projeto e validar exportação/importação ZIP no dispositivo.
