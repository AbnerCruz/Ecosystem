# ADD-0028 — Ordem do proprietário: o Urbe segue sem a simulação visual

**Data:** 2026-10-22. **Autoridade:** instrução explícita do proprietário nesta conversa, com as escolhas confirmadas por ele nas perguntas do agente. **Registro:** DEC-0051. **Escopo:** o cliente C# nativo do Urbe (`apps/urbe/csharp`: `Urbe.Core`, `Urbe.Client`, `Urbe.Android`, `Urbe.Desktop`). **Substitui** a parte de ADD-0020 (DEC-0043) e ADD-0022 (DEC-0045) que faz da cidade simulada a interface do Urbe nativo. **Mantém** nativo sem web (ADD-0020), desempenho primeiro (ADD-0024), beta 2.0 nativa (ADD-0025) e experiência de usuário topo de linha (ADD-0026).

## O que o proprietário disse

Depois de usar a beta 2.0.0-beta.14:

> *"Tamo usando a versão beta pra ir testando. Infelizmente absolutamente qualquer coisa referente a simulação simplesmente não funciona como produto. É pesado, está sendo limitante por causa de espaço e tudo mais. Vamos fazer uma ramificação e seguir o desenvolvimento com essa bifurcação sendo o produto, a ideia de simulação fica totalmente congelada até eu pensar em como substituir, infelizmente já percebo que tal como está não escala bem em muitos aspectos. Está tudo bem, é pra isso mesmo que serve um beta. No novo ramo que vamos seguir o desenvolvimento, o urbe permanece a mesma coisa, a mesma ideia de produto, mas a simulação visual inteira e todas as suas funcionalidades pode arrancar fora. Limpa o código. Essa mudança é crítica, é muito perigosa, não estrague o meu app."*

Nas perguntas que vieram em seguida, o proprietário escolheu:

- **Ramificação:** a `main` segue como o produto, sem a simulação. O estado com a simulação fica congelado num ramo.
- **Tela principal:** o Explorer passa a ser a base do app. Ao abrir, o app continua voltando direto para a última nota.
- **Atualização:** a mesma beta (`app.urbe.csharp`) recebe a versão nova como atualização normal, mantendo o vault.

## Decisões do proprietário

1. **A simulação visual não é mais produto.** Saem do cliente nativo:
   - o mundo procedural (terreno, rios, vegetação, biomas);
   - a cidade (notas como casas, pastas como bairros, links como ruas), o mapa e o minimapa;
   - a vida (moradores, fauna, clima, eventos, iluminação, relógio);
   - as decorações e a aparência editável das construções;
   - o destaque das ligações na cidade;
   - e toda funcionalidade que só existe por causa delas.
2. **A simulação fica congelada, não perdida.** O código com a simulação está preservado no ramo **`urbe-simulacao-congelada`** (commit `9c13dc2e`, beta.14). A 1.8.4 em JavaScript (`apps/urbe/`) também não é tocada. Nada da simulação volta à `main` sem nova ordem do proprietário, que vai pensar em como substituí-la.
3. **O Urbe continua o mesmo produto.** São notas Markdown locais numa pasta real, com:
   - Explorer, editor (Visual e Fonte) e sintaxe do Urbe;
   - modelos, notas de voz e widget;
   - páginas e estúdio, assistente, plugins;
   - backups, importação e lixeira.
   A direção de "criador e produtor de artefatos" continua valendo.
4. **Código limpo.** A simulação é removida do código de produto do cliente nativo, sem ficar escondida atrás de uma opção. O que for só da simulação sai junto com os seus testes.
5. **O Explorer é a base do app.** Ele ocupa a tela onde antes ficava a cidade. Ao abrir, o app volta direto para a última nota (UC-59).
6. **Mesma beta, sem perda de dados.**
   - A atualização vai por cima da beta instalada.
   - Os arquivos da cidade no vault (`.urbe/mapa.json`, `.urbe/mundo.json` e afins) **não são apagados nem reescritos**: só deixam de ser lidos. Assim o ramo congelado, ou uma substituição futura, ainda os encontra.
   - Notas, pastas, anexos e preferências que não são da cidade continuam iguais.
7. **A mudança é crítica.** Vai em PR crítico, que espera a autorização do proprietário para integrar. Só fecha com a validação dele no aparelho (NN-017): abrir o vault, ver as notas, editar, gravar e reabrir sem perder nada.

## Limites que continuam valendo

- Nativo sem web (ADD-0020), desempenho primeiro (ADD-0024) e experiência de usuário topo de linha (ADD-0026, `apps/urbe/docs/csharp/UX.md`).
- Dados primeiro: a gravação continua verificada e nada do usuário se perde.
- A casca Blazor (`Urbe.UI`, `Urbe.App`, `Urbe.Web`) continua congelada e não ganha funcionalidade.
- Agentes não reintroduzem a simulação, nem parte dela, por paridade com a 1.8.4. A paridade com a 1.8.4 continua valendo **fora** da simulação, por exemplo no editor e no Explorer.
