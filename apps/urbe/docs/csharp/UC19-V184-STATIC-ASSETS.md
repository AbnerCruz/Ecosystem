# UC-19 — pacote de sprites 1.8.4-beta da Cidade C#

A UI `apps/urbe/csharp/src/Urbe.UI/wwwroot/urbe.css` no PR Android [#397](https://github.com/AbnerCruz/Ecosystem/pull/397) utiliza seis caminhos relativos `world/v184/*.png`, ausentes na árvore da branch. Sem os arquivos, o WebView renderiza apenas fundos substitutos e deixa vazios os prédios.

Este incremento inclui os **seis arquivos PNG RGBA reais** em `Urbe.UI/wwwroot/world/v184/`, gerados a partir das próprias funções `texture('grass',0)`, `tree('oak',0,false)` e `building(kind,'temperate',variant,false)` do `apps/urbe/src/world/pixel-art.js` original, utilizando um rasterizador de canvas em ambiente de geração. O cliente **não invoca código JavaScript da geração**: a imagem estática é incluída como recurso da Razor Class Library e carregada pelo Android MAUI WebView. Foram preservadas dimensão, paletas, operações da função procedural e estilo pixelado; os polígonos foram gerados com 4× supersampling e **não foi demonstrada equivalência byte-a-byte ao antialias nativo do Canvas2D**. O usuário exige aparência exatamente igual; esse gate continua **pendente** até teste comparativo visual.

| Recurso | Dimensão |
| --- | --- |
| terrain-grass-0.png | 16×16 |
| tree-oak-0.png | 24×32 |
| building-house-temperate-0.png | 48×56 |
| building-house-temperate-1.png | 48×56 |
| building-house-temperate-2.png | 48×56 |
| building-hall-temperate-0.png | 48×56 |

A validação de empacotamento é feita em `tests/web-smoke.mjs`, conferindo assinatura PNG, dimensão, modo RGBA e **publicação efetiva dos seis arquivos em `_content/Urbe.UI/world/v184/`**. Este teste de pacote não constitui versão web do produto e não substitui validação física no Android.

## Integração sem colisões

- PR independente contra `main`, apenas recursos estáticos e teste. Não mexer no `World.razor` ou no `urbe.css` que o agente Android edita em #397.
- O PR #397 deve consumir os mesmos URLs **após** a entrada dos recursos na base comum e reconstruir o APK para carregá-los.
- O PR #421 mantém a implementação do gerador C# de texturas/edifícios para evolução dos cenários; este pacote cobre somente os seis URLs hoje efetivamente referenciados na UI.
- Ausência de release nova; sem alteração da estrutura do vault, permissões SAF, armazenamento, tecnologias de execução, ou protocolos do Hub.
- Próximo passo visual: comparar screenshots da 1.8.4-beta ao mapa Android real; corrigir sprites, disposição, renderização de chunk, zoom, transparências e redimensionamento até paridade; ampliar o pacote conforme os elementos visuais de fato consumidos.
