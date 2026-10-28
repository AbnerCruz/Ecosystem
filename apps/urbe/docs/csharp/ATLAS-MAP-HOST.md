# Atlas Map V1 no Urbe — contrato e validação

Escopo: `apps/urbe/csharp`. O Atlas Forge gera o mundo e exporta o `Atlas Map` HTML autossuficiente; o Urbe **apenas visualiza e permite editar lugares/notas desse conteúdo**. Nenhum código do gerador foi copiado para o Urbe.

## Fluxo

1. Arquivo `.html` incorporado por `![[Mapas/mundo.html]]` ou aberto no painel nativo. `AtlasMapSession.TryCreate` aceita unicamente `format=atlas-map`, `version=1`, `mapId`, `revision`, `project` e coleção GeoJSON no script `atlas-map-data`; no resto, nenhuma ponte é fornecida.
2. Android cria uma ponte privada por WebView, com nonce próprio. `window.AtlasHost.version = 1`, `openNote`, `saveMap`. Só a instância selecionada executa operações; `mapId` não autoriza outra operação.
3. `openNote` valida caminho relativo na raiz do vault e abre **somente uma nota .md já existente** pelo editor nativo. Âncoras são aceitas mas a navegação no cabeçalho fica para uma versão posterior.
4. `saveMap` valida ID, revisão, tamanho e dados do HTML proposto; a escrita condicional usa SHA-256 da versão aberta sob a trava do armazenamento `FolderVaultStorage`/`AndroidVaultStorage`. Se outro editor mudou os bytes, há conflito. O writer canônico verifica o arquivo e só então o adaptador confirma o save.
5. Salvar não recarrega o mapa, não modifica a nota Markdown que incorpora o HTML e não converte coordenadas fictícias para geografia terrestre.

## Restrições de segurança

Sem caminhos arbitrários em `saveMap`. O contexto contém vault selecionado e arquivo aberto. Sem permissão de arquivo/content no WebView. CSP no HTML do Atlas proíbe rede, subframes e navegação por formulários; o worker Blob do motor físico local permanece permitido. O adaptador é injetado **após** o HTML salvar a sua cópia inicial para não persistir um nonce ou uma sessão nativa em arquivos exportados. O Studio e outros HTML mantêm suas pontes/isolamento atuais.

## Testes e pendências

A suíte `AtlasMapHostTests` cobre formato, caminhos maliciosos, revisão/ID e conflitos pelo writer no computador. Isso **não comprova** o Android. Checklist DEVICE: abrir no Moto g32/Android 13; ativar offline; criar/editar/excluir lugar; abrir nota presente/ausente; salvar, fechar e reabrir; mudar arquivo fora do mapa e verificar conflito; testar múltiplos embeds, campos/teclado, scroll/zoom, painel/Fonte, suspensão/recriação, processo encerrado, importação JSON e custo de RAM para grade 2048×1024. Confirmar que `urbePage` e os demais tipos de visualização continuam funcionando.

**Bloqueio de entrega:** implementar proteção explícita contra descarte de instância com edições não salvas (não confiar em `beforeunload` do WebView). Não marcar pronto para release sem teste de dispositivo e fluxo de aprovação da arquitetura em ADR-0044.
