# Download em segundo plano — P4-7

O pedido do proprietário foi estender o P4-2 para **segundo plano também**. Esta implementação mantém a transferência em um serviço Android `dataSync`, iniciado exclusivamente depois de **Baixar e conferir** na tela do Hub. Sair da tela, mudar de aplicativo ou recriar a Activity não cancela; ao voltar, a tela consulta a mesma operação e seu progresso/resultado. Há uma transferência por processo, sem fila automática.

## Notificação e controle

Durante o download, uma notificação informa progresso e oferece **Cancelar** e acesso ao Hub. Cancelar na tela ou na notificação solicita o descarte do parcial; a próxima tentativa só é habilitada depois da limpeza. Comandos e progresso de uma operação anterior não podem cancelar ou modificar outra. Ao concluir, a notificação mostra o resultado real e o serviço para. A Activity observa o estado somente enquanto está visível; sua destruição não é dona do download.

Android 13+ pede autorização para notificações na primeira solicitação de download do processo. A recusa permite continuar, como previsto pelo Android para foreground services: a notificação pode ficar oculta da gaveta, mas o serviço aparece no painel de aplicativos ativos do sistema. A tela explica essa situação e mantém Cancelar disponível. Abrir permissões não cria instalação nem concede capacidades a agente/plugin.

## Modelo de permissões

| Permissão Android | Motivo | Limite |
|---|---|---|
| INTERNET (existente) | GET público do asset selecionado | Sem token, cookies ou escrita no GitHub |
| FOREGROUND_SERVICE | Manter operação explícita com controle do sistema | Serviço não exportado; não inicia pelo boot ou pelo catálogo |
| FOREGROUND_SERVICE_DATA_SYNC | Declarar tipo dataSync em Android 14+ | Apenas download/verificação, não execução/instalação |
| POST_NOTIFICATIONS | Progresso, cancelamento e resultado em Android 13+ | Solicitação pela UI; recusa explícita, sem ampliar a confiança |

Não usa wake lock, acesso a armazenamento compartilhado, instalador, inventário de apps, acesso a projetos/vaults nem credenciais. O catálogo de capabilities continua intacto: serviço Android interno não é Service compartilhado/capability pública. `network.access` cobre o GET já declarado; permissões novas são exclusivamente as declarações Android acima. Este PR é crítico por ampliar a fronteira de permissões e exige a autorização de integração definida na política, embora a direção já tenha sido solicitada e não precise de outra DEC.

## Integridade e limites de lifecycle

O downloader P4-2 permanece autoridade dos bytes: HTTPS, metadados do canal, máximo 128 MiB/5 minutos, tamanho, SHA-256, parcial único e publicação atômica em cache privado. Não há exportação, instalador, retomada automática, scheduler, reinício pelo boot ou pelo sistema (`NotSticky`). O serviço `dataSync` não promete sobrevivência a force-stop, processo morto, limites do Android ou políticas de bateria/rede do fabricante.

Interrupção do serviço solicita cancelamento e descarte; timeout do sistema para o serviço. Processo morto não executa limpeza garantida: marcador privado identifica tentativa sem resultado recuperável quando ainda presente. Ao reabrir, o Hub explica a situação e exige nova solicitação; o downloader limpa parciais abandonados na próxima tentativa. Não retorna um APK anterior como verificado após reiniciar o processo. Android pode limpar cache/marcador. Marcador não é autoridade de sucesso, não contém URL, credencial ou aprovação e nunca autoriza reinício.

## Conferência em aparelho

Use o APK candidato do PR P4-7 ou a release que contiver essa mudança; **dev.4 só contém o P4-2 anterior**.

1. Solicite download de APK com SHA-256, autorize notificações e saia para outro app. Confira progresso e Cancelar na notificação; volte ao Hub e confira a mesma operação, sem iniciar outra.
2. Repita e aguarde concluir em segundo plano. Notificação e tela devem concordar com tamanho/SHA-256 conferidos; o serviço deve desaparecer dos apps ativos. Nenhum instalador deve abrir.
3. Cancele pela notificação durante outra transferência. Volte: deve informar cancelamento e permitir nova tentativa após descartar parcial. Repita com Cancelar na tela.
4. Recrie a tela durante download (rotação se aplicável, opção de desenvolvedor de não manter Activities, ou retorno pela notificação). Não deve duplicar download nem perder progresso/resultado do processo vivo.
5. Android 13+: negue notificações e confira a explicação na tela, continuação ao sair e controle pelo painel de apps ativos; Cancelar na tela continua disponível. Verifique também com autorização concedida.
6. Encerre o processo/force-stop durante transferência. Reabra: não deve reiniciar sozinho ou anunciar cópia antiga como sucesso; quando existir marcador, deve explicar tentativa sem resultado. Solicite novamente e confira descarte de parcial/novo resultado.
7. Confira erro de rede, falta de espaço e nova tentativa; não confunda teste de encerramento do processo com conclusão em segundo plano normal. Nenhuma validação automática substitui essa conferência de lifecycle/notificação (NN-017).
