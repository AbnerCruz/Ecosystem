# Atlas Forge

Oficina local de mundos esféricos de alta fantasia do Ecosystem. Aplicação independente **Alpha 0.4**, com editor HTML/Canvas e visualizador offline. A fonte canônica do aplicativo é este diretório; a versão é `package.json`.

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

Zoom ancorado por escala métrica, do planeta até amostras locais ≤1 m, gesto de dois dedos, escala métrica, resolução real, curvas de nível e globo sem textura global limitada. Refinamento explícito preserva pegadas autorais e guarda um marco da grade original. A grade regional de clima/drenagem continua separada: 27,8 km N–S no raio terrestre na maior resolução. O detalhe local é procedural e não significa clima/geologia simulados em metros. Use Escala → Chão sobre um ponto de terra.

O build inclui `dist/examples/miravento.world.json`, `Miravento.md` e `miravento.html`: planeta com relevo autoral, castelo, nota, mapa filho e dois marcos. Projetos e documentos permanecem locais, sem acesso automático a pastas externas.

## Verificar

```bash
npm ci
npx playwright install --with-deps chromium
npm run check
npm run benchmark
npm run benchmark:render
```

`check` executa formatação, 24 testes de domínio, compatibilidade, câmera, terreno e build, build e 14 cenários de regressão mais o fluxo multiescala em Chromium, incluindo editor/viewer por `file://` com rede desativada. Evidências: [entrega inicial](../../docs/proposals/atlas-forge/docs/VERIFICATION.md); os caminhos de código anteriores no relatório são históricos. Android físico continua pendente.

## Documentação e limites

[Tutorial](docs/GUIDE.md), [terreno multiescala](docs/TERRAIN.md), [modelos e equações](docs/MODELS.md), [formato persistido](docs/FORMAT.md), [SPEC](SPEC.md), [roadmap](ROADMAP.md), [arquitetura e incorporação](../../docs/architecture/atlas-forge.md).

O relevo/clima/biomas são heurísticos; drenagem é descida esférica com runoff fixo e bacias fechadas, sem lagos dinâmicos. Marcos não simulam eras. Não estão implementados tectônica física, ecologia trófica, erosão, civilizações ou geradores urbanos completos. A fixture de importação v1 é sintética; compatibilidade geral exige corpus real.

O [mandato original](../../docs/proposals/atlas-forge/MISSION.md) e a [auditoria inicial](../../docs/proposals/atlas-forge/docs/AUDIT.md) permanecem como proveniência. A incorporação foi aprovada na DEC-0054/ADR-0035; aprovação arquitetural, integração Git e validação em aparelho são evidências diferentes. Nenhuma capability pública ou extração compartilhada é criada. O CI disponibiliza artefatos de revisão; não há release de produção declarada.
