# Inspector e Desempenho na casca (LUNET-536 M6), 25/11/2026

Estudo antes de migrar o `InspectorPanel` e o Profiler das telas anteriores para a casca Avalonia.

## O que existia

- `InspectorPanel` (Android Views): lê `ObjectInspector.Build(game)` a cada 300 ms e grava as edições por `Game.Dispatcher`. Mostra caixa de seleção, lista de opções, texto, controle deslizante para `[Range]` e escolha de arquivo.
- Profiler: só mostrava a **última captura** do Preview, depois do Stop, em texto. Durante o jogo não havia gráfico nem números.

## Referências

- **Godot 4 (Remote tree e Monitors):** enquanto o jogo roda, a árvore Remote mostra e edita os valores do jogo em execução. Os Monitors registram mesmo com a aba fechada, então dá para abrir depois e ver o que aconteceu ([debugger panel](https://docs.godotengine.org/en/4.4/tutorials/scripting/debug/debugger_panel.html), [overview](https://docs.godotengine.org/en/4.0/tutorials/scripting/debug/overview_of_debugging_tools.html)).
- **Unity:** um único quadro acima do orçamento já causa engasgo, por isso a média esconde o problema. No celular, o calor exige folga além do orçamento ([best practices for profiling](https://unity.com/how-to/best-practices-for-profiling-game-performance)).
- **Roblox MicroProfiler:** primeiro você acha o pico no gráfico de tempo por quadro, depois toca na barra para ver a causa ([MicroProfiler](https://create.roblox.com/docs/performance-optimization/microprofiler)).
- **Ferramentas com linhas de referência:** linhas em 16,6 ms (60 fps) e 33,3 ms (30 fps). O PlayCanvas Mini Stats usa uma marca d'água no orçamento ([mini stats](https://developer.playcanvas.com/user-manual/optimization/mini-stats/)). Um defeito no Profile Analyzer do Unity mostrou que rótulos ambíguos como "2 60Hz" confundem, por isso os rótulos daqui dizem "60 fps · 16,7 ms".

## Decisões

1. **Valores do jogo que roda, não do código.** A mudança vale até o jogo parar, e o painel diz isso. Igual à árvore Remote do Godot.
2. **Pausado, a edição aparece na hora.** A casca redesenha sem avançar o tempo (`GameSession.Redraw`, `Tick(0)`), assim você ajusta e vê sem soltar o jogo.
3. **Um quadro, uma barra.** As barras ficam verdes até 16,7 ms, amarelas até 33,3 ms e vermelhas acima disso. O resumo mostra média, 95% e pior quadro. Tocar numa barra mostra Update, Draw e passos.
4. **O histórico enche com o painel fechado:** 300 quadros, cinco segundos a 60 fps.
5. **Honestidade de medida:** o tempo é de CPU nesta tela, onde o quadro é rasterizado em CPU, sem esperar a GPU. Os tempos da GPU entram quando existir a prévia GL embutida (M5).
6. **Celular:** os dois painéis abrem numa gaveta de meia tela, com o jogo acima, e ficam um toque adiante na barra do jogo.

## Fora deste passo

- Escolha de arquivo e recurso nos campos `[File]` e `[Asset]`: por enquanto se digita o caminho.
- Árvore de objetos aninhados recolhível.
- GPU e memória.
