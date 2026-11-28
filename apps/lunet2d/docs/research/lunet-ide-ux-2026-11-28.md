# UX pós-615 — estudo e implementação inicial (#1062)
Base 616 (84dcac66). O Explorer escrevia Script.cs antes de pedir nome; Rename só troca caminho. O DocumentEditor tinha uma segunda criação imediata. Abas tinham dois alvos 48 dp por item; menu era WrapPanel; apagar usava ícone Close e ação primária. A linguagem visual existente define violeta, azul, Danger, 48 dp e menus verticais. Não alterar a autoridade tokens.json.
## Referências e confronto
- [VS Code UI](https://code.visualstudio.com/docs/editing/getting-started/userinterface): abas de arquivos abertos e navegação rápida, preservar sessão.
- [VS Code #329644](https://github.com/microsoft/vscode/issues/329644) e [#329605](https://github.com/microsoft/vscode/issues/329605): reclamações sobre tamanho e fechamento acidental; separar os alvos e não sobrepor o X.
- [Acode browser](https://docs.acode.app/user-guide/file-browser), [discussão 2170](https://github.com/Acode-Foundation/Acode/discussions/2170): operações contextuais e sincronização após apagar; manter árvore e sessão.
- [Android acessibilidade](https://developer.android.com/guide/topics/ui/accessibility/apps), [cor](https://developer.android.com/design/ui/mobile/guides/styles/color), [diálogos](https://developer.android.com/develop/ui/compose/components/dialog): alvos 48dp, cor semântica, confirmação de ação destrutiva.
## Decisões
Formulário contextual no explorador (não criar arquivo temporário), Enter/cancelar/Back; validar no backend existente, não reescrever classe depois de renomear. Abas inativas em celular fecháveis pelo menu de toque longo; somente ativa mostra X para preservar área sem reduzir alvo. Cores existentes em títulos de seção e ícones funcionais. Não alterar SAF/autosave/preview. Menu vertical inline é uma primeira melhoria, não diálogo modal completo.
## Verificação
Teste headless de criação nominal, cancelamento, erro, colisão e subpasta. Jornada ADB com capturas. Execução local indisponível (sem SDK, dotnet, KVM e rede Git). Resultados CI não confirmados. A revisão visual global e validação em tablet continuam pendentes.
