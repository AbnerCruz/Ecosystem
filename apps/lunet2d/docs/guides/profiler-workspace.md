# Profiler no workspace — Fase 4

1. Abra um projeto ou crie um Laboratório novo. Desligue a internet para conferir o fluxo offline.
2. Aperte **Run**. No Preview, ligue **▥ Perf** e jogue por alguns segundos. A coleta é opcional e usa a janela recente de até 90 quadros.
3. Aperte **Stop**. A IDE abre a aba **Profiler** com a última janela efetivamente medida, horário e indicação de captura histórica. O jogo já parou.
4. Redimensione o painel, role até as últimas linhas e toque **Exportar captura** para escolher um aplicativo de destino. O envio só acontece pela sua escolha no compartilhamento do Android.
5. Alterne entre **Problemas**, **Console** e **Profiler**. Digitar código/receber diagnósticos em segundo plano não deve trocar a aba Profiler. Uma compilação com erro abre Problemas normalmente.
6. Repita com **Preview isolado** nas configurações. A captura volta ao IDE pela mesma área auxiliar de cache usada pelo console; falha/process death não gera captura falsa.
7. Confira pausa/Step, ocultar Perf antes de Stop, Run sem ligar Perf e voltar do segundo plano. Sem amostras reais, o painel explica como coletar; nunca exibe zeros como medição.
8. Salve um layout com a aba Profiler e reabra o projeto. A aba continua selecionada; a captura é temporária e não é salva no projeto ou restaurada após encerrar o processo do app.

A sobreposição ao vivo e o relatório usam o mesmo formatter e as mesmas medições: FPS/cadência de callbacks GL, frame/P50/P95/pior/engasgos, CPU Tick/Update/Draw, draw calls/triângulos, alocações da thread GL, heap .NET do processo, contagens GC desde ligar Perf e estimativa das texturas RGBA8 conhecidas. Pausado continua desenhando e aparece marcado. Captura da execução isolada mede o processo isolado; execução rápida mede o processo da IDE. O texto informa explicitamente que GPU total/tempo e audio underruns não são medidos. A janela não equivale à média de toda a partida.

Este incremento fecha a ausência de superfície do Profiler no workspace, não a auditoria/gate da Fase 4. A validação Android real permanece separada do CI.
