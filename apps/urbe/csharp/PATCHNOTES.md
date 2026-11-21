# Numa — Notas da versão atual

**Versão canônica preparada:** 2.0.0-beta.65
**Canal:** Numa 2.0 nativo, Android e Windows
**Publicação:** pelo workflow oficial depois da integração.
**Atualização:** instala por cima da beta anterior (app.urbe.csharp, mesmo certificado).

## Código livre nos vídeos (#1004)

- **O Assistente pode programar o quadro.** Além das formas e fórmulas, um elemento do vídeo pode ser um pequeno programa que desenha cada quadro. Com isso dá para pedir o que não cabe em formas prontas: simulações, campos de partículas com rastro, fractais, visualizações de dados que se montam na tela.
- **Seguro por construção.** O programa só sabe desenhar: círculos, retângulos, polígonos, linhas, textos, gradientes e brilho. Ele não enxerga suas notas, seus arquivos nem a internet, e cada quadro tem limite de tempo e de memória, para um programa errado não travar o app.
- **O mesmo vídeo sempre.** Os números aleatórios do programa saem iguais em todo quadro e em todo aparelho, então a prévia e o vídeo exportado são o mesmo.
- **Erro com endereço.** Se o programa falha, a revisão do vídeo diz em qual linha, e o Assistente corrige ali.
- **Edite o programa à mão.** No Estúdio, "Editar código" abre o programa do elemento; o app confere o programa antes de salvar, para um erro não estragar o vídeo.
- **Programa direto na cena.** Ao montar um vídeo, o Assistente já pode desenhar uma cena com um programa, sozinho ou por trás do layout. O app testa o programa uma vez antes de gravar.
- **Simulações que não mudam.** Coisas que caem, quicam ou crescem ficam iguais não importa como o vídeo é tocado: pulando, voltando ou exportando, e em qualquer aparelho.
- **Imagens do seu vault.** O programa pode usar imagens do seu vault, desde que o elemento liste quais são. Só valem caminhos de dentro do vault.
- **Janelas mais consistentes.** Os tamanhos de letra das janelas e de boa parte da interface seguem o mesmo padrão do resto da interface.
- **Transições entre cenas.** Agora uma cena pode entrar deslizando, sendo revelada como uma cortina ou com um zoom.
- **Programas que sabem o tamanho da cena.** O programa de um elemento sabe quanto dura a cena, mede o tamanho dos textos, escolhe cores por matiz e tem ajudas para girar e desenhar formas em 3D, como uma esfera de pontos girando.
- **Crie código pelo Estúdio.** Ao adicionar um elemento, a opção "Programa (código)" já vem com um programa de exemplo para você mudar.
- **Flashcards sem janelas.** Crie um conjunto escrevendo só o título na lista; dentro do conjunto, adicione um cartão com frente e verso ali mesmo, e edite ou remova um cartão no próprio lugar. Um conjunto feito por você pode começar vazio.
- **O Assistente usa o que o app já faz.** Quando você pede um vídeo ou um mapa mental, ele usa a ferramenta do app em vez de criar um plugin, a não ser que você peça um plugin.

## Vozes naturais

- **Escolha Dora, Alex ou Faber.** Em "Voz da narração" agora há vozes naturais em português: Dora e Alex (Kokoro) e Faber (Piper). Elas narram os vídeos, o podcast (o segundo apresentador usa a outra voz do mesmo modelo) e os vídeos do Assistente.
- **Baixa uma vez, depois funciona sem internet.** A voz é baixada só quando você a escolhe: 132 MB para Dora e Alex, 67 MB para Faber. O app confere o arquivo antes de usar e o guarda na pasta privada dele, fora das suas notas.
- **Licenças.** As licenças vêm junto do download e aparecem antes de baixar. A pronúncia do português usa o espeak-ng, que é software livre sob GPL-3.0; o código-fonte está em https://github.com/espeak-ng/espeak-ng. Os modelos de voz têm licença própria (Apache-2.0 do Kokoro; CC0 dos dados da voz Faber).
- **O app ficou maior.** O APK cresceu cerca de 26 MB, por causa do motor que roda as vozes no aparelho. As vozes em si não vêm no APK.

## Dados do aplicativo

- **Veja o que o app guarda e onde.** Uma nova tela, "Dados do aplicativo", lista o que o app mantém (configurações, histórico, vozes baixadas e o resto) e em que pasta. A tela só mostra: nada é apagado por ela.

## Não testado no aparelho

- Testado com testes automáticos num computador: 1800 círculos com rastro a 360p levaram cerca de 24 ms por quadro; não medi no seu Android. A esfera 3D do teste de código levou cerca de 56 ms por quadro a 720p, também só no computador.
- O painel de Flashcards sem janelas não foi usado com o dedo num aparelho.
- O app Android compila, mas não foi aberto em aparelho nem emulador, e não reexecutei um pedido real de vídeo com o seu modelo.
- As vozes naturais não foram ouvidas no aparelho, e a velocidade delas num celular modesto não foi medida.
