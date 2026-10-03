# Modelo de segurança para instalação e launcher

**Política A aceita pelo proprietário na DEC-0030 (Issue #112); instalador ainda não implementado e sem concessão vigente.** ADR-0018 / DEC-0030 são as autoridades da escolha; este documento explica os requisitos de P4-3. Os trechos B/C registram alternativas avaliadas, não modos habilitados.

## Identidade e proveniência

A seleção deriva do Product e Distribution Profile declarados. A identidade Android deve apontar à autoridade de build do Product ou a um mapeamento explicitamente aprovado; o Hub não infere pacote pelo nome do arquivo nem mantém IDs de Products no código. Registrar a identidade aprovada, fonte e certificados por canal exige mudança rastreável; descobrir um assinante no próprio APK não significa aprová-lo.

No fluxo A, nenhum install fica disponível sem identidade e SHA-256 do certificado (ou linhagem explicitamente aprovada). No fluxo B, apresentar que a primeira instalação confia no canal declarado sem pin de assinante. Chaves públicas de desenvolvimento não asseguram autoria mesmo com pin; nunca apresentar esse canal como assinatura privada de produção. Rotação/chaves de produção são decisões separadas.

## Sequência de instalação proposta para A/B

1. Usuário escolhe um APK no catálogo e baixa/verifica pelo P4-2. Nenhuma ação automática ao atualizar catálogo.
2. Antes da instalação, reabrir o arquivo privado, conferir tamanho e SHA-256 novamente e analisar pacote, versão e certificados com APIs Android, sem executar código do APK. Comparar à identidade aprovada; no fluxo A, comparar também certificados/linhagem aprovada. Arquivo inválido, pacote diferente, metadados ausentes ou assinatura não aprovada impedem continuar.
3. Consultar somente o pacote alvo instalado: versão, assinatura e entrada de launcher. Impedir downgrade, não desinstalar ou apagar dados para contornar incompatibilidade. A instalação do sistema permanece autoridade sobre assinatura, políticas e compatibilidade; a análise prévia não promete execução no dispositivo.
4. Mostrar Product, versão atual/nova, canal e condição de desenvolvimento. Solicitar ação explícita. Se `CanRequestPackageInstalls` negar, explicar e abrir configurações da fonte somente por ação do usuário; conferir de novo ao retornar. Negar/revogar não causa repetição automática.
5. Entregar cópia à `PackageInstaller.Session` do Android, com ação do usuário requerida nas APIs que a expõem. Android 10+ não privilegiado continua sujeito ao instalador e consentimento do sistema. Nunca usar root, Device Owner ou instalação silenciosa. Revalidar os bytes efetivamente copiados e a identidade/estado antes do commit; se algo mudou, abandonar a sessão.
6. Interpretar ação pendente, cancelamento, falha e sucesso separadamente. Consultar o pacote instalado após resultado real; somente então mostrar versão instalada. Eventos duplicados, Activity recriada, saída de primeiro plano e processo morto não fabricam sucesso: preservar somente identificador mínimo de sessão/operação e reconciliar com Android. Abandonar sessão inconclusiva quando seguro; nunca tocar em sessões de outros apps.

A aprovação de download não se reaproveita como aprovação durável de instalação. Dados de resultado não autorizam instalar outro pacote. Limites de storage, versão Android e ABI continuam explícitos; falha permite tentar novamente pelo canal independente. Nenhum caminho remove o canal standalone do Product.

## Permissões e abertura

- `REQUEST_INSTALL_PACKAGES` é permissão Android proposta exclusivamente para A/B. Catálogo de permissões e manifest só mudarão na implementação após decisão; consentimento Android não concede capacidade a agente/plugin.
- O modelo exige concessões explícitas separadas de instalar/atualizar e abrir, limitadas ao pacote/canal alvo e operação pedida. IDs concretos dessas concessões devem ser definidos no contrato, sem reutilizar `execute.code` como permissão genérica de instalar.
- Não solicitar `QUERY_ALL_PACKAGES`. Declarar visibilidade somente para pacotes aprovados; ausência/inacessibilidade não vira inferência de versão.
- Abrir somente após toque e consulta real da entrada de launcher do pacote instalado. Não executar URLs/comandos arbitrários derivados de assets, nem abrir Product automaticamente após instalar. Sem entrada, mostrar indisponibilidade.
- Cache privado do Hub não concede acesso a projetos/vault nem armazenamento compartilhado. Não exportar APK, solicitar armazenamento geral ou permitir que plugins preencham uma sessão de instalação.

## Verificação exigida antes de concluir P4-3

Testes de política devem cobrir identidade errada, ausência/divergência de pin (A), assinatura de atualização incompatível, arquivo alterado depois do download, downgrade, permissão negada/revogada, cancelamento e resultados duplicados/pós-recriação. Verificação Android deve comprovar que o instalador exige ação e que o launcher resolve o pacote aprovado.

Em aparelho, registrar por build instalação/atualização/abertura e recuperação nos caminhos realmente disponíveis dos Products do catálogo; registrar também uso standalone sem Hub. Produto sem APK ou assinatura necessária é limite real, não aprovação fictícia. O gate P4-4 exige evidência humana conforme NN-017.
