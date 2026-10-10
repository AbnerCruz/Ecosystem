# UC-19 — Contrato de paridade visual EXATA com Urbe 1.8.4-beta

**Ordem expressa do proprietário (10/10/2026): CANCELAR A REFORMULAÇÃO GRÁFICA.** O Urbe C# Android (e Windows posteriormente) deverá usar exatamente a arte da versão **1.8.4-beta**, sem mudanças de cores, sprites, fontes, terreno, vegetação, composição, HUD ou interface. Sprites similares, novos SVGs e "inspirações" não contam. ART-1 da beta.7 foi rejeitada como referência visual.

## Fonte de verdade, sem interpretação artística

- Versão: `apps/urbe/package.json` = `1.8.4-beta`.
- Sprites/biomas/texturas: `apps/urbe/src/world/pixel-art.js` — Git blob SHA `d47cf2902e8aeb5ca2c529d2e4c9617fb71ca931`, sprites Canvas, tiles de 16 px.
- Terreno/geografia: `apps/urbe/src/world/terrain.js` — SHA `7d4a1e88b153a6d942f968572a0260d5154e42ef`.
- Cores e HUD/estilo: `apps/urbe/src/styles/base.css` — SHA `5947d429f8d907947cd0f6484abc640f1a69a14f`.
- UI base/hierarquia: `apps/urbe/index.html` — SHA `4a5ab0065cebd8bbc832cc976bf8a8068072dac3`; além dos arquivos de estilo referenciados.
- Lógica da renderização, escala, pan/zoom, estradas, sprites por `house1/2/3`, posicionamento: `apps/urbe/src/app.js` e módulos `src/world/*`.

Não há liberdade criativa para **nenhum agente** substituir esse material por uma nova direção de arte. Se um sistema ainda não permitir a mesma renderização, manter o item pendente, não inventar outro visual.

## Implementação sem voltar a publicar Web

O aplicativo distribuído continua **C# MAUI Android** e depois Windows. Não construir nova distribuição Web/PWA. O JS antigo **não será o motor do produto**; ele pode rodar exclusivamente em ferramenta **de build offline** para exportar diretamente imagens PNG, preservando os pixels e as variantes oficiais, ou em testes de comparação. O extrator `apps/urbe/csharp/tools/export-legacy-v184.mjs` trava a versão e SHA da fonte e usa o Canvas original do Chromium para gerar texturas e sprites; o app recebe **imagens estáticas**, não o gerador JS.

## Etapas obrigatórias, em ordem

- [~] **LEGACY-1 — Arte original:** exportar PNGs fiéis de `pixel-art.js` (todas as variantes, árvores e texturas), mapear metadado `sprite=house1/house2/house3`, preservar e comparar hashes. Em andamento; não equivale à interface toda.
- [~] **LEGACY-2 — Mundo original:** port C# em andamento, reproduzindo `terrain.js` 1.8.4-beta. Implementados: hash UTF-16, mulberry32, gradientes/FBM, 19 biomas, placas/limites, elevação/mar/clima, priority-flood, lagos, fluxo, spawn original, **segmentos/largura de rios, amostragem contínua por tile e vegetação/decoração original**, com golden tests extraídos do runtime JS para `urbe` e `Cidade Alpha`. Classes `LegacyTerrainMath`, `LegacyTectonicField`, `LegacyElevationClimateField`, `LegacyHydrologyField`, `LegacyWorldSpawn`, `LegacyRiverField`, `LegacyTileSampler`, `LegacyVegetation`. **Ainda faltam** construção da cena/renderizador top-down contínuo, regiões/estradas dos wikilinks, cidadãos, câmera e correspondência visual integral; tabuleiro 8×8 continua provisório. Não substituir a cena visível antes do aceite técnico.
- [ ] **LEGACY-3 — Interface original:** copiar o aspecto de 1.8.4-beta dos painéis, HUD, toolbar, telas, ícones, cores e CSS sem reformular a composição; adaptar apenas os handlers para C#.
- [ ] **LEGACY-4 — Aceite pixel a pixel:** capturas do mesmo cenário no mesmo viewport, densidade e dispositivo (Android horizontal), comparação de cores/pixels, pan, zoom e fluidez. G-C3 fica não aprovado até validação humana do proprietário.

## Bloqueios de qualidade

- Nem CI verde nem APK publicado significam paridade visual.
- Nenhum elemento artístico novo pode ser aprovado como substituto dos arquivos originais.
- Não tocar no vault para alterar estética; preservar `pasta=bairro`, `nota=casa`, metadados `.urbe/mapa.json` e os mecanismos de salvamento C#.
- As imagens geradas no build não são prova de que geometria, HUD, câmera ou renderização inteira já coincidem. **Declarar lacunas e mantê-las abertas.**
- A comparação deve ser feita no Android real antes de marcar UC-19 ou G-C3 como concluídos.

Este contrato **substitui** o plano anterior de reformulação ART-1/ART-4, agora cancelado.
