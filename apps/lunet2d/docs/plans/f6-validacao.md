# Estudo e verificação da F6

No fluxo aprovado em R-MA, a apresentação segue a progressão por ramo. O vídeo do canal Sobreviva ao Brasil não pôde ser reproduzido na consulta. A [documentação do Xmind](https://xmind.com/academy/present-mind-maps-pitch-mode) sustentou o comportamento escolhido, com navegação e foco por ramo. O [Mind Elixir](https://docs.mind-elixir.com/pt-BR) e o [Freeplane](https://github.com/freeplane/freeplane) serviram de contraste para criação e recolhimento. A árvore do Lunet usa layout calculado com ligações extras entre tópicos. [JSON Canvas](https://jsoncanvas.org/spec/1.0/) exige geometria livre; o esquema nativo desta etapa não promete intercâmbio com esse formato.

Os fluxos #962 e #1018 já forneciam Biblioteca versionada, hash e staging, além de editores globais para sprite, música e efeitos. A F6 reaproveita os editores de projeto para os demais formatos. O [Asset Browser do Blender](https://docs.blender.org/manual/en/5.0/editors/asset_browser.html) e a [identidade de recursos no Godot](https://docs.godotengine.org/en/stable/classes/class_resourceuid.html) ajudam a distinguir reuso, identidade e localização. Mantemos a cópia explícita no destino, com caminhos originais da dependência de tile e nomes originais em colunas de referência. O fork registra sua revisão em `.lunet/asset-forks`.

A matriz de fontes, derivados e consumidores está no [manual](../manual/mapas-e-studios.md). Colisões são recusadas e podem ser resolvidas no destino antes de repetir. O editor de partículas atual continua usando a forma local e a API de jogo continua recebendo textura explícita. A apresentação é do mapa e a posição vem da hierarquia.

## Evidências

- Testes .NET de mapa vazio e grande, ciclos, fold, desfazer/refazer, referências ausentes, projeto movido, preservação de nota e esboço.
- Testes de todos os tipos, duas cópias, revisão seguinte, dependências de mapas e dados, cancelamento, conflito e recuperação de um bundle.
- Capturas Android de mapa em Notas, Tile e Data globais. O CI oficial exporta e instala um jogo no Android 13. Gestos em aparelho físico, uma rodada manual de cada asset no jogo e TalkBack não foram verificados.
