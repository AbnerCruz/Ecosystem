# Explorar — referências para criar jogos (LUNET-512)

## Intenção e fonte de dados

A seção **Explorar** é um catálogo de estudo, não uma loja de jogos. Funciona sem rede e sem abrir um projeto. A versão inicial incorpora referências textuais curadas (título, gênero, perspectiva, estética, mecânicas e sugestão do que observar), sem vídeos, screenshots protegidas, assets extraídos ou download dos jogos.

**Fonte de verdade:** arquivo local privado `inspiration.json` no diretório de dados do Lunet. A lista curada fica no código portable `InspirationLibrary`; favoritos, notas e referências adicionadas pelo usuário persistem com escrita atômica. Uma falha de leitura interrompe o acesso com aviso, sem substituir dados corrompidos. Links adicionados manualmente aceitam somente HTTP(S); nunca são acessados automaticamente.

A arquitetura aplica os princípios oficiais de offline-first do Android: a camada local tem a verdade, redes opcionais só poderão enriquecer a experiência sem travar uso offline. Pesquisa e comparação já documentadas em `docs/architecture/audits/lunet-home-studios-explorar-2026-11-15.md`.

## Usar no Android## Conteúdo curado e uso para pesquisa (16/11/2026)

O catálogo embutido agora tem **131 referências** (12 originais + 119 fichas novas), todas acessíveis sem rede. Cada ficha traz título, gênero, perspectiva, estilo visual, mecânicas pesquisáveis e uma pergunta ou observação concreta para estudar, em vez de apenas uma lista de nomes. Há jogos de exploração, sandbox, sobrevivência, RPG top-down, metroidvania, roguelite, puzzle, táticas, gestão, defesa, ritmo, narrativa, arcade e design mobile. Os filtros oferecem taxonomias combináveis; um jogo é apresentado sob um gênero principal, mas pode ser encontrado por mecânicas transversais.

Ações práticas: busque **montarias**, **voo**, **programação**, **IA de agentes**, **gestos**, **deckbuilding**, **agricultura** ou **construção**; combine perspectiva e estética; salve notas sobre o que se pode adaptar no próprio protótipo. *Terraria* e *Owlboy* dão pontos de partida distintos para estudar movimento aéreo, sem sugerir que ambos sejam jogos de montarias top-down.

**Pesquisar gameplay** abre, mediante toque, uma busca pública no YouTube pelo nome do jogo e por `gameplay`. O Lunet **não** baixa nem inclui o vídeo, não endossa cada resultado e não faz chamadas online sozinho. Exige navegador/rede apenas ao escolher abrir a busca. As fichas pessoais continuam aceitando links fornecidos pelo usuário; não há autoimportação de imagens ou assets.

Para evitar criar centenas de views de uma vez em um aparelho Android, a lista exibe inicialmente 18 fichas e permite **Mostrar mais**. Mudar filtros ou pesquisa volta a mostrar as primeiras 18 fichas da nova seleção. A quantidade total de resultados continua visível.

O índice foi curado manualmente; use títulos e classificações para inspiração, não como catálogo oficial de plataformas, avaliações ou disponibilidade comercial. A pesquisa comparou o modelo de tags e gêneros do [itch.io](https://itch.io/games/tag-2d/tag-metroidvania), a documentação [Android offline-first](https://developer.android.com/topic/architecture/data-layer/offline-first) e exemplos concretos de mecânicas. As marcas continuam pertencendo aos seus titulares.



1. Abra o Lunet sem projeto. No painel inicial, toque em **Explorar referências**.
2. Busque por título, gênero, mecânica, estilo ou anotações. Os quatro seletores de filtros são combináveis; “Apenas favoritos” restringe os resultados.
3. Abra uma ficha, marque favorito ou salve anotações e associe a um projeto existente. Quando houver associação válida, **Abrir projeto associado** leva ao editor.
4. Use **+ Referência** para adicionar um jogo ou ideia de estudo, com título, gênero, perspectiva, estilo, lista de mecânicas e objetivo de estudo. O link é opcional; não há importação de imagens.
5. Referências pessoais podem ser editadas e excluídas com confirmação. As fichas curadas mantêm metadados protegidos, mas aceitam notas/favoritos próprios.

Os nomes dos jogos são referências educativas, não endosso nem licença para reproduzir marcas, código, arte, personagens ou sons. O catálogo futuro de jogos produzidos/publicados com Lunet é um produto **diferente**.

## Testes e limites da entrega

- `InspirationLibraryTests`: catálogo sem rede, filtros combinados com acentos, CRUD local, notas/favoritos persistidos após reabrir, vínculos de projeto, links inválidos, corrupção de JSON preservada e campos inválidos.
- Android UI: conferir tela compacta moto g32 Android 13, acessibilidade dos filtros, teclado/IME, navegação Back, favoritos e notas após fechar/reabrir, exclusão e associação a projeto.
- **Não contém** sincronização online, thumbnails licenciadas, integração automática com APIs de jogos ou biblioteca global de assets. Esses recursos seguem requisitos separados. Sem testes DEVICE, o item não é concluído.
