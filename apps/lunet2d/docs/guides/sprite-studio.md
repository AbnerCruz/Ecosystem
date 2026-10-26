# Sprites e animação — primeira entrega pré-beta

Abra um projeto e toque em **Sprites e animação**, abaixo da barra Run.
Escolha **Criar sprite / animação** e 16, 32 ou 64 pixels. O acesso ao editor também aparece em **Arte → Sprites e animação**. Desenhe na grade,
escolha uma cor e alterne lápis, borracha, balde ou conta-gotas.
Cada gesto é salvo no projeto; se falhar, a tela mostra **Não salvo**.
As ferramentas **Linha**, **Retângulo** e **Ret. cheio** usam início e fim do arrasto, com prévia antes de soltar. **Zoom +/−** aproxima a imagem e **Mover tela** arrasta o enquadramento; **Centralizar** restaura. **Grade** alterna as linhas de referência; **Cor ARGB** aceita `#AARRGGBB` (incluindo transparência). **Espelhar H/V** transforma somente o frame ativo.

Use Duplicar para criar o próximo frame, desenhe a diferença e pressione Play.
FPS altera a velocidade; Onion skin mostra o frame anterior. Desfazer/refazer
inclui pixels, frames e FPS. A faixa numerada seleciona o frame.

**Exportar PNG** produz uma spritesheet horizontal em Assets e exibe o trecho
C# para SpriteSheet/SpriteAnimationClip. Exportações posteriores pedem confirmação
antes de substituir o PNG. O PNG é usado pelo jogo sem abrir Studio.

**Importar PNG do projeto** abre uma imagem de até 128 × 128 como primeiro frame,
preservando o arquivo original. Use o Explorer para trazer PNGs externos.

Fonte: `Assets/sprite-<id>.sprite.json`, JSON versão 1 com Width/Height (1–128),
Fps (1–60), Frames (1–64 arrays de pixels ARGB32 sem sinal, linhas da esquerda
para a direita). JSON é autoridade editável/versionável; PNG é exportação derivada.
Não modifica Game.cs nem lógica existente. Feche e reabra o Studio para recarregar
edições externas. Formatos inválidos são rejeitados, nunca migrados silenciosamente.

Teste no aparelho: desenhar → usar linha e retângulo → zoom/pan → selecionar cor ARGB → duplicar → espelhar vertical → Play → exportar; voltar para
Código, reabrir o sprite, mandar o app para segundo plano e conferir a persistência.
Confirme transparência e ordem dos frames no PNG pelo jogo.

Limites desta entrega: sem layers, seleção/laço, rotação, simetria, eventos ou
máquina de estados. Esses requisitos continuam na Fase 5. DEVICE ainda pendente.
