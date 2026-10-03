# Assinatura de desenvolvimento do Hub

Decisão do proprietário: **DEC-0029, alternativa A** (registro em `docs/governance/responses/DEC-0029.md`). O canal das releases é a **DEC-0028, alternativa A**: GitHub Releases públicas deste repositório, tag `hub-vX.Y.Z-dev.N`, como pré-lançamento.

## O problema

O .NET para Android cria uma chave de depuração nova em cada máquina. Cada build de CI roda numa máquina limpa, então cada release sairia com assinatura diferente e o Android recusaria atualizar uma sobre a outra ("o pacote tem um conflito com um pacote já existente"), obrigando a desinstalar.

## O que foi decidido

Assinar o APK do **canal de desenvolvimento** com uma chave fixa e **pública**, versionada em `apps/hub/tools/hub-dev.keystore` (PKCS12, alias `hubdev`, senha `android`). A impressão digital SHA-256 esperada do certificado fica em `apps/hub/tools/hub-dev.keystore.sha256`, que é a **única autoridade** dela (NN-001): os workflows `hub-ci` e `hub-release` leem esse arquivo e **falham** se o APK sair com outro certificado.

## O que isto NÃO é

- **Não é segredo.** Qualquer pessoa pode assinar um APK com esta chave. Ela protege contra o conflito de atualização, **não contra falsificação**.
- É uma **exceção declarada** ao MANIFEST §30.2 (signing keys nunca commitadas), limitada a uma chave pública de desenvolvimento e ao canal de desenvolvimento (pré-lançamento).
- **Não vale para um canal estável nem para distribuir o Hub a outras pessoas.** Antes disso é preciso uma nova decisão do proprietário: chave de release **privada**, guardada como segredo do CI e cadastrada por ele (o agente nunca vê o valor).

## Consequências

- Todas as releases de desenvolvimento passam a atualizar por cima umas das outras. Uma única vez será preciso desinstalar uma versão anterior assinada com chave efêmera (o Hub não guarda dados do usuário: só lê e mantém um cache).
- Trocar a chave no futuro (migração para a chave privada) exige nova desinstalação.
- O Hub continua sendo só leitura: o APK não grava nada no repositório e não embute token.
