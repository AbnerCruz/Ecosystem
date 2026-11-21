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

## Não testado no aparelho

- Testado com testes automáticos num computador: 1800 círculos com rastro a 360p levaram cerca de 24 ms por quadro; não medi no seu Android.
- O app Android compila, mas não foi aberto em aparelho nem emulador, e não reexecutei um pedido real de vídeo com o seu modelo.
