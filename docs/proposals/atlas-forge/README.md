# Atlas Forge

Oficina local de mundos esféricos de alta fantasia. Este é um **corte vertical executável, Alpha 0.2**, proposto para o Ecosystem. Não é a conclusão do roadmap, uma capability global registrada ou uma release de produção.

## Abrir e desenvolver

Requer Node 22+ para gerar os artefatos. O editor distribuído não precisa de Node, servidor, conta, Hub, Urbe ou acesso à rede.

```bash
cd docs/proposals/atlas-forge
npm ci
npm run build
npm start
```

Abra `http://localhost:4173`. Alternativamente, abra **`dist/atlas-forge.html` diretamente no navegador**, inclusive offline. O Worker está incorporado ao HTML. IndexedDB em `file://` depende do navegador; a alternativa de download/importação JSON sempre está disponível. O aplicativo informa se a cópia local não puder ser gravada e verificada.

O build também gera `dist/examples/miravento.world.json`, `dist/examples/Miravento.md` e `dist/examples/miravento.html`: exemplo de planeta, cordilheira autoral, castelo, nota, mapa filho e dois marcos históricos. O HTML publicado é independente e não inclui solvers.

## O que funciona

- Geração reproduzível por seed sobre esfera; oceanos ponderados por área real das células.
- Mapa equiretangular e globo ortográfico, zoom, pan, rotação longitudinal e seleção.
- Oito camadas: atlas, altitude, temperatura sazonal, chuva, drenagem, bacias, biomas e intervenções.
- Pincéis geodésicos de elevação/escavação, pressão de caneta, máscaras preservadas, undo/redo e recálculo causal em Worker cancelável.
- Lugares com identidade persistente, busca, tipo e descrição; notas Markdown por cópia escolhida pelo usuário, SHA-256 e renderização segura.
- Grafo conectado de salas persistente, navegação planeta → mapa filho → planeta.
- JSON v2 validado, importação forward-only do contrato documentado Alpha v1, cópia IndexedDB verificada e backup manual.
- Marcos com configuração, estado e histórico; restauração e ramificação sem apagar o futuro original.
- Exportação de um atlas HTML único, com camadas, mapa/globo, lugares, notas e mapas filhos, utilizável sem rede.
- Painéis móveis, toque, HTML semântico, foco visível, seleção geográfica por formulário e atalhos.

## Verificar

```bash
npm test
npx playwright install --with-deps chromium
npm run check
npm run benchmark
```

`npm run check` executa testes do núcleo, build/exemplos e fluxos reais em Chromium. Capturas e relatório ficam em `test-results/`. O teste abre os HTMLs por `file://` com rede desativada. Layout móvel automatizado **não** equivale a homologação Android em aparelho.

## Modelos e limites

O relevo é ruído esférico **heurístico**, sem placas tectônicas. O clima é **heurístico**: latitude, lapse rate, duas condições sazonais e transporte zonal de umidade. A drenagem é um **modelo físico simplificado** de descida estrita e acumulação com runoff fixo. Bacias fechadas são identificadas, mas não há nível dinâmico de lagos, transbordamento, erosão, sedimentos ou conservação atmosférica. Biomas são classes climáticas, sem populações.

Marcos não simulam eras; mapas filhos não são cidades simuladas. Não existem ecologia trófica, evolução, civilizações, regras mágicas ou integração autorizada com o vault do Urbe. Veja [os modelos](docs/MODELS.md), [o formato](docs/FORMAT.md), [o diagnóstico](docs/AUDIT.md), [o tutorial](docs/GUIDE.md), [evidências](docs/VERIFICATION.md) e [o roadmap](ROADMAP.md).

## Lugar no Ecosystem

A implementação fica deliberadamente isolada nesta proposta, com dependências internas e nenhum import de outro Product. [ADR-0035](../../adr/0035-atlas-forge-incorporacao-gradual.md) propõe a incorporação futura em `apps/atlas-forge`, independente; [DEC-0054](../../governance/decisions.json) trata a localização/classificação e a exceção de stack web. Não houve registro em `ecosystem.json`, extração para `tools/`, alteração do Urbe ou publicação de release. A intenção de reuso não é prova de dois Hosts reais.

O mandato original está em [MISSION.md](MISSION.md). A Alpha real não foi recebida: a fixture v1 é sintética e não prova regressão do aplicativo original.
