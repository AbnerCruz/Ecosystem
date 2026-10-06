# Catálogo de releases — P4-1

O Hub lê `docs/distribution/current.profile.json` e resolve o canal `github-release` primário de cada Product a partir de `source.repository`, `ecosystem.repository` ou `publicUrl` em `ecosystem.json`, conforme `locationFrom`. `ecosystem.repository` aponta explicitamente para o monorepo e é o valor usado pelos canais diretos atuais de Lunet2D, Urbe e Hub. `target` descreve direção futura e não é um canal operacional. Web não vira release, canal ausente não é presumido e duas entradas primárias não são escolhidas arbitrariamente.

Consulta até 100 releases recentes do repositório, ignora rascunhos, ordena pela publicação e inclui pré-lançamentos identificados. No próprio monorepo, aceita somente tags `<component-id>-v…`, sem escolher a release de outro Product. A consulta do monorepo é reutilizada. A tela mostra até três releases por Product e quatro APKs por release, com o link do canal para consulta e recuperação.

Os metadados do APK vêm da API: nome, URL HTTPS, tamanho não negativo e SHA-256 quando `digest` contém `sha256:` e 64 dígitos hexadecimais. Sidecars de checksum não são APKs. Campos incompletos/URLs inválidas são explicados; checksum ausente ou inválido fica indisponível. **SHA-256 informado não significa bytes baixados/verificados, assinatura conferida ou instalação validada.** P4-1 não instala, baixa nem muda canais de atualização.

Offline continua mostrando o último snapshot marcado como antigo; releases e canais também recebem `Stale`. Cache da Fase 3, sem os novos campos, continua carregando e não fabrica metadados. Sem perfil disponível, o Hub explica a falha em vez de escolher o repositório de origem por conta própria.

## Conferência visual em aparelho

Depois de publicada a build contendo P4-1, instale o APK de desenvolvimento do [canal do Hub](https://github.com/AbnerCruz/Ecosystem/releases), abra com internet e toque **Atualizar**. Confira **Releases e artefatos**, legibilidade de nomes/tamanhos/URLs/checksums e rolagem em tela estreita. Um Product sem canal ou APK deve explicar isso. Depois de uma carga boa, reabra offline: o aviso de último estado conhecido deve permanecer.

A evidência desta conferência está no [handoff de P4-1](../../../docs/governance/handoffs/HO-20261003-p4-1-catalogo-de-releases.json); implementação e testes do Core não a substituem (NN-017). O gate de instalar/atualizar/abrir Products continua sendo o gate da Fase 4 no ROADMAP.
