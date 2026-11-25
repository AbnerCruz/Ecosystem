# Estudo — Lunet Avalonia no Android, mobile first (25/11/2026)

Missão do proprietário: "Mobile first, não faça nada sem antes pesquisar e estudar referências (...). Terminar o desenvolvimento integral do Lunet conforme o arquivo" + "quero release". Base: `main` `1b20fd8` (casca `Lunet.Client` fatia 1 integrada, release `dev.1000594` publicada). Plano das ondas: [`../plans/lunet-casca-avalonia-2026-11-25.md`](../plans/lunet-casca-avalonia-2026-11-25.md).

## 1. Auditoria interna

| Fato observado | Onde |
|---|---|
| O Numa já roda Avalonia 12.1.3 no Android com `AvaloniaAndroidApplication<App>` + `AvaloniaMainActivity`, `WindowSoftInputMode = AdjustResize`, tema de janela com a cor de fundo do app e Back por `BackRequested`. | cabeça Android do Numa (`MainActivity.cs`) |
| O Numa aprendeu no aparelho que o Avalonia não afasta a tela do teclado: mede `InputPane.OccludedRect` e só cobre a altura do teclado quando a janela é edge-to-edge (Android 15+); com adjustResize (até 14) somar a altura empurrava o app e deixava uma faixa preta (registros de 16/10 e 22/10/2026). Também espera 220 ms antes de devolver o espaço, porque trocar de campo fecha e reabre o teclado. | `MainView.KeyboardCover/FitKeyboard` do Numa |
| O Lunet Android atual (`io.lunet.studio`) é Debug, sem AOT (Roslyn e o Preview usam reflexão), assinado com a chave pública de desenvolvimento estável (ADR 0004), minSdk 29, só arm64. Projetos ficam na pasta interna do app; a pasta SAF guarda revisões ZIP. | `Lunet.Android.csproj`, `AndroidProjectFolder.cs` |
| O pipeline `lunet2d-release.yml` já instala o workload Android, compila, confere a assinatura e publica um APK por versão. | `.github/workflows/lunet2d-release.yml` |
| A barra de código atual tem 21 símbolos de 40 dp numa fileira rolável (estudo de 24/11, item 2). | `Lunet.Android/Editor` |

## 2. Pesquisa externa (documentação e código)

- **Insets e edge-to-edge:** o Avalonia aplica sozinho o padding de área segura desde a 11.1 (`TopLevel.AutoSafeAreaPadding`), com `SafeAreaChanged` quando as barras mudam; no Android 15, apps com targetSdk 35 desenham sob as barras por padrão. Fonte: [Insets Manager](https://docs.avaloniaui.net/docs/services/insets-manager), [Android — insets](https://developer.android.com/jetpack/compose/layouts/insets).
- **Teclado:** `InputPane` expõe `State`, `OccludedRect` e `StateChanged` no Android; a documentação diz que o Avalonia **não** ajusta a vista nem a rolagem sozinho. Fonte: [Input Pane](https://docs.avaloniaui.net/docs/services/input-pane).
- **Dicas de teclado:** `TextInputOptions`/`InputScope` informam o tipo de teclado no Android. Fonte: [Text Input and IME](https://docs.avaloniaui.net/docs/input-interaction/text-input).
- **Quadros do jogo:** para atualizar muitas vezes por segundo, um único `WriteableBitmap` reutilizado com cópia direta de pixels; alocar por quadro é antipadrão. Fonte: [Performance](https://docs.avaloniaui.net/docs/app-development/performance), [Custom rendering](https://docs.avaloniaui.net/docs/graphics-animation/custom-rendering).
- **Partida a frio:** AOT perfilado é o meio-termo de tamanho e partida no .NET Android; medir por "time to initial display". Fontes: [startup tracing](https://devblogs.microsoft.com/xamarin/faster-android-startup-times-with-startup-tracing/), [Android — launch time](https://developer.android.com/topic/performance/launch-time).

## 3. Comunidades — o que aprovam, rejeitam e pedem

- **Bugs de IME do Avalonia no Android:** teclado que não reabre depois de fechado à mão ([#15695](https://www.Github.com/AvaloniaUI/Avalonia/issues/15695), correção [#15603](https://www.Github.com/AvaloniaUI/Avalonia/pull/15603)), alças de seleção em campo vazio ([#15694](https://www.Github.com/AvaloniaUI/Avalonia/issues/15694)), composição de sílabas ([#16956](https://github.meowingcats01.workers.dev/AvaloniaUI/Avalonia/issues/16956)). Consequência: testar acentos PT-BR com teclado real é obrigatório antes de trocar o app principal.
- **Editor Android do Godot:** a própria documentação recomenda teclado e mouse Bluetooth; usuários relatam ferramentas feitas para mouse que não funcionam no toque (arrastar nós, clique direito). Fonte: [Using the Android editor](https://docs.godotengine.org/en/latest/tutorials/editor/using_the_android_editor.html), [fórum](https://forum.godotengine.org/t/why-cant-i-reorder-or-reparent-nodes-in-godot-mobile-android-editor/140375). Consequência: nenhum fluxo do Lunet pode depender de mouse, clique direito ou arrastar fino.
- **Editores de código no celular (Acode, Spck):** elogiados pelos recursos, criticados por interface "poluída"; teclado extra é diferencial citado. Fontes: [AppBrain — Acode](https://www.appbrain.com/app/com.foxdebug.acodefree), [Spck](https://play.google.com/store/apps/details?id=io.spck).
- **Unexpected Keyboard:** feito para programar no Termux; cada tecla guarda símbolos extras e você desliza na direção do canto. Fonte: [Julow/Unexpected-Keyboard](https://awesome.ecosyste.ms/projects/github.com%2FJulow%2FUnexpected-Keyboard). É exatamente a correção pedida pelo estudo (item 2).

## 4. Confronto com o Lunet

| Pedido do estudo | Decisão desta fatia |
|---|---|
| Casca nova no celular sem perder o app que funciona | APK novo `io.lunet.studio.shell` ("Lunet Nova"), instalável **ao lado** de `io.lunet.studio`; mesma chave de desenvolvimento; publicado na mesma release. Substitui o principal só quando cobrir o fluxo (portão da Onda 1). |
| Teclado não cobre o código | Padrão do Numa: `KeyboardFit` (cobertura só em edge-to-edge, espera de 220 ms) e, com o teclado aberto, as abas e a folha de apoio saem da frente. |
| Barra de código de deslize (3–4 símbolos por tecla) e inserção estrutural | `CodeKeys`: cada tecla tem símbolo principal e até quatro por direção; `{` cria bloco indentado num único passo de desfazer; pares `()`, `[]`, `""`; setas e Tab. Teclas de 48 dp. |
| Nada depende de mouse | Toque e deslize apenas; nenhuma ação em clique direito ou hover. |
| Partida a frio < 1,5 s | Sem AOT nesta fatia (Roslyn por reflexão, igual ao app atual); a medição entra no relatório da Onda 1 com o número real. |

## 5. Tarefas consolidadas (verificáveis)

1. `KeyboardFit` + testes (cobertura, espera) — `Lunet.Client`.
2. `CodeKeys` (resolução do deslize, inserção estrutural) + testes; `CodeKeyBar` no editor em layout compacto.
3. Cabeça `src/Lunet.Shell.Android` (`net10.0-android`, arm64, minSdk 29, chave de dev, projetos na pasta interna, Back, cor de janela).
4. Release: `lunet2d-release.yml` compila, confere a assinatura e anexa `Lunet-Nova-<versão>-arm64.apk` na mesma release.
5. Relatar honestamente: compilado e testado em máquina; não testado em aparelho.
