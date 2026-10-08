# Desenho de depuração — Laboratório pronto para Run

`DebugDraw.Polygon`, `Ray`, `Axes` e `Grid` mostram a geometria usada pelo jogo, reutilizando SpriteBatch, sua câmera e o clipping. Não mudam colisores, coordenadas, física ou estado do jogo. O teste no aparelho foi adiado pelo proprietário em 2026-10-08; o trabalho automático continua e DEVICE permanece pendente.

## Teste quando estiver disponível

1. Crie **novo projeto → Laboratório → Run**, toque no cabeçalho até **Página 7/7**. Projetos existentes mantêm o código anterior.
2. Veja a grade, contorno amarelo, eixos vermelho/verde e raio. Toque na região central para mudar a direção: apontar através do círculo deve mostrar acerto verde; apontar para fora deve mostrar vermelho.
3. **Girar** altera contorno/eixos; **Espelhar X** reflete o eixo X. A transformação do contorno usa a mesma matriz do framework.
4. **Ocultar debug** remove as linhas e preserva o círculo. **Mostrar debug** restaura a geometria.
5. Ative **Câmera** e toque na área novamente. As formas são transformadas pela câmera; o toque é convertido de tela para mundo e a UI permanece fixa.
6. Segure um botão, pause o aplicativo e volte: soltar não deve executar ação. Um novo toque deve funcionar. Troque de página usando outro dedo enquanto segura um controle; controles ocultos devem cancelar captura.
7. Após a página 7, o cabeçalho volta à primeira. Confira câmera, sliders, botões e cena nas páginas anteriores. Repita offline, Preview rápido e isolado.

## Contratos e limites

- `Polygon`: ReadOnlySpan de pontos, aberto com dois ou mais ou fechado com três ou mais; máximo 4096. Matriz local→mundo opcional, sem preencher/triangular. Formas côncavas são contornos válidos; não são prova de colisão. Pontos repetidos geram segmentos de comprimento zero que não desenham. Entrada não é modificada.
- `Ray`: Ray2D normalizado e trecho de comprimento explícito, zero permitido. Não realiza raycast; para teste de colisão use Ray2D.Intersects ou o motor de física. `default(Ray2D)` e valores não finitos são rejeitados.
- `Axes`: parte do pivô mundial Transform.Position; aplica rotação e escala assinada a eixos locais. Origin não desloca o pivô desenhado. Escala zero colapsa o eixo. Cores X/Y independentes.
- `Grid`: passo X/Y, origem no canto da área, inclui cada borda uma vez e rejeita mais de 4096 linhas antes de desenhar. Área com dimensão zero não desenha. Não recorta desenhos alheios.

Espessuras/passos são positivos e finitos. Argumentos inválidos ou intermediários float inseguros são rejeitados antes de escrever qualquer segmento do helper; desenhos anteriores no lote são preservados. Há margem conservadora nos extremos de float. Os helpers não alocam após aquecer recursos; os callbacks do jogo e a formatação do HUD podem alocar. Use na thread do jogo entre Begin/End. APIs Line/Circle/Rect/Cross existentes preservam sua implementação e seus contratos.
