# Atlas Forge — prompt de execução e critérios do produto

Atualização: 23/10/2026. Entrada de continuidade para [MISSION.md](MISSION.md), que preserva o mandato completo. Não substitui MANIFEST, decisões do Ecosystem, SPEC do aplicativo ou estado vivo de Issues/PRs. O runtime canônico permanece em `apps/atlas-forge`.

## Missão

Construa uma ferramenta HTML offline de autoria, geração e evolução de mundos de alta fantasia, do planeta ao chão. O usuário deve gerar a natureza por seed, desenhar a forma que deseja e deixar o motor completar os sistemas dependentes, ou combinar geração e edição. Sistemas naturais precisam produzir consequências mensuráveis; o autor mantém controle das exceções narrativas.

Não encerre o trabalho num gerador de manchas ou numa demonstração visual. Entregue incrementos usáveis, com modelo, cálculo, Canvas, edição pertinente, persistência, publicação e testes. Identifique honestamente o que falta; botões, ruído e snapshots não são evidência de tectônica, ecologia ou tempo geológico.

## Escolhas confirmadas pelo proprietário nesta retomada

1. **Seed: natureza completa como padrão; cidades, reinos e outros lugares opcionais.** Não inserir castelos, notas ou interiores de exemplo na abertura. Inspecionar não gera conteúdo. O usuário escolhe quais geradores executar.
2. **Física natural como base; magia opcional com regras configuráveis.** Uma exceção mágica deve declarar alcance, efeitos, fontes/sumidouros e interação com as leis naturais, sem contaminar silenciosamente o modelo físico.
3. **Escala do planeta até o tile de grama.** O proprietário explicitou que ampliar uma grade global, mesmo 1.440×720, não atende. A arquitetura precisa de níveis de detalhe e geração local sob demanda; a unidade visual não pode se transformar num continente ao aproximar.
4. **Aplicação independente no Ecosystem.** O Atlas Forge já tem identidade e caminho aprovados por ADR-0035/DEC-0054. Não incorporar a simulação ao Urbe nativo.

## Resultado de uso que orienta a engenharia

O autor informa uma seed ou desenha continentes. O motor produz relevo coerente, águas, clima, solos e habitats. O autor aproxima até um terreno local e seus tiles de chão, coloca/edita elementos de fantasia, configura uma planta e um herbívoro fantástico que depende dela, observa alimentação e dispersão, avança milhares de anos e compara transformações. Ele posiciona uma cidade, associa `Castelo.md`, inspeciona mapas locais encadeados e exporta um HTML em que tudo isso continua consultável sem o editor.

Implemente as dependências desse cenário progressivamente. Não simule sua conclusão com descrições, gráficos inventados ou plantas de nove salas apresentadas como cidades completas.

## 1. Escalas, dados e geração sob demanda

- Separar malha física global, campos regionais, terreno local, objetos e viewport. Um pixel de tela não é a unidade do planeta.
- Geração em blocos determinísticos com IDs e algoritmo versionado; cache limitado, descarte/regeração e prioridade para o viewport atual. Não alocar o planeta inteiro em metros.
- O mesmo ponto deve manter altura e identidade ao mudar de LOD, revisitar uma região, salvar/reabrir e publicar. Bordas de tiles, antimeridiano e polos precisam de testes.
- O terreno local deve ter relevo consultável, encostas e materiais; uma textura de grama repetida sobre um único pixel global não é suficiente. Detalhe geométrico procedural deve ser identificado como tal enquanto não resultar dos motores geológicos.
- Escala métrica, densidade efetiva de amostras e resolução dos dados físicos devem estar visíveis. Clima global não ganha resolução de um metro só porque o chão é renderizado em metros.
- Edição métrica persistente requer modelo versionado de restrições/overrides locais, undo/redo e migração. Não esconder comandos em descrições de entidades ou sobrescrever áreas enormes com um pincel que aparenta ser local.
- Canvas concentra mapas, ferramentas espaciais e navegação. Controles e seleção de arquivos usam HTML semântico; Markdown não deve virar uma imagem sem acessibilidade.

## 2. Três modos de autoria

**Procedural:** seed e parâmetros reproduzem os motores instalados e suas versões. A primeira abertura permanece vazia; gerar é uma ação explícita.

**Desenho:** máscaras de terra/mar, costas, cordilheiras e depressões são restrições autorais. O motor completa altitudes, drenagem, clima e habitats sem perder a silhueta imposta. Documentar conflitos entre restrições, oferecer recalcular/manter exceção e preservar o original.

**Híbrido:** editar um mundo existente, congelar regiões/camadas, comparar estados e invalidar somente dependências afetadas. Fonte canônica única, operações atômicas, cancelamento e proteção contra respostas atrasadas.

## 3. Física, geografia e geologia

- Esfera parametrizável, unidades SI, datum, áreas e distâncias geodésicas, costura e polos corretos.
- Placas persistentes com velocidades angulares; convergência, divergência, falhas, subsidência, crosta, vulcanismo e relevo derivados de processos explícitos.
- Água: precipitação, interceptação, infiltração, escoamento, rios, armazenamento de lagos, evaporação, transbordamento e saídas oceânicas/fechadas. Exibir o balanço e o erro numérico.
- Superfície: erosão, transporte e deposição de sedimentos, rocha, solos e coesão das raízes. Nunca criar/remover material sem contabilizar fontes/sumidouros declarados.
- Validação mínima: divisor de águas, vale inclinado, bacia fechada, crista orográfica, litoral periódico e conservação aproximada de água/sedimentos.

## 4. Meteorologia, ventos e clima

- Distinguir meteorologia transitória de climatologia. Insolação orbital, sazonalidade, albedo e capacidade térmica são entradas de energia; latitude sozinha não deve ser apresentada como modelo atmosférico completo.
- Evoluir para orçamento radiativo, influência de atmosfera/gases, altitude, oceanos e transporte térmico. Declarar todas as aproximações.
- Ventos vetoriais com unidades e transporte métrico; pressão, rotação, Coriolis, fricção, umidade, nuvens, precipitação orográfica e sombra de chuva conforme o motor evoluir.
- Não integrar milhões de dias meteorológicos para passar uma era geológica. Usar escalas de tempo e submodelos adequados.
- Testes: potência incidente global S/4 no caso circular ideal, inversão sazonal dos hemisférios, noite polar, orografia, chuva a sotavento e balanços de energia/água dos modelos que os alegarem.

## 5. Química e biogeoquímica

- Modelar estoques e fluxos escolhidos: água/salinidade, carbono, nitrogênio, fósforo, nutrientes e composição atmosférica/solo; ampliar somente com uma responsabilidade científica definida.
- Intemperismo, ciclagem, decomposição e efeitos em fertilidade/clima devem ter unidades e fontes/sumidouros.
- Não prometer química universal ou validação laboratorial. Cada submodelo declara equações, parâmetros, hipóteses, estabilidade e intervalo útil.

## 6. Flora, fauna e espécies personalizadas

- Catálogo editável com produtores, herbívoros, predadores, onívoros, decompositores e relações tróficas. Presets baseados em espécies reais, incluindo bovinos e leões, com necessidades explícitas. Bovinos domésticos não devem aparecer como fauna selvagem universal sem regra de manejo.
- Espécies próprias têm identidade, habitat, tamanho/biomassa, dieta, recursos necessários, tolerâncias de temperatura/água, reprodução, mortalidade e dispersão. Traits opcionais de fantasia são regras, não apenas texto.
- Populações e biomassa agregadas por habitat; capacidade de suporte depende dos recursos. Não criar um agente por animal de um planeta inteiro.
- Predadores dependem de presas; herbívoros dependem de produtores; reprodução, competição, migração e extinção precisam de causas observáveis.
- Feedback obrigatório: vegetação afeta infiltração, coesão e erosão; sobrepastoreio muda cobertura e solo; clima/relevo/água alteram as espécies. Não tratar ecologia como uma camada decorativa.
- Gates: herbívoro sem alimento declina; predador sem presa declina; dispersão respeita barreiras/habitat; espécie personalizada entra na rede; balanços não ficam negativos nem criam energia sem fonte.

## 7. Eras e causalidade

- Cada era parte do estado anterior e avança um intervalo declarado, incluindo milhares de anos. Persistir configuração, relógio, eventos, diffs e ancestralidade.
- Geologia, clima, ecologia e ocupação operam em escalas compatíveis, com passos adaptativos, estabilidade e orçamento de cálculo. Snapshots continuam úteis para retorno; não são evolução por si só.
- Comparar mudanças reais em relevo, costas, drenagem, cobertura e populações. O total de tempo avançado precisa corresponder aos passos executados.
- Ramificar sem apagar futuros preservados. Mudanças de costa/terreno devem sinalizar impactos em lugares autorais, sem excluí-los silenciosamente.

## 8. Mundo habitado e mapas encadeados

- Posicionar cidades, acampamentos, ruínas, castelos, reinos e demais elementos. Reinos com território exigem geometria territorial; um ponto não representa uma fronteira pronta.
- Assentamentos opcionais consideram água, relevo, recursos e acesso; efeitos de uso da terra são modelados quando disponíveis.
- Cidade → bairro → edifício → interior; ruína, caverna e dungeon podem ter geradores específicos. Cada mapa possui ID, seed/algoritmo, escala, vínculo ao pai, geometria editável e conteúdo persistido.
- Cidades precisam de ruas conectadas, parcelas/edifícios, acessos e adaptação ao sítio. Dungeons precisam de conectividade e entradas. Não reutilizar o mesmo esquema ilustrativo como todos os tipos de local.

## 9. Notas, exportação e Urbe

- O Atlas Forge é um Product independente. O Urbe atual usa C#/Avalonia, sem WebView, e retirou sua simulação visual (ADR-0034). Não reintroduzi-la nem importar o motor de mundos para o cliente de notas.
- Imediato: escolher um `.md` no celular/PC, incorporar uma cópia com identidade/hash/data e exibir Markdown seguro. Original preservado; atualização explícita e ausência/quebra de vínculo identificadas.
- Futuro: adapter sob contrato autorizado e consumidor real; leitura de notas por permissão. Nunca escrever diretamente nos registros internos do vault.
- Publicação principal: HTML interativo offline, dados + renderer, sem editor/solvers planetários. Preservar objetos clicáveis, Markdown e hierarquia. O avaliador necessário para reproduzir terreno procedural local deve ter versão incorporada.
- SVG, PNG/WebP e GeoJSON são formatos auxiliares. SVG sozinho não garante o aplicativo interativo; exportar metadados/IDs e viewer quando necessários. Permitir incorporação em outro HTML com limites de CSP e caminhos documentados.
- Sem executar scripts/HTML/URLs vindos de notas/projetos. Sem envio automático de dados à rede.

## 10. Disciplina científica e produto

Cada sistema é identificado como A (heurística), B (físico simplificado) ou C (validado para cenários explícitos). Equações e testes não autorizam chamar todo o mundo de cientificamente validado.

Uma alteração de algoritmo ou formato tem versão, migração/compatibilidade, fixtures e teste. Campos derivados são caches; não duplicar autoridade. Arquivos desconhecidos não são truncados. Geração/cálculo longo em Worker, cancelável e com commit só no sucesso.

Medir frame, geração, memória e tamanho de arquivo por escala; testar desktop e toque. Emulação não é homologação Android. Registrar Issues, PRs, handoffs e evidências. Sem nova Library/Tool global especulativa nem release alheia como efeito colateral.

## 11. Estado verificado e ordem imediata

A base existente possui esfera, grade global, autoria regional, clima/biomas heurísticos, drenagem simplificada, marcadores/notas, snapshots e viewer offline. Placas, ecologia, química, eras causais, cidades completas e edição métrica persistente continuam lacunas reais.

**Prioridade explícita desta correção: AF-017, planeta → terreno → chão.** O incremento implementa terreno procedural contínuo em tiles, cache limitado, Worker, escala métrica, sondagem local e publicação com o mesmo avaliador. Não promove o modelo de detalhe procedural a tectônica física. AF-016 acrescenta a entrada solar sazonal em W/m²; o clima antigo continua identificado como heurístico até seu acoplamento físico.

Depois: restrições de desenho e edição local versionada; energia/clima e água/lagos; tectônica/erosão/solos; ecologia trófica; eras causais; mundo habitado/geradores locais. Pode haver trabalho incremental nessas frentes, mas nenhum gate é encerrado por existir um controle na UI.

Execute o primeiro item verificável, prove o fluxo no navegador e publique o código pelos mecanismos vigentes. Entregue o HTML usável, as evidências, os limites restantes e a próxima dependência técnica concreta.
