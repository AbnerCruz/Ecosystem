# P4-9 — corte direto de distribuição do Urbe

Autoridade: DEC-0039 / ADD-0017. A estratégia anterior de versão-ponte e sincronização foi abandonada pelo proprietário.

## Estado escolhido

- `AbnerCruz/Urbe` é histórico congelado, não canal atual;
- `sync-from-ecosystem.yml` foi removido do repositório legado;
- o primeiro release direto pode ser `urbe-v1.8.3-beta`;
- Web/PWA atual: `https://abnercruz.github.io/Ecosystem/urbe/`;
- APK/Windows: Releases do `AbnerCruz/Ecosystem`;
- Android beta direto usa a chave pública estável registrada em `apps/urbe/tools/urbe-dev.keystore`.

## Antes de instalar no Android

A assinatura direta não é a assinatura da instalação legada. Portanto:

1. abra o Urbe antigo;
2. faça backup/export da cidade/vault e confirme que o arquivo/pasta existe fora do armazenamento que será apagado;
3. só depois desinstale a versão antiga.

Essa perda de atualização in-place é deliberada e foi aceita pelo proprietário.

## Publicar o primeiro release direto

No Ecosystem execute **urbe-cutover**:

- `version = 1.8.3-beta`

O workflow:

1. confere a versão em `apps/urbe/package.json`;
2. confere que o publish aponta para `AbnerCruz/Ecosystem` com prefixo `urbe-v`;
3. cria `urbe-v1.8.3-beta` na `main`;
4. dispara `urbe-release.yml`;
5. o build confere o keystore/certificado beta;
6. publica APK, checksum e instalador Windows no Ecosystem.

Não crie `urbe/v*` e não execute nada em `AbnerCruz/Urbe`.

## DEVICE

Depois da release:

1. com o backup já guardado, desinstale a instalação legada;
2. instale `Urbe-1.8.3-beta.apk` direto do Ecosystem;
3. restaure/aponte para a cidade;
4. confirme abertura, edição, fechar/reabrir e domínio essencial offline;
5. abra `https://abnercruz.github.io/Ecosystem/urbe/` e confirme a PWA;
6. numa release direta posterior, confirmar atualização in-place entre builds assinadas pelo novo certificado.

P4-9 só fecha após a primeira release direta existir e essa migração deliberada ser validada. O repositório antigo não volta a ser pré-requisito nem rollback automático.
