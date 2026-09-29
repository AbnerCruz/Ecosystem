# ADR 0004 — Assinatura estável das builds de desenvolvimento

## Contexto

O CI gerava o APK em modo Debug, e o .NET for Android cria uma chave de debug nova em cada máquina. Como cada build de CI roda numa máquina limpa, cada release de desenvolvimento saía com assinatura diferente e o Android recusava instalar uma sobre a outra ("o pacote tem um conflito com um pacote já existente"), obrigando o usuário a desinstalar — e perder os projetos, que ficam no armazenamento privado do app.

## Decisão

Assinar as builds de desenvolvimento do **app Lunet** com uma chave fixa, `tools/lunet-dev.keystore` (senha `android`, alias `lunetdev`), versionada no repositório. O CI falha se o APK sair com outro certificado (impressão SHA-256 conferida no workflow).

Esta é uma chave **pública, só de desenvolvimento**: qualquer pessoa pode assinar um APK com ela. Ela protege contra o conflito de atualização, não contra falsificação. Quando existir a distribuição estável, as releases usarão uma chave de release privada, guardada como segredo do CI, e a chave de desenvolvimento não será aceita como canal Stable.

Isto não se aplica aos jogos dos usuários: eles serão assinados com a chave do próprio usuário (Fase 9); o Lunet nunca assina jogos de terceiros com a chave dele.

## Consequências

- Todas as releases a partir desta mantêm a mesma assinatura e atualizam por cima umas das outras.
- Uma única vez será preciso desinstalar a versão anterior (assinada com chaves efêmeras), exportando antes os projetos em ZIP.
- Trocar a chave no futuro exige nova desinstalação; por isso a chave de release privada deve ser definida antes do canal Stable.

## Alternativas

- Segredo do GitHub Actions com a chave: mais seguro, mas não consigo criá-lo daqui e impediria builds de forks/PRs sem o segredo. Adotar quando houver canal Stable.
- Manter chaves efêmeras: descartado, inviabiliza atualizar o app.
