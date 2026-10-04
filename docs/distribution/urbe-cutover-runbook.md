# P4-9 — corte de distribuição do Urbe

Este roteiro executa somente as operações de tag/release já autorizadas por ADD-0015 / DEC-0031 / ADR-0019. Ele não troca chave, não apaga o canal antigo e não conclui o gate sem validação DEVICE.

## Estado atual

- a versão-ponte é `1.8.3-beta`;
- o espelho `AbnerCruz/Urbe` já contém a árvore da ponte sincronizada a partir do Ecosystem;
- o canal antigo ainda não publicou `v1.8.3-beta`;
- ainda não existe release direto `urbe-v*` no Ecosystem;
- `urbe-cutover.yml` existe para evitar criação manual e propensa a erro de tags pelo celular.

## Etapa A — entregar a ponte pelo canal antigo

1. No Ecosystem, execute **urbe-cutover** com:
   - `stage = legacy-bridge`
   - `version = 1.8.3-beta`
2. O workflow só aceita a versão canônica do `package.json`, confere a configuração do feed e cria `urbe/v1.8.3-beta` no commit atual da `main`.
3. Execute **sync-from-ecosystem** no repositório `AbnerCruz/Urbe`. O script canônico detecta a nova tag, materializa a árvore daquele commit como `v1.8.3-beta` na origem e dispara `release.yml`.
4. Verifique que o release legado contém APK/EXE `1.8.3-beta` e foi assinado pela identidade histórica. Nenhum release direto é permitido antes desta etapa.

## Etapa B — primeiro release direto

1. Depois da ponte publicada, incremente a versão do Urbe para a próxima beta e mantenha CHANGELOG.
2. Integre a alteração normalmente.
3. Execute **urbe-cutover** com:
   - `stage = direct`
   - `version = <versão nova>`
4. O workflow cria `urbe-v<versão>` e dispara explicitamente `urbe-release.yml` sobre a tag. O pipeline falha fechado se a chave Android histórica não estiver disponível.
5. Verifique artefatos, assinatura, package id e versionCode.

## Etapa C — DEVICE e corte do perfil

Em aparelho com a versão anterior instalada:

- atualizar pelo canal antigo para 1.8.3-beta;
- a partir da ponte, detectar e instalar a versão direta;
- confirmar que o mesmo vault abre íntegro e continua editável;
- fechar/reabrir, testar offline no domínio essencial e conferir que o Hub não é requisito.

Só depois da evidência humana:

- trocar o canal primário do Urbe em `current.profile.json` para `ecosystem.repository`;
- manter o repositório antigo como recuperação/histórico;
- encerrar P4-9 e, se nenhum item da fase estiver aberto, aprovar o gate da Fase 4.

## Falha segura

Se assinatura, versão, tag, artefato, updater ou vault divergirem, não cortar o canal. Não publicar APK de teste com chave diferente. Não reescrever tags existentes.
