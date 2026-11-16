# Motores e bibliotecas do Urbe: licença, plataformas e tamanho (CST-190, 16/11/2026)

A matriz lista o que o cliente C# **incorpora hoje**. Licenças e tamanhos foram lidos dos pacotes (`.nuspec` e cache NuGet), não de memória. O tamanho é o do pacote inteiro, com todas as plataformas. O peso real no APK é menor; o APK de prévia do CI tem 62,8 MB. Uma referência estudada não vira dependência aprovada.

| Biblioteca | Para quê | Licença (pacote) | Android | Windows | Offline | Pacote |
|---|---|---|---|---|---|---|
| Avalonia 12.1.3 (+ Android, Desktop, Fluent) | Interface nativa | MIT | sim | sim | sim | 45 MB |
| SkiaSharp 3.119.4 | Desenho, PNG do infográfico, rasterização | MIT | sim | sim | sim | 34 MB |
| CSharpMath 1.0.0-pre.2 (+ Rendering) | Fórmulas | MIT | sim | sim | sim | 3 MB |
| PdfPig 0.1.16 (+ Rendering.Skia 0.1.16.6) | Leitura e páginas de PDF | Apache-2.0 | sim | sim | sim | 72 MB |
| VersOne.Epub 3.3.6 | Leitura de EPUB | Unlicense | sim | sim | sim | 2,5 MB |
| LLamaSharp 0.30.0 + backends CPU | IA local (modelo no aparelho) | MIT | sim (arm64: 62 MB nativos) | sim | sim | 150–156 MB por backend |
| ModelContextProtocol.Core 2.4.0 | Cliente MCP (ADR-0048) | Apache-2.0 | sim (servidores remotos) | sim | não (servidores externos) | 12 MB |
| Microsoft.Web.WebView2 1.0.4314.35 | Navegador de estudo no Windows (ADR-0047) | Licença Microsoft no estilo BSD (redistribuição permitida) | não usa | sim | depende da página | 58 MB |

**Recursos da plataforma, sem pacote:**
- **Android:** TextToSpeech (podcast e narração), MediaCodec (vídeo e decodificação de áudio) e reconhecimento de fala.
- **Windows:** SAPI (voz).
- **FFmpeg:** usado no computador só se a pessoa o tiver instalado; não é distribuído pelo Urbe. Fica fora do pacote do app justamente por causa das licenças LGPL/GPL das compilações usuais.

## Avaliados e não incorporados

- **Quarto, Pandoc, Paged.js, Vivliostyle:** as exportações (EPUB, DOCX, ODT, PPTX, PDF) são escritas em C# no próprio Urbe, sem runtime externo. Não há Node, Deno nem Haskell no aparelho.
- **Motion Canvas, Remotion:** dependem de Node e de um navegador para renderizar. O vídeo do Urbe é desenhado pelo próprio renderizador (SkiaSharp) e codificado pelo MediaCodec ou pelo FFmpeg (CST-170 a 172).
- **Runtime WebAssembly para executar código (CST-201):** em avaliação na ADR-0051, com suporte Android não confirmado. Nada incorporado.

## Critérios para incorporar algo novo

Licença que permita distribuição comercial; Android e Windows; uso a partir de C#; execução offline quando o recurso é local; custo no APK e na memória do moto g32; manutenção ativa; adaptador desacoplado, para poder trocar a biblioteca.
