# Migração 1.x → 2.x (REQ-082 · ADR-0004)

## Para quem usa o Urbe
- **Nada é apagado.** A 2.x lê todos os vaults da 1.x. Suas notas são arquivos comuns e não são alteradas pela migração.
- **Na primeira gravação** de um vault 1.x o Urbe cria `.urbe/vault.json` (versão do formato) e faz uma **cópia de segurança** dos arquivos de controle antigos (`.urbe/mapa.json`, histórico, lixeira, composições, journal…) em `.urbe/backup/<data>-1-2/`. Você vê o aviso "Vault atualizado para o formato do Urbe 2.0".
- **Histórico, lixeira e composições** passam a ser gravados em arquivos novos (`.urbe/history.v2.json`, `trash.v2.json`, `compositions.v2.json`). Os arquivos antigos ficam **intactos**: se você abrir o mesmo vault na 1.x, ela continua funcionando (mas não vê o que foi feito depois da migração nesses três itens).
- **Mapa da cidade:** continua em `.urbe/mapa.json` (`v: 4`, legível pela 1.x).
- **Plugins** precisam ser aprovados de novo se a aprovação anterior usava a impressão digital antiga (a 2.x exige SHA-256).
- **Vault de uma versão mais nova:** se o vault foi criado por uma versão futura do Urbe, ele abre **somente para leitura** (com aviso) para nada ser corrompido; arquivos de versão desconhecida são preservados sem alteração.

## Voltar atrás (rollback)
1. Feche o Urbe.
2. Abra o Urbe 2.x e use o comando **Restaurar backup do vault** (`workspace.restoreBackup`) escolhendo o backup `…-1-2`, **ou** copie manualmente os arquivos de `.urbe/backup/<data>-1-2/files/urbe/` de volta para `.urbe/` e apague `.urbe/vault.json`.
3. Abra o vault na 1.x.

## Avisos de instalação
- **Windows:** o instalador não é assinado; o SmartScreen mostra "Windows protegeu o computador" → *Mais informações* → *Executar assim mesmo*.
- **Android:** o APK vem de fora da loja; permita "instalar apps desconhecidos" para o navegador/gerenciador de arquivos usado. O Urbe pede "acesso a todos os arquivos" para gravar em `Documentos/Urbe`.

## Para quem desenvolve
- Contrato por formato: `discovery/DATA-CATALOG.md` §9. Fixtures: `tests/fixtures/vaults/` (`node tools/make-fixtures.mjs`). Testes: `tests/vault-format.mjs`, `tests/vault-migration.mjs`, `tests/e2e/fixtures.e2e.mjs`.
