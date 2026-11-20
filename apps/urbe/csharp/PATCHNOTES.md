# Numa — Notas da versão atual

**Versão canônica preparada:** 2.0.0-beta.64
**Canal:** Numa 2.0 nativo, Android e Windows
**Publicação:** pelo workflow oficial depois da integração.
**Atualização:** instala por cima da beta anterior (app.urbe.csharp, mesmo certificado).

## Flashcards nativos (#1005)

- **Uma biblioteca de Flashcards.** Em Estudo, no alto da lista de fontes, a linha **Flashcards** mostra seus conjuntos e quantos cartões vencem hoje. Ali ficam juntos os conjuntos feitos no app e as notas com linhas `pergunta::resposta`; os cartões das notas continuam sendo o texto delas, sem cópia.
- **Tela própria.** A biblioteca abre em tela cheia: a lista de conjuntos, cada conjunto com seus cartões e o estado de cada um, e a revisão com o cartão grande, **Mostrar resposta** e os quatro botões dizendo quando o cartão volta. No fim, um resumo de quantos você lembrou.
- **Criar, revisar e editar.** Crie um conjunto escrevendo os cartões, revise com Errei, Difícil, Bom e Fácil (a mesma revisão espaçada de antes, com o progresso que você já tinha) e edite ou exclua cartões. Mudar só a resposta mantém o progresso.
- **O Assistente usa a ferramenta certa.** "Faça dois flashcards sobre animais" vira um conjunto da biblioteca numa chamada só. Ele não cria mais plugin, nota ou pasta para isso, e quando a biblioteca não está disponível ele diz isso em vez de improvisar.
- **O conjunto aparece na conversa.** Quando o Assistente cria flashcards, a conversa mostra o conjunto, com quantos cartões tem e o botão **Abrir**, que leva direto a ele na biblioteca.
- **Nada sobra no Explorer.** Os conjuntos ficam guardados dentro da sua pasta de notas, numa área que o Explorer não mostra; vão junto em backups e na cópia da pasta.
- **O guia acha o que se pergunta.** Há um artigo de Flashcards no guia. A busca entende "cartões", "cards" e "memorização" e, quando o guia não cobre um assunto, diz isso em vez de trazer páginas sobre outra coisa.

## Vídeos: movimento procedural (#1004)

- **Um elemento, muitas cópias.** Um elemento do vídeo pode se repetir até 2000 vezes. Cada cópia sabe o próprio número, e as fórmulas de movimento usam isso: estrelas espalhadas, partículas em órbita, grades, ondas e entradas uma depois da outra são um elemento com uma fórmula, e não dezenas de elementos montados à mão.
- **Texto letra a letra.** Um título ou texto pode ser dividido em letras ou palavras: cada uma entra, sobe, gira ou some no seu tempo, sem sair do lugar dela na frase. É o efeito de tipografia animada dos vídeos de abertura, feito com uma fórmula.
- **Brilho, sombra, desfoque e gradiente.** Qualquer elemento do vídeo pode brilhar numa cor, projetar sombra, ficar desfocado ou ser pintado em gradiente, e esses efeitos também se animam: um ponto de luz pulsando, um título que entra saindo do desfoque.
- **Revelar por dentro de uma forma.** Um elemento pode aparecer só dentro de um retângulo, elipse ou círculo da cena; com a forma invisível crescendo, o texto ou a imagem se revela aos poucos.
- **Componentes reutilizáveis.** Um conjunto de elementos (um rótulo com caixa, um cartão de dado) é feito uma vez e usado em várias cenas, cada uso com seu próprio texto. No Estúdio, tocar num componente seleciona o componente inteiro.
- **Câmera por cena.** Cada cena pode aproximar, afastar, deslizar e girar a câmera ao longo do tempo, com o fundo parado: um zoom lento numa fórmula, um passeio de uma parte do diagrama a outra. No Estúdio, tocar e arrastar acerta o elemento onde a câmera o mostra.
- **Efeitos mais leves.** O brilho de formas simples é desenhado direto, e o efeito de cada letra de um título ocupa só a letra: uma cena com 600 partículas brilhando, câmera girando e título animado passou de 1,5 s para cerca de 60 ms por quadro, num computador.
- **Fórmulas mais capazes.** Números aleatórios que saem iguais em todo quadro e em todo aparelho, ruído suave, passos suaves e funções de dois ou três valores (mínimo, máximo, resto, mistura, limite). Uma fórmula pode somar ao movimento ou definir o valor por inteiro.
- **O Assistente sabe usar.** As instruções de vídeo explicam as cópias e as fórmulas, com exemplo, e a lista de cenas mostra quantas cópias cada elemento tem.
- **Revisão sem alarme falso.** Partículas que passam da borda de propósito não contam mais como erro; texto repetido continua revisado como texto.

## Não testado no aparelho

- Testado com testes automáticos num computador: 2000 partículas a 720p são desenhadas e medidas, abaixo de 1 s por quadro no limite do teste; não medi no seu Android.
- Não reexecutei um pedido real de vídeo nem de flashcards com o seu modelo; o caso da beta.60 foi provado com testes automáticos, não medido de novo com o modelo.
