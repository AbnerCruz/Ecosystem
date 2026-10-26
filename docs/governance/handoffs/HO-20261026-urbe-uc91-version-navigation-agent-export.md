# Handoff — Urbe UC-91 (26/10/2026)

## Estado

Implementação inicial em branch feat/urbe-uc89-version-navigation-export-20261026, Issue #793, ADR-0043 proposto. Não integrada à main. Não publicada em APK. CI e DEVICE ainda pendentes.

## Escopo entregue à revisão

- Core AssistantExport: Markdown leve e JSON completo para conversa específica e memória com escopo agent; usa os contratos atuais de AiConversation/AiConfig, sem serializar Providers/ApiKey.
- UI do Assistente: ação de exportar conversa atual, exportar conversa histórica e exportar memória do agente; seleção de modo e seletor nativo de destino.
- Android Back: MainActivity encaminha para MainView.HandleSystemBack, com precedência de modais, subviews e editor.
- Versões: AppUpdate.List filtra tag e artefato exato e ordena versões; centro em Configurações oferece patchnotes embutidos e catálogo remoto paginado para download manual com advertência de downgrade.
- Dois documentos canônicos de lançamento da beta.31 publicada: PATCHNOTES.md e PATCHNOTES-HISTORY.md, incluídos como recursos do aplicativo. Os patchnotes específicos legados permanecem como fontes.
- VaultCompatibility: manifesto opcional, mínimo de versão de escrita e features por arquivo, integrados a VaultReader e aos dois adapters nativos. Arquivos incompatíveis são FutureFiles; escritas individuais continuam protegidas pelo PlanOne. Diagnóstico de arquivos protegidos no menu do vault, com duplicar ou mover à lixeira conscientemente.

## Fontes e normas verificadas

MANIFEST.md (incluindo NN-001, NN-003, NN-008, NN-010, NN-011, NN-016, NN-017, NN-018, NN-019), AGENTS.md raiz, apps/urbe/AGENTS.md, ecosystem.json, apps/urbe/docs/csharp/ROADMAP.md, ADR-0032/0033/0041/0042, fluxo de release canônico, código do Urbe.Core/VaultReader/VaultWriter, adapters Android/desktop e store de agentes. Sem alterações em outros produtos, sem alterar versão canônica ou assinatura, sem misturar dados de IA ao vault.

## Testes adicionados — ainda sem execução comprovada

- AssistantExportTests: estrutura e completude de transcript, chamada/retorno de ferramenta, metadados, métricas, separação Lite e credenciais.
- AppUpdateHistoryTests: semver, versão e artefato real da plataforma, drafts e links estranhos.
- VaultCompatibilityTests: bloqueio granular, features desconhecidas, manifesto inválido e vault tradicional.
- AndroidBackAndPatchnotesUiTests: prioridade da navegação Android no shell headless e recurso offline.

## Pendências e riscos antes de integração

1. Executar dotnet test Core e UI, consistency, CI Android/Windows e corrigir quaisquer erros de compilação e regressões. Não foi possível executar dotnet nesta sessão: runtime dotnet indisponível no ambiente de trabalho local.
2. Pipeline .github/workflows/urbe-csharp-mobile-release.yml ainda publica release-notes boilerplate. Atualização pretendida para usar PATCHNOTES.md **não foi aplicada**, pois a ferramenta de edição do workflow retornou bloqueio de segurança. Rever alteração somente por processo autorizado e adicionar validação de rotação/versão ao CI.
3. Validar Android físico, inclusive tecla/gesto Back com IME, diálogos, grafos e salvamentos pendentes; SAF picker da exportação; arquivos grandes e bloqueio de escrita no vault SAF real.
4. A segurança por feature depende de um manifesto declarado; esta fatia implementa a **leitura/proteção**, não geração automática por cada editor. Até os editores emitirem metadados por recurso, a proteção legada por formatos reconhecidos segue valendo e não deve ser vendida como cobertura universal.
5. Uma cópia de arquivo com recurso desconhecido não equivale a conversão. O original deve continuar intacto e a cópia ser revisada pelo usuário, preferencialmente por um app capaz de ler o formato.
6. O JSON completo exporta tudo que o Assistente efetivamente **persistiu** (incluindo métricas disponíveis), não telemetria não registrada pelo provedor/modelo. Anexos e conteúdo de mensagens podem revelar dados privados que o usuário inseriu; aviso e escolha do destino são obrigatórios.
7. Não sugerir desinstalar APK antigo como via fácil de downgrade: risco de perder conversas, memórias, modelos e configurações privados. O catálogo só abre download externo; não instala nem migra dados.
8. O patchnote embutido da beta.31 é fiel à última release publicada. Quando houver nova versão, rotacionar os dois arquivos, acompanhar versão do csproj e só publicar pelo integrador confiável.

## Próximo executor

Corrigir as falhas reveladas pelo CI do PR draft. Rever contratos e manifests com foco em persistência/versionamento (crítico), então integrar conforme governança e testar no Android físico; caso aprovado, preparar beta.32 ou versão posterior com novo número, never reusar tags. Nenhuma declaração de conclusão, de teste DEVICE ou de release nesta etapa.
