# Modelos, equações e aproximações

Classificação: **A** heurístico/visual; **B** físico simplificado com grandezas; **C** validado para cenários específicos. Nenhum módulo deste corte é C. Testes sintéticos demonstram propriedades, não validação científica geral.

## Geometria

Datum: esfera ideal de raio R metros, altitude relativa a um nível do mar fixo z=0. Latitude em graus norte e longitude periódica em `[-180,180)`. Células amostradas nos centros de uma grade explícita: criação em 360×180, 720×360 ou 1.440×720; importação admite grades pares 12–1.440, altura = largura/2. O helper de domínio conserva seu default histórico 180×90; a UI pede 720×360 explicitamente. O bitmap do viewport e a grade física são diferentes.

Área de uma célula: `R² Δλ [sin(φ_norte) − sin(φ_sul)]`. Soma global: `4πR²`. Distância usa haversine em círculo máximo, com clamp de arredondamento. Vizinha D8; ao cruzar polo, a linha reflete e longitude gira 180°. Não se conecta todo o anel polar a um supernó. As diagonais são arestas idealizadas; a malha não é isotrópica. Normas e unidades têm testes com tolerância relativa de 1e−12 para área e 1e−7 rad para roundtrips.

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

Pipeline: relevo → clima → hidrologia → biomas. Recálculo atual é global, executado em Worker. Cancelamento termina o Worker, invalida job ID e conserva o último estado completo; não há commit parcial. Raster por camada é cache derivado. O globo reaproveita lookup da projeção inversa (latitude/longitude relativas à câmera), aplica rotação por deslocamento longitudinal no viewport visível em até 2× DPR; a textura não é limitada a 640×640 nem cresce com a área invisível do planeta ampliado. A grade física só muda por criação/refinamento explícito. Curvas de nível usam interseções lineares em dois triângulos por célula; subdividir para desenhar não acrescenta informação física. LOD, tiles, cache multirresolução e recálculo regional não existem ainda. Até 30 comandos por direção, 12 marcos completos e 24 MiB por projeto; esses limites evitam retenção sem limite, não substituem benchmark mobile.

## Resolução e autoria

No raio terrestre, 1.440 × 720 equivale a cerca de **27,8 km N–S por célula** (E–W diminui com cos(latitude)). É resolução regional; ruas, edifícios e detalhes de metros não podem ser inferidos desse terreno. O zoom 128× serve também para selecionar, posicionar e inspecionar lugares autorais. A interface mostra o limite em vez de sintetizar texturas como se fossem dados. O campo procedural mantém seis bandas e a mesma versão de algoritmo; resolução maior amostra a mesma função com maior densidade, não acrescenta geologia.

Refinar mantém altitudes absolutas em toda a pegada angular dos overrides e recalcula derivados. Não há invariância de precipitação à resolução neste modelo; essa limitação já existente permanece. O projeto anterior é preservado em marco com sua própria grade; ao restaurá-lo, suas dimensões, máscaras e diário retornam integralmente.

## Entrada solar sazonal — B (AF-016)

`daily-mean-circular-1`: `Q = S0/π × [H sinφ sinδ + cosφ cosδ sinH]`, em W/m² no topo da atmosfera; `S0=1.361 W/m²`, `δ=tilt×sin(2πfase)`, `H=acos(clamp(-tanφ tanδ,-1,1))`. Órbita circular, amostras no centro das linhas; 96 fases para média anual. Junho/dezembro usam fases 0,25/0,75. Noite/dia polar são tratados pelo limite do ângulo horário.

Campos por latitude, sem variação longitudinal neste modelo ideal. Testes em 720×360 e tilt 0/23,5/60 verificam média ponderada por área `S0/4 = 340,25 W/m²` nos três campos, erro absoluto <0,005 W/m². É uma identidade geométrica no modelo ideal, não calibração de uma atmosfera real. Não inclui albedo, nuvens, gases, órbita excêntrica ou calor oceânico; ainda não é a entrada do campo térmico heurístico. A interface e publicação identificam a separação.

## Terreno métrico — A (AF-017)

Ver [TERRAIN.md](TERRAIN.md). O detalhe local usa um campo contínuo e versionado, ancorado nas altitudes regionais, com amostras sob demanda ≤1 m N–S no nível mais fino. A resolução do chão não muda a resolução dos solvers globais. Grama é cobertura procedural; biologia trófica e efeitos de raízes/pastoreio continuam futuros.
