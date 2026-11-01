# Urbe — Notas da versão atual

**Versão canônica preparada:** 2.0.0-beta.40
**Canal:** Urbe 2.0 nativo, Android e Windows
**Publicação:** exclusivamente pelo workflow oficial, depois do CI e da integração
**Identidade Android:** app.urbe.csharp, mesmo certificado e atualização sobre a beta.39

## IA — Planejamento adaptativo em camadas

- Tarefas complexas podem registrar o pedido original, o ponto de partida e o resultado final esperado em `update_plan`, sem criar um planejador independente.
- Os objetivos possuem IDs e dependências verificadas contra IDs repetidos, referências ausentes, ciclos e avanço antes das pré-condições.
- Para iniciar um nó, o agente precisa definir testes e critérios de aceite. Para declará-lo concluído, registra separadamente evidências de testes, implementação e confronto com objetivos anteriores.
- Uma lista de nós marcados como concluídos não encerra automaticamente o objetivo: há confrontos separados com o plano e o pedido original.
- O plano simples continua disponível para tarefas pequenas, evitando chamadas desnecessárias. A ferramenta mantém a lista de objetivos na interface atual.
- Evidência textual fornecida pelo modelo é apresentada como relato, nunca como comprovante automático de CI, teste executado, verificação independente ou validação física.

## Recursos mantidos da beta.39

- Abas controladas pelo usuário: navegação comum substitui a aba atual, enquanto o botão + abre uma aba adicional sob escolha.
- Correções de preservação de rascunhos, renderização de fórmulas e previews de imagem.
- Estúdio de Publicações com referências às notas reais, exportação HTML e impressão/salvamento como PDF pelo diálogo da plataforma.

## Limites conhecidos

- O modelo de planejamento estruturado é opt-in e não substitui os verificadores independentes por formato. Os recibos de CI, testes reais e validação no dispositivo continuam sendo produzidos fora dos campos declarativos do plano.
- O planejador não é ainda um executor distribuído persistente de grafos; interromper ou encerrar o processo permanece sujeito às capacidades de recuperação existentes do Urbe.
- Esta versão não conclui todo o plano Numa Consolidação 2.5 nem os formatos editoriais ainda pendentes.
- Nenhum resultado desta release é declarado validado fisicamente no Android sem evidência correspondente.
