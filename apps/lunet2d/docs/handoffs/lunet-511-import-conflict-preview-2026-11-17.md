# LUNET-511 — prévia de conflitos de importação (17/11/2026)

- Escopo: exclusivamente apps/lunet2d no repositório Ecosystem, PR #962; sem alteração de versão ou merge.
- Estado inicial: main, AGENTS.md, ROADMAP, auditoria Home/Studios/Explorar, PRs #959/#960/#961/#962 consultados.
- Código: ReusableAssetLibrary.FindImportConflicts faz prévia somente-leitura de colisões de arquivos; MainActivity.AssetLibrary mostra caminhos antes de confirmar a cópia; ImportToProject mantém sua própria verificação contra alterações concorrentes.
- Regressão: FindImportConflicts_ReportsExistingFilesWithoutChangingProject verifica caminhos, ausência de escrita e recusa da importação conflitante.
- Documentação: ROADMAP, CHANGELOG e guia da biblioteca.
- Validação: código/teste publicados no PR; resultado do CI do novo head e DEVICE Android 13 ainda não confirmados neste handoff.
- Próximo passo: CI verde, inspeção de APK no dispositivo e desacoplamento real dos Studios do projeto.
