# Atlas Forge

Oficina local de mundos esféricos de alta fantasia do Ecosystem. Aplicação independente **Alpha 0.6.0**, com editor HTML/Canvas e visualizador offline. A fonte canônica do aplicativo é este diretório; a versão é `package.json`.

## Executar

Node 22+ é necessário somente para desenvolvimento e build:

```bash
cd apps/atlas-forge
npm ci
npm run build
npm start
```

Abra `http://localhost:4173` ou abra `dist/atlas-forge.html` diretamente no navegador, sem servidor e sem rede. O HTML incorpora CSS, editor e Worker. IndexedDB em `file://` depende do navegador; download/importação JSON é a alternativa disponível. A interface informa quando a cópia local não pode ser verificada.

## Usar

Comece sem conteúdo inserido automaticamente. Gere terreno por seed e resolução explícitas (até 1.440 × 720); explore mapa/globo, altitude, temperatura sazonal, chuva, drenagem, bacias, biomas e intervenções. Pincele elevação/escavação, desfaça/refaça e regenere preservando o relevo autoral. Crie lugares com notas Markdown por cópia e mapas filhos persistentes. Salve JSON, crie marcos e ramificações e exporte um atlas HTML independente.

Zoom ancorado por escala métrica, do planeta até amostras locais ≤1 m, gesto de dois dedos, escala métrica, resolução real, curvas de nível e globo sem textura global limitada. Refinamento explícito preserva pegadas autorais e guarda um marco da grade original. A grade regional de clima/drenagem continua separada: 27,8 km N–S no raio terrestre na maior resolução. O detalhe local é procedural e não significa clima/geologia simulados em metros. Aproxime e afaste com a roda, +/− ou dois dedos. O mesmo terreno permanece visível do planeta ao chão; os atalhos apenas ajustam o zoom.

O build inclui `dist/examples/miravento.world.json`, `Miravento.md` e `miravento.html`: planeta com relevo autoral, castelo, nota, mapa filho e dois marcos. Projetos e documentos permanecem locais, sem acesso automático a pastas externas.

## Biosfera e eras

Use **Terra/Mar** para desenhar limites ou **Novo oceano para desenhar continentes** para começar sem continentes. São pincéis regionais, com altura/profundidade mínima, undo e máscaras persistidas; aproximação métrica mantém o mesmo terreno, mas ainda não permite esculpir cada metro.

Em **Biosfera e eras**, configure espécies e alimentos, salve e introduza a espécie numa região selecionada ou em todos os habitats adequados. As camadas Cobertura vegetal, Biomassa e Nitrogênio mostram os estoques regionais. Simule 100, 1.000 ou 10.000 anos; Cancelar preserva o estado anterior. O seletor de marcos recupera o estado completo antes de cada era. O HTML publicado permite consultar ano e estoques ao clicar no mapa.

## Geografia e cartografia 0.6 — AF-022

**Ao criar um mundo novo**, `spherical-tectonic-2` combina centros de placas em esfera, velocidades angulares, compressão em limites convergentes, riftes em zonas de extensão, flutuação da crosta e ruído de relevo. O nível do mar é escolhido por área na esfera. A distribuição de placas e o relevo são reprodutíveis pela seed; ainda é um modelo tectônico heurístico, **não** simulação geológica profunda validada ou dinâmica de milhões de anos. A geração anterior foi descontinuada: projetos de modelos antigos são recusados explicitamente, sem conversão ou exclusão automática do arquivo original.

O clima novo usa transporte zonal métrico de umidade e precipitação orográfica simplificada, latitude, altitude, influência marítima e demanda térmica. Biomas incorporam a relação água/temperatura, com mosaicos climáticos locais que evitam faixas perfeitamente retangulares. Mesmo assim, não há circulação 3D da atmosfera, solos físicos ou dinâmica climática temporal validada.

O mapa emprega **amostragem nearest-neighbor, sem blur**, rampas discretas de quatro cores por material, sombras de pixels e sprites naturais procedurais de 16×16 ampliados sem filtro. O globo reutiliza quadros estabilizados para reduzir redesenhos. Alterar zoom não regenera o continente. Estes desenhos são estilo pixel art 16-bit; não são sprites industriais feitos à mão. Inspecione alterações reais em testes/screenshot do seu Android antes de avaliar performance final.

## Natureza local e escala humana — AF-021

No mapa natural, a menos de 1,8 m/pixel, **Árvores e pedras** exibe símbolos de copas, arbustos e rochas. Os pontos são derivados determinísticamente da seed, das coordenadas físicas, do material do terreno e da cobertura vegetal regional; não são instâncias persistidas nem indivíduos da ecologia. A camada pode ser desligada para reduzir trabalho de desenho.

**Escala humana** abre um quadro proporcional em zoom próximo, com altura humana convencional de 1,7 m e planta de casa de 10 × 8 m quando cabem no quadro. A referência é uma comparação gráfica, sem sugerir que a altura humana é uma pegada no mapa. O mapa já oferece régua em metros e altitude no inspetor. As coordenadas não alteram snapshots ou exportação JSON.

Materiais locais ainda não representam depósitos minerais reais; as placas são aproximação cinemática estática, não tectônica temporal ou minerais físicos. Química geral, meteorologia dinâmica e organismos individuais continuam pendentes. Validação de toque/percepção em Android físico ainda é necessária.

## Verificar

```bash
npm ci
npx playwright install --with-deps chromium
npm run check
npm run benchmark
npm run benchmark:render
```

`check` executa formatação, testes de domínio, compatibilidade, câmera, terreno e build, build e 14 cenários de regressão mais os fluxos multiescala e de zoom contínuo em Chromium, incluindo editor/viewer por `file://` com rede desativada. Evidências: [entrega inicial](../../docs/proposals/atlas-forge/docs/VERIFICATION.md); os caminhos de código anteriores no relatório são históricos. Android físico continua pendente.

## Documentação e limites

[Tutorial](docs/GUIDE.md), [terreno multiescala](docs/TERRAIN.md), [modelos e equações](docs/MODELS.md), [formato persistido](docs/FORMAT.md), [SPEC](SPEC.md), [roadmap](ROADMAP.md), [arquitetura e incorporação](../../docs/architecture/atlas-forge.md).

O relevo/clima/biomas são heurísticos; drenagem é descida esférica com runoff fixo e bacias fechadas, sem lagos dinâmicos. Eras agora simulam recursos, populações e erosão regional; marcos continuam snapshots de retorno. Tectônica temporal/persistente, atmosfera dinâmica, química geral, magia configurável, civilizações e geradores urbanos completos continuam pendentes. A fixture de importação v1 é sintética; compatibilidade geral exige corpus real.

O [mandato original](../../docs/proposals/atlas-forge/MISSION.md) e a [auditoria inicial](../../docs/proposals/atlas-forge/docs/AUDIT.md) permanecem como proveniência. A incorporação foi aprovada na DEC-0054/ADR-0035; aprovação arquitetural, integração Git e validação em aparelho são evidências diferentes. Nenhuma capability pública ou extração compartilhada é criada. O CI disponibiliza artefatos de revisão; não há release de produção declarada.
