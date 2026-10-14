# ADD-0023 — Ordem do proprietário: tudo acontece no Ecosystem (sem sincronizar espelho) e o conteúdo abre dentro do Urbe

**Data:** 2026-10-14. **Autoridade:** instrução explícita do proprietário nesta conversa. **Registro:** DEC-0046. **Escopo:** governança dos agentes (todos os Products) e o cliente C# do Urbe (`apps/urbe/csharp`). **Complementa** ADD-0015 (distribuição direta) e ADD-0020/ADR-0032 (UI nativa), sem substituí-los no resto.

## O que o proprietário disse (fatos, com as palavras dele)

1. Sobre o agente ter apontado, ao fim de uma integração, que faltava disparar `sync-from-ecosystem.yml` em `AbnerCruz/Urbe`: *"Remove essa especificação de sincronização com o espelho, já falei várias vezes que é tudo no ecosystem agora."*
2. Sobre a prévia de PDF, áudio e vídeo abrir em outro app do aparelho: *"Não vem com desculpa de que a prévia abre em outro app, dá um jeito da visualização aparecer no próprio Urbe, muitos painéis assim serão necessários porque envolve vários produtos html no projeto e quanto antes resolvermos isso melhor."*

## Decisões do proprietário

1. **Tudo é no Ecosystem.** Desenvolvimento, integração e publicação dos Products acontecem neste repositório e nos seus pipelines diretos (ADD-0015, `docs/distribution/direct-releases.md`). **Nenhum agente dispara, exige ou anuncia como passo pendente a sincronização com os repositórios espelho** (`AbnerCruz/Urbe`, `AbnerCruz/Lunet2D`, `sync-from-ecosystem.yml`). A instrução de DEC-0017-A de disparar essa sincronização após mudar `apps/<id>` deixa de valer para agentes. Os repositórios espelho não são apagados nem arquivados por esta decisão: isso, se um dia for feito, é item próprio.
2. **O conteúdo abre dentro do Urbe.** HTML, PDF, áudio e vídeo do vault (e os produtos HTML do projeto: páginas, composições, artefatos) são vistos **num painel do próprio Urbe**, sobre a cidade, e não mandados a outro aplicativo. "Abrir em outro app" pode existir como ação extra, nunca como a forma de ver. Esse painel é peça reutilizável: outros painéis de conteúdo HTML virão.

## Como isso se encaixa no que já estava decidido (registro do agente)

- ADD-0020 §2 veda WebView, HTML e CSS **como camada de interface** do cliente C#. A interface continua nativa (Avalonia). Um visualizador de conteúdo — WebView do sistema, renderizador de PDF ou reprodutor de mídia — **dentro de um painel nativo**, mostrando arquivos do usuário, não é a camada de interface: é conteúdo, como uma imagem desenhada no painel. ADR-0032 recebe esta emenda.
- O conteúdo HTML é do usuário: o visualizador o mostra isolado (sem acesso ao vault além do arquivo aberto, sem pontes com o app). Isso respeita NN-016.

## Consequências

- `AGENTS.md` perde a instrução de sincronizar espelhos (§3) e a nota histórica da §7 que a mantinha para o Urbe.
- O cliente C# ganha um visualizador de conteúdo embutido, usado primeiro pela prévia de arquivo (UC-19) e depois pelas páginas e composições (UC-35, UC-45).
