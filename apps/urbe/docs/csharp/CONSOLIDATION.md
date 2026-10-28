# Consolidação Numa — progresso verificado (CST)

Este arquivo é a autoridade do **andamento técnico** dos itens CST da consolidação do proprietário (“Numa Product Consolidation — Roadmap Atualizado”, 25–28/10/2026, versão 2.0). Os IDs e os critérios de conclusão são os do documento do proprietário. Aqui fica o que foi entregue, com evidência, e o que ainda falta.

Regras:
- **Entregue (automatizado)** significa código e testes na `main`.
- **Validado** só depois do teste no aparelho (NN-017), feito pelo proprietário.
- Um item parcial diz o que falta.

| ID | Entrega | Evidência | Pendente |
|---|---|---|---|
| CST-027–033, 035 | Selos `{ia:"modelo"}` administrados pelo Urbe: um por bloco, correção mantém o autor, sem loop ([contrato](AI-AUTHORSHIP-SEALS.md)) | PR #846; `AiAuthorshipTests`, `AssistantAuthorshipSealTests`, `EditorAuthorshipProtectionTests` | DEVICE |
| CST-034 (parte) | Aprovação editada: o texto da pessoa fica sem selo; nota alterada durante a aprovação não é sobrescrita | PR #846; `ThePersonsEditOfTheProposalKeepsAuthorshipHonest`, `ANoteChangedDuringTheApprovalIsNotOverwritten` | DEVICE |
| Nota 25/10 | A pasta Personalização não aparece como projeto na Área de trabalho (continua no Explorador) | `WorkspaceIndexTests.TheAppsOwnFolderIsNotAProject` | DEVICE |
| CST-053 | Toda nota rápida é checkbox (widget, ditado, app e agente), sem checkbox duplicado; a ferramenta `add_quick_note` da IA usa a nota do mês, a seção do dia e o horário, e só a entrada recebe selo | `QuickNotesTests`, `AnAgentsQuickNoteIsACheckboxInTheMonthsNote` | DEVICE (widget Android) |
| CST-040 | `[[…]]` e `![[…]]` sugerem notas **e** qualquer arquivo do vault (imagens, PDF, áudio, código, páginas, grafos). Nomes que começam pelo digitado vêm primeiro; o caminho entra quando há nomes iguais | `WikiTargetsTests`, `Fonte_WikilinksAndEmbedsFindEveryFileAndAFileLinkOpensIt` | Visual: mesma lista, conferir no aparelho |
| CST-041 (parte) | Um link para um arquivo que não é nota abre o visualizador, em vez de oferecer criar uma nota com esse nome | mesmo teste | Revisar a mensagem de incorporação não suportada |
| CST-039 | Mover para: há busca a partir de 7 pastas; o destino exclui a pasta atual dos itens, a própria pasta e as subpastas; colisões avisam ou usam nome único | revisão do código (`ChooseFolderAsync`, `MoveFolderAsync`, `FolderMovePath`) | Teste de UI do seletor |
| CST-070 | Os blocos de instrução (título opcional + texto) já aparecem em **Novo agente**, no mesmo editor da configuração posterior | `Assistant_NewAgentHasInstructionBlocksFromTheStart` | DEVICE |
| CST-077 | Os agentes respondem por padrão no idioma do sistema. Há escolha em Configurações → Contexto → Idioma das respostas, e um pedido explícito de outro idioma prevalece | `AgentLanguageTests` | DEVICE |
