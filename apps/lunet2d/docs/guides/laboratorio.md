# Laboratório 2.0 — central única de validação (LUNET-418)

O novo template **Laboratório** cria um projeto C# normal, offline e pronto para Run. Não edita arquivos de projetos anteriores. O `ProjectStore` e o formato de `lunet.json` não foram alterados.

## Usar no celular

Crie **Novo projeto → Laboratório** e toque em **Run**, sem copiar código. O cabeçalho mostra o nome e número de cada módulo. Toque no cabeçalho ou em **PRÓXIMO** para avançar; **ANTERIOR** volta. Toque em **ÍNDICE** para abrir o menu em duas colunas e escolher qualquer categoria diretamente. **FECHAR** retorna à tela anterior. A navegação cancela o toque retido nos controles.

## Os 12 módulos

1. **Dispositivos:** música com fade, volume, som, vibração, sensores, controle, joystick, botão virtual, pinça e rotação.
2. **Gráficos:** blend, shader, render target, filtros, clipping, pixel-perfect, resolução virtual e área segura.
3. **Câmera:** zoom, giro, conversão entre toque/tela/mundo e HUD fixo.
4. **Sliders:** arraste com dois dedos, intervalos contínuos/discretos, captura e desabilitação.
5. **Botões:** clique ao soltar, arraste para fora, disable e mudança de posição por âncoras.
6. **Cenas:** entidades/componentes, movimento, visibilidade, remover e reanexar.
7. **Depuração:** grade, polígonos, eixos, raios e câmera.
8. **Animação e FX:** SpriteAnimator, Tween para o destino tocado, pausa e ParticleEmitter com explosões.
9. **Tilemap e A***: grade com camadas e obstáculos, busca de rota ao tocar em célula livre, câmera com zoom/rotação e viewport culling.
10. **Colisões:** separação SAT, destino vermelho e corpo verde resolvido por colisão.
11. **Fontes e painéis:** fonte bitmap proporcional e atlas procedural, alteração de escala e NineSlice preservando bordas.
12. **Temas de UI:** `UiTheme.Dark`, `UiTheme.Light` e `UiTheme.HighContrast` oficiais, botão e slider reais com contador e valor.

Os tiles, sprites e fontes de exemplo são gerados em memória, sem arquivos externos. Música e efeitos são os assets locais do template. A página de colisão cobre **SAT geométrico**, não física contínua; TrueType, mundo físico com juntas e editor Tile Studio seguem pendentes porque ainda não existem como recursos completos.

## Roteiro único de DEVICE

Instale a release de desenvolvimento e faça backup dos projetos importantes. Teste sem internet em **Preview rápido** e **isolado**. Visite todas as páginas e confirme renderização e reação dos controles. Em Animação, alterne pausa/explosão; em Tilemap, toque atrás da parede, gire e dê zoom; em Colisões, arraste o quadrado para dentro do obstáculo; em Fontes e Temas, alterne escalas e cores. Navegue pelo índice, use o segundo dedo e teste pausar/retomar. Reabra o laboratório e um projeto antigo, verificando que os dados anteriores permanecem inalterados.

Registre **passou / falhou / não testado** por módulo e informe APK, Android e modo Preview. Falhas visuais, ergonomia, toque e desempenho só são aprovados no aparelho: os testes CI compilam/executam o template, mas não substituem DEVICE.
