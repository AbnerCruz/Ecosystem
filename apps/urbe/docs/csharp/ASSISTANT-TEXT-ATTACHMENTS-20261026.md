# UC-63 / Issue #818 — Anexos textuais no chat da IA

Data: 26/10/2026. Cliente: Urbe C# nativo Android / Windows; sem WebView, sem formatos persistidos novos.

## Investigação no código

`AssistantPanel.AttachDeviceFileAsync` já mencionava `.html` e `.htm`, mas limitava **qualquer texto a 96 KiB**, decidia o tipo exclusivamente por uma lista curta de extensões, só aceitava UTF-8 estrito, e um erro de leitura em um arquivo abortava o restante da seleção. O seletor Avalonia não tinha filtro restritivo, portanto atribuir a falha exclusivamente ao Android/Sistema de Arquivos é incorreto.

O envio de arquivos textuais já usa blocos `AiChatBlock` de texto oculto como contexto; arquivos de imagem/PDF usam blocos multimodais. Esses contratos permanecem.

## Correção

- **Todos os arquivos efetivamente textuais** são elegíveis independentemente da extensão: HTML/HTM/XHTML, MD, TXT, CSV/TSV, JSON/JSONL, YAML, XML/SVG, CSS, código-fonte e configurações, inclusive extensões não catalogadas. A extensão, isoladamente, não autoriza um payload binário.
- Decodificação explícita **UTF-8** (com ou sem BOM), **UTF-16 LE/BE com BOM**; conteúdo não textual, controles binários, formatos binários reconhecidos e codificações desconhecidas são recusados com mensagem. Um HTML é **texto inerte**, nunca executado ou renderizado pelo seletor.
- **384 KiB por arquivo textual, 512 KiB somados por mensagem**, com teto de **quatro anexos**. A leitura da URI Android é por `OpenReadAsync`, limitada e compatível com streams sem `Seek`; o arquivo jamais é copiado ao vault. Sem truncamento silencioso: se passar do limite, informar tamanho e permitir dividir o conteúdo.
- Preservados limites/mágicas existentes: imagens (4 MiB), PDFs (8 MiB), ambos com verificação de assinatura. Suas estruturas e compatibilidade com provedores continuam como antes.
- Falha em um arquivo não impede que os outros selecionados sejam anexados. Nome de arquivo é limitado e sanitizado antes de integrar contexto do modelo; conteúdo é declarado não confiável.
- O texto anexado permanece acessível nas mensagens seguintes da mesma conversa para revisões do documento; o orçamento existente do motor de contexto decide quando compactar mensagens antigas. Não há perda automática do anexo após um único turno.
- Corrigido também um problema no preparo da requisição: anexos multimodais de imagem/PDF eram confundidos com resultados de ferramenta e podiam desaparecer antes da serialização para o provedor.

## Verificação

Casos planejados na suíte automatizada:

1. HTML de 150 KiB (antes rejeitado), Markdown, SVG, TSX, JSONL, CSProj e extensões desconhecidas.
2. UTF-8 BOM e UTF-16 BOM, arquivo vazio, cabeçalhos de PDF/ZIP falsificados e dados binários.
3. Leitura em blocos com stream Android não posicionável e corte exato por tamanho.
4. Serialização de contexto, continuidade de texto nos turnos seguintes e preservação de imagens/PDF até o provedor.
5. Sem regressões de imagens/PDF, estados da conversa e demais fluxos de IA.

**Gates:** `Urbe.Core.Tests`, `Urbe.Client.Tests`, `urbe-checks` e `consistency` em CI, mais validação física Android com `ACTION_OPEN_DOCUMENT`/SAF, conteúdo fora do vault, permissões, seletor múltiplo, falha parcial e conversa real. CI headless não comprova esse último gate (NN-017). Não declarar release/dispositivo aprovado sem evidência.

## Referências externas

- Código Avalonia Android `AndroidStorageProvider.OpenFilePickerAsync`: `ACTION_OPEN_DOCUMENT`, `FileTypeFilter=null` gera `*/*`.
- Android Developers, *Open files using the Storage Access Framework*: `https://developer.android.com/guide/topics/providers/document-provider`.
- Avalonia UI, *Storage Items / IStorageFile*: `https://docs.avaloniaui.net/docs/services/storage/storage-item`.

