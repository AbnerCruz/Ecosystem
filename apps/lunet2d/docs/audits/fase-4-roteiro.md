# Fase 4 — roteiro consolidado de fechamento

**Preparação, ainda não candidata final.** A auditoria lista requisitos técnicos ausentes. Execute como gate somente depois de resolvidos, com link da release e nome exato do APK registrados aqui. O APK 1000321 é referência das entregas anteriores; não contém ToJson desta revisão nem a aba workspace do PR #493.

## Instalação e preparo

1. Antes de atualizar, exporte seus projetos importantes como ZIP pelo menu da lista de projetos. Não desinstale o aplicativo.
2. Instale por cima da versão anterior o APK arm64 da candidata a ser vinculada após integração. Registre versão, modelo do aparelho e versão do Android.
3. Desligue a internet. Crie um **novo** projeto Laboratório; projetos antigos não recebem mudanças do template automaticamente. Não substitua código dos seus jogos para testar.
4. Abra a documentação offline para consultar os guias indicados. Use projeto descartável para exemplos C#.

## Casos de fechamento

| Código | Ação no telefone | Resultado esperado |
| --- | --- | --- |
| F4-01 | No Laboratório, percorra os 12 módulos com Anterior/Próximo, acione controles e volte ao primeiro. | Todas as páginas desenham, respondem ao toque e retornam sem erro no Console. Aprovações antigas #367/#451 permanecem válidas; repetir aqui é regressão conjunta da candidata. |
| F4-02 | Em Colisões, alterne SAT e Box2D; observe a bola, sensor e toque Reiniciar. Toque **Cenário** para testar **Juntas** e **Filtros / eventos** conforme o guia offline `laboratorio`; aguarde cinco segundos em cada filtro e teste impulso/reinício. | SAT continua funcionando; bola cai e colide; sensor muda ao entrar/sair; reset restaura simulação; juntas limitam movimento e filtros/eventos seguem o guia. |
| F4-03 | Em Fontes e Painéis, aumente/diminua o painel e escala; teste fonte bitmap e exemplo TrueType do guia, com arquivo .ttf que você pode usar. | Cantos do nine-slice não deformam; texto não some; números proporcionais e acentos TTF são legíveis. Rode guia nine-slice no destino pequeno também. |
| F4-04 | Em Tilemap e A*, toque destinos acessíveis e bloqueados; gire a câmera. No projeto descartável, use `TileMap.Parse(map.ToJson())` e desenhe o resultado. | Paredes continuam bloqueando; caminho evita paredes; giro não remove tiles visíveis; mapa reimportado preserva camadas, desenho e colisão. |
| F4-05 | Execute guias de âncoras/stack/grid/scroll/lista/toggle/temas; arraste e solte controles e repita ao redimensionar a área. | Layout adapta; scroll não dispara seleção acidental; botões e toggle respondem uma vez; paleta e painel permanecem legíveis. |
| F4-06 | Com Preview rápido, ative Perf e deixe rodar ao menos 10 segundos; pause, faça Step, retome, Stop; abra aba Profiler, redimensione e compartilhe a captura. | Métricas reais variam; pausa está indicada; última captura continua após Stop; compartilhamento contém o relatório; aba e tamanho sobrevivem a reabrir a IDE. Exige PR #493 integrado. |
| F4-07 | Repita captura/Stop no Preview isolado; depois rode novamente com Perf desligado. | Captura retorna à IDE; não há falso aviso de crash; captura velha não é apresentada como execução nova. |
| F4-08 | Execute o roteiro do backend instrumentado de áudio quando estiver implementado, tocando efeitos/música, pausando e retomando. | Contador representa underruns reais com suporte declarado. **Bloqueado no backend atual:** “não medido” é honesto, mas não aprova este requisito do gate. |
| F4-09 | Execute um jogo completo de teste que combine física, câmera, animação, partículas, UI, conteúdo e áudio pelas APIs oficiais, com condição de vitória/derrota e reinício. | Jogo é jogável offline, reinicia sem vazar recursos e usa somente APIs públicas. Os 12 módulos isolados do Lab não substituem este caso. Projeto reproduzível: guia offline `gate-framework-advanced` (crie novo Coletor de moedas e substitua a classe principal pelo código do guia). |
| F4-10 | No Laboratório novo, página Colisões → Física Box2D, alterne **CARGA: 1 CORPO** e **CARGA: 64 CORPOS**; capture ▥ Perf por 60 segundos em cada modo e durante o jogo completo. Informe versão, aparelho e relatório. | Tempos, GC e memória ficam registrados sob as duas cargas no aparelho; reiniciar/alternar deve funcionar sem crash ou recursos vazados. Sem números de desempenho inventados por CI. |

## Falhas e regressão

1. Coloque o app em segundo plano durante jogo e áudio, retorne, pause, Step, Stop e Run novamente: jogo/áudio devem retomar conforme o lifecycle; painel não pode mostrar captura de outro projeto.
2. Feche/reabra o app, abra projeto anterior, edite/salve C#, provoque erro de compilação e corrija: código deve permanecer salvo e diagnósticos/Run devem funcionar.
3. Abra um JSON tilemap com versão inválida no projeto descartável: erro legível, sem alterar o arquivo original. Reponha o arquivo e rode novamente.
4. Confira que projetos antigos, exportação ZIP e documentação continuam funcionando offline.
5. Conexão com Tile Studio continua requisito aberto da auditoria. Não há passo clicável enquanto o editor não existe; sua ausência não conta como teste aprovado.

## Resposta

Responda por código: **passou**, **falhou: descrição**, ou **não testado**. Para falhas, informe modelo/Android, versão do APK e, se possível, captura/Console. Depois de todos os requisitos resolvidos e casos aprovados, registre explicitamente se aprova o **gate integral da Fase 4**. Não se pede essa aprovação antes de uma candidata completa estar disponível.
