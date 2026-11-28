# IDE do Lunet: mobile-first, IA no modelo do Numa e arquivos direto na pasta (estudo, 27/11/2026)

Pedido do proprietário (27/11/2026, no celular):
- "Odiei a interface da IDE": estudar com foco mobile-first, olhando explorador de arquivos, abas, configurações, atalhos, executar e console.
- Inspirar-se na filosofia de estrutura do VS Code e buscar o retorno das comunidades.
- "O que mais me incomoda é a navbar e a forma que foi organizada e construída."
- "Usar a IA tá impossível de ruim, o Numa faz isso bem, inspire-se."
- Os arquivos devem ser editados direto no dispositivo, "não é cópia, não é espelho", como no Numa.
- Decisão do proprietário, na mesma conversa: **só a pasta escolhida, por SAF, como o Numa**, em vez da permissão de acesso a todos os arquivos.

## Ajuste do Preview após a versão 613

- **Rodar** abre Jogo em tela cheia dentro da IDE. O controle do painel ainda permite reduzir sua altura e ver o código junto com o jogo. Console e Problemas continuam no mesmo painel.
- A seta da IDE e o Voltar do sistema encerram o Preview e voltam ao arquivo aberto. O próximo Voltar no editor vai à área de trabalho, depois de fechar o teclado se estiver visível.
- Na auditoria da versão 613, a compilação GPU lia fontes e executava Roslyn na thread da interface. Abrir, pausar e redimensionar a prévia também percorriam o caminho que recriava o layout e remontava o controle nativo. O ajuste move a compilação para fora da thread da interface, reaproveita o cache do compilador e mantém a superfície montada ao pausar e continuar. Falhas gerenciadas no renderizador chegam à IDE como diagnóstico.
- O [código de NativeControlHost](https://github.com/AvaloniaUI/Avalonia/blob/main/src/Avalonia.Controls/NativeControlHost.cs) mostra que a associação nativa depende da árvore visual. A [referência de GLSurfaceView](https://developer.android.com/reference/android/opengl/GLSurfaceView) descreve o ciclo próprio da thread e do contexto EGL. O relato do celular não veio com log de falha ou medições de latência.

## 1. Auditoria do que existe (main em 70a06fe)

### IDE (`WorkspaceView`, `DocumentEditor`, `SupportPanelView`)

| Ponto | Hoje | Problema |
|---|---|---|
| Navegação | Barra inferior com Código, Jogo, Recursos e Notas, mais o menu "Mais" com painéis | Pastas do projeto (Recursos, Notas) e o jogo viram telas separadas. O jogo e o código não aparecem juntos no celular. Para ver o console, sai-se do código. |
| Arquivos | A faixa superior lista todos os arquivos do tipo da aba como se fossem abas | Não há árvore de pastas. Aba aberta e arquivo existente se confundem, e não dá para fechar uma aba. |
| Executar | Botão flutuante Rodar, que leva à aba Jogo | O resultado tira o código da tela. Console e Problemas ficam em painéis de outra tela. |
| Estado | Subtítulo "Parado/Rodando" no topo | Não há barra de status com erros, linha/coluna ou estado da execução. |
| Ajustes e atalhos | Ajustes globais; paleta de comandos (F1) existe, mas sem entrada visível no celular | O que o VS Code resolve com a paleta fica escondido. |
| IA | Um dos painéis de apoio, no lugar do código | A conversa disputa espaço com o código. As mudanças aparecem só como diff na conversa, não no arquivo. |

### Arquivos (`ProjectFolderSync`)
- Os jogos vivem numa cópia de trabalho interna (`ProjectWorkingCopies/<id>`).
- A pasta escolhida recebe só revisões em ZIP. É exatamente a "cópia/espelho" que o proprietário rejeita.
- `LunetProject` já centraliza leitura e escrita (ReadText, WriteText, Import, ListTree, Rename, Delete).
- Cerca de 80 pontos fora dele usam `project.Directory` com caminhos de disco.
- O conteúdo do jogo já passa por `IContentSource`, e o compilador recebe textos, sem disco.

### Numa (o app de notas do Ecosystem): o que ele faz bem
- **Arquivos:** `AndroidVaultStorage` usa a pasta SAF como autoridade, sem cópia oculta. A pasta é percorrida uma vez por abertura (DocumentsContract, só as colunas necessárias), e há cache dos bytes de arquivos que não mudaram (tamanho e data de modificação).
- **IA (`AssistantPanel`, `MainView.Assistant`):**
  - painel sobre o editor, ocupando cerca de meia tela, com uma rolagem só e o campo de escrita sempre visível;
  - a mudança do agente aparece **no próprio arquivo** para aceitar ou recusar (UC-80), com aviso "Desfazer" depois;
  - toda escrita passa pelo arquivo real primeiro;
  - girar a tela ou abrir o teclado redimensiona o painel sem perder o texto.

## 2. Pesquisa externa
- **VS Code:**
  - o layout existe para deixar o máximo de espaço ao código;
  - a barra de atividades troca a visão da barra lateral (Explorador, Busca, Git), e o painel inferior é de Console, Problemas e Saída;
  - na barra de status, a esquerda é do projeto e a direita do arquivo;
  - a paleta de comandos dá acesso a tudo;
  - fontes: [UX Guidelines](https://code.visualstudio.com/api/ux-guidelines/overview), [User interface](https://code.visualstudio.com/docs/getstarted/userinterface), [Modernizing the VS Code UI (out/2026)](https://code.visualstudio.com/blogs/2026/10/15/modern-ui).
- **Editores móveis (Acode, Spck):**
  - a queixa recorrente é entrar e sair arquivos do app e editar o armazenamento externo;
  - os elogios são o visual limpo e o Git integrado;
  - fontes: [Spck na Play Store](https://play.google.com/store/apps/details?id=io.spck), [Acode](https://github.com/k34o/Acode).
  - Não achei threads do Reddit específicas sobre abas e explorador. O resto vem de lojas e páginas dos próprios fabricantes, que não são neutras.
- **SAF:**
  - listar arquivo por arquivo é lento, com várias chamadas entre processos por consulta;
  - a recomendação é consultar com o `DocumentsContract` direto, pedir só as colunas necessárias e manter um cache;
  - fontes: [CommonsWare, Scoped Storage Stories](https://commonsware.com/blog/2019/11/23/scoped-storage-stories-documentscontract.html), [Android, documents and files](https://developer.android.com/training/data-storage/shared/documents-files).
  - O Numa já faz isso, e o Lunet reaproveita o padrão, sem dependência entre Products (NN-002): o código é portado e adaptado.

## 3. Decisões de desenho (celular primeiro; o tablet ganha colunas)

```
┌──────────────────────────────┐
│ ☰  Game.cs ●  Player.cs ✕  ▶ ⋮│  topo: explorador · abas abertas (fecháveis) · Rodar · mais
├──────────────────────────────┤
│                              │
│          código              │  o editor ocupa tudo
│                              │
├──────────────────────────────┤
│ Jogo | Console | Problemas  ⌄│  painel inferior (folha que se arrasta; fechado por padrão)
├──────────────────────────────┤
│ ⊗ 2  ⚠ 1   Rodando   L12:C8 ✦│  barra de status · ✦ abre a IA
└──────────────────────────────┘
```

1. **Sem a navbar da IDE.**
   - A barra Código/Jogo/Recursos/Notas sai.
   - Recursos e Notas são pastas na árvore do Explorador.
   - Arquivos não-código (imagem, música, mapa) abrem no seu Studio.
2. **Explorador (☰):** gaveta com a árvore da pasta do jogo. Permite novo arquivo ou pasta, renomear, apagar e importar.
3. **Abas reais:** só os arquivos abertos, com ● quando não salvos e ✕ para fechar; deslizar troca de aba.
4. **Rodar (▶) não sai do código:**
   - o jogo abre no painel inferior (no tablet, ao lado), com Console e Problemas na mesma folha;
   - a folha pode ir a tela cheia.
5. **Barra de status:** erros e avisos (um toque abre Problemas), estado da execução, linha e coluna, e a IA.
6. **⋮ = paleta de comandos** (o que já existe), mais Ajustes e Git.
7. **IA como no Numa:**
   - painel compacto por padrão, em meia tela no celular ou coluna em tela larga, com [expansão da mesma conversa](#7-chat-da-ide) e o campo de escrita fixo;
   - a mudança proposta aparece no próprio arquivo, para aceitar ou recusar;
   - "Desfazer" depois de aplicar.
8. **Arquivos diretos (SAF):**
   - a pasta escolhida é a autoridade;
   - uma camada `IProjectFiles` no `Lunet.Core` (disco no desktop e nos testes; SAF no Android, portando o padrão do Numa);
   - `LunetProject`, o compilador, o conteúdo do jogo e a IA leem e gravam por ela, e a cópia de trabalho com revisões ZIP sai.
   - **Migração:** os jogos que hoje estão em `Projects`/cópia de trabalho são gravados uma vez na pasta escolhida, conferidos e só então deixam de ser usados. Nada é apagado sem confirmação, e o proprietário não perde dados.

## 4. Fatias (cada uma com teste, captura e PR próprio)
- **F1:**
  - estrutura nova da IDE: topo com ☰, abas abertas, ▶ e ⋮;
  - remover a navbar; Explorador em gaveta com a árvore;
  - painel inferior Jogo/Console/Problemas; barra de status.
- **F2:** IA no modelo do Numa: painel sobre o editor e mudança no arquivo para aceitar ou recusar.
- **F3:** `IProjectFiles` com backend de disco; todo acesso a arquivos do jogo passa por ela, com os 80 pontos migrados.
- **F4:** backend SAF no Android e migração dos jogos existentes para a pasta, com verificação.
- **F5:** remover a cópia de trabalho e as revisões ZIP, só depois de F4 conferido no aparelho.

## 5. Riscos e o que não se promete
- **Velocidade:** o SAF é mais lento que o disco. Compilar e carregar imagens lê pelo cache em memória das leituras; a escrita é sempre na pasta e conferida.
- **Aparelho:** nada aqui é dado como validado no aparelho sem teste nele (NN-017).


## 6. G6 e G7 — salvamento e armazenamento direto

A IDE registra um rascunho no armazenamento privado antes de gravar o arquivo, em segundo plano e em intervalos de 400 ms enquanto há edições. Confirma o texto por releitura. No próximo início, o mesmo arquivo abre com o texto recuperado. Se outra aplicação alterou a origem, o editor preserva a versão externa e oferece **Salvar cópia** para o rascunho. Uma interrupção anterior ao primeiro registro pode perder as teclas desse intervalo.

No Android, **Ajustes → Pasta de projetos** abre o seletor do sistema. A pasta fica como origem para editor, Preview e Studios. O projeto inteiro e seus metadados são conferidos antes de trocar a raiz. Se o app já usava uma cópia de trabalho com permissão persistida, a migração tenta usar a pasta anterior ao iniciar. Quem perdeu a permissão pode reconectar pelos Ajustes. Projetos com o mesmo nome e conteúdo diferente exigem uma pasta sem conflito. Os dados da origem ficam preservados para recuperação. A opção **Usar os jogos internos** volta a listar os jogos internos.

O Git integrado depende de um caminho de disco. Projetos acessados só por SAF podem ser versionados por um cliente que tenha acesso à pasta. O Preview mantém processo separado no Android e usa a permissão persistida para abrir recursos na pasta.


## 7. Chat da IDE

### O que a auditoria encontrou

Na base `270fc72` a IA da IDE já abre na folha inferior do celular e em coluna no desktop. A sessão grava as etapas com identificador, duração e estado no histórico por jogo. A casca mostrava apenas as seis últimas durante a execução e descartava a visualização depois da resposta. O parser já reconhecia listas, tabelas, citações, links e código. A casca achatava os estilos em texto e deixava a tabela vazia.

### Pesquisa e decisões

O chat segue os componentes da casca existente. A [linguagem visual global do Ecosystem](../../../../docs/design/linguagem-visual.md) orienta a adoção de perfis por produto e registra a migração de consumidores v1.

| Referência | Observação | Uso no Lunet |
| --- | --- | --- |
| [Claude Code Desktop](https://code.claude.com/docs/en/desktop) | A densidade da transcrição pode variar, mantendo o texto da resposta completo. | Uma linha de atividade por resposta. Um toque mostra o que a IA leu, pesquisou, alterou ou verificou. |
| [Claude Code #91827](https://github.com/anthropics/claude-code/issues/91827) e [#86732](https://github.com/anthropics/claude-code/issues/86732) | Usuários relatam que diffs e logs em linha empurram as decisões para fora da área visível e pedem controle granular. | Etapas recolhidas por padrão, expansão por resposta, páginas de até 12 etapas, estado e erro com texto. A prosa da resposta fica sempre visível. |
| [Claude Code #75900](https://github.com/anthropics/claude-code/issues/75900) | O relato mostra por que a narração publicada pelo agente importa durante uma execução. | O texto recebido em streaming fica legível abaixo da atividade. A tela usa passos observáveis e o texto que o provedor publica na conversa. |
| [Blocos de código no ChatGPT](https://help.openai.com/en/articles/20001246-working-with-writing-blocks-and-code-blocks-in-chatgpt) e [feedback sobre copiar Markdown](https://community.openai.com/t/bad-chatgpt-s-markdown-in-copy-paste-working-solution/1405669) | O bloco de código tem contexto e ação próprios. Uma resposta exportada perde valor quando copiar destrói sua estrutura. | Código com linguagem, rolagem horizontal e cópia própria. A ação da resposta copia o Markdown original. |
| [Feedback sobre rolagem no Android](https://community.openai.com/t/android-app-scrolling-jumps-to-the-beginning-of-long-conversations/1418265) e [posição de leitura](https://community.openai.com/t/android-remember-last-read-position-in-long-chats/1412098) | Saltos interrompem a leitura de mensagens antigas. | A conversa acompanha a última mensagem enquanto a pessoa está no fim. Ao subir, o botão de retorno oferece um salto explícito. |

- **Tamanho.** Expandir ocupa a área da IDE. Reduzir e Voltar devolvem o compacto. Fechar libera o editor. Uma única instância de chat conserva o rascunho, o histórico e a execução ao mudar de tamanho.
- **Conversa.** A mensagem da pessoa usa um balão tonal. A resposta do Lunet aproveita a largura de leitura e renderiza títulos, listas numeradas e com marcadores, ênfase, código em linha, citações, tabelas e links web. O compositor agrupa Estudar, Fazer, anexar e enviar ou parar. A conversa expandida usa coluna de leitura de até 780 dp.
- **Atividade.** O histórico usa os eventos observáveis que a sessão já salva. Históricos anteriores sem etapas exibem a resposta normalmente. O progresso em execução é atualizado em um único bloco a cada 100 ms no máximo, com o detalhamento visual atualizado somente quando mudam as etapas.
- **Revisão.** Em Fazer, a conversa expandida permanece aberta ao concluir. **Ver no editor** leva à revisão no arquivo. No compacto, a revisão abre no arquivo ao concluir, conforme o fluxo da IDE. As alterações também podem ser inspecionadas na conversa e desfeitas pelo checkpoint.
- **Capturas.** `Lunet --screens` inclui compacto, expandido e etapas abertas. O ambiente headless usa uma conversa de demonstração e renderiza os controles reais da casca. `LUNET_SCREENS_ALL=1` acrescenta 320 dp, 360 dp e paisagem.
