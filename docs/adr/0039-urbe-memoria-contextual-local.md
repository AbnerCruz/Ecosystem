# ADR-0039 — Memória contextual local e aprendizado dos agentes do Urbe

## Status

Proposto. Direção autorizada pelo proprietário na conversa de 24/10/2026 ("Faz tudo", após descrição M1–M8).
A integração crítica de persistência ainda depende da política de integração do Ecosystem e de testes.

## Contexto

A memória atual guarda listas de strings por VaultName na configuração privada do Assistente. O histórico de conversas pode ter turnos omitidos do contexto; as notas do vault continuam fonte de verdade independente. O proprietário determinou que o Urbe receba primeiro mecanismos avançados de memória, com aproveitamento eventual no Ecosystem, sem tornar o Ecosystem prioridade.

## Problema

Construir memória sem copiar notas, expor segredos, confundir fatos com conjecturas, manter memória entre agentes indevidamente, ou tornar a inicialização/editor pesados. A memória deve suportar versões temporais, proveniência, experiências e procedimentos, e não duplicar o Agent Runtime.

## Opções

1. Instalar Mem0/Graphiti/Zep/Letta como dependência obrigatória, incluindo banco/serviço externo.
2. Guardar todas as conversas num prompt maior, sem recuperação.
3. Evoluir o AssistantStore local e as ferramentas do Urbe, com mecanismos adicionais atrás de contratos locais e limites explícitos; propor adaptadores especializados somente quando houver ganho verificável.
4. Transferir imediatamente memória para o Agent Runtime do Ecosystem.

## Decisão proposta

Opção 3. Memórias estruturadas e versionadas na configuração privada já existente, mantendo leitura do legado e migrando no primeiro write estruturado. O Urbe.Core detém o domínio, o AssistantPanel expõe interface nativa e o vault permanece dono do conteúdo documental. Leitura contextual sob orçamento de tokens; separação de escopos global/vault/agent, referências de fonte, confiança/validade/estado, relações com KnowledgeIndex. Nenhum uso de IA em background obrigatório nem gasto de API sem ação humana.

Factos extraídos de notas precisam de citação, não de autoridade de instrução. Registros episodicos de execuções são observações, não prova de êxito; procedimentos são aprendizados propostos até validação. Reescrita/supersessão e exclusão requerem confirmação. O usuário pode consultar, corrigir, fixar e apagar memórias no cliente nativo.

Nenhum contrato compartilhado com Ecosystem AI nasce neste ADR. Extração segue ADR-0033, teste com segundo consumidor real e Extraction Review, sem mudar UI ou ferramentas do Urbe por imposição do outro Product.

## Consequências

- Privacidade local-first; dados novos usam campo memoryRecords e versão de schema. O campo memory legado continua como entrada e deixa de ser autoridade após migração. Não apagar dados arbitrários do vault.
- UI simples de memórias e ferramentas read/write integradas ao loop existente, sem segunda instância de loop ou scheduler.
- Busca lexical e grafo documental inicial NÃO são busca vetorial semântica completa. Agentes não treinam pesos de modelos.
- A escolha inicial do escopo de vault usa VaultName existente. Antes de uso multi-vault avançado, endereçar colisões com identidade persistente do vault.
- Sem CI e aparelho físico, não declarar gate M1–M8 concluído, beta ou release.
- Uma avaliação de vetor semântico precisa medir ganho real frente à busca lexical, consumo de armazenamento, custo de provider, política de consentimento, degradação offline e privacidade.
- Qualquer promoção de memória geral para framework requer ADR separado, contratos, permissões e provas.

## Alternativas rejeitadas

Opções 1 e 2: custo, privacidade e pouco controle. Opção 4: antecipa acoplamento e ignora prioridade expressa do Urbe.

## Referências

MANIFEST NN-001, NN-006, NN-017, NN-020, NN-022; ADR-0017, ADR-0033; ADD-0027; apps/urbe/docs/csharp/ROADMAP.md UC-87; apps/urbe/docs/csharp/ASSISTANT-MEMORY.md.
