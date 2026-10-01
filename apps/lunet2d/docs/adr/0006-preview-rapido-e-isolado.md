# ADR 0006 — Preview rápido e Preview isolado

## Contexto

O spec (§10) pede os conceitos internos **Fast Preview** e **Isolated Preview**, e diz para não prometer hot reload mágico. Um jogo do usuário roda código arbitrário: pode entrar em laço infinito, alocar memória sem parar ou derrubar o processo.

## Decisão

- **Preview rápido** (padrão): o jogo roda no processo do IDE, numa `GameLoadContext` descartável. Inicia mais depressa e permite o Inspector direto nos objetos do jogo.
- **Preview isolado** (opção em Configurações): o jogo compilado é gravado na pasta de cache e roda em `IsolatedPreviewActivity`, num processo separado (`android:process=":preview"`). Se ele travar ou o sistema matar o processo, o IDE continua vivo. O console volta ao IDE por um arquivo de log; uma marca `running` que sobra indica encerramento inesperado, e o IDE avisa.
- Os dois usam o mesmo `PreviewHost` (tela OpenGL, controles Stop/Restart/Pause/Step, Inspector, sensores, toque, teclado e controle) e o mesmo `PreviewRenderer`.
- Mudanças entre Runs são classificadas por `ChangeClassifier` (só corpos × reinício necessário), mas **o jogo sempre reinicia**: não há hot reload real.

## Consequências

- Isolado paga o custo de iniciar outro processo e não compartilha memória com o IDE, então o Inspector edita o jogo dentro do processo do Preview (funciona), mas não há inspeção cruzada.
- Um laço infinito no Preview rápido ainda pode consumir a thread de renderização; no isolado, o usuário sai pelo botão Voltar/Recentes e o IDE segue intacto. Encerrar à força um Preview isolado travado depende do sistema (ANR); um watchdog é trabalho futuro.
