# UC-19 — Critérios de aceite gráfico do Urbe C# Android

**Estado: em desenvolvimento, não aprovado.** UC-19 / fase M3; gate G-C3. O tabuleiro de 64 lotes e os botões do beta.6 não são o mundo definitivo. Diretriz do proprietário: **Android primeiro, desktop depois, sem distribuição Web**.

## Referência de arte

O Urbe antigo utiliza pixel art medieval, tiles de trabalho de **16 px**, terrenos com biomas/transições, profundidade, vegetação e edifícios distintos. A referência de código/visual é `apps/urbe/src/world/pixel-art.js`. **Não transferir o runtime JS para o novo cliente C#.** Os SVGs estáticos da fatia ART-1 reproduzem a paleta e a linguagem visual, não a qualidade ou a amplitude do motor anterior.

## Etapas obrigatórias de UI da Cidade

- [~] **ART-1 — Primeira identidade gráfica:** eliminar casas geométricas de CSS e lotes com aparência de cards; sprites pixelados incorporados localmente, variedades determinísticas de casas, bairro, terreno, rua e vegetação; não carregar imagens remotas. Aguardar inspeção no celular real.
- [ ] **ART-2 — Reconstrução real do mundo:** terreno contínuo, desenho top-down 2D com biomas e detalhes, ruas entre notas/bairros, vegetação e personagens/eventos, variedade artística e escala espacial comparável ao Urbe anterior. Investigar solução nativa C# para cenário/câmera sem reinstalar JavaScript do produto.
- [ ] **ART-3 — HUD e interação:** câmera, pan, zoom, foco/seleção, construção, navegação e acessibilidade otimizados para celular **horizontal**; desempenho não pode depender de milhares de controles de tela.
- [ ] **ART-4 — Polimento e aceite:** comparar capturas do Urbe antigo com Android C# na mesma orientação; revisar legibilidade, acabamento e identidade com o proprietário; executar roteiro de toque em aparelho físico e medir desempenho real. Nenhum orçamento numérico de FPS é aprovado sem baseline.

## Condições para fechar UC-19 / G-C3

1. Não marcar UC-19 como concluído por compilação verde, ruas/cards decorativos ou funcionamento da gravação: exige paridade de **qualidade visual** e toque validado.
2. Manter `pasta=bairro`, `nota=casa`, escrita reversível e a recuperação de posições em `.urbe/mapa.json`, sem mutações de vault só pela aparência.
3. Sprites locais incorporados à biblioteca `Urbe.UI`, sem scripts/recursos remotos; interações reais controladas pela sessão C#.
4. Testar e registrar capturas comparativas no Android físico, especialmente paisagem, pan, zoom, construção, abrir nota e conforto de uso. Sem esse teste, gate **G-C3 não aprovado**.
5. A versão Web antiga é apenas a **referência** de identidade artística e comportamento; não será publicada como novo cliente.

A fatia ART-1 fica rastreada em `Urbe.UI/wwwroot/world/`, `WorldArt.cs`, `World.razor` e `urbe.css`. O novo visual ainda é **provisório** até ART-2/ART-4.
