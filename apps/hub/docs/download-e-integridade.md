# Download e integridade — P4-2

Este documento descreve o comportamento original do P4-2, publicado até `hub-v0.0.1-dev.4`. A extensão solicitada pelo proprietário para continuar fora da Activity está documentada em [Download em segundo plano — P4-7](download-em-segundo-plano.md), com permissões, notificação e roteiro próprios.

O painel **Baixar APK** usa os mesmos Products, três releases e quatro APKs por release do catálogo. A seleção mantém Product, canal, tag, artefato e indicação de metadados antigos. Não permite digitar URL ou caminho de destino. Canais/release indisponíveis e cache anterior ao catálogo não fabricam uma seleção.

**Baixar e conferir** faz um GET público da URL daquele repositório/tag/artefato em GitHub Releases. Nome e tag são conferidos; diferenças de maiúsculas no nome do repositório não mudam sua identidade. O cliente dos artefatos é separado das leituras da API e não recebe token, cookie ou credencial. O transporte usa HTTPS, inclusive após redirecionamento. Digest ausente/inválido, arquivo que não é APK, tamanho vazio ou acima de **128 MiB** deixam o download indisponível com motivo explícito. O limite total de tempo é **5 minutos**, incluindo a leitura do corpo.

O corpo é lido em streaming com buffer de 64 KiB, limitado ao tamanho declarado mais um byte para detectar excesso. Tamanho HTTP divergente, corpo truncado/maior, erro de rede, cancelamento, timeout, falha de armazenamento e SHA-256 divergente rejeitam o arquivo. O SHA-256 é comparado aos bytes recebidos, não apenas reexibido da API. Apenas tamanho e SHA-256 conferidos produzem o resultado de sucesso; isso não confere assinatura, identidade do pacote nem compatibilidade de instalação.

O destino Android é exclusivamente `CacheDir/hub-artifacts`, privado do Hub, fora de projetos/vaults e do armazenamento compartilhado. Nomes remotos nunca viram caminhos locais: a transferência usa `hub-<id>.part`; após fechar/conferir, renomeia para `hub-verified.apk`, substituindo a cópia anterior. Há uma transferência por instância, uma cópia final e uma parcial, cada uma limitada a 128 MiB. Partes abandonadas por encerramento do processo são removidas na próxima tentativa. Uma falha preserva a cópia anterior, mas não a devolve como resultado da nova seleção. Falha no descarte aparece explicitamente. A cópia não é considerada verificada depois de reiniciar o processo, e o Android pode limpar o cache.

**Cancelar** interrompe a transferência e descarta a parte incompleta. Sair do primeiro plano (`OnStop`) ou destruir a Activity também cancela. Atualizar o catálogo ou trocar a seleção fica desabilitado durante o download. Não há retomada, serviço em segundo plano, exportação pública, compartilhamento, instalador ou nova permissão Android. `network.access` já declarada cobre o GET; `fs.read`/`fs.write` do catálogo de permissões se referem ao projeto/vault do Context, que este fluxo não acessa. P4-3 mantém ADR/modelo de permissões e segurança próprios antes de conduzir instalação/launcher.

## Conferência em aparelho

Use uma release de desenvolvimento contendo P4-2 do [canal do Hub](https://github.com/AbnerCruz/Ecosystem/releases). Abra, atualize e selecione um APK com SHA-256 disponível. Confira seleção, legibilidade e progresso; baixe até mostrar **tamanho e SHA-256 conferidos**. Não deve abrir instalador nem pedir acesso ao armazenamento compartilhado.

Inicie outra transferência e toque **Cancelar**; a tela deve voltar a permitir nova tentativa e explicar o cancelamento. Inicie novamente e coloque o app em segundo plano: ao voltar, o download deve estar cancelado, sem continuar escondido. Repita com falha de rede e recupere atualizando/tentando novamente. Um APK sem checksum ou acima do limite deve explicar por que o botão não está disponível. Cache offline deve continuar identificado como último estado conhecido.

Essa conferência de UI/lifecycle é registrada separadamente dos testes do Core no handoff de P4-2 (NN-017). Não substitui o gate futuro de instalar/atualizar/abrir Products da Fase 4.
