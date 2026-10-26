# ADR-0043 — Exportação auditável, catálogo de versões e compatibilidade por arquivo do Urbe

## Status

Proposto na PR da UC-91, issue #793 (26/10/2026). Implementação inicial em revisão; CI, consistency e DEVICE ainda não executados/verificados. Não implica aprovação automática do contrato pela governança.

## Contexto

O proprietário exige patchnotes verificáveis, escolha manual de versões, proteção contra perda de dados ao reabrir um vault em outro build, botão Voltar do Android e exportação integral dos registros de execução/memória por agente. A beta atual 2.0.0-beta.31 distribui APK/ZIP nativos. Urbe.Core/AssistantStore já separa a configuração privada dos provedores, memórias e chats do vault físico.

## Problema

Como escolher versões e exportar histórico e memória sem expor credenciais, perder dados ou permitir sobrescrita incompatível, mantendo o Urbe nativo e o vault como fonte de verdade.

## Opções

1. Instalação/rollback automático e liberação ampla de escrita: rejeitada por risco a dados locais e limitações do Android.
2. Bloqueio integral de qualquer vault criado em versão diferente: excessivamente restritivo.
3. Catálogo manual, exportação fiel e compatibilidade declarada por arquivo com cópia/lixeira explícitas: escolhida.

## Decisão

1. A versão instalada é a versão do binário (Android PackageInfo.VersionName / versão do executável desktop); a versão de publicação continua canônica em Urbe.Android.csproj. Não existem versões paralelas editáveis no vault. O app apenas exibe projeções e consulta releases GitHub sob urbe-csharp-v, com assets exatos da plataforma; não instala APK automaticamente nem promete downgrade in-place.
2. O par PATCHNOTES.md (detalhado da versão publicada) e PATCHNOTES-HISTORY.md (histórico compacto) é mantido na pasta C# do produto e incorporado como recurso da interface. A próxima release deve atualizar o atual e transferir a anterior para o histórico no mesmo change set de version bump. A versão declarada deve corresponder à versão canônica da release; a auditoria do pipeline ainda precisa ser automatizada.
3. A segurança do vault é **por recurso e por arquivo**, não pela mera desigualdade de número do APK. O contrato opcional .urbe/compatibility.json tem schemaVersion=1 e lista entradas de arquivos existentes com minWriterVersion e features. Se esta versão do app não compreende as features, ou é anterior à versão mínima, o arquivo é reconhecido como FutureFile; leitores podem mostrar o arquivo original, mas o planner recusa escrita in-place. Metadados ilegíveis de compatibilidade colocam o vault inteiro em somente leitura. Metadados opcionais ausentes preservam os guards legados de schema futuro. Remoção explícita de arquivo permite entrada antiga sem arquivo, para o vault não ser bloqueado por manifesto obsoleto.
4. Cópia independente e envio à lixeira de um arquivo protegido exigem escolha consciente, sem apagar a fonte durante a cópia; a cópia pode demandar conversão manual de dados não suportados. Nunca remover automaticamente formatos desconhecidos ou suspender leitura de outros arquivos compatíveis.
5. Exportação **não é importação**. Dois formatos versionados: Markdown leve e JSON completo. O completo serializa o AiConversation no schema já persistido (tool_use, tool_result, metadados, fila, execuções, eficiência, fontes, formulários, reversões). Memória individual exporta só registros com Scope=agent e AgentId exato, inclusive estados arquivados e disputados, sem copiar memórias globais ou de outro agente. As chaves dos provedores continuam apenas na configuração privada. O arquivo exportado pode conter conteúdo sensível fornecido pelo usuário; o usuário escolhe seu destino.
6. Android Back prioriza modal, action sheet, subviews do Assistente, visualizadores, grafos, notas sobrepostas e trilha do editor; só chega ao comportamento nativo na raiz. A primeira ação não deve descartar alterações de notas não salvas.

## Formato declarativo exemplo

~~~json
{
  "schemaVersion": 1,
  "files": [
    {
      "path": "Projetos/Meu grafo.grafo.json",
      "minWriterVersion": "2.0.0-beta.31",
      "features": ["graph.v1"]
    }
  ]
}
~~~

Sem esse manifesto, a proteção existente por formato de vault, páginas, temas e documentos desconhecidos continua ativa. Nenhum mecanismo adiciona silenciosamente esse manifesto aos vaults antigos: futura automação de geração é uma etapa separada e precisa de versão/compatibilidade dos próprios recursos.

## Consequências

A arquitetura é aditiva: arquivos antigos que não declararem novos requisitos continuam sob os guards existentes. Uma declaração incompatível preserva o arquivo em somente leitura sem paralisar os demais. Exportações podem conter informação privada compartilhada voluntariamente.

## Alternativas rejeitadas

Sem upload obrigatório para servidor, sem gerenciar APKs ou exigir desinstalação automática, sem transformar Markdown em fonte paralela de verdade e sem remover proteção de arquivos futuros ao duplicar o original.

## Segurança e consequências

Sem novos segredos na exportação por contrato, mas o próprio transcript pode conter chaves digitadas pelo usuário; não alegar anonimização. APIs externas só são consultadas por pedido explícito no catálogo. Ao baixar versão anterior o Android pode bloquear downgrade e uma desinstalação pode eliminar conversas e memórias privadas. Não oferecer suposta restauração automática. Compatibilidade geral não é provada somente por versão: ausência de manifesto e de regra específica implica **desconhecido**, não garantia absoluta. Nenhum APK/tag anterior é substituído.

## Testes necessários

Core: AppUpdateHistoryTests, AssistantExportTests, VaultCompatibilityTests e suites legadas. UI: AndroidBackAndPatchnotesUiTests e regressão completa. CI: dotnet test, consistency, build Android/Windows. Dispositivo: gesto/físico Back, teclados/IME, save/copy/trash com vault real, exportação por Storage Access Framework, downgrade avisos sem desinstalar. Revisão de dados persistidos antes de integrar. Integração/assinatura/release pelo pipeline canônico somente após gates.

## Normas relacionadas

MANIFEST.md (NN-001, NN-003, NN-008, NN-010, NN-011, NN-016, NN-017, NN-018, NN-019); ADR-0032, ADR-0033, ADR-0041, ADR-0042; apps/urbe/docs/csharp/ROADMAP.md; DATA-CATALOG.md; UC-91/issue #793.
