# Sliders de UI

## Testar sem escrever código

**Depois que esta entrega estiver publicada**, crie um **novo** projeto do modelo **Laboratório** e toque em Run. Toque três vezes na barra de página no topo para chegar à **página 4/4**. Projetos Laboratório criados antes desta atualização conservam seu código original.

1. Arraste a primeira barra: o raio do círculo muda continuamente. Arrastar além das extremidades mantém o valor no mínimo/máximo.
2. Arraste a segunda: o valor muda em passos de 10 e altera a cor do círculo.
3. Toque em Desabilitar passos. A segunda barra fica cinza e não responde; a primeira continua funcionando. Reabilite e repita.
4. Use um dedo em cada barra: cada slider acompanha seu próprio dedo. Solte um e continue arrastando o outro.
5. Segure uma barra e envie o app para segundo plano. Ao voltar, a captura antiga foi cancelada; o último valor foi mantido. Faça um toque novo.
6. Troque de página e volte; as barras deixam de acompanhar toques quando ocultas. Verifique também as páginas de dispositivos, gráficos e câmera.
7. Repita offline nos modos Preview rápido e isolado e execute um projeto anterior.

Registre passou/falhou/não testado, versão do APK, aparelho/Android e modo de Preview. O exemplo é parte do template, não exige copiar código desta documentação. Nenhuma validação Android é inferida de testes portáteis.

## API para desenvolver

`Lunet.UI.TouchSlider(bounds, minimum, maximum, value, step, knobWidth)` cria o controle. O intervalo é finito e estritamente crescente. Step zero é contínuo; um passo positivo quantiza a partir de Minimum, com empate para o passo superior. Minimum e Maximum são sempre alcançáveis, mesmo quando o passo não divide a faixa; o último intervalo pode ser menor. Valor/limites da geometria usam precisão float, com cálculo do intervalo em double.

`Value` permite atribuição por código, limitada/quantizada, sem publicar um novo evento de input. `NormalizedValue` fornece a fração 0–1. `WasChanged` só é verdadeiro no Update em que o toque alterou o valor final; o próximo Update limpa. Mover dentro do mesmo passo não gera mudanças repetidas.

Atualize uma vez por passo com `Update(Input)`, inclusive desabilitado/oculto, para acompanhar o histórico de dedos. O primeiro Pressed novo dentro de Bounds captura seu ID; entrar arrastando de fora não ativa. Released aplica a posição final e solta. Arrastar fora mantém a captura e limita o valor. Cancelled, ausência, fase desconhecida ou posição não finita cancelam sem modificar o último valor. Cancel/disable não desfazem mudanças anteriores.

`Bounds` é a área de toque no espaço virtual do input. O percurso do centro do cursor vai de `Bounds.X + KnobWidth/2` a `Bounds.Right - KnobWidth/2`; os extremos representam Minimum/Maximum. Largura menor ou igual ao cursor, ou altura zero, impede interação. `GetKnobBounds()` informa o cursor atual; em áreas estreitas sua largura é reduzida. Altere Bounds antes de Update ao recalcular layout.

Chame `Cancel()` em OnPause/ao ocultar. O histórico de IDs evita recapturar Pressed retido; uma instância nova não conhece snapshots anteriores ao seu primeiro Update. Os snapshots do host não reconstroem um ciclo inteiro de toque substituído antes do próximo Update.

`Draw(batch, TouchSliderStyle.Default)` desenha trilho, preenchimento e cursor entre Begin/End, sem fonte ou recursos próprios. Uma paleta personalizada pode alterar cada estado; default(TouchSliderStyle) é transparente. Zero área não desenha. Câmera/clip do SpriteBatch afetam somente o desenho; o jogo mantém geometria e input no mesmo espaço.

Não há consumo de input, roteamento/z-order, foco, teclado/gamepad, orientação vertical, tooltip/rótulo automático nem tema global. Controles sobrepostos exigem escolha explícita do jogo. Update/Draw da API não alocam após criar o controle/recursos e aquecer o caminho; textos interpolados do Laboratório alocam e não fazem parte dessa garantia.
