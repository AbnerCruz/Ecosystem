# ADR-0044 — Atlas Map V1 dentro do visualizador nativo do Urbe

## Status

**Proposto** — decisão de integração sujeita à governança e à prova DEVICE, sem merge/release presumidos. Escopo delegado pelo proprietário com o arquivo `Urbe-Atlas-Map-Instrucoes.md` em 2026-10-27.

## Contexto

FATO OBSERVADO (main `64deb1b6247df2b2637bad1cd4ae062dfa191636`): `MainView.Embeds` mostra `![[Mapas/mundo.html]]` pelo `ContentViewers`; Android usa um WebView sem acesso a arquivos/conteúdos. `ContentRequest.Messages` pertence ao Studio. Não existe ponte do Atlas. O HTML independente exportado pelo Atlas Forge 4.0 consome um `window.AtlasHost` opcional, versão 1, com `openNote({mapId,path})` e `saveMap({mapId,revision,html})`.

A fronteira de DEC-0043/ADR-0032 proíbe camada de interface HTML; DEC-0046/ADD-0023 permite conteúdo HTML em painel nativo, mas sua decisão anterior era isolamento sem ponte. A exceção é portanto **restrita à operação pedida**; nunca criar uma API genérica de filesystem nem reintroduzir geração planetária no Urbe.

## Problema

Permitir vínculos para notas existentes e preservar marcadores no HTML do vault, com confirmação real do host e proteção de integridade, sem conceder ao HTML acesso geral ao dispositivo ou tornar o Atlas uma nova UI web do Urbe.

## Opções

1. **Ponte exclusiva para documentos Atlas Map V1 no visualizador Android** (proposta nesta fatia): seleção por contrato de dados embutidos, identidade de sessão/vault, duas operações tipadas, CAS por SHA-256 no escritor e navegação nativa.
2. Download Blob do WebView para substituir o arquivo (rejeitada): o WebView não oferece confirmação de persistência no vault e o download não é comprovado nesse fluxo.
3. API genérica de leitura/escrita do vault para qualquer HTML (rejeitada): viola o isolamento do conteúdo, NN-016 e ADD-0023.
4. Reimplementar o motor de geração no Urbe (rejeitada): contradiz o escopo e ADR-0034/0035.

## Decisão proposta

A opção 1 permanece **local ao produto Urbe**, sem capability compartilhada. `ContentRequest` pode carregar um callback nativo restrito de navegação. O `AtlasMapSession` recebe somente o HTML já lido e o armazenamento vinculado; valida formato/versão/ID, caminho do vault, hash inicial e revisão. O escritor do vault compara o hash **sob a trava da escrita**, antes de salvar, e verifica os bytes depois. Uma revisão autoral não é uma versão do disco.

No head Android, somente arquivos exportados reconhecidos instalam `AtlasHost` após carregar, por adaptador `AddJavascriptInterface` com nonce por instância. A WebView preserva `AllowFileAccess=false`, `AllowContentAccess=false` e navegação externa bloqueada; aplica CSP para bloquear subframes/rede e permitir o worker `blob:` offline do Atlas. O JavaScript não recebe token ou caminho de armazenamento nativo. A API exposta é uma única recepção de mensagens, com payload validado e operações fixas; erros nunca viram confirmação positiva. A ponte do Studio `urbePage` não é reaproveitada.

Um `mapId` é identidade de conteúdo e nunca credencial. O vínculo de autoridade é o documento/vault/sessão selecionado. O `openNote` exige caminho relativo de `.md` existente; `#âncora` é aceito, mas a V1 abre a nota, não a posição do cabeçalho.

## Consequências e gates

- Sem WebView como interface nativa e sem dependência do Atlas Forge em runtime; HTML autossuficiente permanece conteúdo.
- A recarga do WebView não é feita após salvar: câmera, ferramentas e formulários ficam na instância atual.
- A implementação desta fatia ainda **não resolve** o ciclo de vida de alterações não salvas em todo descarte de embed e não inclui teste DEVICE de teclado, worker, importação de lugares, recorte, tamanho/performance ou perda de processo. Até resolver, a integração deve permanecer em revisão, sem afirmação de prontidão de release.
- Requer prova de escrita no Android SAF, de que o adaptador é inacessível a outros HTML/frames e de que os demais visualizadores e o Studio não regrediram.
- Reuso avaliado como `product-specific` (ADR-0011/NN-022): o único consumidor é o HTML Atlas Map embutido no Urbe; extrair seria prematuro.

## Referências

MANIFEST §0.1/§22–24; NN-001/002/006/008/009/010/011/016/017/022; ADR-0011/0015/0032/0034/0035; DEC-0043/0046; ADD-0023; `apps/urbe/csharp/src/Urbe.Client/ContentViewers.cs`; `apps/urbe/csharp/src/Urbe.Android/AndroidContentViewers.cs`; `apps/urbe/docs/csharp/PRODUCT-DIRECTION.md`.
