# Projetos de referência para regressão (LUNET-451)

Estes projetos são pastas Lunet **reais e versionadas**, não screenshots, mockups ou templates efêmeros. Servem para detectar regressões de compilação, carregamento, Preview e transporte ZIP. Abrir diretamente pelo importador de projetos (ZIP) ou por uma cópia de cada pasta.

- `portrait/`: orientação 360 × 640; criação/desenho procedural no Framework e logging.
- `landscape/`: orientação 640 × 360; input por toque e viewport horizontal.
- `full-stack/`: sprite editável `.sprite.json` + PNG RGBA, tilemap JSON v1 com duas camadas e colisão, WAV PCM, simulação Box2D, botão UI e contador de abertura persistido por `Save`.

## Prova automática

`dotnet test --project tests/Lunet.Tests --filter FullyQualifiedName~RegressionProjectTests`

Cada pasta é aberta com `ProjectStore`, compilada por `GameCompiler` (Roslyn), instanciada por `GameLoader` e executada por `GameHost` sobre `RecordingBackend`; a suíte confere desenho, logs, texturas, formatos de asset, saves e ZIP round-trip com igualdade de bytes. Isso não prova GPU, tela física, áudio audível, toque real nem instalação Android.

## Aceitação em aparelho — pendente

Importar cada pasta ZIP individualmente pelo Lunet, abrir `Game.cs`, executar o Preview e gerar APK **offline**. Instalar os três sem desinstalar o anterior; conferir orientação, visual, toque, som e persistência no `full-stack`. Fechar/abrir o jogo e comparar contagem de lançamentos; reiniciar o Lunet e reabrir os projetos. Em caso de falha, registrar versão, Android, projeto, etapa e log. Não marcar DEVICE como aprovado antes desse roteiro.

Assets: PNG e WAV sintéticos produzidos para a suíte, sem conteúdo de terceiros; fontes JSON preservadas e texturas/sons binários são arquivos do próprio projeto.
