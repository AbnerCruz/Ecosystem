# ADD-0032 — Ordem do proprietário: o Urbe tem uma IA padrão que funciona offline

**Data:** 2026-10-24. **Autoridade:** instrução explícita do proprietário nesta conversa. **Registro:** DEC-0056. **Escopo:** Urbe nativo (Android primeiro, Windows depois).

## O que o proprietário disse

> Eu estava pensando, e pensei: Por que não colocar um quebra galho na IA? Ao inves de ficar dependendo de conexão com a internet, para condigurar os proprios modelos e tals, as vezes a pessoa não tem acesso e isso não deveria impedi-la de usar a IA do app. Então eu quero aue o urbe tenha uma IA padrão que funcione offline, tanto para os textos quanto para as imagens. Os melhores modelos existentes por gentileza. Pode fazer e finaliza com release

Depois de ouvir os limites (modelos de imagem de qualidade são pesados demais para a CPU de um celular e não há motor de imagem pronto para Android), o proprietário liberou o acesso de rede para baixar e verificar os modelos e aceitou o recorte: **texto primeiro, nos dois sistemas; imagens em passo seguinte**.

## Decisão e aplicação

- Um provedor novo do Assistente, **IA no aparelho** (preset `device`), roda um modelo aberto (Gemma 4, Apache-2.0) por llama.cpp **dentro do app**, sem internet e sem chave, com as ferramentas de notas (subconjunto pequeno, para o tempo de espera).
- O modelo **não vai dentro do APK**: é baixado uma vez (retomável, com verificação SHA-256 antes de ser mantido) para a pasta de dados do app, nunca para o vault. O app oferece o modelo menor (E2B, ~3,3 GB) e o maior (E4B, ~5,2 GB), recomendando conforme a memória do aparelho.
- A baixa só acontece por ação da pessoa (NN-016: nada de rede em segundo plano). Depois dela, tudo funciona offline.
- Código novo: `Urbe.LocalAi` (motor e armazenamento), referenciado só pelas heads Desktop e Android; `Urbe.Core` define o contrato (`ILocalTextRuntime`), `Urbe.Client` a tela. Sem dependência entre Products (NN-002), `product-specific` (NN-022).

## Limites declarados (NN-017)

- O motor foi provado em CPU x64 com o modelo real (conversa em português, chamada de ferramenta, laço do agente). **O caminho nativo no Android não foi testado em aparelho**: depende da validação do proprietário.
- **Imagens offline não foram entregues.** Ficam como item próprio do ROADMAP.
- A primeira resposta depois de carregar o modelo paga o processamento do contexto inicial (dezenas de segundos em CPU de celular); as seguintes reaproveitam o cache.
