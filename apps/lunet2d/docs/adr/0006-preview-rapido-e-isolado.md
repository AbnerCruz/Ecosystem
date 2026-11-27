# ADR 0006 — Preview rápido e Preview isolado

## Contexto

O spec (§10) pede os conceitos internos **Fast Preview** e **Isolated Preview**, e diz para não prometer hot reload mágico. Um jogo do usuário roda código arbitrário: pode entrar em laço infinito, alocar memória sem parar ou derrubar o processo.

## Decisão atual da IDE Android (27/11/2026)

A IDE Avalonia abre o Preview em tela cheia na `IsolatedPreviewActivity`. O processo `:preview` é dono da superfície EGL e da execução do jogo. O Android entrega o resultado à Activity da IDE, que reabre o mesmo arquivo e importa as últimas linhas do Console. Uma saída sem resultado normal aparece em Problemas com o motivo registrado pelo Android, quando disponível.

A mudança elimina a composição de `GLSurfaceView` dentro de `NativeControlHost`. O [código Android de GLSurfaceView](https://android.googlesource.com/platform/frameworks/base/+/90b04c5170a141034d63debe4e04dcefc8b997f6/opengl/java/android/opengl/GLSurfaceView.java) inicializa EGL antes de invocar o renderer. Uma exceção nessa inicialização ou um crash nativo pode encerrar o processo antes de chegar ao tratamento C# do renderer. A versão 614 ainda hospedava essas etapas no processo da IDE. O relato do aparelho não veio com o rastreamento dessa falha.

Cada execução da casca usa seus próprios arquivos temporários. Recursos e saves apontam para o mesmo projeto. Voltar usa o resultado da Activity, de modo que selecionar uma imagem do jogo ou alternar aplicativos não encerra a execução por engano. A jornada do SDK toca em Rodar, confirma quadros e PIDs distintos, mata somente o processo do preview, confirma a sobrevivência da IDE e abre o jogo de novo para exercitar Voltar.

## Decisão original das telas anteriores

- **Preview rápido** (padrão): o jogo roda no processo do IDE, numa `GameLoadContext` descartável. Inicia mais depressa e permite o Inspector direto nos objetos do jogo.
- **Preview isolado** (opção em Configurações): o jogo compilado é gravado na pasta de cache e roda em `IsolatedPreviewActivity`, num processo separado (`android:process=":preview"`). Se ele travar ou o sistema matar o processo, o IDE continua vivo. O console volta ao IDE por um arquivo de log; uma marca `running` que sobra indica encerramento inesperado, e o IDE avisa.
- Os dois usam o mesmo `PreviewHost` (tela OpenGL, controles Stop/Restart/Pause/Step, Inspector, sensores, toque, teclado e controle) e o mesmo `PreviewRenderer`.
- Mudanças entre Runs são classificadas por `ChangeClassifier` (só corpos × reinício necessário), mas **o jogo sempre reinicia**: não há hot reload real.

## Consequências

- Isolado paga o custo de iniciar outro processo e não compartilha memória com o IDE, então o Inspector edita o jogo dentro do processo do Preview (funciona), mas não há inspeção cruzada.
- Um laço infinito no Preview rápido ainda pode consumir a thread de renderização; no isolado, o usuário sai pelo botão Voltar/Recentes e o IDE segue intacto. Encerrar à força um Preview isolado travado depende do sistema (ANR); um watchdog é trabalho futuro.
