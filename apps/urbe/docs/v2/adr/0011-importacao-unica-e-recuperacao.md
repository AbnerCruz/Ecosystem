# ADR-0011 — Importação única e recuperação

- Status: Proposto para revisão crítica da execução; direção de produto consolidada pela instrução explícita do proprietário em 2026-10-06 (Issue #261).
- Data: 2026-10-06
- Requisitos: REQ-110, REQ-007, REQ-035, REQ-038, REQ-044, REQ-053
- Referências: ADR-0004, ADR-0010; Ecosystem ADR-0025; P4-9

## Contexto

O proprietário relatou falha de importação da cidade/vault em aparelho no candidato `urbe-v1.8.3-beta`. A aprovação #259 anterior permanece histórica. Fatos inspecionados na main `33ad25e`: o Android não possui picker de árvore; o ZIP importado ignora `.urbe/` e incorpora notas, sem restaurar o conjunto de metadados/identidade. Clarificação humana posterior na Issue #261: «Selecionei para importar, importar pasta e ai não aconteceu nada». O sintoma ocorreu no acionamento do picker, antes de ler os dados, compatível com a ausência da API de árvore. Não foi relatada exceção ou falha de stream após seleção. CI/DEVICE não são equivalentes.

## Decisão de produto consolidada

Uma ação Importar oferece Arquivos ou Pasta inteira, com explicações curtas, inspeção e contagens antes da confirmação. Android usa SAF com dois intents nativos; nunca converter content:// em paths. Detectar estrutura/conteúdo/manifesto, preservar vault atual, identidade, forward e assets; conflitos exigem escolha. Novo candidato e DEVICE próprio; cliente JS permanece distribuído até o gate C#.

## Execução proposta para revisão

- Host entrega fontes somente leitura e streams: seleção explícita, staging privado, tokens, permissões limitadas ao necessário. Android ContentResolver/DocumentsContract; Windows pickers separados do armazenamento ativo; Web File System Access/input nativos equivalentes. Não modificar o vault durante seleção.
- `Urbe.Core/ImportPipeline` inspeciona, valida e planeja; `ImportTransaction` aplica por IO abstrato, sob exclusão de escritor, com revisão do conjunto atual. Nenhuma regra conhece plataformas nativas ou DOM.
- Correção crítica transitória do cliente distribuído em `src/persistence/import.js`, uma implementação portátil para Web/Android/Windows. Remove o parser/incorporador legado substituído, sem híbrido WASM/C# ou corte prematuro. Essa compatibilidade transitória é removida no gate UC-31/UC-32, não cresce como outro produto.
- Backup de bytes dos caminhos afetados, verificado antes da mutação; registro `.urbe/import.v1.json` prepara rollback antes da primeira escrita viva. Remover registro é o commit. Reabrir antes do commit restaura originais/removes adições, depois de verificar todo backup. Falha no rollback preserva o registro e bloqueia load/escritores; não fingir sucesso.
- Artefatos aditivos e schemas em DATA-CATALOG §10/contrato de importação. Formatos existentes não mudam. Backups atuais não são apagados/substituídos. Preferências/permissões de plugins só com escolha explícita após sucesso, credenciais de IA nunca aplicadas.
- Limites de staging/expansão com erro claro (64 MiB/arquivo, 256 MiB/seleção, 10000 arquivos); não alegar importação sem limite em memória. URI stream não exige seek. Entradas reservadas corruptas/futuras e hashes divergentes recusados antes de escrever.

## Alternativas rejeitadas

Novo botão sobre o importador anterior não restaura os dados. Resolver content:// em path viola SAF. Usar pickVault para importar muda o armazenamento ativo sem validar a fonte. Distribuir C# agora pula gates. Importar arquivo por arquivo sem recuperação permite estado parcial.

## Validação e integração

Corpus de produção com IO real e processo encerrado; Core com streams não seekable e transação em filesystem real; Chromium da UX/restauração/identidade/reabertura. Contratos e oráculo UC-2 revisados explicitamente apenas para novo requisito/capability/correção conhecida, preservando corpus histórico. PR crítico de user-data/security/constitution aguarda a autorização de execução pelo integrador; não simular label do proprietário. Publicar pelo pipeline aprovado após integração. P4-9 só fecha após DEVICE do novo APK exato.
