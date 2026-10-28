# Selos de autoria da IA — contrato (UC-51 / CST-027–033, CST-035)

**Estado:** implementado no Urbe C# nativo em 28/10/2026 (`AiAuthorship`, `Urbe.Core`). Validação no aparelho pendente (NN-017). Este documento substitui as regras 1, 2 e 4 de [ASSISTANT-AUTHORSHIP-INTEGRITY-20261026.md](ASSISTANT-AUTHORSHIP-INTEGRITY-20261026.md), que tentavam proteger o selo bloqueando gravações. Esse bloqueio deixava o agente em loop e acumulava selos.

**Fontes da intenção:** FeaturesCheck do proprietário (seções *Marca de autoria da IA* e *Marca d'água de autoria em blocos gerados por IA*) e a consolidação Numa (CST-027 a CST-035). A sintaxe `{ia:"modelo"}` no `.md` continua a aprovada em DEC-0045 / UC-43.

## 1. Problema observado (evidência nos arquivos do proprietário, 25–28/10/2026)

| Sintoma | Exemplo real | Causa no código anterior |
|---|---|---|
| Só o último bloco era marcado | resposta de vários parágrafos com um único selo no fim | `NoteMarks.WithAiMark` colocava uma marca no fim do texto inteiro |
| Selos acumulados | `{ia:"gpt-6-luna"} {ia:"gpt-6-luna"} {ia:"gpt-6-luna"}` | o selo antigo não podia sair, e cada nova escrita somava outro |
| Selo no meio do negrito | `- [x] ** {ia:"gpt-6-luna"}Páginas …:**` (negrito quebrado) | o agente editava em volta de um selo que não podia mover |
| Loop do agente | 8 tentativas de remover o selo (beta.32) e edições repetidas por falha de correspondência exata | o modelo via os selos, tentava limpá-los e recebia `protected`; o `old_text` copiado não batia com os bytes |
| Correção ortográfica virava autoria da IA | parágrafo humano corrigido ganhava selo | qualquer `edit_note` marcava o `new_text` |

## 2. Regras

1. **O selo é do aplicativo.** Os agentes leem as notas **sem selos** (`read_note`, `read_notes`, `grep_notes`, `search_notes`, seleção do editor). Os selos que um agente escrever são ignorados. Nenhuma ferramenta adiciona, remove, move ou altera selo: `edit_note` sobre um selo devolve um `noop` explicativo, sem erro e sem gravação. O prompt diz isso aos agentes (CST-033).
2. **Bloco de autoria.** Um título, uma linha de parágrafo, um item de lista, uma citação inteira, uma tabela, um bloco de código ou uma fórmula em destaque (`$$`): são os blocos que o editor Visual desenha, com listas divididas por item. Front matter, linhas `---` e cercas de contêiner `:::` são estrutura e nunca recebem selo.
3. **Posição canônica, Markdown válido em qualquer editor.** O selo fica no fim da linha do bloco, antes dos espaços de quebra dura (`texto {ia:"m"}  `). Numa tabela, fica dentro da última célula (`| 1 | 2 {ia:"m"} |`). Num bloco de código ou fórmula, fica na linha logo depois do fechamento. Há um selo por bloco.
4. **O que a IA escreve.** O Urbe compara a nota anterior com o texto proposto, sem selos, no estilo da atribuição hierárquica do WikiWho: primeiro os blocos idênticos, na ordem e depois movidos; depois o bloco mais parecido de cada região alterada, por palavras compartilhadas.
   - Bloco idêntico ou movido: mantém o autor e os bytes.
   - **Correção** mantém o autor original, humano ou IA. É correção quando todas as palavras já existiam na região, ou são grafias próximas (acentos, maiúsculas, erro de digitação), ou quando há só palavras novas isoladas: nenhuma sequência de 3 palavras novas, no máximo 2 palavras novas (ou 1/5 do bloco), e as palavras mantidas somam pelo menos o dobro das novas. Formatação, pontuação, junção, divisão, reordenação e exclusão de palavras não geram conteúdo.
   - Bloco criado ou **regenerado** recebe o selo do modelo atual e substitui o selo antigo, sem acumular.
5. **O que a pessoa escreve.** A edição humana nunca cria e nunca tira o selo de um bloco que continua existindo. Editar um bloco da IA no lugar, dividir ou juntar mantém o selo. Apagar o bloco leva o selo junto. Um bloco novo da pessoa não tem selo.
   - **Visual:** a caixa de edição mostra o texto sem selo, e o Urbe recompõe o bloco a cada alteração. Um selo digitado ou colado é descartado.
   - **Fonte:** o texto é o arquivo real. Apagar, alterar ou forjar só o selo é desfeito com aviso. Apagar a linha inteira (ou o bloco de código com a linha do selo) é permitido. Apagar só a linha de selo de um código não é.
6. **Aprovação editada.** No cartão **Editar**, a pessoa edita o texto sem selos. Ao aprovar, os blocos da proposta continuam da IA (também quando corrigidos pela pessoa), e o que a pessoa acrescentou fica sem selo. Se a nota mudar enquanto a aprovação espera, nada é gravado, e o agente é instruído a reler (CST-034).
7. **Legado.** Selos acumulados ou fora do lugar são **mostrados** na posição canônica, uma vez, no editor Visual, na exportação e nas incorporações de notas (`Canonical`). Os bytes só mudam no bloco que alguém edita. As páginas (`.page.json`) mantêm suas marcas por trecho (UC-82).
8. **Só notas Markdown.** Arquivos `.json`, `.css`, `.js` e semelhantes escritos pelo agente nunca recebem selo.
9. **Notas fixadas pelo comando inline** (`&Nome&:`, UC-80): a citação acrescentada recebe o selo pelo mesmo motor.

## 3. Limites honestos

- O selo continua sendo texto Markdown. Uma edição feita fora do Urbe, direto no sistema de arquivos, não é impedida. Não há imutabilidade criptográfica.
- A classificação entre correção e geração é por palavras, não por significado. Uma tradução conta como geração. Uma frase nova de até duas palavras isoladas num parágrafo longo conta como correção.
- Um texto literal `{ia:"…"}` fora de código numa nota é tratado como selo. Para escrevê-lo como texto, use código inline.
- Desempenho medido em headless: a FeaturesCheck do proprietário (101 selos, 126 blocos) é atribuída em ~3 ms; uma nota de 6 000 blocos fica abaixo do teto de 400 ms do teste. Isso não substitui a medição no moto g32 (CST-114).

## 4. Testes

- `AiAuthorshipTests` (Core): regras 2 a 5 e 7, posição em código, tabela, fórmula, front matter, contêiner e CRLF, idempotência e ida-e-volta, desempenho.
- `AssistantAuthorshipSealTests` (Core): leitura sem selos; reescrita com selo por bloco; edição sobre selo legado no meio do negrito; correção ortográfica sem selo; tentativas de remover ou forjar sem gravação e sem erro; criação; acréscimo; aprovação editada; nota alterada durante a aprovação; arquivo não-Markdown; prompt.
- `AssistantAuthorshipRecoveryTests` (Core, beta.32): o caso de remoção continua sem gravação e termina normalmente.
- `EditorAuthorshipProtectionTests` (Avalonia headless): a caixa Visual sem selo e o bloco que o mantém; selo digitado descartado; divisão; código com a linha de selo; Fonte não apaga só o selo do código.

## 5. Validação pendente no aparelho (DEVICE)

No Android físico, com Gboard, nos modos Visual e Fonte:

- editar e dividir um bloco da IA;
- selecionar tudo e colar;
- Desfazer e Refazer;
- alternar o modo;
- aprovar uma proposta editada;
- aplicar uma sugestão inline e reabrir a nota.

Em todos esses casos, conferir que não há salto de caret nem latência perceptível.
