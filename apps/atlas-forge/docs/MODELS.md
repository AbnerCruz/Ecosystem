# Modelos, equações e aproximações

Classificação: **A** heurístico/visual; **B** físico simplificado com grandezas; **C** validado para cenários específicos. Nenhum módulo deste corte é C. Testes sintéticos demonstram propriedades, não validação científica geral.

## Geometria

Datum: esfera ideal de raio R metros, altitude relativa a um nível do mar fixo z=0. Latitude em graus norte e longitude periódica em `[-180,180)`. Células amostradas nos centros de uma grade explícita: criação em 360×180, 720×360 ou 1.440×720; importação admite grades pares 12–1.440, altura = largura/2. O helper de domínio conserva seu default histórico 180×90; a UI pede 720×360 explicitamente. O bitmap do viewport e a grade física são diferentes.

Área de uma célula: `R² Δλ [sin(φ_norte) − sin(φ_sul)]`. Soma global: `4πR²`. Distância usa haversine em círculo máximo, com clamp de arredondamento. Vizinha D8; ao cruzar polo, a linha reflete e longitude gira 180°. Não se conecta todo o anel polar a um supernó. As diagonais são arestas idealizadas; a malha não é isotrópica. Normas e unidades têm testes com tolerância relativa de 1e−12 para área e 1e−7 rad para roundtrips.

## AF-022 — placas cinemáticas, clima métrico e mosaicos (2026-10-24)

**Versão de algoritmo do mundo:** `spherical-tectonic-2` é a única autoridade. O modelo antigo `spherical-causal-1` e formatos v1/v2 não são aceitos por decisão expressa do proprietário; o importador informa a incompatibilidade e preserva o arquivo externo. O formato atual dos novos projetos continua v3. Modelo de placas: `spherical-kinematics-1`. O avaliador métrico de alturas locais mantém `spherical-detail-1` sem alteração de campo; apenas a apresentação dos materiais usa `pixel-ramp-2`.

**Modelo de placas A (heurístico cinematicamente coerente, não geologia validada):** 24 centros distribuidos na esfera por sequência de Fibonacci com desvio determinístico e seis províncias cratônicas amplas; cada placa tem uma classificação de crosta e um vetor de velocidade angular `ω` tangencial. A velocidade no ponto `p` é `v=ω×p`. Para as duas placas mais próximas, `δ = acos(p·c₂) − acos(p·c₁) ≥ 0` mede proximidade à fronteira, com influência `w = exp(−δ/0,115 rad)`. Convergência `C = max(0, ((v₁−v₂)·n)/0,035)` utiliza a normal tangente à fronteira. Uma função explícita e limitada transforma compressão em quilômetros de orogênese, divergência em rifte/dorsal, e ondas de ruído em relevo dobrado. Províncias cratônicas atravessam várias placas e controlam flutuação regional e plataformas costeiras, para evitar que continentes sejam polígonos de Voronoi. A diferença angular entre as duas placas mais próximas usa aproximação local em produtos escalares no caminho de alta resolução para evitar milhões de operações acos. O nível marinho vem do quantil ponderado por área (como na versão legada). Sem placas editáveis/persistidas individualmente, cálculo de magma, espessura litosférica ou tempo geológico tectônico; isto **não substitui AF-006**.

**Climatologia A/B simplificada:** velocidade zonal determina transporte estacionário periódico de uma umidade `q` com unidade efetiva mm·m/ano; em cada célula, comprimento zonal `Δx = 2πR cos(φ)/largura` e reposição marítima `q += (q_saturação−q)(1−exp(−Δx/420 km))`. Chuva `P = 55 + q_antes · f / Δx · banda_latitudinal` mm/ano, onde `f` compõe remoção advectiva por `Δx` e elevação a barlavento (`1−exp(−subida/1250 m)`); 6 varreduras estabilizam a costura. Temperatura usa lapse rate de 6,5 K/km, declínio latitude e sazonalidade dependente de latitude/tilt/oceano. **Não é** previsão meteorológica, balanço de energia acoplado à insolação, circulação tridimensional ou clima validado. Não há modo de clima legado nem bifurcação do solver por versão antiga.

**Biomas e arte:** biomas de novos mundos combinam temperatura, chuva e um índice de demanda hídrica crescente com calor; a renderização interpola clima e usa mosaicos espaciais contínuos com variação de chuva em escala ~1,2 km, reduzindo fronteiras geométricas retas sem afirmar solos físicos. O renderer usa nearest-neighbor tanto no plano quanto no globo, sprites procedurais cacheados de 16×16 e rampas discretas em 4 cores por material, sem mudar alturas/máscaras/espécies. Imagens são estilização **16-bit-inspired**, não simulação de botânica ou um sprite sheet de artista. Reaproveitar pixels do globo quando quadro estabilizado reduz recomputações; medir desempenho real no Android separadamente.

## Superfície — A

24 ondas senoidais determinísticas em coordenadas cartesianas unitárias, seis bandas de frequência. Isso garante continuidade da amostragem esférica e não representa placas. Quantil ponderado por área escolhe nível oceânico conforme porcentagem desejada; altura = 1.800 m × diferença do ruído para o quantil. Limite ±12.000 m. Clamping é explícito e não representa processo geológico.

Uma seed, configuração e algoritmo `spherical-causal-1` fixam o resultado. Streams são derivados de `seed:subsystem`; nenhum estado aleatório global. Relevo autoral é override absoluto em metros, aplicado após geração. Somente o comando explícito de descartar intervenções remove máscaras. Undo/redo usa valores e máscaras anteriores, não renderizações.

## Temperatura e umidade — A

Média térmica: `T = T_base + 24 [cos(latitude) − 0,65] − 0,0065 max(z,0)` em °C. Amplitude entre solstícios = `0,45 × tilt × sin(latitude)` (°C), atenuada por 0,35 em oceanos. Junho aquece o norte e dezembro aquece o sul. Não há balanço radiativo, circulação atmosférica dinâmica ou correntes oceânicas.

Vento zonal parametrizado: leste entre latitudes absolutas 30–60°, oeste nas demais. Reservatório de umidade adimensional `q`: no mar, `q += 0,45(1−q)`; em cada passo precipita fração `f = clamp(0,025 + ascent/3.200m, 0,025, 0,85)`, com `q -= q f`. Chuva aproximada = `70 + 20.000 q_anterior f [0,45 + 0,55 cos²(latitude)]` mm/ano. Cinco varreduras periódicas, resultado da última, reduzem influência da costura; não garantem convergência física ou continuidade exata da chuva na costura. O reservatório é reposto nos oceanos; sem oceano a chuva de fundo continua como aproximação explícita.

Esse modelo mostra barlavento e sombra de chuva em um cenário de cordilheira sob vento fixo. Transporte não é métrico e varia com resolução; não interpretar resultados como previsão, balanço hídrico planetário ou calibração terrestre. Chuva é anual; o seletor de estação muda somente temperatura.

O utilitário `insolation` implementa insolação média diária em órbita circular com 1.361 W/m², declinação sazonal e ângulo horário limitado na noite/dia polar. É testado, **mas ainda não determina o campo térmico da UI**. A aproximação futura pertence a AF-008.

## Hidrologia — B

Cada célula terrestre escolhe a maior queda positiva `(z_i−z_j)/d_esférica(i,j)` entre vizinhos. Declive estrito implica grafo acíclico; não há rios que sobem nem preenchimento artificial para chegar ao oceano. Células oceânicas terminam em oceano; mínimas terrestres e flats sem queda são sumidouros fechados, não necessariamente lagos reais. A extensão/volume de lago não é modelada.

Fonte de vazão: `Q_i = P_i/1000 × 0,35 × A_i / segundos_por_ano` em m³/s. Acumulação segue ordem topográfica decrescente. IDs das bacias são os índices dos terminais. Toda água de runoff termina em mar ou sumidouro: a soma nas saídas coincide com as fontes dentro de erro relativo 1e−12 no cenário de teste. Os 65% restantes são uma perda fixa agregada (evapotranspiração/infiltração), não reservatórios simulados.

Somente segmentos acima de 1.500 m³/s aparecem como rios; o campo completo continua no inspetor. Segmentos cruzando a costura são omitidos pelo renderer para não traçar uma reta pelo planeta inteiro; a conectividade física permanece correta. Esse limite de renderização deve ser resolvido em AF-005.

## Biomas e mapas locais — A

Biomas são classes discretas de temperatura, chuva e altitude. Não modelam solos, biomassa, sucessão, competição ou fauna. O mapa filho é um grafo conectado de nove salas baseado em seed + identidade da entidade; o resultado fica persistido. Não é uma cidade nem um solver arquitetônico. Escala local é referência de 100 m.

## Processamento e limites

Pipeline: relevo → clima → hidrologia → biomas. Recálculo atual é global, executado em Worker. Cancelamento termina o Worker, invalida job ID e conserva o último estado completo; não há commit parcial. Raster por camada é cache derivado. O globo reaproveita lookup da projeção inversa (latitude/longitude relativas à câmera), aplica rotação por deslocamento longitudinal no viewport visível em até 2× DPR; a textura não é limitada a 640×640 nem cresce com a área invisível do planeta ampliado. A grade física só muda por criação/refinamento explícito. Curvas de nível usam interseções lineares em dois triângulos por célula; subdividir para desenhar não acrescenta informação física. LOD visual e cache multirresolução são descritos em TERRAIN.md; recálculo regional de solvers ainda não existe. Até 30 comandos por direção, 12 marcos completos e 24 MiB por projeto; esses limites evitam retenção sem limite, não substituem benchmark mobile.

## Resolução e autoria

No raio terrestre, 1.440 × 720 equivale a cerca de **27,8 km N–S por célula** (E–W diminui com cos(latitude)). É resolução regional; ruas, edifícios e detalhes de metros não podem ser inferidos desse terreno. O zoom 128× serve também para selecionar, posicionar e inspecionar lugares autorais. A interface mostra o limite em vez de sintetizar texturas como se fossem dados. O campo procedural mantém seis bandas e a mesma versão de algoritmo; resolução maior amostra a mesma função com maior densidade, não acrescenta geologia.

Refinar mantém altitudes absolutas em toda a pegada angular dos overrides e recalcula derivados. Não há invariância de precipitação à resolução neste modelo; essa limitação já existente permanece. O projeto anterior é preservado em marco com sua própria grade; ao restaurá-lo, suas dimensões, máscaras e diário retornam integralmente.

## Entrada solar sazonal — B (AF-016)

`daily-mean-circular-1`: `Q = S0/π × [H sinφ sinδ + cosφ cosδ sinH]`, em W/m² no topo da atmosfera; `S0=1.361 W/m²`, `δ=tilt×sin(2πfase)`, `H=acos(clamp(-tanφ tanδ,-1,1))`. Órbita circular, amostras no centro das linhas; 96 fases para média anual. Junho/dezembro usam fases 0,25/0,75. Noite/dia polar são tratados pelo limite do ângulo horário.

Campos por latitude, sem variação longitudinal neste modelo ideal. Testes em 720×360 e tilt 0/23,5/60 verificam média ponderada por área `S0/4 = 340,25 W/m²` nos três campos, erro absoluto <0,005 W/m². É uma identidade geométrica no modelo ideal, não calibração de uma atmosfera real. Não inclui albedo, nuvens, gases, órbita excêntrica ou calor oceânico; ainda não é a entrada do campo térmico heurístico. A interface e publicação identificam a separação.

## Terreno métrico — A (AF-017)

Ver [TERRAIN.md](TERRAIN.md). O detalhe local usa um campo contínuo e versionado, ancorado nas altitudes regionais, com amostras sob demanda ≤1 m N–S no nível mais fino. A resolução do chão não muda a resolução dos solvers globais. Grama no chão é cobertura procedural. A biosfera agregada abaixo afeta erosão regional; não determina cada lâmina de grama.

## Biosfera `resources-trophic-1` — B, parâmetros heurísticos

Grade de até 36×18 regiões esféricas; densidades kg/m², áreas reais em m². Não são agentes individuais nem previsão de populações terrestres. Massa individual é metadado de espécie; a dinâmica usa biomassa. Clima/relevo amostrados no centro de cada região determinam adequação 0–1 conforme habitat terrestre/aquático, intervalo térmico e chuva mínima.

Em cada ano: decomposição de detritos a taxa 0,2/ano libera N; produtores disputam o estoque mineral proporcionalmente às demandas `produtividade × adequação × B/(0,1+B)/(1+B/3)`. Todos têm composição fixa 0,02 kg N/kg de biomassa. Entrada de carbono fotossintético e saída por respiração são contabilizadas, mas não há atmosfera química acoplada nem balanço de energia.

Consumidores requisitam alimento proporcionalmente à biomassa disponível de cada presa; demandas simultâneas compartilham a oferta, sem estoque negativo. Fração assimilada vira biomassa; restante e mortalidade viram detritos. Mortalidade efetiva = taxa configurada + `0,6 × (1−adequação)` por ano. Ausência de alimento não cria crescimento; populações decaem. Decompositores podem consumir `detritus`. Presets incluem gramíneas, árvores, cervos, bovinos, leões, algas, peixes e fungos. Bovinos manejados começam com estoque zero.

Dispersão usa vizinhos adequados e distâncias esféricas: fração máxima anual 0,2, limitada por velocidade/distância e dividida pelos destinos. Transferências são ponderadas pelas áreas para conservar massa. Não há migração entre escalas, comportamento, evolução genética, incêndio ou doenças. O usuário introduz explicitamente espécies novas; isso é um aporte externo de biomassa/N.

## Erosão e eras — B, coeficientes heurísticos

A cada 100 anos (ou resto final), profundidade erodida em metros = mínimo de 2% da queda para o destino e `anos × 0,002 × chuva/1000 × declive × (1−0,85×cobertura)`. Cobertura = biomassa de produtores terrestres / 2, limitada a 0–1. Volume removido é depositado no vizinho jusante, dividido pela área do destino; limite de deposição mantém teto de 12.000 m. Células com override são fixas e não trocam sedimentos. Clima, drenagem e habitats são recalculados depois do transporte.

Era avança de 1 a 10.000 anos em passos ecológicos anuais, mantendo a relação causal com o estado anterior. Balanços de N e matéria orgânica, conservação de volume, redução de erosão pela vegetação e restauração de marcos têm cenários sintéticos. Não é tectônica: não há placas móveis, soerguimento, estratigrafia, ciclo das rochas ou mudança do nível do mar. Relatórios mostram resíduos numéricos; não são calibração científica. Plantas afetam relevo pela erosão regional, sem atualização física de solo a cada metro.
