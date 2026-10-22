# ATLAS FORGE — MISSÃO DE DESENVOLVIMENTO E ROADMAP UNIFICADOS

**Versão do mandato:** 1.0 — 22/10/2026  
**Destinatário:** agente autônomo de planejamento, arquitetura, engenharia, implementação e testes  
**Contexto:** `AbnerCruz/Ecosystem` (`main`) e protótipo independente Atlas Forge Alpha 0.1  
**Prioridade:** transformar prova de conceito em uma ferramenta robusta, modular, confiável e extensível para criação de mundos esféricos de alta fantasia  
**Estado deste documento:** instrução de delegação + especificação de produto + roadmap executável; não é evidência de que as funcionalidades descritas já existem.

---

# PARTE I — PROMPT EXECUTIVO PARA O AGENTE

## 0. Sua missão e o que esperamos de você

Você foi designado para **construir**, não apenas discutir, o Atlas Forge: um ambiente de geração, simulação, edição, investigação e publicação de mundos procedurais de alta fantasia, cuja geometria fundamental é a **superfície de um planeta esférico**.

O usuário já experimentou a Alpha 0.1. Ela mostrou que o conceito funciona, mas a qualidade científica, a profundidade dos sistemas, os geradores, a arquitetura e a experiência de uso ainda estão muito aquém da visão pretendida. **Não trate a Alpha como produto quase pronto.** Ela é um conjunto de provas de interação e exportação que precisa ser transformado em engenharia de verdade.

O objetivo não é criar um gerador de bonitas manchas coloridas chamado “simulador geológico”. O objetivo é criar um **sistema causal de autoria de mundos**, no qual:

1. Movimento e interação das placas produzam tendências coerentes de relevo e vulcanismo.
2. Topografia, latitude, água, atmosfera e sazonalidade condicionem temperatura, ventos e chuvas.
3. Chuvas, encostas e bacias produzam drenagem, rios, lagos, erosão e depósitos.
4. Rocha, solo, clima e água condicionem vegetação, biomassa e habitats.
5. Espécies personalizadas entrem em relações tróficas e se espalhem segundo recursos, competição, dispersão e capacidade de suporte.
6. Civilizações usem e transformem um território que tem consequências ecológicas e geográficas reais.
7. Eras geológicas e históricas alterem os estados anteriores em períodos configuráveis, com causalidade observável, histórico e ramificações.
8. O autor possa editar qualquer região, impor restrições, preservar decisões narrativas, reexecutar sistemas dependentes e publicar o resultado em HTML interativo.
9. Planeta, continente, reino, cidade, edifício, caverna, ruína e dungeon possam constituir **mapas encadeados**, consultáveis em várias escalas.
10. Os pontos do mapa possam ter documentos Markdown — especialmente notas compatíveis com o conceito de vault do Urbe — sem violar a integridade dos arquivos originais.

**O diferencial competitivo pretendido é a combinação de plausibilidade sistêmica + liberdade de criação + exploração narrativa + publicação independente.** Nenhuma dessas quatro frentes deve ser sacrificada silenciosamente em nome das outras.

### Diretrizes de execução

- Comece diagnosticando o repositório e o protótipo reais. Continue com uma implementação verificável na mesma execução; **não entregue só um documento de planejamento**.
- Trabalhe por incrementos verticais utilizáveis: sempre que possível, um mecanismo deve chegar do modelo e cálculo até a UI, persistência, teste e publicação.
- Prossiga pelo roadmap na medida da capacidade da sessão e das autorizações disponíveis. Quando houver restrição, registre exatamente o bloqueio e a próxima tarefa; não declare prontas etapas incompletas.
- Decida autonomamente detalhes técnicos locais, mas não tome como sua uma autorização para alterar a governança canônica, integrar mudanças críticas, publicar releases ou modificar o Urbe sem os gates aplicáveis.
- Evite monólito, refatoração interminável, arquitetura especulativa, código morto, controles decorativos e demonstrações simuladas apresentadas como sistemas concluídos.
- Preserve as ideias e funcionalidades já demonstradas quando fizerem sentido; substitua suas implementações precárias de forma incremental, com testes de regressão e migração de dados.

## 1. Fontes de verdade, arquivos e inspeção obrigatória

**Repositório principal:** `https://github.com/AbnerCruz/Ecosystem` — branch `main`.

**Arquivos de entrada da prova de conceito (anexar/fornecer ao agente, se disponíveis):**

- `atlasforge.html` — aplicação Alpha 0.1 em arquivo único.
- `atlas-example-world.json` — exemplo de projeto salvo.
- `atlas-example-published.html` — exemplo de visualizador exportado.
- `ATLAS-FORGE-ARQUITETURA.md` — arquitetura, limites e roadmap originais; seu conteúdo também está preservado na Parte III deste arquivo.
- `ATLAS-FORGE-GUIA.md` — instruções operacionais da Alpha.
- `atlas-forge-alpha01.zip` — distribuição da Alpha, quando estiver disponível.

A presença destes arquivos no contexto da conversa **não garante acesso a eles pelo agente delegado**. Antes de assumir que os recebeu, verifique e reporte quais estão realmente acessíveis. Se não forem fornecidos, use este documento como especificação, mas não invente uma auditoria de código inexistente.

Antes de editar código no Ecosystem, leia as fontes normativas em sua versão atual, pelo menos:

- `MANIFEST.md` (integral, incluindo `0.1 NON-NEGOTIABLES`).
- `AGENTS.md`, `ARCHITECTURE.md`, `ROADMAP.md`, `ecosystem.json`.
- `docs/architecture/product-model.md`, `docs/architecture/local-first.md`, `docs/architecture/capability-runtime.md`.
- `docs/adr/0034-urbe-sem-simulacao-visual.md` e `docs/governance/addenda/ADD-0028-urbe-sem-simulacao-visual.md`.
- Documentos atuais da governança de PRs, integração, validação e decisões/adendos que incidirem na tarefa.
- Documentos normativos locais pertinentes ao componente eventualmente criado.

**Contexto factual de 22/10/2026:** a `main` do Ecosystem registra a remoção da simulação visual do Urbe nativo por DEC-0051/ADR-0034. O Urbe C# usa Avalonia e não deve incorporar uma simulação Canvas via WebView por conveniência. O Ecosystem proíbe dependências diretas improvisadas entre Products, infraestrutura compartilhada sem responsabilidade e extrações sem consumidor real. Os documentos atuais do repositório prevalecem sobre este resumo se o estado tiver evoluído.

Faça inventário do código, commits/PRs/issues/handoffs pertinentes, build, testes, limitações reais, conflitos e decisões abertas. Separe no diagnóstico **fatos observados**, **inferências**, **propostas** e **decisões efetivamente autorizadas**.

### Posicionamento de produto

O Atlas Forge deve funcionar como **aplicação independente**, utilizável sem Urbe, Lunet ou Hub. O usuário quer que se torne uma ferramenta valiosa do Ecosystem; isso **não** significa incorporá-lo ao núcleo do Urbe nem registrá-lo como capability global especulativa.

Proponha sua organização no monorepo (por exemplo `apps/atlas-forge/`, se autorizada pela governança), classificação arquitetural e canal de distribuição mediante os mecanismos normativos aplicáveis. Se houver mudança estrutural crítica, prepare a decisão/ADR e o PR apropriados; não burle a política. Comece a implementação isolada e testável sem criar uma dependência obrigatória do Hub.

O stack inicial é **HTML5, CSS, JavaScript modular e Canvas**. O padrão C# do Ecosystem para nova infraestrutura compartilhada **não é justificativa para reescrever o editor web**. Um núcleo C#/WASM só pode ser cogitado com ganho mensurado e decisão de arquitetura, e não como condição para a primeira versão.

## 2. Contrato de experiência do usuário

### 2.1 Três modos de criação

**Procedural:** informar uma seed e parâmetros do planeta; gerar automaticamente os subsistemas instalados em sequência causal, mostrando progresso e resultados verificáveis.

**Autoral:** desenhar máscaras e restrições — forma de continentes, costas, cordilheiras, depressões, rios desejados, biomas obrigatórios, regiões protegidas, terrenos proibidos — e pedir ao motor para completar o restante coerentemente.

**Híbrido:** editar qualquer campo num mundo já gerado, congelar partes, comparar o antes/depois e recalcular apenas regiões/sistemas dependentes. O usuário decide onde a liberdade narrativa prevalece sobre o resultado físico.

Regra de ouro: **alterações manuais persistem**, a menos que o usuário as descarte conscientemente. Se o novo estado não puder ser fisicamente consistente, apresente uma explicação, ofereça alternativas (recalcular, manter exceção, fixar camada) e permita o override fantástico explícito.

### 2.2 Navegação e autoria

- Mapa esférico em projeção equiretangular como modo de edição principal, com visualização ortográfica de globo e troca futura de projeções sem perder coordenadas.
- Pan, zoom, rotação do globo, seleção, múltipla seleção, ferramentas de pincel, máscaras, linhas, polígonos, pincel por pressão quando houver caneta, undo/redo, comparação de camadas e histórico.
- Painéis para informações científicas (incluindo unidades, confiança e limites dos modelos), entidades narrativas, catálogos de espécies, propriedades das eras e geradores locais.
- Alternância de mapas topográfico, hipsométrico, temperatura sazonal, precipitação, vento, pressão, drenagem, solos, geologia, biomas, vegetação, densidades de populações, perigos, recursos naturais, fronteiras e assentamentos conforme os motores evoluírem.
- Buscas, filtros e legendas; seleção de objeto por toque ou mouse; navegação progressiva para mapas filhos e retorno ao ancestral.
- Mobile-first real, com painéis recolhíveis, gestos confiáveis, áreas de toque confortáveis, acessibilidade básica, teclado no desktop, estados de carregamento e operações canceláveis.
- Aparências cartográficas (atlas acadêmico, pergaminho, fantasia, relevo) devem ser **skins de visualização**, não a fonte dos dados físicos.

**Canvas concentra o mapa e as ferramentas de interação espacial.** Use elementos HTML semânticos para painéis, inputs, modais, seleção de arquivos, acessibilidade e leitura de Markdown. A exigência de Canvas não significa programar um sistema de formulários e acessibilidade inteiro manualmente em pixels.

### 2.3 Publicação como funcionalidade central

Um mundo não está completo se for prisioneiro do editor. O usuário deve poder produzir um artefato HTML interativo de leitura, com camadas selecionadas, entidades clicáveis, informações, Markdown e navegação por mapas filhos. A publicação não deve carregar os solvers de tectônica e ecologia quando não forem necessários à visualização.

O visualizador público deve funcionar offline e ser independente do editor; exportar HTML de arquivo único quando o tamanho permitir e pacote de arquivos relativos para mundos grandes. Garanta uso sem servidor onde possível, documentando restrições de `file://`, módulos, recursos e CORS. SVG e PNG são exportações auxiliares, não substituem o modelo de dados interativo.

## 3. Modelo planetário canônico: geometria esférica de verdade

A geometria do mundo é uma esfera parametrizada: raio configurável; latitude `[-90°, +90°]`; longitude periódica `[-180°, +180°)`; datum e unidades explicitados. A projeção 2D jamais define distâncias nem áreas físicas. Áreas em grades latitude–longitude diferem muito perto dos polos.

Implementar/testar:

- Geodésia esférica, vetores 3D unitários, transformações geográficas e distância por arco de círculo máximo.
- Continuidade longitudinal no antimeridiano; tratamento de células e conectividade nos polos.
- Áreas e comprimentos de arestas apropriados à geometria; pesos de área em estatísticas.
- Grade inicial lat/lon com metadados geográficos, com desenho preparado para mudança posterior de topologia (por exemplo, cubed sphere, icosaédrica ou malha adaptativa) caso se justifique.
- Separação explícita entre grade física, raster de renderização, tile de cache e resolução do viewport.
- Altimetria/batimetria com metros e referência definida; não persistir apenas elevação normalizada `[-1,1]` como altura física final.
- Campos com unidades, domínio, `NoData` e validação: altitude, temperatura, fluxos, precipitação, energia, nutrientes, solo, populações etc.

### Configurações planetárias

Inclinação axial, duração do dia/ano, excentricidade orbital, potência da estrela/irradiância, rotação, gravidade, pressão/composição atmosférica, distribuição de água, salinidade, fluxo geotérmico, intensidade tectônica, estado criosférico e composição da superfície. Quando um parâmetro ainda não afetar o solver, a UI deve marcá-lo **não implementado**, em vez de fingir influência física.

## 4. Arquitetura do motor

Adotar dependências explícitas, schemas versionados e interfaces/contratos internos testáveis. Como referência, uma decomposição inicial aceitável é:

```text
atlas-forge/
  app/                 composição do editor, comandos, painéis, timeline
  core/                WorldState, PlanetConfig, campos, objetos, IDs, tipos
  geometry/            esfera, projeções, topologia, distâncias e áreas
  generation/          PRNG e geração por subsistema
  solvers/
    geodynamics/       placas, limites, soerguimento, vulcanismo
    geomorphology/     intemperismo, sedimentos, erosão, formação de solos
    climate/           insolação, energia, circulação e umidade
    hydrology/         bacias, rios, lagos, fluxo e armazenamento
    chemistry/         carbono, nutrientes, pH e salinidade (quando implementado)
    ecology/           traços, nichos, biomassa, populações, dispersão
    civilizations/     assentamentos, uso do solo e sistemas sociais
    deep-time/         eventos, eras e passos temporais
  generators/          continentes, cidades, cavernas, dungeons, ruínas
  rendering/           Canvas, layers, tile cache, seleção, símbolos, LOD
  workers/             filas, cancelamento, progresso, limites de memória
  storage/             projetos, importação, migrations, integridade
  documents/           vínculo MD, snapshots, renderer seguro
  publishing/          HTML viewer, assets, GeoJSON, SVG, raster
  tests/               unitários, propriedades, regressão, e2e, benchmarks
  docs/                arquitetura, equações, limitações, guia, status real
```

**É uma proposta de responsabilidades, não uma obrigação de diretórios exatos.** Respeite o estilo e a governança do repositório. Não crie um `shared/` global no Ecosystem para componentes sem consumidor e contrato.

### 4.1 Modelo e persistência

Distinguir rigorosamente:

1. `WorldDefinition` — configuração inicial, seed, opções, regras de fantasia, versões dos algoritmos.
2. `WorldState` — campos numéricos, entidades, espécies, relações, dados históricos e alterações autorais com fonte de verdade única.
3. `DerivedLayers` — caches e projeções recalculáveis, com dependências e versões.
4. `EditJournal` — comandos, undo/redo, máscaras congeladas, intervenções e justificativas.
5. `Timeline` — checkpoints, eventos, deltas e ramificações de eras.
6. `Publication` — projeção imutável de leitura para visualizador externo.

Formato recomendado a avaliar: arquivo de projeto versionado com manifesto JSON, campos binários por tiles, geometrias vetoriais, documentos incorporados e hashes de integridade. **Primeiro, ofereça importação segura do JSON v1 da Alpha 0.1**; depois proponha formato futuro com migração forward-only, fixtures, backup e detecção de versões desconhecidas. Nunca sobrescrever silenciosamente projetos de versões futuras.

Projetos devem poder ser salvos localmente, abertos após reiniciar e exportados/importados. Usar IndexedDB/OPFS quando justificado e suportado; fornecer alternativa de download/importação manual. Não alegar sincronização em tempo real de arquivo local apenas porque o navegador importou uma cópia.

### 4.2 Determinismo, processamento e cache

- Seed + parâmetros + versão de algoritmo + estado autoral definem uma geração reproduzível.
- PRNG independente por subsistema/tile/evento; adicionar espécies não pode reorganizar continentes por consumir valores aleatórios de outra fila.
- Worker para tarefas caras, progresso por etapa, cancelamento real, geração interrompível, limites de RAM e recuperação de erro.
- Renderização por tiles/LOD, seleção desacoplada dos pixels, caches invalidáveis, cálculo incremental de regiões alteradas.
- Ciclos causais (erosão modifica relevo; vegetação modifica clima; civilizações modificam água) exigem passos/iterações nomeadas, critério de parada, controle de instabilidade e orçamento, não recursão infinita.
- Testes numéricos com tolerâncias declaradas quando diferenças de ponto flutuante entre plataformas não permitirem comparação byte a byte.
- A escolha entre Canvas 2D, WebGL2, OffscreenCanvas e WASM deve ser orientada por benchmark; não impor tecnologia apenas por prestígio.

## 5. Sistemas de simulação: requisitos de domínio e honestidade científica

**Regra transversal:** classifique cada módulo como (A) visual/heurístico, (B) modelo físico simplificado com grandezas identificadas, ou (C) modelo validado para cenário e limites específicos. Não apresentar A como B nem B como um simulador científico geral. Documentar equações, aproximações, conservação ou perda, unidades, resolução, condições de contorno e cenários de validação.

### 5.1 Geodinâmica / tectônica

Placas globais persistentes com identidade, tipo de crosta, movimento angular e fronteiras geográficas; divergência, convergência, colisão, subducção idealizada, falhas transformantes, orogenia, dorsais, fossas, hotspots e vulcanismo onde os modelos suportarem. Montanhas devem emergir de regras geodinâmicas e poder ser editadas manualmente depois.

Os passos temporais precisam refletir velocidades e escalas geológicas plausíveis; nunca apresentar simplesmente fractal noise rotacionado como placas que se movem.

### 5.2 Relevo, solos e química ambiental

Erosão hídrica/eólica/glacial progressiva quando implementada, intemperismo físico/químico, sedimento, transporte, deposição, deltas, cânions, solos, permeabilidade, armazenamento de água, fertilidade, pH, salinidade, carbono e nutrientes relevantes. Distinguir parâmetros estimados e valores calculados.

### 5.3 Clima e meteorologia

Configuração estelar/orbital → insolação por latitude/estação → balanço energético simplificado → campos térmicos → gradientes de pressão, ventos e umidade → precipitação, evaporação, sombra de chuva e aridez. Correntes oceânicas, gelo/albedo, sazonalidade, secas e eventos extremos serão adicionados por módulos com testes.

**Não confundir climatologia de longo prazo com simulação de cada frente meteorológica.** O usuário quer relevância causal e visualização sazonal; modelos de circulação atmosfericamente plausíveis e consistentes bastam no início, desde que não se anuncie fidelidade de previsão real.

### 5.4 Hidrologia

Bacias, escoamento e acumulação, rios e afluentes, lagos, depressões fechadas e bacias endorreicas, recarga/evaporação/precipitação, direção de fluxo com área e distâncias esféricas, erosão e planícies de inundação em versão incremental.

Critérios mínimos: fluxos coerentes com elevação e topologia, reconhecimento de sumidouros legítimos, conectividade da rede, nenhuma linha de rio surgindo magicamente após a costa ou atravessando montanha por simples interpolação. Exceções mágicas devem ser entidades autorais identificadas.

### 5.5 Biomas, fauna, flora e cadeias alimentares

Esta é uma **funcionalidade central, não um pós-escrito**. O usuário precisa criar espécies próprias, inseri-las em ecossistemas e observar consequências. Implementar:

- Catálogo básico de produtores, herbívoros, predadores, detritívoros, decompositores e espécies aquáticas; exemplos inspirados em vacas, leões, cervos, lobos, gramíneas, árvores, fungos etc.
- Editor de espécie por traços: taxonomia ficcional, nome, descrição, aparência referenciável, massa, dieta, fisiologia, tolerância térmica/hídrica, habitats, reprodução, mortalidade, competição, migração, dispersão, relações tróficas, domesticação e poderes fantásticos opcionais.
- Redes tróficas editáveis, habitats adequados, recursos/biomassa, limites de capacidade de suporte, estoques e fluxos agregados por região/célula.
- Populações agregadas com crescimento, mortalidade, consumo, predação, migração, competição, extinção, invasão, sucessão e recuperação.
- Interações bidirecionais: herbivoria modifica cobertura vegetal/solo; vegetação modifica retenção hídrica e erosão; agricultura e fauna domesticada afetam territórios.
- Feedback legível explicando **por que** determinada espécie prosperou, migrou ou foi extinta.

Não simular individualmente bilhões de organismos. Use aproximações por população/biomassa e refine apenas cenários locais relevantes. Não permitir crescimento infinito ou geração espontânea de energia sem fonte declarada; apresentar limites das aproximações de conservação.

### 5.6 Regras de fantasia

FantasyRules constitui um módulo de regras explícitas, versionadas e desativáveis: magia ambiental, regiões amaldiçoadas, espécies que usam mana, materiais fictícios, clima mágico, dragões, ilhas anômalas, anomalias gravitacionais e outras exceções. Mostrar qual fenômeno fantástico explica a divergência em relação ao modelo de base. Não executar scripts arbitrários importados do projeto ou de um Markdown como “regra fantástica”.

## 6. Eras geológicas, causalidade histórica e intervenções

A linha do tempo não pode ser apenas um contador que aplica blur ao relevo. Cada época deve avançar do estado anterior (ou de uma ramificação), com eventos e subsistemas que operam na própria escala temporal:

- Geodinâmica e morfologia: milhares a milhões de anos.
- Clima e ciclos ecológicos: sazonalidade a séculos/milênios, conforme modelo.
- Evolução de espécies: surgimento/extinção em longos períodos, sem prometer genética real.
- Civilizações: décadas, séculos e intervalos históricos configuráveis.

Criar eras com duração escolhida, checkpoints, eventos naturais ou autorais, comparação de camadas antes/depois, replay, bifurcação, edição de era anterior e política clara de invalidação do futuro dependente.

Se o usuário cria uma cordilheira, o Atlas Forge deve conseguir explicar e mostrar a possível alteração da chuva, vegetação, migração e ocupação ao longo das eras seguintes. O encadeamento causal deve existir de fato, ainda que a primeira implementação seja uma aproximação documentada.

## 7. Mundo habitado e mapas em múltiplas escalas

Representar entidades com UUID/ID estável, geometria georreferenciada, tipo, nome, descrição, propriedades, era de existência, relacionamentos e documentos associados. Entre os tipos: regiões, impérios, reinos, fronteiras, tribos, cidades, aldeias, castelos, torres, fortalezas, pontes, portos, minas, fazendas, rotas, cavernas, templos, dungeons, acampamentos e ruínas.

### Navegação hierárquica

`Planeta → continente → região/reino → cidade → bairro → edifício → cômodo` ou `montanha → caverna → galerias → dungeon → sala`.

Cada mapa local tem escala, coordenadas locais, ancoragem espacial, versão e identidade; mapa filho não é apenas uma imagem aleatória gerada a cada clique. Ao voltar, a seleção e o estado de navegação são preservados.

### Geradores locais

- **Cidades:** localização condicionada por água, recursos, defesa e transporte; redes de ruas e bairros, lotes, edifícios, muralhas, mercado e rios/canais conforme contexto.
- **Dungeons:** grafo topológico de salas/corredores, níveis, entradas/saídas, conectividade, chaves/obstáculos, salas especiais e temas configuráveis.
- **Cavernas:** galerias/câmaras e conexões coerentes, entradas georreferenciadas, água subterrânea e características geológicas simplificadas.
- **Ruínas:** estado originário, destruição, envelhecimento, sedimentação, vegetação e intervenção histórica.
- **Cenas/interiores:** grid ou vetor com objetos selecionáveis, portas, pisos e relações narrativas.

Todos os geradores aceitam seed, parâmetros, constraints, edição manual e regeneração controlada. Considere formatos reaproveitáveis pelo Lunet2D, sem criar dependências diretas.

## 8. Notas Markdown, Urbe e exportação

### 8.1 Fluxo de notas exigido

1. Selecionar um castelo no mapa.
2. Clicar em **Vincular nota**.
3. Selecionar um `.md` no computador/celular com permissão explícita.
4. Mostrar o documento renderizado e permitir relink, remoção e atualização manual de snapshot.
5. Salvar vínculo estável, nome, hash da cópia e metadados apropriados, sem sobrescrever o arquivo de origem.
6. Na publicação HTML offline, abrir o castelo e visualizar a nota incorporada.
7. Se o castelo possuir mapa local, clicar em **Inspecionar**, explorar seu interior e retornar à origem.

Suportar futuramente wikilinks/`[[nota]]`, anexos seguros, caminhos relativos e identificação canônica de nota quando um adapter autorizado do Urbe fornecer isso. **Não modificar** `.urbe/notas.json`, `.urbe/mapa.json`, `.urbe/mundo.json` nem pressupor acesso silencioso ao vault. O Urbe nativo atual não é um host WebView: integração interna requer projeto específico e autorização.

### 8.2 Segurança

- Markdown e metadados importados são conteúdo não confiável: sanitização e escape, sem execução de HTML ativo ou scripts, esquemas de URL perigosos ou injeção no payload da publicação.
- Validar formato, versão, tamanho, tipo e integridade de arquivos importados.
- Impedir path traversal em ZIPs, caminhos relativos e nomes de assets.
- Usar política apropriada de scripts/CSP na publicação quando possível; não carregar dependências de rede ocultas.
- O projeto local não deve exfiltrar notas, credenciais nem documentos. Sem IA ou serviços em nuvem obrigatórios.

## 9. Validação científica e técnica

Testes de UI não são suficientes. Criar **cenários sintéticos controlados** com expectativas objetivas, unidades e tolerâncias:

- Mesma seed/config/versão → mesmas camadas dentro da tolerância declarada.
- Antimeridiano e polos → conectividade, vizinhança, cálculo de área e renderização corretos.
- Planeta com vale e divisor → drenagem conectada e posição esperada dos divisores.
- Bacia fechada → lago ou escoamento endorreico, não rio fantasma até o oceano.
- Cordilheira sob fluxo úmido → maior precipitação a barlavento e condição de sombra de chuva a sotavento, nos casos assumidos pelo modelo.
- Hemisférios/estações → insolação e mudanças sazonais coerentes com inclinação axial.
- Espécie herbívora sem alimento → declínio/extinção em vez de crescimento ilimitado.
- Introdução de predador → impacto populacional esperado num ecossistema controlado.
- Perda de vegetação em encosta → efeito sobre solo/erosão conforme equações adotadas.
- Avanço por eras → estado seguinte depende do estado anterior; voltar/ramificar não destrói histórico original.
- Overrides manuais → permanecem íntegros após regeneração causal.
- Importação JSON v1 → preserva seed, altura aproximada, marcadores, Markdown e snapshots da Alpha.
- HTML publicado → abre offline sem editor, marcadores clicáveis, notas renderizadas, mapas locais navegáveis.
- Segurança → conteúdo Markdown hostil não executa script nem quebra a exportação.
- Recuperação → interrupção/cancelamento não corrompe estado persistido.
- Desempenho → benchmark documentado por dimensão de grid, dispositivos reais, frame/cálculo, uso de RAM e tempo de exportação.

Garanta suíte automática em desktop/Chromium e registre a necessidade de validação física Android. Meta de UX: interação fluida e operações longas canceláveis; números de orçamento definitivos devem nascer de medições, não de promessas arbitrárias. Preferencialmente testar em Android intermediário, inclusive Moto G32/Android 13 quando disponibilizado.

## 10. Governança, documentação e entrega

Produzir, na estrutura e formato aprovados pelo Ecosystem:

1. Código executável e modular, com comandos para executar e testar.
2. Testes automatizados, fixtures determinísticas e benchmarks reproduzíveis.
3. Exemplos: planeta por seed, mundo desenhado, regiões com biomas, espécies, eras, cidade com mapa filho, castelo com Markdown.
4. Manual do usuário e tutorial navegável, indicando funcionalidade pronta versus projetada.
5. Especificação de formato persistido, versões, migrações e modelo de integração futura.
6. Notas científicas de cada solver, equações, parâmetros, limitações e validações.
7. Roadmap com IDs estáveis de tarefas locais, estados verificáveis e histórico de handoffs.
8. Exportação HTML independente, pacote editável e publicação de exemplo.
9. Evidência de execução real: commits, PRs, checks, cenários testados, limitações e próximo trabalho.

Nenhuma modificação em Product alheio sem fronteira/contrato aprovado. Integração em `main` apenas pelos mecanismos de integração vigentes, nunca por atalhos. Sem release com gates pendentes e sem dizer que DEVICE foi aprovado apenas porque a UI passou em Playwright.

## 11. Primeira entrega obrigatória da missão

**Não inicie tentando implementar todos os motores ao mesmo tempo.** A primeira entrega deve ser um corte vertical que substitua a fragilidade estrutural da Alpha e inaugure um subsistema científico corretamente testável.

Escopo mínimo recomendado:

A. Criar estrutura modular preservando funcionamento equivalente à Alpha: seed, globo, edição de elevação, seleção de entidade, Markdown, save/import e HTML publicado.

B. Migrar JSON v1 da Alpha para um modelo versionado com unidades e IDs confiáveis; manter um corpus fixo de projetos de regressão.

C. Implementar geometria esférica testada, com antimeridiano, áreas, coordenadas e alturas em metros. Separar grades científicas da renderização.

D. Tirar geração e reprocessamento pesado da thread principal, com progresso, cancelamento e proteção contra sobrescritas tardias.

E. Construir **um primeiro sistema causal sério**, escolhendo a ordem que permita demonstrar objetivamente um resultado: relevo → drenagem/bacias → clima orográfico ou clima → drenagem. Documentar compromissos e preparar o caminho para tectônica.

F. Publicar cenário end-to-end: um planeta gerado por seed, uma região editada, um castelo com arquivo `.md` e visualizador offline abrindo tudo corretamente.

G. Registrar antes/depois, testes, benchmark, limitações e roadmap atualizado, sem renomear a Alpha superficialmente como 1.0.

Se o custo técnico for alto, conclua o menor incremento íntegro primeiro, mas **continue pelo roadmap** enquanto houver condições, sem repetir diagnóstico a cada tarefa. Dê evidência do que efetivamente foi feito.

---

# PARTE II — ROADMAP DE IMPLEMENTAÇÃO

## Leitura do roadmap

- **M0–M8** são marcos de produto, não alegações de trabalho concluído.
- Cada marco contém entregas verificáveis e um **gate**. Não marcar completo se só existe UI ou modelo demonstrativo sem uso real.
- Tarefas internas precisam de IDs estáveis; alinhe-os com os mecanismos do Ecosystem antes de escrever os arquivos normativos.
- A ordem abaixo é recomendada por dependência. Subsistemas independentes podem avançar em paralelo quando houver testes, ownership e integração governada.
- Não iniciar abstrações compartilhadas na plataforma antes de haver o consumidor real e os processos de Extraction Review exigidos.

## M0 — Incorporação controlada e baseline de qualidade

**Objetivo:** entender e preservar o protótipo, obter um ambiente de desenvolvimento reprodutível e escolher o posicionamento do Atlas Forge sem conflitar com a governança.

**Entregas:**

- Auditoria da `main` do Ecosystem e da Alpha 0.1, arquivos realmente recebidos e divergências normativas.
- Matriz de funcionalidades: real, parcial, demonstrativa, inexistente.
- Testes que preservem o fluxo seed → edição → entidade → Markdown → import/save → publicação → clique no visualizador.
- Estrutura de desenvolvimento local/CI, lint, check e testes automatizados.
- Decisão proposta de localização/classificação no Ecosystem e registro/handoff exigido.
- Corpus de arquivos Alpha v1 para compatibilidade.

**Gate M0:** build/teste executável, corpus abrindo e exportando sem regressão, classificação do componente tratada pela governança; nenhuma funcionalidade científica nova é exigida para declarar somente M0 concluído.

## M1 — Núcleo canônico, geometria esférica, persistência e edição

**Objetivo:** transformar HTML monolítico numa aplicação que consiga evoluir sem destruir mundos.

**Entregas:**

- `WorldState` e `PlanetConfig` separados da UI; campos tipados com unidades.
- Geometria esférica e projeções com casos-limite polares e antimeridiano.
- PRNG determinístico com streams independentes por subsistema.
- Grid e caches separados, worker, progresso/cancelamento, dirty regions.
- Editor por comandos com histórico e travas autorais; operação de desmanchar/restaurar.
- Importação do formato Alpha v1 e novo formato de persistência versionado com testes.
- Exportação HTML e Markdown regressivamente funcional; preview mobile/desktop.
- Referência de desempenho com números medidos.

**Gate M1:** editar, gerar, salvar, reabrir e publicar sem perda; antimeridiano e operações espaciais testadas; nenhuma tarefa pesada congela a interface sem opção de cancelamento.

## M2 — Geologia, tectônica e superfície

**Objetivo:** fazer continentes e relevo começarem a possuir uma história geodinâmica.

**Entregas:**

- Formação inicial de placas geográficas persistentes, tipos de crosta e fronteiras.
- Vetores/velocidades angulares e interações convergentes/divergentes/transformantes aproximadas.
- Regras documentadas de soerguimento, subsidência, dorsais, fossas e hotspots.
- Altitude/batimetria em metros, processos iniciais de erosão e transporte sedimentar.
- Edição manual, máscaras e restrições autorais convivendo com o solver.
- Camadas de placas, tipos de fronteira, idade da crosta quando suportada, relevo e risco tectônico.
- Testes em cenários sintéticos de convergência, divergência e erosão.

**Gate M2:** mudanças de parâmetros tectônicos alteram relevo de maneira coerente e reprodutível; placas persistem entre estados; o sistema separa física simplificada, heurística e edição autoral.

## M3 — Hidrologia, atmosfera e clima planetário

**Objetivo:** fechar o primeiro ciclo geográfico útil e verificável.

**Entregas:**

- Insolação orbital sazonal e balanço energético simplificado.
- Temperatura, pressão e circulação atmosférica parametrizada; ventos e umidade.
- Evaporação, transporte de umidade, precipitação orográfica e sombra de chuva.
- Hidrologia topologicamente consistente: bacias, rios, acumulação, lagos, saída oceânica/endorreia, vazões sazonais iniciais.
- Processos iniciais de glaciação e seca, quando cientificamente e computacionalmente justificáveis.
- Mapas e inspetor para temperatura, chuva, vento, correntes (quando existentes), balanço hídrico, rios e bacias.
- Reprocessamento incremental ao editar montanhas/costas.

**Gate M3:** cenário controlado mostra barlavento/sotavento com chuva coerente; rede de drenagem sem rios órfãos; ciclos sem instabilidade oculta; desempenho medido.

## M4 — Biosfera, catálogo de espécies e ecossistemas

**Objetivo:** oferecer o diferencial da criação de fauna/flora com consequências ambientais reais.

**Entregas:**

- Biomas derivados de campos climáticos/solos e limitados por recursos.
- Catálogo inicial editável de fauna/flora/decompositores com descrições e parâmetros científicos simplificados.
- Criador de espécies fantásticas com fisiologia/nicho/recursos/reprodução/dieta/dispersão; validação de configurações.
- Grafo de relações tróficas, fluxos de biomassa e população/capacidade de suporte agregados.
- Reprodução, mortalidade, competição, predação, dispersão e invasão ecológica.
- Interações vegetação ↔ água/erosão/solo e mudanças de cobertura do território.
- Ferramentas de inspecionar habitat, previsão de sobrevivência, mapa de distribuição e explicação de extinção/migração.
- Cenários testáveis de predador-presa, limitação de nutrientes e ausência de habitat.

**Gate M4:** inserir espécie personalizada altera distribuição e, quando apropriado, cadeias alimentares; mudanças geográficas alteram sobrevivência; explicações e dados persistem/reabrem corretamente.

## M5 — Tempo profundo, evolução e cenários alternativos

**Objetivo:** transformar eras em evolução causal e histórico navegável.

**Entregas:**

- Era com duração configurável, estado inicial, eventos, snapshots/deltas e ramificações.
- Movimento geológico e mudanças de litoral/relevo; retroalimentações climáticas e ecológicas respeitando escalas.
- Comparação temporal lado a lado/slider, mapas de diferença e relatório de mudanças por era.
- Regras iniciais de especiação/extinção **modeladas como abstrações**, sem reivindicar genética rigorosa.
- Catástrofes geológicas/climáticas, eventos criados pelo usuário e regras fantásticas explícitas.
- Operações de voltar, editar passado, ramificar futuro e reproduzir corretamente.
- Compactação e persistência com limites de armazenamento mensuráveis.

**Gate M5:** criar cordilheira, avançar eras e observar mudanças de clima/vegetação/população em cenários testados; voltar ou ramificar não altera indevidamente a história original.

## M6 — Povos, história e sistemas de civilização

**Objetivo:** inserir ocupação e transformação humana/fantástica sem obrigar uma simulação de bilhões de agentes individuais.

**Entregas:**

- Entidades e polígonos de reinos, rotas comerciais, recursos, aldeias, cidades, fortificações, fazendas, minas, portos e ruínas.
- Potencial de assentamento a partir de água, fertilidade, relevo, transporte, recursos, defesa e perigos.
- Desenvolvimento histórico agregado, exploração econômica simplificada e pressão sobre terra/vegetação/água.
- Fronteiras e identidades narrativas editáveis, anexos Markdown, história por eras e relações espaciais.
- Possibilidade de ações autorais que prevaleçam sobre heurísticas políticas, com explicação de inconsistências físicas.
- Catástrofes, migrações e abandono que produzam mudanças persistentes no mapa.

**Gate M6:** fundar/alterar assentamento modifica entidades e impactos ambientais definidos; história e fronteiras sobrevivem a salvamento, exportação e alterações temporais.

## M7 — Escalas locais: cidades, dungeons, cavernas, ruínas e interiores

**Objetivo:** transformar um mapa mundial em uma rede de mapas realmente exploráveis.

**Entregas:**

- Relações pai-filho persistentes, IDs estáveis, coordenadas locais e ancoragem global.
- Editor e geradores por seed para ruas/bairros/lotes/edifícios, dungeon topológica, caverna, ruína evolutiva e cenas/interiores.
- Regras de conectividade, acessos, saídas, níveis, topografia local, objetos e infraestrutura.
- Navegação selecionar → painel → Markdown → inspecionar → mapa filho → retornar, preservando estado.
- Controle do tamanho e geração sob demanda, sem carregar o planeta inteiro em detalhe local.
- Exportação HTML com mapas filhos navegáveis e documentos incorporados.

**Gate M7:** cidade de demonstração gerada/coerente com posição global, castelo inspecionável, dungeon conectada e HTML publicado funcionando completamente offline.

## M8 — Produto robusto, publicação, interoperabilidade e distribuição

**Objetivo:** chegar ao nível de ferramenta publicável e utilizável a longo prazo.

**Entregas:**

- Refinamento UX desktop/tablet/mobile, acessibilidade, performance, feedback de processos e tutorial.
- Visuais de cartografia e personalização, filtros, legendas, buscas e relatórios de mundo.
- Pacote de projeto interoperável e versionado; export PNG/WebP/SVG/GeoJSON/HTML; viewer standalone leve e seguro.
- Documentos Markdown com vínculos robustos; adaptadores futuros com Urbe sob contrato aprovado, sem acoplamento.
- Avaliação de exportação para Lunet2D e tabletop RPG como **possibilidade**, não integração presumida.
- Estratégia de backup/restauração, testes de corrupção, migração e compatibilidade.
- Testes de estresse e medições em dispositivos reais; publicação pelos mecanismos aprovados do Ecosystem.
- Manual, guias de científicidade, exemplos completos, notas de versão, roadmap remanescente.

**Gate M8:** produto executável/distribuído de acordo com governança, documentação e testes válidos, sem dependência obrigatória do Hub, com fluxo ponta a ponta validado pelo usuário em dispositivo real quando exigido.

## Frentes transversais (valem para todos os marcos)

**T1 — Observabilidade científica:** cada solver descreve unidades, fórmulas, premissas, erros, conservação, resolução e confiança do resultado.

**T2 — Segurança e dados:** sanitize Markdown/HTML, autentique qualquer futura integração entre aplicativos quando aplicável, valide arquivos, preserve backups, impeça travessia de caminhos, nunca inclua credenciais no export.

**T3 — Reprodutibilidade:** seeds e versões de algoritmos, testes de propriedades, comparação de fixtures, benchmarks, desvio numérico documentado.

**T4 — Experiência e performance:** viewport mobile/desktop, limite de memória, operações canceláveis, profilers, LOD e threads de trabalho.

**T5 — Governança e entrega:** ADR quando necessário, PRs, CI, handoffs, estados reais e validação humana sem antecipar aprovação.

### Cenário obrigatório de demonstração integrada ao final

O usuário cria um planeta esférico por seed, redesenha parte da costa, força uma cordilheira, acompanha alteração de chuva e rios, cria um herbívoro fantástico que depende de uma planta específica, avança eras e observa dispersão/extinção, funda um reino e uma cidade, coloca um castelo, liga `Castelo.md`, abre um mapa local da cidade, entra no castelo e publica um HTML explorável que mantém objetos, notas e hierarquia sem o editor.

Cada etapa dessa demonstração deve mostrar **efeitos reais dos subsistemas implementados**. Se algum motor ainda não existir, o cenário parcial e o manual devem indicá-lo inequivocamente.

---

# PARTE III — BASELINE TÉCNICO ORIGINAL DA ALPHA 0.1 (DOCUMENTO DE REFERÊNCIA)

O texto abaixo é a documentação produzida junto com o protótipo original. É preservado para fornecer fatos observados, limites técnicos e decisões anteriores. **Em caso de conflito**, prevalecem as normas atuais do Ecosystem e as decisões do proprietário. A implementação real deverá ser conferida no código e não presumida a partir deste resumo.

# Atlas Forge — arquitetura técnica e plano de execução

**Data:** 22/10/2026  
**Estado:** especificação de arquitetura + prova de conceito Alpha 0.1  
**Produto:** simulador e editor procedural de mundos esféricos de fantasia  
**Implementação disponível:** `atlasforge.html` (HTML5/CSS/JavaScript + Canvas; arquivo único)

## 1. Decisão fundamental

O mundo é uma **esfera física parametrizável**. A representação padrão é uma projeção equiretangular, na qual cada pixel corresponde a latitude e longitude; a exploração alternativa usa uma **projeção ortográfica de globo**, não um modelo 3D geométrico. A geometria esférica, e não a tela, é a autoridade da posição.

- Latitude: `[-90°, +90°]`; longitude: `[-180°, +180°)`.
- Distâncias aproximadas sobre a esfera: `d = R × Δσ`, sendo `Δσ` o ângulo central em radianos.
- Área da superfície: `4πR²`. Toda estatística global deve ser ponderada pela área esférica, especialmente perto dos polos.
- O meridiano de ±180° é contínuo; as regiões polares exigem tratamento próprio na evolução do motor.
- A escala planetária é separada da resolução de visualização. Um pixel do Canvas nunca é uma entidade científica.

### Por que independente do Urbe?

No repositório `AbnerCruz/Ecosystem`, a decisão de 22/10/2026 **DEC-0051 / ADR-0034** retirou a simulação visual do Urbe nativo. Sua UI atual é Avalonia/C# e não usa WebView. Implementar Atlas Forge **dentro** do Urbe contradiria essa direção e recolocaria o problema de peso/complexidade em seu domínio.

**Decisão de projeto proposta:** Atlas Forge nasce como **produto independente**. Seus projetos e exports podem ser consumidos pelo Urbe e por outros sistemas, sem importar o núcleo da simulação para eles. Registrar um novo Product no monorepo e construir capabilities são etapas posteriores, sujeitas a `MANIFEST.md`, `AGENTS.md`, ADRs e aprovação aplicável. **Nenhuma mudança foi feita no repositório.**

Fontes verificadas na `main` do Ecosystem:
- `MANIFEST.md`, `AGENTS.md`, `ARCHITECTURE.md`, `ROADMAP.md`
- `docs/adr/0034-urbe-sem-simulacao-visual.md`
- `docs/governance/addenda/ADD-0028-urbe-sem-simulacao-visual.md`
- `docs/architecture/product-model.md`
- `docs/architecture/local-first.md`
- `docs/architecture/capability-runtime.md`

## 2. Arquitetura-alvo

```text
                      ATLAS FORGE — aplicação independente
┌──────────────────────────────────────────────────────────────────────┐
│ EXPERIÊNCIA                                                         │
│ HTML/CSS: painéis | Canvas/WebGL: mapas | toque/caneta/teclado       │
│ UI state | inspetor | pincéis | timeline | navegação hierárquica     │
├──────────────────────────────────────────────────────────────────────┤
│ CAMADA DE APLICAÇÃO                                                 │
│ WorldController | EditorCommands | Undo/Redo | TaskScheduler         │
│ DependencyGraph | DirtyRegions | SimulationBudget | Workers          │
├──────────────────────────────────────────────────────────────────────┤
│ DOMÍNIO — WORLD MODEL                                               │
│ PlanetConfig | SphericalGrid | Field<T> | Feature | Entity           │
│ SpeciesDefinition | Population | Epoch | Event | Constraint          │
├──────────────────────────────────────────────────────────────────────┤
│ MOTORES / SOLVERS                                                   │
│ SphericalGeometry  │ Geodynamics │ SurfaceProcesses │ Hydrology     │
│ Insolation/Climate │ Chemistry   │ Biomes/Ecology  │ Civilization   │
│ TemporalEvolution  │ CityGen     │ DungeonGen      │ FantasyRules    │
├──────────────────────────────────────────────────────────────────────┤
│ DADOS E INTEROPERABILIDADE                                          │
│ ProjectStore | IndexedDB | ZIP+binary layers | JSON/GeoJSON         │
│ MarkdownAdapter | HTMLViewerExporter | Raster/SVG exporters          │
└──────────────────────────────────────────────────────────────────────┘
             ↓ exporta / importa mediante contrato
         Urbe (notas .md)    Lunet2D    sites HTML    jogos
```

### Limites obrigatórios

1. A UI **nunca** é a fonte de verdade de altura, temperatura, espécies ou entidades.
2. Toda geração depende de `seed`, `config`, versão do algoritmo e intervenções autorais persistidas.
3. `WorldState` é o dado canônico; caches e tiles derivados são regeneráveis.
4. Um sistema só recalcula **o que depende** dos dados modificados. Quando existir dependência cíclica, use iterações com critério de parada e orçamento explícito.
5. Modelo físico, heurística e exceção fantástica são conceitos separados e visíveis.
6. Um arquivo `.md` permanece Markdown; um ponto no mapa guarda um vínculo e, opcionalmente, uma cópia incorporada para publicação.

## 3. Grafo causal dos sistemas

```text
órbita, estrela, atmosfera, raio planetário
    ├───────────→ radiação solar ──→ energia/clima ──────────┐
    │                                                       │
tectônica → elevação → gradientes de pressão/ventos ────────┤
    │          ├────────────→ precipitação orográfica         │
    │          ↓                                              ↓
    └──────→ drenagem/erosão ← solo ← vegetação ← biomas ← clima
                             ↑       ↕                ↑
                         sedimentos  nutrientes      espécies
                                          ↕          ↕
                                     teias tróficas / populações
                                               ↓
                                      ocupação/civilizações
                                               ↓
                                     uso da terra e impactos
```

Este não é um DAG puro. Erosão modifica relevo; vegetação modifica solo e água; civilizações modificam vegetação e rios. A arquitetura deverá agrupar esses ciclos em passos de evolução coerentes, com convergência aproximada ou integração temporal explícita.

## 4. Resoluções e temporalidade

| Escala | Exemplos de modelos | Estratégia |
|---|---|---|
| Planetária | insolação, macroplacas, oceanos, zonas climáticas | malha esférica global e aproximações |
| Regional | bacias, erosão, solos, habitats, fronteiras | refinamento local e tiles |
| Local | cidades, fortalezas, cavernas, dungeons | geradores especializados sob demanda |
| Tempo geológico | placas, erosão, especiação, costas | passos adaptativos, snapshots, eventos |
| Tempo ecológico | reprodução, morte, dispersão, sucessão | populações agregadas por habitat |
| Tempo humano | assentamentos, conflitos, construção | eventos, agentes agregados e cronologia |

**Regra:** não calcular milhões de passos diários de um modelo meteorológico para simular uma era de 10 milhões de anos. O modelo deve operar na escala temporal adequada a cada processo.

## 5. O que foi efetivamente implementado na Alpha 0.1

| Função | Estado / significado |
|---|---|
| Geração por seed | **Funcional.** Ruído 3D sobre a esfera, com continuidade no meridiano de 180°. |
| Oceano configurável | **Funcional.** Corte de elevação escolhido por quantil ponderado pela área esférica. |
| Relevo editável | **Funcional.** Pincéis de elevar/escavar na projeção plana. |
| Parâmetros planetários | **Parcial.** Raio escala a contribuição da área na drenagem; inclinação axial modifica insolação anual; temperatura base afeta temperatura. |
| Insolação | **Aproximação funcional.** Integra amostras sazonais da incidência solar por latitude e inclinação axial. |
| Precipitação | **Heurística funcional.** Latitude, ruído, efeito orográfico local e proximidade grosseira com a costa. Não há circulação atmosférica dinâmica. |
| Rios | **Aproximação funcional.** Direcionamento D8 por menor altitude, acumulação de fluxo e visualização. Não resolve depressões internas nem aplica conservação hídrica integral. |
| Biomas | **Classificação heurística.** Temperatura, chuva e altitude; sem simulação real de flora. |
| Globo | **Funcional.** Projeção ortográfica raster com rotação longitudinal; não é malha 3D. |
| Marcadores | **Funcional.** Castelo, cidade, ruína, acampamento e dungeon com nome e descrição. |
| Markdown | **Funcional, simplificado.** Importação de arquivo .md com até 1 MB e renderização segura de subconjunto da sintaxe. Sem sincronização com arquivo original. |
| Hierarquia de mapas | **Prova de conceito.** Inspecionar abre esquema local ilustrativo; ruas e edifícios não constituem simulação urbana. |
| Eras | **Prova de conceito.** Passos cumulativos de 10 mil anos com erosão local simplificada e snapshots. Não simula placas, evolução ou eventos geológicos completos. |
| Salvar/importar mundo | **Funcional.** JSON versionado com campo de elevação codificado em base64, parâmetros, eras e objetos. |
| Publicar HTML | **Funcional.** HTML autossuficiente com quatro imagens de camadas, marcadores clicáveis, Markdown incorporado e inspeção ilustrativa. |
| Fauna, flora e cadeias alimentares | **Não implementado.** |
| Placas tectônicas físicas | **Não implementado.** |
| Química, biogeoquímica e meteorologia dinâmica | **Não implementado.** |
| Sistema de civilizações | **Não implementado.** |
| Salvamento automático / armazenamento IndexedDB | **Não implementado.** Os arquivos são explicitamente baixados/carregados. |

A Alpha usa uma **malha retangular equiretangular de 360×180**, com posições geográficas esféricas. Isso serve à prova funcional, não à simulação de alta resolução. Processos que dependem de área local já possuem ponderação por latitude, mas distâncias de vizinhança, erosão e dispersão ainda não usam uma métrica esférica rigorosa.

## 6. Contrato de dados do protótipo

O JSON atual contém:

```json
{
  "format": "atlasforge.world",
  "version": 1,
  "width": 360,
  "height": 180,
  "seed": "AURORA-42",
  "params": {
    "ocean": 68,
    "tilt": 23.5,
    "radius": 6371,
    "baseTemp": 15
  },
  "elevation": "BASE64_DE_ALTURAS_QUANTIZADAS_U16",
  "entities": [
    {
      "id": "p-...",
      "type": "castle",
      "name": "Fortaleza de Miravento",
      "lat": 37,
      "lon": -9,
      "desc": "...",
      "mdName": "Fortaleza de Miravento.md",
      "md": "# Fortaleza de Miravento"
    }
  ],
  "era": 0,
  "eras": [{"year": 0, "height": "BASE64_U16"}]
}
```

Altura do protótipo é **normalizada de -1 a 1**, não altitude SI precisa. Os metros ilustrativos são derivação visual. A próxima revisão do modelo deve criar `ElevationMeters` e `BathymetryMeters` canônicos, com unidades e datum explícitos.

### Formato futuro proposto

```text
WorldArchive.zip
├── world.json                  versão, parâmetros e algoritmo
├── layers/                    rasters georreferenciados binários
├── geometry/                  linhas, polígonos e entidades GeoJSON
├── species/                   definições e relações tróficas
├── epochs/                    eventos e estados temporais
├── places/                    mapas locais
├── documents/                 snapshots Markdown escolhidos
└── manifest.json              hashes, licenças, referências e versões
```

**Não adotar este formato como contrato oficial sem testes de consumidor e revisão arquitetural.** O contrato canônico do protótipo ainda é JSON v1.

## 7. Integração com o Urbe

Caminho imediato e sem risco: selecionar arquivos `.md` manualmente no Atlas Forge. O aplicativo armazena uma cópia de texto no projeto e na publicação, sem tocar no vault.

Caminho futuro recomendado:

- Criar mapeamento de entidades com um ID estável da nota e, como fallback, caminho relativo dentro do vault.
- Resolver arquivos através de uma interface autorizada de leitura, concedida pelo usuário/Host.
- Expor prévia, atualização explícita e indicação de vínculos quebrados.
- Publicar HTML externo consumível por qualquer navegador; para integração nativa, avaliar renderização própria em Avalonia **sem presumir WebView**.
- Nunca editar diretamente `.urbe/notas.json`, `.urbe/mapa.json` ou `.urbe/mundo.json`.
- Nunca reintroduzir a simulação retirada do Urbe para efetuar a integração.

## 8. Roadmap de arquitetura

**M1 — Refatoração de núcleo:** separar `world-model`, `geometry`, `generation`, `climate-lite`, `hydrology-lite`, `renderer`, `editor`, `persistence` e `viewer`; extrair o cálculo pesado para Worker; adicionar teste determinístico para cada solver e semântica de sobreposições autorais.

**M2 — Geofísica:** placas esféricas com centro, bordas e velocidades; leis simplificadas de convergência/divergência; altimetria em metros; hipsometria, erosão e sedimentos com balanços documentados.

**M3 — Hidrologia e clima:** preencher/classificar depressões e bacias endorreicas; corrigir distância e área da grade; conservação aproximada da água; circulação atmosférica e oceanos em resolução apropriada; calibração climática.

**M4 — Ecologia:** banco de espécies e traits; redes tróficas; estoques de biomassa e populações por região; capacidade de suporte, dispersão e mortalidade; testes de sustentabilidade de cadeias alimentares.

**M5 — Tempo profundo:** modelo de eventos, evolução de placas, mudança climática em épocas, estados diferencias, ramificações semânticas e extensões locais.

**M6 — Mundo habitado:** geração topológica de cidades, dungeons, cavernas e mapas filhos; entidades persistentes e navegação completa; civilizações opcionais por regras.

**M7 — Integração e produção:** empacotamento versionado e interoperável; visualizador HTML independente; Markdown expandido e seguro; recursos offline; avaliação de contratos de Ecosystem com consumidores concretos.

## 9. Critérios de qualidade

- Reprodutibilidade sob seeds e configurações iguais.
- Mensuração do custo por célula, frame e cenário.
- Comparação visual e quantitativa com cenários sintéticos de geografia.
- Sem afirmar conservação física ou precisão climática em modelos heurísticos.
- Sem modificar notas/vault do Urbe sem permissão e protocolo nativo próprio.
- Sem substituir dados autorais com regeneração silenciosa.
- Sem depender de rede para criar, abrir, salvar e visualizar um projeto já distribuído.
- Testes automatizados em Chromium e validação real em Android antes de declarar homologação mobile.

## 10. Evidências da prova de conceito

Foram executados testes de navegador Chromium usando Playwright, com aprovação de:

1. Geração e regeneração determinísticas por seed.
2. Alteração de seed produzindo outro relevo.
3. Avançar era, voltar e avançar novamente.
4. Exportar e reimportar JSON, preservando entidades, seed e era.
5. Alternar para visualização em globo.
6. Exportar visualizador HTML, clicar em castelo e ler Markdown.
7. Inspecionar mapa local, voltar ao mapa-múndi e alternar camada.
8. Renderizar interface em viewport Android simulada, sem exceções JavaScript.

**Limitação da evidência:** navegador automatizado não substitui teste no dispositivo Android real, medições de desempenho ou validação científica.
