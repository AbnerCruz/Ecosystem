# Lunet — visão futura do editor gráfico modular

Registrado em: 17/10/2026.
Status: direção futura registrada por ordem do proprietário, Issue #631. Não iniciar implementação agora; não alterar a prioridade do roadmap vigente com base nesta nota.

## Intenção do proprietário

O editor do Lunet deve evoluir para uma ferramenta integrada de criação visual: **Aseprite + Photoshop + Illustrator + Universal LPC Spritesheet Generator**, reunindo os recursos mais úteis de cada referência e adaptando-os ao fluxo do Lunet.

A concepção original — desenhar tudo pixel a pixel, camada por camada — continua válida. A descoberta do Universal LPC acrescenta um segundo caminho: montar personagens e sprites animados combinando componentes reutilizáveis. Ambos devem conviver no mesmo editor, permitindo alternar entre composição modular e criação manual.

O objetivo é incorporar o conceito do LPC e superá-lo em modularidade e integração com o Lunet. Não é uma decisão de copiar seu código, adotar sua tecnologia ou limitar o editor ao estilo visual LPC.

## Referências e capacidades desejadas

| Referência | Contribuição à visão do Lunet |
| --- | --- |
| Aseprite | Desenho pixel a pixel, paletas, frames, animações e edição de spritesheets. |
| Photoshop | Composição em camadas, seleções, máscaras e edição raster. |
| Illustrator | Formas, caminhos e elementos vetoriais editáveis, com conversão controlada para os assets do jogo. |
| Universal LPC | Montagem de personagens por partes, variações e componentes visuais combináveis em animações. |

As capacidades acima traduzem a intenção em direções de produto; a lista exata de ferramentas ainda será definida. Não se pretende reproduzir integralmente quatro produtos.

## Direção proposta para detalhamento futuro

- Componentes independentes e reutilizáveis: corpos, cabelos, roupas, equipamentos, acessórios e outras categorias extensíveis.
- Permitir que o usuário desenhe componentes próprios e os transforme em peças reutilizáveis; não depender exclusivamente de uma biblioteca pronta.
- Preservar a estrutura editável: montagem e suas partes continuam disponíveis depois da exportação.
- Combinar edição manual, composição por camadas e montagem modular sem obrigar o usuário a escolher um único método.
- Definir compatibilidade entre peças, direções, frames, animações e pontos de encaixe; evitar combinações visualmente quebradas.
- Usar o resultado diretamente nos projetos do Lunet, com animações, direções e metadados úteis ao motor.
- Arquitetura extensível para novos componentes, bibliotecas, formatos e ferramentas, respeitando as decisões vigentes do Ecosystem e sua implementação em C#.
- Experiência adequada a toque, stylus e telas de tablets/celulares; organizar a complexidade em espaços de trabalho conforme a tarefa. Esta visão não modifica a plataforma Android definida no SPEC §2.

## Pontos a resolver quando entrar no roadmap

1. Estudar o funcionamento do Universal LPC antes de escolher entre compatibilidade, importação de assets e implementação própria do conceito.
2. Definir um modelo de documento que relacione camadas, componentes, animações e elementos vetoriais, sem achatar prematuramente o projeto.
3. Separar a visão completa de entregas incrementais. O risco principal é acumular ferramentas desconexas e tornar a interface impraticável no mobile.
4. Avaliar desempenho e memória com múltiplas camadas, peças e frames em dispositivos Android.
5. Examinar as licenças e os créditos específicos antes de reutilizar código ou arte do LPC; preservar autoria e proveniência dos assets importados.

## Referência consultada

- Gerador: https://liberatedpixelcup.github.io/Universal-LPC-Spritesheet-Character-Generator/
- Repositório e documentação: https://github.com/LiberatedPixelCup/Universal-LPC-Spritesheet-Character-Generator

A documentação apresenta um gerador de personagens baseado em sprites LPC e informa que a arte possui licenças e créditos por asset. Isso exige avaliação específica caso haja reutilização; inspiração conceitual e incorporação de código ou arte são decisões distintas.

## Relação com o planejamento vigente

Esta é a referência detalhada da visão futura citada no SPEC §13 e no backlog do ROADMAP. O SPEC continua sendo a autoridade do produto e o ROADMAP continua sendo a autoridade de fases e gates.

A anotação não inicia implementação, não muda a fase atual e não torna todos os recursos dos quatro produtos requisitos imediatos da Fase 5. Quando essa evolução for planejada, decompor capacidades, critérios de aceite e dependências no ROADMAP, respeitando a política Visual ↔ Source e os contratos do Editor SDK.

A inspiração modular pode orientar o desenho do Sprite Studio quando chegar sua fase. Reutilização de código/arte do LPC, formato de componentes, compatibilidade entre peças e eventual promoção para ferramenta compartilhada exigem análise própria. Nenhum novo backend, contrato ou dependência foi escolhido nesta nota.
