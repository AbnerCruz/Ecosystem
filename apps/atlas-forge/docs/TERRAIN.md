# Terreno contínuo e escalas — AF-017

O mapa regional é uma grade de amostras físicas; ampliar seus pixels não produz terreno. A versão 0.4.1 usa a mesma superfície contínua em toda a navegação, gerada sob demanda, com escala espacial em metros, e conserva o modelo global para seus solvers atuais.

## Campo e autoria

Algoritmo A `spherical-detail-1`: interpola o relevo regional e acrescenta um residual determinístico em coordenadas cartesianas da esfera. O ruído de valor usa oito escalas métricas (24 km até 1,5 m), stream independente e amplitudes versionadas. Subtrai a interpolação do residual nos nós regionais: toda amostra regional, inclusive override autoral, permanece uma âncora exata. O resultado tem altura consultável, gradiente e cobertura local; não é um novo continente sorteado por tile.

A costa usa atenuação do residual próxima ao datum, e os polos têm valor único. Esse relevo é procedural e não simula tectônica/erosão. Materiais usam altitude local, temperatura e chuva regionais interpoladas e a aproximação de lapse rate. IDs discretos de bioma não são ampliados em blocos retangulares. Coberturas: água, grama, mata, areia, rocha e neve. Tufos de grama são representação da cobertura, não uma população vegetal simulada. O clima/drenagem continuam globais.

O campo local é cache derivado de seed + grade + versão. A definição v2/algoritmo global implica a versão local 1; não trocar esse algoritmo mantendo o ID. Uma versão futura com edição métrica persistente precisa de formato/migração e uma autoridade explícita para suas restrições. Pincéis regionais são bloqueados no detalhe; marcadores/notas funcionam com coordenadas precisas.

## Blocos e níveis de detalhe

Blocos equiretangulares de 96×96 amostras. No nível z, há `2^(z+1)` blocos longitudinais e `2^z` latitudinais. Passo N–S: `πR / (96 × 2^z)`; E–W é reduzido por `cos(latitude)`. O nível máximo é escolhido para passo N–S ≤1 m. Não há alocação de todas essas células: só os blocos visíveis são solicitados.

LOD acompanha metros por pixel, com redução de nível quando necessário para limitar o conjunto visível a 48 blocos no nível alvo, com seus ancestrais. Cache LRU: até 96 blocos. Normal e cores usam uma borda extra de amostras do mesmo campo. O plano desenha retângulos adjacentes; o globo usa projeção inversa por pixel, inclusive no limbo e polos, lendo a mesma pirâmide de tiles. A camada Altitude inclui curvas locais calculadas das alturas.

Worker recebe o contexto regional e gera blocos em fila substituível; prioridade muda com a vista. Epoch invalida respostas de um mundo anterior. CPU pesada fica fora do editor. O cache é reutilizado ao editar nome/notas/marcadores em todas as escalas, inclusive ao voltar ao planeta. Isso ainda não é refinamento regional dos solvers científicos.

## Escala e inspeção

Roda, botões +/− e pinch percorrem toda a escala continuamente. Os atalhos de zoom (mundo inteiro, 500 m/pixel, 5 m/pixel, 3 cm/pixel) apenas deslocam a mesma câmera; não selecionam cenas. Zoom mantém a posição geográfica; seu teto é calculado pela dimensão do planeta/viewport para alcançar o chão. A grama emerge gradualmente conforme seu tamanho físico ocupa pixels; a área real da amostra local aparece no inspetor.

A altura local vem do campo contínuo, não da cor de uma imagem. Latitude/longitude têm sete casas no detalhe. Temperatura, chuva e vazão são identificadas como globais. A publicação contém a mesma versão do avaliador e reconstrói os blocos offline; não depende de servidor de tiles.

## Evidências e limites

`tests/surface.test.mjs`: seed, continuidade métrica, antimeridiano/polo, âncoras autorais, bordas e parentesco de tiles, roundtrip e teto de espaçamento. `tests/terrain-browser.mjs`: arquivo offline, planeta → região → terreno → grama, altura consultada, pan, LRU, marcador explícito, publicação offline e globo.

O relatório `test-results/terrain-report.json` registra tempos de chegada de blocos e escala efetiva; não é promessa de desempenho em Android físico. Não há ainda erosão física fina, edição métrica persistente, população de grama, ruas ou edifícios gerados neste campo. Esses sistemas continuam no mandato e no roadmap com critérios próprios.

## Correção AF-018 — continuidade visual

A 0.4 usava raster/paleta globais até 1.500 m/pixel e outro desenho abaixo desse limiar. Isso foi rejeitado pelo proprietário: aproximar e afastar devem percorrer o mesmo mundo. A 0.4.1 remove esse desvio nas camadas naturais e de altitude. Desde o enquadramento do planeta, cores, alturas e materiais vêm do mesmo avaliador e da mesma pirâmide; camadas científicas discretas continuam mostrando suas próprias grandezas.

Os dois blocos raiz ficam residentes. Ancestrais visíveis são requisitados antes dos filhos e permanecem desenhados durante o carregamento. Filhos entram com transição de 180 ms, combinada à fração contínua de LOD da câmera. A volta não depende de uma cena anterior salva: as mesmas coordenadas e níveis recompõem a imagem. A textura fina usa coordenadas físicas, nunca uma nova seed por nível. Rios regionais conservam suas coordenadas ao aproximar, em vez de desaparecer no antigo limiar.

`tests/zoom-browser.mjs` exercita a roda em ambas as direções, compara altura/âncora, verifica cobertura durante o carregamento, imagem na volta, antigo limiar e cobertura do globo. Grava `zoom-continuity.webm` e `zoom-continuity-report.json`. Não confundir cobertura verificada com validação humana da qualidade visual. O formato v2 e as alturas de `spherical-detail-1` são preservados; esta correção altera amostragem/desenho/cache, não o mundo salvo.
