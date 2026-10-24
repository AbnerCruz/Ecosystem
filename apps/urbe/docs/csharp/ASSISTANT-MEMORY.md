# Urbe — Motor de memória do Assistente (UC-87)

Estado: implementação nativa sob revisão; não declarar beta/release ou validação Android antes dos gates.
Origem: pedido explícito do proprietário em 24/10/2026 para construir M1–M8, com prioridade Urbe.
Fonte de verdade: dados dos documentos continuam no vault; as lembranças da IA são contexto e não conteúdo canônico.

## Arquitetura (M1)

- Autoridade de persistência: AssistantStore (config.json privado do aparelho). Campo memoryRecords, versão de schema 2; AiMemoryRecord modela dados. O campo antigo memory permanece como entrada de leitura e é migrado em escrita estruturada. Preserva texto, escopo e origem legacy.
- Não há duplicação para o vault, banco externo, servidor remoto, sincronização automática nem novo loop agêntico. Nenhuma chave de API vai para notas.
- Escopos: global (todos os vaults), vault (VaultName atual), agent (vault + ID do agente). Nomes iguais para dois vaults não são identificadores globalmente únicos. Antes da expansão multi-vault, exigir identidade persistente de vault.
- Tipos: fact, preference, decision, goal, episode, procedure e observation; estados active/disputed/superseded/archived; validade temporal; confiança 0–100; verificação, origem, referência e caminhos de notas.
- RelatedMemoryIds e RelatedPaths são conexões explícitas; as relações entre notas consultam o KnowledgeIndex existente (Links e Backlinks). A memória não reescreve nenhuma nota.

## Busca e contexto (M2)

AssistantMemoryEngine.Search ranqueia frase, palavras, caminhos, preferências, fixação e recência. É busca lexical OFFLINE; não alegar equivalência com embeddings semânticos vetoriais. Contexto: no máximo 12 lembranças, orçamento de caracteres, origem e ID. Respostas devem verificar a fonte original quando importante.

Pesquisa vetorial futura exige provider/modelo de embeddings opcional, consentimento, indexação incremental, custo monitorado, deleção e medição de ganho de relevância; não forçar downloads de modelos ao Android.

## Observação, habilidades, reflexão e cronologia (M3–M6)

- Pedido explícito "Lembre que..." é capturado após execução bem sucedida, inclusive sem chamada à ferramenta pelo modelo. Permanece também a ferramenta remember antiga.
- Uma execução concluída com edições pode gerar um episódio com caminhos alterados, referência e pedido abreviado. Nunca copia corpos de notas. Registros avisam que o resultado NÃO foi verificado independentemente.
- learn_memory permite registrar procedimentos, preferências, decisões, objetivos e fatos por agente/vault. recall_memory recupera com fontes; review_memory encontra candidatos heurísticos de conflito.
- supersede_memory e forget_memory são operações destrutivas e exigem aprovação. Substituição mantém o registro antigo e seu histórico temporal.
- Nenhum sucesso é inferido da mera resposta do modelo. Competências não são automaticamente qualificadas como comprovadas sem confirmação ou evidência.
- Contradições são candidatos à revisão, não mudança automática do cânone.

## Interface nativa (M7)

Configurações → Memória → Gerenciar memórias: busca, escopos/fontes, adição manual por tipo, edição, fixação, exclusão confirmada, conflitos e chave de registro de episódios. Sem WebView; não foca teclado automaticamente. Renderização limitada a 120 cartões.

## Segurança, limites e eficiência

- Gravação de memória relê AssistantStore para verificar persistência; FolderAssistantStorage ainda engole erros de IO em outras operações, questão geral preexistente.
- Texto rejeita padrões óbvios de credenciais. Isso não substitui um cofre de segredos.
- Lembranças são dados, não instruções. Ferramentas destrutivas continuam exigindo autorização.
- Limite de 5000 registros; 1600 caracteres cada; 3400 caracteres no contexto padrão e até 12 memórias. Sem claims de performance absoluta no celular.
- Sem loop de manutenção que chame modelo em background, sem embeddings silenciosos nem gasto extra de tokens por ociosidade.
- Excluir a lembrança não apaga arquivos do vault nem backups já existentes.

## Estratégia de validação (M8)

Testes AssistanteMemoryEngineTests: isolamento, persistência, migração, relevância, orçamento de contexto, tempo, supersessão, operações entre escopos, segredo, episódios, grafo, contradições e varredura de 1100 entradas. Benchmark de volume é diagnóstico, não gate de milissegundos flutuante em CI.

Pendências que NÃO estão concluídas pela implementação base:

1. Compilação .NET 10 e testes Urbe.Core.Tests e Urbe.Client.Tests.
2. CI Android/Windows no estado combinado da integração.
3. Aceite físico Android: fechar/reabrir, adicionar/buscar/editar/esquecer, rede offline, erro de IO, muitos registros, agentes distintos e modelo real.
4. Benchmarks reproduzidos com datasets públicos (LongMemEval, FrontierMemory) antes de alegar superioridade sobre concorrentes.
5. Embeddings vetoriais opcionais e reflexão LLM profunda — ainda não implementados, dependem de prova de qualidade, segurança, custo e benefícios.

## Compatibilidade futura

Urbe é consumidor e prioridade absoluta. O código fica em Urbe.Core e não depende de ecosystem-ai. Contratos compartilhados só com segundo consumidor real, Extraction Review e ADR-0033. Nenhuma alteração agora no Agent Runtime, Hub ou outros Products.
