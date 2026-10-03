# Testar o Ecosystem Hub no celular — hub-v0.0.1-dev.2

Roteiro do gate P3-8, Fase 3 do [ROADMAP](../../../ROADMAP.md). Resultado humano: verificação no [handoff do gate](../../governance/handoffs/HO-20261003-p3-8-gate-no-celular.json). Este roteiro não registra aprovação.

Objetivo: abrir o Hub e compreender o estado de Lunet2D e Urbe sem abrir o GitHub manualmente.

## Preparação

Android 10 ou posterior, aparelho arm64, conexão com a internet para a primeira carga e permissão do Android para instalar o APK pelo navegador.

[Baixar APK](https://github.com/AbnerCruz/Ecosystem/releases/download/hub-v0.0.1-dev.2/ecosystem-hub-0.0.1-dev.2.apk) · [Release e notas](https://github.com/AbnerCruz/Ecosystem/releases/tag/hub-v0.0.1-dev.2)

SHA-256 do APK: `147f42d433ded78b59545fa64adb44cd31613e5af76361813f714f9e04a93731` (arquivo baixado e conferido pelo agente).

## Passos

1. Baixe e instale o APK. Se já usa uma build do Hub, tente atualizar por cima e relate qualquer recusa de assinatura; evite desinstalar antes de informar o erro.
2. Abra **Ecosystem Hub** com internet e aguarde a carga. Toque **Atualizar** se necessário. O status deve indicar a leitura recente; campos sem fonte devem dizer que não estão disponíveis.
3. Confira que Lunet2D e Urbe aparecem com nome, versão, estado e informação de release disponível ou ausência explicada. A leitura do GitHub pode ser parcial por limite da API pública; o motivo deve aparecer nos avisos.
4. Leia **Passado / Agora / Próximo** e os avisos. Confirme que consegue identificar o que já terminou, o que está em andamento e o que vem depois para os produtos, sem precisar abrir o GitHub para entender essas informações.
5. Role a tela e toque **Atualizar**. Confira legibilidade, toques, conteúdo sem ficar sob as barras do Android e ausência de travamento.
6. Depois de uma carga bem-sucedida, ative o modo avião, feche e reabra o app. O último estado conhecido deve aparecer marcado como antigo/offline; toque Atualizar e confirme que a falha não apaga os dados já carregados. Volte a conectar e atualize novamente.

Resultado esperado: instala e abre; permite compreender o estado dos dois produtos; falhas de rede ou dados ausentes são explicadas; a cópia offline é marcada como último estado conhecido. O app só lê dados; não oferece escrita no repositório ou instalação/atualização de outros Products nesta fase.

## Registrar o resultado

Volte ao [portal](https://abnercruz.github.io/Ecosystem/#inbox), localize a validação **P3-8** e escolha **Aprovar** ou **Reprovar**; confirme a Issue preenchida no GitHub. Ao reprovar, informe o aparelho/Android, o passo e o comportamento observado. Aprovar significa que você executou o roteiro e que o gate foi atendido.

## Limites do canal

Esta é uma build de desenvolvimento, depurável, assinada com a chave pública estável decidida para esse canal. A chave permite atualização entre builds, mas não prova autoria; não trate o canal como distribuição estável para terceiros. [Detalhes da assinatura](../../../apps/hub/docs/assinatura-de-desenvolvimento.md).
