# Estudo — teclado nativo da IDE Lunet (Teclas · Atalhos · Métodos), 25/11/2026

**Pedido do proprietário** (PR #1040, comentário de 25/11): substituir o teclado de software do sistema durante a edição de código por um **teclado próprio, nativo e integrado à IDE**, com três abas na mesma superfície:
- **Teclas** (padrão): estilo Gboard.
- **Atalhos**: o que já existe em `CodeKeys`/`CodeKeyBar`.
- **Métodos**: catálogo hierárquico por namespace e classe da API real do Lunet e do projeto, com recentes e mais usados no topo.

Trocar de aba não pode perder cursor, seleção nem foco. Estudar antes de implementar; separar teste automatizado de teste físico; não ampliar o escopo.

## 1. Auditoria do código atual

| Fato observado | Onde |
|---|---|
| O editor da casca é um `TextBox` do Avalonia (provisório até a LUNET-533). Cursor, seleção e desfazer são dele. | `Lunet.Client/Views/DocumentEditor.cs` |
| `CodeKeys` já resolve deslize (limiar de 18 dp) e produz uma edição única. Cobre: `{` com bloco indentado, pares que envolvem a seleção, Tab de 4 espaços, setas com coluna preservada. `CodeKeyBar` aplica a edição por `SelectedText`, que é um passo de desfazer. | `Lunet.Client/Shell/CodeKeys.cs`, `Views/CodeKeyBar.cs` |
| `KeyboardFit` + `InputPane`: o app se ajusta ao teclado do sistema e esconde as abas enquanto ele está aberto. | `Lunet.Client/Shell/KeyboardFit.cs`, `LunetShell.cs` |
| O catálogo da API **já é gerado** dos comentários XML do Framework: 144 tipos em 10 namespaces, 1.121 membros (421 métodos, 473 propriedades, 68 construtores…), com assinatura, resumo e parâmetros. `DocsTests` falha se o arquivo estiver desatualizado. Não há catálogo manual a manter. | `docs/api/lunet-framework.json`, `tests/Lunet.Tests/DocsTests.cs` |
| `CodeAnalyzer` (Roslyn) dá conclusão, assinatura, definição e referências do projeto. Os métodos declarados no jogo saem das árvores de sintaxe. | `Lunet.Editor/CodeAnalyzer.cs` |

## 2. Referências e documentação

**Como tirar o teclado do sistema sem perder o foco** (o ponto que decide a arquitetura) — lido no código-fonte do Avalonia:
- `InputMethodManager.TryFindAndApplyClient` não entrega um cliente de texto ao sistema quando `InputMethod.GetIsInputMethodEnabled(focused)` é falso. O controle continua focado, com cursor e seleção. Fonte: [`InputMethodManager.cs`](https://github.com/AvaloniaUI/Avalonia/blob/master/src/Avalonia.Base/Input/TextInput/InputMethodManager.cs), [`InputMethod.cs`](https://github.com/AvaloniaUI/Avalonia/blob/master/src/Avalonia.Base/Input/InputMethod.cs), [API `IsInputMethodEnabledProperty`](https://api-docs.avaloniaui.net/docs/F_Avalonia_Input_InputMethod_IsInputMethodEnabledProperty).
- No Android, `AndroidInputMethod.SetClient(null)` chama `HideSoftInputFromWindow`, ou seja, o teclado do sistema fecha. Fonte: [`AndroidInputMethod.cs`](https://github.com/AvaloniaUI/Avalonia/blob/master/src/Android/Avalonia.Android/Platform/Input/AndroidInputMethod.cs).
- Teclas de um **teclado físico** continuam virando `RawTextInputEventArgs` (e `KeyDown`) no controle focado. Fonte: [`AndroidKeyboardEventsHelper.cs`](https://github.com/AvaloniaUI/Avalonia/blob/master/src/Android/Avalonia.Android/Platform/Specific/Helpers/AndroidKeyboardEventsHelper.cs).
- Relato de comunidade: `windowSoftInputMode="stateAlwaysHidden"` **não** basta no Avalonia ([discussão #14274](https://githubrp.bychv-a.workers.dev/AvaloniaUI/Avalonia/discussions/14274)). O teclado virtual pronto do Avalonia é pago, só no Avalonia Pro ([VirtualKeyboardScope](https://docs.avaloniaui.net/controls/input/text-input/virtualkeyboard/virtualkeyboardscope)).

**Teclados móveis:**
- Gboard: "glide cursor" (arrastar no espaço move o cursor) e "glide delete" (arrastar do apagar para a esquerda apaga palavras). Fontes: [Computerworld](https://www.computerworld.com/article/3048523/gboard-android-typing-shortcuts.html), [Android Authority](https://www.androidauthority.com/gboard-cursor-mode-3641074/).
- AOSP LatinIME: repetição começa em 400 ms e repete a cada 50 ms; toque longo em 300–400 ms. Fonte: [commits do LatinIME](https://gitlab.e.foundation/e/os/android_packages_inputmethods_LatinIME/-/commit/736d740aa5966ad945d0f3ebdb48cebdfdd1ea16).
- FlorisBoard: popups de acentos no toque longo (a → á, à, ä…). Fonte: [florisboard](https://github.com/yashx/florisboard), [issue #2440](https://www.github.com/florisboard/florisboard/issues/2440).
- Toque longo e repetição competem pelo mesmo gesto; a regra geral é repetir só nas teclas sem variantes (apagar, setas, espaço). Fonte: [GNOME OSK](https://gitlab.gnome.org/GNOME/gnome-shell/issues/212), [Eclectic Light](https://eclecticlight.co/2024/03/22/accents-or-repeat-how-is-your-keyboard-configured/?like_comment=94303).

**Editores de código móveis:**
- Swift Playgrounds (iPad): a barra de atalhos mostra conclusões que se inserem com um toque. O autor pode reduzi-la a poucos métodos para iniciantes. Fontes: [Apple — customizing the shortcut bar](https://developer.apple.com/documentation/swift-playgrounds/customizing-the-completions-in-the-shortcut-bar.md), [guia](https://support.apple.com/guide/playgrounds-ipad/itc14b3eb0fa/ipados).
- Unexpected Keyboard: símbolos nos cantos, por deslize ([estudo de 25/11](casca-android-avalonia-2026-11-25.md)).

## 3. Comunidades — problemas reais

- **Avalonia/Android:**
  - Teclado que não reabre depois de fechado à mão ([#15695](https://www.Github.com/AvaloniaUI/Avalonia/issues/15695)).
  - Alças de seleção em campo vazio ([#15694](https://www.Github.com/AvaloniaUI/Avalonia/issues/15694)).
  - Composição de sílabas errada ([#16956](https://github.meowingcats01.workers.dev/AvaloniaUI/Avalonia/issues/16956)).
  - **Um teclado próprio elimina essas três classes de bug na edição de código**: nenhum IME compõe texto no editor.
- **Swift Playgrounds:** a barra de sugestões some ou fica "Suggestions Unavailable"; Tab do teclado físico não percorre as conclusões ([fórum 1](https://developer.apple.com/forums/thread/82338), [fórum 2](https://developer.apple.com/forums/thread/655571)). Lição: o catálogo de métodos não pode depender de um serviço que possa falhar sem aviso; o catálogo do Lunet é local e sempre disponível.
- **Acessibilidade:** o Google avisa que teclados que não são o Gboard podem não funcionar com o TalkBack ([ajuda TalkBack](https://support.google.com/accessibility/android/answer/6006598)). O TalkBack tem modos próprios de digitação ([TalkBack 16.1](https://support.google.com/accessibility/android/answer/16561624)).
- **Editor Android do Godot:** recomenda teclado físico; o que depende de mouse não funciona no toque ([docs](https://docs.godotengine.org/en/latest/tutorials/editor/using_the_android_editor.html)).

## 4. Confronto e decisões

| Questão | Alternativas | Decisão |
|---|---|---|
| Teclado da IDE × IME global | (a) Serviço de IME Android que a pessoa instala e ativa nas configurações. (b) Superfície interna do app. | **(b)**. Não exige instalar nem ativar teclado global, funciona igual no Android e no Windows e não sofre os bugs de composição. (a) exigiria permissões e passos fora do app. |
| Suprimir o teclado do sistema | `stateAlwaysHidden` (não funciona, comunidade) · VirtualKeyboardScope (pago) · `InputMethod.IsInputMethodEnabled = false` no editor | **`IsInputMethodEnabled = false`** enquanto o teclado Lunet está ativo: o editor mantém foco, cursor e seleção; o sistema fecha seu teclado. |
| Teclado físico | Esconder ou manter o teclado Lunet | Com teclado físico conectado (`Configuration.Keyboard = Qwerty` e não oculto), o teclado Lunet some e o físico digita direto. As abas Atalhos e Métodos ficam a um toque numa faixa fina. |
| Acessibilidade | Forçar o teclado Lunet · respeitar o leitor de tela | Cada tecla tem nome acessível. Se um leitor de tela estiver ativo, o padrão volta ao teclado do sistema (compatível com TalkBack), com a troca a um toque. |
| Idiomas/voz/emoji | Reimplementar tudo · tecla "teclado do sistema" | Tecla **🌐 Sistema** devolve o teclado do Android quando a pessoa precisa (ditado, outro idioma). O foco não se perde. |
| Toque longo × repetição | — | **Repetem** (400 ms e depois a cada 50 ms): apagar, setas, espaço. **Popup de variantes** (400 ms): vogais e `c` (á à â ã ä, é ê, í, ó ô õ, ú, ç). Letras não repetem. |
| Gestos Gboard | — | Arrastar no **espaço** move o cursor um caractere a cada ~12 dp. Arrastar do **apagar** para a esquerda apaga por palavra. |
| Desfazer | — | Cada tecla é uma edição: letras por `TextInput` no editor (agrupadas pelo próprio `TextBox`); atalhos como hoje (uma edição). Teclas **Desfazer/Refazer** na aba Atalhos. |
| Latência | Responder ao soltar · ao tocar | Destaque visual ao **tocar**; o caractere entra ao **soltar** (permite deslize e popup, como o Gboard). Sem animação na digitação. |
| Métodos — fonte | Lista manual · catálogo gerado + Roslyn | **Catálogo gerado** (`lunet-framework.json`, embutido) + **métodos do projeto** lidos das árvores de sintaxe do jogo a cada abertura da aba. Nada manual. |
| Métodos — ordem | — | Topo: **Recentes** (mais recente primeiro) e **Mais usados** (contagem, desempate alfabético). Depois o **catálogo**: namespaces → classes → membros em ordem alfabética; o projeto aparece como o namespace "Este jogo". |
| Métodos — assinaturas e sobrecargas | Inserir assinatura com marcadores · nome + parênteses | Cada membro mostra a assinatura curta. Sobrecargas agrupadas numa linha ("+2"), que se expande ao tocar a seta. **Tocar insere** `Nome()` com o cursor dentro dos parênteses (ou `Nome` para propriedades). Depois de `.`, insere só o membro; senão, membro estático vira `Tipo.Nome()`. Sem marcadores para apagar. |
| Foco ao trocar de aba | — | As abas e o teclado são controles **não focáveis**; o editor nunca perde o foco lógico, então cursor e seleção ficam onde estavam. |
| Orientação e tamanho | — | Altura do teclado: 40% da janela, entre 220 e 320 dp, recalculada ao girar. Teclas com no mínimo 40 dp de largura no retrato, área de toque cheia sem vãos. |

## 5. Tarefas consolidadas (verificáveis)

1. `KeyboardLayout` (lógica pura): linhas QWERTY, Shift (toque) e Caps (toque duplo), página de números e símbolos, variantes de acentos, repetição, gestos de espaço e apagar. Testes unitários.
2. `MethodCatalog` (lógica pura): lê o JSON embutido e os métodos do projeto, mais um histórico de uso persistido; ordena as seções conforme a tabela; gera o texto de inserção conforme o contexto do cursor. Testes unitários.
3. `LunetKeyboard` (controle): três abas (Teclas · Atalhos · Métodos) numa superfície; aplica edições no editor; desliga o IME do sistema no editor enquanto ativo; tecla Sistema; esconde com teclado físico. Testes headless de foco e seleção ao trocar de aba.
4. A `CodeKeyBar` deixa de ficar solta: o conteúdo dela é a aba **Atalhos**, mais Desfazer e Refazer.
5. Capturas headless do celular e capturas do emulador Android 13 no CI. **Teste físico pendente**, dito assim.
