# F8 · Explorar referências e experimentos — LUNET-512 / #1034

Base: F7 #1033, head b8324da3d5a498e601ba51321dfb61fa45f0c93d. A F6 já está integrada; manter ordem de integração das alterações compartilhadas na Home/Activity/harness.

## Estudo

A base possui 131 fichas (12 + 119), identificadores estáveis, busca normalizada, paginação inicial de 18 e JSON V1 para notas/favoritos/referências. O vínculo anterior guardava nome de pasta e não era identidade. Foram lidos InspirationLibrary, InspirationCatalogExpansion, MainActivity.Inspiration, LauncherProjectCatalog, documentação e testes anteriores e a auditoria de Home/Studios/Explorar.

- [Steam Tags](https://store.steampowered.com/tag/) e [Steamworks](https://partner.steamgames.com/doc/store/tags): gêneros, perspectiva, estética e mecânicas são categorias distintas e combináveis. Adotar seleção múltipla sem transformar o Lunet em loja.
- [Discussão itch](https://itch.io/t/6525179/how-to-tag-your-indie-game-so-the-right-players-can-actually-find-it): um desenvolvedor alerta para tags genéricas ou inventadas; preservar classificação editorial e não completar resultados com suposições. Esta é uma discussão individual, não consenso medido.
- [Godot demo, player.gd](https://github.com/godotengine/godot-demo-projects/blob/b761b4c/2d/platformer/player/player.gd): código lido via GitHub; o exemplo distingue aceleração, salto, gravidade, animação e ação de tiro. Propor experiências controladas sobre uma mecânica, sem atribuir essa implementação a jogos comerciais ou copiar código.
- Nenhuma mídia comercial incorporada. A busca de gameplay já existente permanece um link externo acionado pelo usuário. Autoria/licença de referências próprias são registradas, desconhecimento não é permissão.

## Implementação

- Filtros: OR dentro de gênero, perspectiva e estética; AND entre mecânicas e categorias. Busca por termos sem acentos, inclusive nas opções. Ordenação original por favorito e título; paginação inicial 18; busca sem escrita por tecla. Estado de busca, comparação, posição e quantidade visível separado da biblioteca pessoal.
- Ficha diferencia curadoria, observações pessoais e proposta de experimento; guia offline pertinente, anotar experimento, comparar duas referências do recorte por metadados compartilhados e distintos.
- V2 preserva leitura V1, notas, favoritos, títulos e IDs. GameId resolve apenas projeto único; nome legado não é migrado por adivinhação. Vínculo ausente fica preservado. Arquivamento recuperável de referências pessoais preserva autoria, licença e notas.
- Guia de uso atualizado em docs/guides/inspiracao.md. Sem novo backend, downloads, sincronização, ativos de terceiros ou dependência de IA.

## Verificação

Testes F8: busca combinada e mecânicas, preservação de GameId após mudança/ausência de pasta, comparação, experimentos, atribuição e recuperação após arquivamento. InspirationCatalogPopulationTests percorre todas as mecânicas das fichas e verifica a pesquisa e proposta de estudo. Os testes existentes verificam 131 entradas, V1, campos inválidos e falhas de persistência. Harness Android captura lista filtrada, comparação, ficha e retorno pelo Back. Nenhum teste local foi executado nesta sessão (SDK indisponível); os resultados precisam vir do CI.

Limites: a comparação é textual, baseada na classificação existente, não uma nova auditoria factual de cada jogo. Experimentos específicos para algumas mecânicas e modelo autoral genérico para as demais. Não há benchmark de latência medido, verificação visual física, nem teste manual de todos os fluxos Android; capturas emuladas ainda por confirmar. A pesquisa não verificou integralmente a prévia interativa aprovada nem a documentação normativa completa nesta sessão. Não declarar fase concluída antes do CI, integração e release.
