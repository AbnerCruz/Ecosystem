# Profiler ao vivo no Preview — LUNET-419

Abra um jogo no Lunet e pressione **Run**. Na barra superior do Preview, toque em **▥ Perf**. O painel aparece no canto superior direito, sobre a área de jogo, sem exigir importação, scripts ou conectividade.

O painel informa **FPS**, tempo médio entre quadros (**Frame ms**), tempo de CPU de `GameHost.Tick` (**CPU Tick ms**), submissões reais de `GlesBackend.DrawQuads` (**Draw calls**) e **triângulos por quadro** (dois por quad). Também mostra **bytes alocados por quadro na thread GL**, o heap .NET global consultado pela UI, diferenças de coleções GC 0/1/2 desde que o painel foi aberto e quantos quadros recentes demoraram mais de 33 ms.

A janela circular guarda os últimos 90 quadros; média de FPS usa o tempo real decorrido e não o limite de 60 updates/s. Draw calls/triângulos incluem desenho em alvos fora da tela. O tempo de CPU mede apenas a chamada síncrona de `GameHost.Tick`: **não** inclui tempo de GPU, swap/present, composição Android nem custo separado de Update e Draw. Alocações são da thread de GL, não do processo inteiro. O heap global inclui código da IDE. Não há contador de áudio underrun, memória GPU ou custo de draw pelo driver. Métrica ausente **não** aparece como 0.

**Toque novamente em Perf** para encerrar a coleta. Abrir o Inspector esconde o Profiler. Pausar o Preview mantém o desenho dos quadros, e o painel indica pausa; um único Step executa um passo do jogo. Reiniciar o jogo ou o contexto GL zera a janela. O painel não grava informações, não liga telemetria de rede e não altera `Game.cs`.

## Roteiro Android

1. Instale a development release e execute o **Laboratório 2.0** offline.
2. Abra **▥ Perf** na tela de jogo. Observe FPS e Frame ms após alguns quadros; ao alternar áreas do Laboratório, draw calls/triângulos devem responder à quantidade de sprites (por exemplo, partículas no módulo 8).
3. Abra o Inspector e confirme que Perf desapareceu. Feche-o e reabra Perf; as médias devem reiniciar.
4. Em Perf, pause o jogo, pressione Step e retome. Confira que a indicação de pausa aparece e não há crash.
5. Teste o Preview isolado e o Preview rápido, feche e reabra um projeto antigo e verifique ausência de alterações.
6. Compare em tela com taxa alta (se disponível), em landscape e portrait, especialmente se o overlay cobre botões.

Registre passou/falhou/não testado, modo do Preview, versão APK, Android, comportamento de toque e captura caso haja texto cortado. CI não valida o tempo real, GL ou ergonomia em aparelho. A Fase 4 não é fechada por esta entrega.
