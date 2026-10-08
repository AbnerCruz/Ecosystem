# Cenas e componentes — Laboratório pronto para Run

A organização por `Lunet.Scenes.Scene2D`, `Entity2D` e `Component2D` é opcional. Jogos existentes continuam usando `Game.Update/Draw` diretamente. Uma entidade pode reunir movimento e desenho implementados em componentes distintos; o jogo chama a cena explicitamente.

## Teste no aparelho

1. Crie **um novo projeto** com o modelo **Laboratório** e aperte **Run**. Projetos existentes preservam seu código e suas páginas.
2. Toque no cabeçalho até **Página 6/7**. Uma bola amarela atravessa a área marcada.
3. Toque **Pausar movimento**: a bola permanece visível e parada. Toque **Retomar movimento**: volta a andar.
4. Toque **Ocultar entidade**: a bola desaparece, mas o movimento continua. Toque **Mostrar entidade**: reaparece na posição atual.
5. Toque **Remover da cena**: a bola desaparece e o contador muda para zero. **Reanexar entidade** restaura o mesmo objeto e seus componentes; o contador volta a um.
6. Segure **Pausar movimento**, mande o app para segundo plano e retorne. Soltar o dedo não deve ativar o botão. Um novo toque deve funcionar.
7. Troque de página com outro dedo enquanto segura um botão. Volte à página 6: nenhum clique escondido deve ter sido executado. Toque no cabeçalho após a página 7 para voltar à primeira.
8. Repita offline, no Preview rápido e no isolado. Confira câmera, sliders e botões nas páginas anteriores.

Resultado esperado: organização e flags funcionam sem falha do Preview; apenas projetos novos recebem a nova página. Toque, aparência, Android e performance percebida aguardam validação humana.

## Contrato de execução

A cena percorre entidades e componentes na ordem em que foram adicionados. `IsEnabled` governa Update; `IsVisible` governa Draw, independentemente. Alterações de flags são observadas antes do próximo callback. `Transform` inicia como identidade; cada componente decide como usar posição/rotação/escala, sem aplicação gráfica automática.

Uma entidade pertence a no máximo uma cena; um componente, a no máximo uma entidade. Remover/Clear desanexa sem destruir componentes ou recursos. `Get<T>()` retorna o primeiro componente compatível. Lookup, Update e Draw não alocam após preparação; criação, adição e callbacks do jogo podem alocar.

Mudanças estruturais (`Add`, `Remove`, `Clear`), inclusive dos componentes de outra entidade da mesma cena, são rejeitadas durante Update/Draw, antes de mudar ownership. Faça spawn/despawn antes ou depois do percurso, com uma fila de comandos do próprio jogo se necessário. Não há fila implícita nem rollback: exceções propagam para o host e o bloqueio é liberado em finally. Percursos reentrantes da mesma cena são rejeitados. Use apenas na thread do jogo.

O jogo controla Begin/End, câmera, blend e clipping; componentes desenham no lote já aberto. Recursos gráficos continuam sob responsabilidade do jogo. Não há serialização, editor de cenas, hierarquia de transforms, eventos de attach/dispose, scheduler, sistema de consultas ECS ou ordenação automática por profundidade nesta API.
