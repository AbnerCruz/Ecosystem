# Urbe — Notas da versão atual

**Versão canônica preparada:** 2.0.0-beta.49
**Canal:** Urbe 2.0 nativo, Android e Windows
**Publicação:** pelo workflow oficial depois da integração; preparada não significa publicada.
**Atualização:** por cima da beta instalada (app.urbe.csharp, mesmo certificado). Não precisa desinstalar.

## Escrever em notas longas sem travar

Em notas grandes, cada letra digitada no modo **Fonte** demorava a aparecer. A causa foi encontrada: as alças de seleção do toque (as "gotas" que aparecem para arrastar a seleção) recalculavam a posição a cada letra, **mesmo escondidas**, e para isso o texto inteiro da nota era refeito três a quatro vezes por tecla.

Agora as alças só acompanham o texto enquanto estão na tela. Elas continuam aparecendo e funcionando do mesmo jeito quando você toca no texto ou seleciona um trecho.

Medido no computador com uma nota de estudo de 14 KB:

- **Fonte:** de 127 para 77 ms por letra (cerca de 40% mais rápido).
- **Visual:** de 33 para 23 ms por letra ao editar um bloco.

O celular é mais lento que o computador, então a diferença lá deve ser maior em tempo absoluto. Ainda resta um custo no modo Fonte em notas muito grandes: o campo de texto do sistema refaz o layout da nota inteira duas vezes por letra. Isso só some com um editor que refaça apenas o parágrafo alterado, que é o próximo passo.

## Publicações: Word, OpenDocument, PowerPoint e apresentação

No estúdio de Publicações, o menu ⋯ ganhou novas saídas, todas feitas a partir das notas como estão agora:

- **Documento Word (DOCX)** e **OpenDocument (ODT)**: títulos de verdade (aparecem na navegação e no sumário), listas e listas numeradas, tarefas, citações, código, tabelas, imagens do vault e links. Livros ganham sumário e cada nota começa numa página nova.
- **Apresentação PowerPoint (PPTX)** editável: cada nota, cada título # ou ## e cada linha `---` começa um slide. Texto, tópicos, tabelas e imagens são objetos do PowerPoint, não fotos. Texto que não cabe continua no slide seguinte, marcado “(cont.)”, em vez de ser cortado.
- **Apresentação HTML**: o layout Slides agora gera uma apresentação de verdade. Setas, espaço ou deslizar trocam de slide; **F** tela cheia; **O** visão geral; **N** notas do apresentador. Um slide cujo conteúdo não cabe é avisado, e a impressão em PDF sai com um slide por página.
- **Notas do apresentador**: escreva dentro de uma caixa `:::notas … :::`. No PowerPoint elas vão para a área de notas; nunca aparecem no slide.
- **Caixas** (`:::tipo título`) saem como caixas em todos os formatos (antes apareciam os `:::` crus).
- **Gráficos** (` ```grafico `) saem como desenho em todos os formatos: vetor no HTML, EPUB e ODT; no Word e no PowerPoint, imagem com o vetor original junto.
- Depois de salvar, o Urbe diz o que o formato não guarda igual (por exemplo, fórmulas vão como TeX editável e links entre notas viram texto).

## Cálculo nas notas

Novo bloco ` ```calculo ` (menu `/` → **Cálculo**): cada linha aparece com o resultado ao lado, e a nota guarda só as linhas.

- Contas com vírgula decimal, porcentagem, fatorial, raiz, π, e funções como média, mediana, desvio-padrão, mdc, mmc e combinação.
- Definições que valem para as linhas de baixo: `preco = 12,5`, `f(x) = x^2 + 1`.
- **Unidades com conversão**: `100 km/h em m/s`, `37 °C em °F`, `2 h + 30 min em min`. Somar metros com segundos dá erro, nunca um número errado.
- Matrizes (determinante, inversa, produto), `derivada de …`, `simplificar`, `expandir`, `resolver … = …` (exato até o 2º grau; acima disso aproximado, marcado com ≈) e `integral de …` (com `de a até b`, o valor).
- O Assistente ganhou a ferramenta **calcular**, com o mesmo motor: faz as contas em vez de estimar.

## Skills do Assistente

Crie uma pasta em `Personalização/Skills/` com um `SKILL.md` (nome, descrição e instruções, no formato aberto Agent Skills) para ensinar o Assistente a fazer um trabalho do seu jeito. Ele vê só o nome e a descrição e carrega o resto quando o pedido combina. Uma skill não dá permissões e nada nela é executado. O Urbe já vem com skills de apresentação, livro e cálculo.

## O que ainda falta conferir no aparelho

Os testes automáticos (502 de interface e 1.414 do núcleo) passam, e os arquivos Word, ODT e PowerPoint foram abertos no LibreOffice. Precisam ser conferidos no moto g32: a digitação no Fonte de uma nota grande, as alças de seleção por toque, salvar cada formato pelo seletor de arquivos do Android, o bloco de cálculo e a apresentação HTML. Abrir os arquivos no Word e no PowerPoint também fica para conferir.
