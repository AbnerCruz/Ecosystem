# Numa — Notas da versão atual

**Versão canônica preparada:** 2.0.0-beta.69
**Canal:** Numa 2.0 nativo, Android e Windows
**Publicação:** pelo workflow oficial depois da integração.
**Atualização:** instala por cima da beta anterior (app.urbe.csharp, mesmo certificado).

- Assistente: corrigido o crash `Argument_AddingDuplicate` ao exibir planos de execução contendo propriedades JSON repetidas; ferramentas e histórico fazem normalização defensiva.
- Painel de etapas: corrigido o crash por reutilização de `StackPanel` já pertencente a outro contêiner visual ao abrir o painel novamente.
- Navegador de estudo no Android: descarte do `WebView` protegido contra callbacks tardios e `ObjectDisposedException` durante fechamento e navegação.
- IA: evita reconstruir o histórico completo quando o Assistente está oculto; a atualização visual de respostas no Android tem cadência ajustada para reduzir travamentos.
- Popup compacto: conversas longas mantêm todas as mensagens, mas renderizam o Markdown progressivamente à medida que se aproxima da área visível.
- Publicações: adia a montagem de capítulos e prévias grandes, reduzindo trabalho síncrono e engasgos ao abrir o Estúdio.
- Incluídos testes de regressão para planos malformados, reabertura de etapas, centenas de mensagens e publicações grandes.
- Ainda é necessária validação de estresse no aparelho físico: encerramentos silenciosos do processo Android não foram reproduzidos e não podem ser declarados eliminados.
