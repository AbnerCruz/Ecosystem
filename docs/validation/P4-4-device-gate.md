# P4-4 — Gate DEVICE de launcher, updates e uso standalone

> Objeto da validação humana crítica do gate da Fase 4. Este roteiro não altera canais,
> certificados nem permissões. Ele valida os caminhos **já aprovados e realmente disponíveis**.
> P4-3 já provou o mecanismo de instalação; P4-4 prova o fluxo completo por Product e NN-023.

## Builds e canais sob teste

| Componente | Build alvo | Artefato/canal | SHA-256 do APK |
|---|---|---|---|
| Ecosystem Hub | `hub-v0.0.1-dev.7` | release do Ecosystem | `54ea1fdf09d4db87c998c6863a9b96cc0faee9c243a468ad6195830c9645b6e9` |
| Lunet2D | `lunet2d-v0.0.1-dev.1000008` | release primária do Ecosystem | `9e3881cbcc2a9b1d2208410756fbf19631d0922aa59818cce666abc750c70146` |
| Urbe Android | `v1.8.2-beta` | release primária atual em `AbnerCruz/Urbe` | `61487f2bb0a43067e25aefa64c246d3769460a53920d74aa1e08e1cca6ea8d35` |

Baselines para provar atualização sem apagar dados:

- Lunet2D: `lunet2d-v0.0.1-dev.1000004`, APK SHA-256
  `c9a9ebc31a89dce8a87d7d8ddd363235d0057bb97a3a3921ebfafe4607447dec`.
- Urbe: `v1.8.1-beta`, APK SHA-256
  `bbee34a62d793c7158713ca6dbab610a253ccdef095d1fb7c691c6396ab5ae0b`.

O Hub só possui confiança aprovada para Lunet2D (DEC-0032-A). **Não tente contornar isso**:
Urbe sem pin/certificado aprovado deve continuar indisponível para instalar/abrir pelo Hub; seu
canal independente é o comportamento correto neste gate.

## Pré-condições

1. Usar aparelho Android real compatível com os APKs arm64 e com espaço livre suficiente.
2. Não usar projeto/vault único ou irrecuperável. Faça backup antes do teste.
3. Usar a release publicada do Hub dev.7; não instalar o artifact de CI sobre o Hub de uso diário.
4. Manter rede disponível durante as ações do Hub e durante a consulta de release/update do Urbe.
5. Registrar reprovação se qualquer etapa exigir apagar dados para prosseguir.

## A. Lunet2D — instalar, atualizar e abrir pelo Hub

1. Instale diretamente o Lunet `0.0.1-dev.1000004` pelo release primário do Ecosystem.
2. Abra o Lunet sem o Hub, crie ou abra um projeto de teste e faça uma alteração persistida
   reconhecível.
3. Instale/atualize o Ecosystem Hub para `hub-v0.0.1-dev.7`.
4. No Hub, atualize os dados e selecione o Lunet `0.0.1-dev.1000008`. Faça o download.
   O fluxo deve concluir integridade antes de oferecer instalação.
5. Toque em instalar/atualizar, aceite o consentimento do Android e conclua a atualização.
   O pacote deve continuar `io.lunet.studio`; downgrade, signatário incompatível ou bytes
   divergentes devem bloquear, não improvisar.
6. Toque em **Abrir** no Hub. Confirme que o Lunet inicia e que o projeto/alteração do passo 2
   continua íntegro.
7. Desinstale **somente o Hub**. Abra o Lunet pelo launcher do Android e use o projeto novamente.
   O Product deve continuar funcional e seus dados devem permanecer disponíveis.

## B. Urbe — canal atual real e independência do Hub

1. Com o Hub ausente (ou antes de reinstalá-lo), instale o Urbe `v1.8.1-beta` pelo canal
   primário atual em `AbnerCruz/Urbe`.
2. Abra o Urbe e crie/abra um conteúdo de teste persistente (por exemplo uma nota/cidade/vault)
   que permita reconhecer preservação de dados.
3. Acione o mecanismo atual de atualização baseado na latest release do GitHub e avance para
   `v1.8.2-beta`. Se a build não conseguir percorrer o mecanismo declarado, **reprove o gate**
   em vez de substituir silenciosamente o caminho por outro.
4. Reabra o Urbe, confirme a versão alvo e confirme que o conteúdo do passo 2 permanece íntegro.
5. Reinstale/abra o Hub dev.7 apenas para conferir a fronteira: Urbe **não** deve ser oferecido
   como instalável/abrível pelo Hub enquanto não existir confiança específica aprovada.
6. Desinstale novamente somente o Hub e abra/use o Urbe pelo launcher. O domínio essencial do
   Urbe deve continuar funcionando e os dados devem permanecer disponíveis.
7. A publicação Web/PWA continua independente do Hub; não é substituta da validação Android
   acima, mas sua disponibilidade não pode depender do Hub.

## Critérios de aprovação

Aprovar somente se **todos** forem verdadeiros:

- Lunet foi atualizado pelo Hub, abriu pelo Hub e preservou o projeto.
- Lunet continuou abrindo e funcionando depois da remoção do Hub.
- Urbe foi instalado/atualizado pelo canal atualmente declarado e preservou o conteúdo.
- Urbe continuou abrindo e funcionando depois da remoção do Hub.
- O Hub recusou/omitiu corretamente instalação/abertura do Urbe sem confiança aprovada.
- Nenhum passo exigiu apagar dados, burlar assinatura, trocar canal implicitamente ou conceder
  permissão além das já previstas.
- A experiência observada corresponde aos limites documentados; ausência de Hub não degrada o
  domínio essencial de nenhum Product.

Qualquer falha deve ser registrada como **Reprovar** no portal, com comentário curto indicando
o passo e o comportamento observado. O gate permanece aberto até nova evidência DEVICE.
