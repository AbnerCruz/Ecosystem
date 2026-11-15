# Explorar — referências para criar jogos (LUNET-512)

## Intenção e fonte de dados

A seção **Explorar** é um catálogo de estudo, não uma loja de jogos. Funciona sem rede e sem abrir um projeto. A versão inicial incorpora referências textuais curadas (título, gênero, perspectiva, estética, mecânicas e sugestão do que observar), sem vídeos, screenshots protegidas, assets extraídos ou download dos jogos.

**Fonte de verdade:** arquivo local privado `inspiration.json` no diretório de dados do Lunet. A lista curada fica no código portable `InspirationLibrary`; favoritos, notas e referências adicionadas pelo usuário persistem com escrita atômica. Uma falha de leitura interrompe o acesso com aviso, sem substituir dados corrompidos. Links adicionados manualmente aceitam somente HTTP(S); nunca são acessados automaticamente.

A arquitetura aplica os princípios oficiais de offline-first do Android: a camada local tem a verdade, redes opcionais só poderão enriquecer a experiência sem travar uso offline. Pesquisa e comparação já documentadas em `docs/architecture/audits/lunet-home-studios-explorar-2026-11-15.md`.

## Usar no Android

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
