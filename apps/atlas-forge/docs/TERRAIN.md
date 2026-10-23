# Terreno contínuo e escalas — AF-017

O mapa regional é uma grade de amostras físicas; ampliar seus pixels não produz terreno. A versão 0.4 acrescenta uma superfície contínua gerada sob demanda, com escala espacial em metros, e conserva o modelo global para seus solvers atuais.

## Campo e autoria

Algoritmo A `spherical-detail-1`: interpola o relevo regional e acrescenta um residual determinístico em coordenadas cartesianas da esfera. O ruído de valor usa oito escalas métricas (24 km até 1,5 m), stream independente e amplitudes versionadas. Subtrai a interpolação do residual nos nós regionais: toda amostra regional, inclusive override autoral, permanece uma âncora exata. O resultado tem altura consultável, gradiente e cobertura local; não é um novo continente sorteado por tile.

A costa usa atenuação do residual próxima ao datum, e os polos têm valor único. Esse relevo é procedural e não simula tectônica/erosão. Materiais usam altitude local, temperatura e chuva regionais interpoladas e a aproximação de lapse rate. IDs discretos de bioma não são ampliados em blocos retangulares. Coberturas: água, grama, mata, areia, rocha e neve. Tufos de grama são representação da cobertura, não uma população vegetal simulada. O clima/drenagem continuam globais.

O campo local é cache derivado de seed + grade + versão. A definição v2/algoritmo global implica a versão local 1; não trocar esse algoritmo mantendo o ID. Uma versão futura com edição métrica persistente precisa de formato/migração e uma autoridade explícita para suas restrições. Pincéis regionais são bloqueados no detalhe; marcadores/notas funcionam com coordenadas precisas.

## Blocos e níveis de detalhe

Blocos equiretangulares de 96×96 amostras. No nível z, há `2^(z+1)` blocos longitudinais e `2^z` latitudinais. Passo N–S: `πR / (96 × 2^z)`; E–W é reduzido por `cos(latitude)`. O nível máximo é escolhido para passo N–S ≤1 m. Não há alocação de todas essas células: só os blocos visíveis são solicitados.

LOD acompanha metros por pixel, com redução de nível quando necessário para limitar o conjunto visível a 80 blocos. Cache LRU: até 96 blocos. Normal e cores usam uma borda extra de amostras do mesmo campo. O plano desenha retângulos adjacentes; o globo usa malha de triângulos com recorte sobreposto para evitar fissuras. A camada Altitude inclui curvas locais calculadas das alturas.

Worker recebe o contexto regional e gera blocos em fila substituível; prioridade muda com a vista. Epoch invalida respostas de um mundo anterior. CPU pesada fica fora do editor. O cache é reutilizado ao editar nome/notas/marcadores e pausado ao voltar ao mapa global. Isso ainda não é refinamento regional dos solvers científicos.

## Escala e inspeção

Presets Planeta, Região (~500 m/pixel), Terreno (~5 m/pixel), Chão (~3 cm/pixel). Zoom mantém a posição geográfica; seu teto é calculado pela dimensão do planeta/viewport para alcançar o chão. Um texel de cobertura métrica fica grande o suficiente para mostrar grama; a área real da amostra local aparece no inspetor.

A altura local vem do campo contínuo, não da cor de uma imagem. Latitude/longitude têm sete casas no detalhe. Temperatura, chuva e vazão são identificadas como globais. A publicação contém a mesma versão do avaliador e reconstrói os blocos offline; não depende de servidor de tiles.

## Evidências e limites

`tests/surface.test.mjs`: seed, continuidade métrica, antimeridiano/polo, âncoras autorais, bordas e parentesco de tiles, roundtrip e teto de espaçamento. `tests/terrain-browser.mjs`: arquivo offline, planeta → região → terreno → grama, altura consultada, pan, LRU, marcador explícito, publicação offline e globo.

O relatório `test-results/terrain-report.json` registra tempos de chegada de blocos e escala efetiva; não é promessa de desempenho em Android físico. Não há ainda erosão física fina, edição métrica persistente, população de grama, ruas ou edifícios gerados neste campo. Esses sistemas continuam no mandato e no roadmap com critérios próprios.
