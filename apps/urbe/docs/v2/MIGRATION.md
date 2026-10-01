# Migração 1.x → 2.x (REQ-082 · ADR-0004)

## Para quem usa o Urbe
- **Nada é apagado.** A 2.x lê todos os vaults da 1.x. Suas notas são arquivos comuns e não são alteradas pela migração.
- **Na primeira gravação** de um vault 1.x o Urbe cria `.urbe/vault.json` (versão do formato) e faz uma **cópia de segurança** dos arquivos de controle antigos (`.urbe/mapa.json`, histórico, lixeira, composições, journal…) em `.urbe/backup/<data>-1-2/`. Você vê o aviso "Vault atualizado para o formato do Urbe 2.0".
- **Histórico, lixeira e composições** passam a ser gravados em arquivos novos (`.urbe/history.v2.json`, `trash.v2.json`, `compositions.v2.json`). Os arquivos antigos ficam **intactos**: se você abrir o mesmo vault na 1.x, ela continua funcionando (mas não vê o que foi feito depois da migração nesses três itens).
- **Mapa da cidade:** continua em `.urbe/mapa.json` (`v: 4`, legível pela 1.x).
- **Várias cidades (1.x antiga):** se sua instalação tinha mais de uma cidade, cada uma é copiada **uma única vez** para `Cidades/<nome>/` dentro do vault **Urbe**, com a disposição das casas e os mesmos IDs das notas. As cidades originais continuam guardadas, intactas. Abrir o app de novo não recopia nada nem duplica bairros, e uma nota editada dentro de `Cidades/` nunca é sobrescrita pela original. O comando **Arquivar cidades antigas** só as tira da lista (nada é movido nem apagado); **Mostrar cidades antigas** as traz de volta.
- **Reorganização da cidade:** se a cidade foi salva com outra versão do "mundo" (terreno), o Urbe a reorganiza ao abrir, **guarda uma cópia do mapa anterior** (`.urbe/backup/<data>-layout-layout/`) e pergunta se você quer **desfazer**.
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
- Multi-cidade (RM-F1-19): `src/persistence/multi-city.js`. Antes do load, `urbeEnsureSingleVault` só copia arquivos e marca `pendente` em `.urbe/merged-v1.json` (mesmo nome e formato da 1.x, campo aditivo). O gancho `addLoadHook` funde o mapa da origem antes de montar os documentos, para que os IDs venham da origem. Em `workspace:loaded` o `WorkspacePersistence` grava o mapa, registra `{id:'multi-city',sources}` em `vault.json.migrations` e baixa a pendência. Cidades já migradas vêm do marcador **ou** de `vault.json`. `vault.json.archivedCities` só filtra a lista. Testes: `tests/multi-city.mjs`, `tests/e2e/multi-city.e2e.mjs`.
