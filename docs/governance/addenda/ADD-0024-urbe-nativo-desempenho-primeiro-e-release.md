# ADD-0024 — Ordem do proprietário: desempenho, resposta e acessibilidade no celular primeiro; depois release do Urbe nativo

**Data:** 2026-10-15. **Autoridade:** instrução explícita do proprietário nesta conversa. **Registro:** DEC-0047. **Escopo:** cliente C# do Urbe (`apps/urbe/csharp`). **Complementa** ADD-0020 (nativo sem web), ADD-0022 (observações de uso) e ADD-0023, sem substituí-los.

## O que o proprietário disse (fatos, com as palavras dele)

1. *"Já quero release assim que melhorar a acessibilidade, performance, tempo de resposta etc... Essa é a prioridade. De resto, vai anotando e a gente vai desenvolvendo. Também quero que observe as anotações antigas para adaptarmos o que não foi implementado."*
2. Nas observações da prévia nativa (arquivo `Observações.md` enviado nesta conversa): *"Sinto que tudo está muito com delay, talvez devêssemos estudar bem a performance, tempo de resposta, acessibilidade, eficiência"*; *"No geral, tá tudo absurdamente mal otimizado para celular, os toques, o editor, a performance"*; *"no editor por vezes ao clicar não abre o teclado para escrever"*; *"Demora horrores para salvar e portanto sair"*.
3. Relatório de erros enviado: o app caía ao fechar uma página HTML (`ObjectDisposedException` no `WebView.Destroy`).

## Decisões do proprietário

1. **Prioridade:** desempenho, tempo de resposta, toque e acessibilidade no celular vêm antes de qualquer recurso novo (UC-48 no ROADMAP C#).
2. **Release:** assim que essa fatia estiver feita e validada no aparelho, o cliente nativo sai como release (beta Android), pelo pipeline direto do Ecosystem (ADD-0015). O canal e a tag do cliente nativo são definidos na fatia de release (UC-25/UC-27); os gates de validação humana continuam valendo (NN-017).
3. **Observações novas e antigas** viram itens do ROADMAP, desenvolvidos depois da prioridade acima (UC-49 a UC-55), além das pendências de UC-36 a UC-47.

## Consequências

- ROADMAP C# ganha UC-48 (desempenho e acessibilidade, antes do beta) e UC-49 a UC-55 (observações de 15/10/2026).
- O travamento do relatório é corrigido na mesma fatia.
