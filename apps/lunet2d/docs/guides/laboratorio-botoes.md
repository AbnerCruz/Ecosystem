# Botões no Laboratório

Após esta entrega estar publicada, crie **um novo projeto → Laboratório → Run** e toque no cabeçalho até a página **5/5**. O código já vem no projeto. Projetos existentes mantêm sua versão anterior.

1. Pressione **Somar clique**: o fundo muda enquanto segura; o contador só aumenta quando solta dentro.
2. Arraste para fora e solte: não conta. Arraste para fora, volte e solte dentro: conta uma vez.
3. Toque **Desabilitar**: Somar clique fica cinza e não conta. Toque **Habilitar** para voltar.
4. Toque **Mover para baixo**: Somar clique aparece abaixo. Toques na posição antiga não contam. Use **Mover para cima** para voltar. Seu retângulo vem de `LayoutRect.Fixed`, centralizado na largura virtual.
5. Segure Somar clique, pause o Preview e retome: a soltura antiga não conta. Um novo clique funciona.
6. Troque de página e percorra as cinco páginas: dispositivos, gráficos, câmera e sliders continuam funcionando. Volte aos botões e confira o contador.

Repita offline nos Previews rápido e isolado. Aparência, GL, sensação de toque e pausa do Android dependem da validação no aparelho; a suíte portátil verifica o fluxo do código. O HUD formata texto; a garantia de zero bytes é da API TouchButton.Update/Draw, após aquecimento.
