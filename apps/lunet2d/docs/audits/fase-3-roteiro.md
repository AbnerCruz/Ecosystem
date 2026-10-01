# Fase 3 — Roteiro de teste no celular

Este roteiro valida a IDE do Lunet. Cada passo tem um código (ex.: **C3**). Ao terminar, responda com uma linha por código: **passou**, **falhou** (com o que aconteceu) ou **não testei**. Pode responder em blocos, por exemplo: "A1–A4 passaram; C3 falhou: o teclado cobriu o botão".

## Antes de começar

1. Baixe o APK da release candidata (o link é enviado junto com este roteiro) e instale por cima do anterior. Seus projetos ficam guardados no aparelho e não são apagados ao atualizar.
2. **Faça um backup antes:** na lista de projetos, segure o dedo sobre o projeto e escolha exportar ZIP.
3. Crie um projeto de teste chamado `TesteF3` no modelo **Coletor de moedas** (ele já tem muito código para brincar). Se preferir, use `Em branco` e cole código à vontade.
4. Se tiver teclado físico (Bluetooth/USB) e/ou controle de jogo, deixe por perto; esses passos são opcionais.
5. Para relatar uma falha, diga: código do passo, o que você esperava, o que aconteceu, modelo do aparelho e versão do Android. Uma captura de tela ajuda. Se o app fechar sozinho, anote o que estava fazendo.

## A. Regressão dos fluxos já aprovados (Fases 1 e 2)

- **A1** Abra `TesteF3`, toque em **▶ Run**. O jogo abre no Preview e responde ao toque.
- **A2** Toque em **■ Stop**. Você volta ao editor **com o código intacto** (nada some).
- **A3** Edite uma linha, feche o app pelos recentes, reabra e entre no projeto. A edição está lá.
- **A4** No menu **☰**, crie um arquivo e uma pasta; renomeie e apague.
- **A5** Na lista de projetos, segure o dedo sobre o projeto e exporte o ZIP.

## B. Editor

- **B1** Digite `var x = 1` sem o `;` no fim de um método. Aparece erro na aba **Problemas**; ao tocar nele o cursor vai para a linha.
- **B2** Digite `Con` dentro de um método: aparecem sugestões em botões acima do teclado. Toque numa e ela entra no texto.
- **B3** Encoste o cursor num nome (por exemplo `SpriteBatch`) e use **⋯ → Símbolo → Dica do símbolo**. Aparece a assinatura.
- **B4** **⋯ → Navegar → Ir para definição** num método seu leva até onde ele foi escrito.
- **B5** **⋯ → Navegar → Referências do símbolo** lista os usos; tocar leva ao local.
- **B6** **⋯ → Navegar → Localizar e substituir**: procure uma palavra, substitua todas, depois use desfazer (↶).
- **B7** Aperte ↶ e ↷ várias vezes: o texto volta e avança sem perder nada.
- **B8** Números de linha aparecem à esquerda; pressione Enter depois de `{` e a indentação aumenta sozinha.

## C. Ferramentas Roslyn

- **C1 Formatar:** bagunce a indentação de algumas linhas e use **⋯ → Arquivo → Formatar documento**. O código fica alinhado com 4 espaços, e o cursor continua no mesmo lugar. Aperte ↶: volta ao estado bagunçado de uma vez.
- **C2 Renomear:** cursor num campo seu (por exemplo `_score`), **⋯ → Símbolo → Renomear símbolo**, digite outro nome. Todas as ocorrências mudam, também em outros arquivos. Tente renomear para um nome que já existe: o Lunet **recusa** e explica. Tente renomear `Game`: recusa (é do framework).
- **C3 Correções rápidas:** escreva `var l = new List<int>();` sem ter `using System.Collections.Generic;`, cursor na linha, **⋯ → Símbolo → Correções rápidas**: aparece `using System.Collections.Generic;`; ao escolher, o erro some. Repita com um nome errado (`Scor` em vez de `Score`): aparece "Você quis dizer…".
- **C4 Estrutura do arquivo:** **⋯ → Navegar → Estrutura do arquivo** lista classes e membros; tocar num item leva até ele.
- **C5 Informações do símbolo:** **⋯ → Símbolo → Informações do símbolo** num tipo seu e num tipo do framework mostra acesso, tipo, herança e membros.

## D. Edição avançada

- **D1 Dobrar código:** toque na setinha (▾) ao lado do número de uma linha de método/classe. O trecho some e aparece `{ … }` ao lado; toque em `{ … }` ou na setinha (▸) para abrir. Editar o texto abre tudo.
- **D2 Vários cursores:** **⋯ → Edição → Selecionar próxima ocorrência** com o cursor numa palavra repetida. Repita para marcar outras ocorrências. Digite uma letra: a mudança acontece em todas. Aperte ↶: desfaz tudo de uma vez. Toque em outro lugar do texto para sair do modo.
- **D3** **⋯ → Edição → Adicionar cursor na linha abaixo/acima** e **Selecionar todas as ocorrências** funcionam.
- **D4 Linhas:** teste **Duplicar linha**, **Apagar linha**, **Mover linha para cima/baixo**, **Comentar/descomentar**, **Indentar** e **Recuar**.
- **D5 Minimapa:** a faixa fina à direita mostra o "formato" do arquivo; arrastar nela rola o texto.
- **D6 Teclado físico (opcional):** com um teclado conectado, **Ctrl+S** salva, **Ctrl+/** comenta, **Ctrl+D** duplica, **Alt+↑/↓** move a linha, **F2** renomeia. A lista completa está em **⋯ → Ferramentas → Atalhos de teclado**.

## E. Busca, painéis e configurações

- **E1 Busca no projeto:** **⋯ → Navegar → Buscar no projeto**, procure `Update`. A lista mostra arquivo e linha; tocar abre o arquivo no lugar certo. Teste "Palavra inteira" e "Diferenciar maiúsculas".
- **E2 Layout:** **⋯ → Ferramentas → Layout do workspace**. Esconda o painel, mude a posição para "À direita", ajuste o tamanho e toque em **Aplicar**. Arraste a barra entre o editor e o painel para redimensionar.
- **E3 Rotação:** gire o celular para paisagem: com a posição "Automática", o painel de Problemas/Console vai para a direita. Gire de volta.
- **E4 Layouts salvos:** salve o layout atual com um nome, troque para "Foco no código" e volte ao seu pelo **Carregar layout…**. Feche e reabra o app: o último layout continua.
- **E5 Configurações:** **⋯ → Ferramentas → Configurações** (também na lista de projetos): mude o tamanho da fonte, desligue números de linha e minimapa, salve. As mudanças aparecem e persistem depois de reabrir.
- **E6 Exportar logs:** **⋯ → Ferramentas → Exportar logs**: abre o compartilhamento do Android com o texto do Console e dos Problemas.

## F. Documentação offline

Ative o **modo avião** para este bloco.

- **F1** **⋯ → Ferramentas → Documentação**: abre a tela com guias e namespaces.
- **F2** Abra um guia (por exemplo "Primeiros passos"); o texto e os blocos de código estão legíveis.
- **F3** Digite `SpriteBatch` na busca e toque em **Search** no teclado. Abra o tipo, depois um método: aparecem assinatura, parâmetros, retorno e um exemplo de código. O texto digitado na busca é legível.
- **F4** Toque num nome no código e use **⋯ → Símbolo → Documentação do símbolo**: abre a página desse tipo/membro. O botão **Explicar na Documentação** da Dica do símbolo faz o mesmo.
- **F5** Botões ← e ⌂ navegam; o botão Voltar do Android também.

## G. Inspector e Preview

- **G1** Rode o jogo e toque em **🔍** no Preview. No Coletor de moedas aparecem `score`, `best` e `lives` (com barra de 0 a 5); os valores mudam sozinhos enquanto o jogo roda. (O Inspector mostra campos públicos ou marcados com `[Inspect]`; campos privados comuns não aparecem.)
- **G2** Altere um número (por exemplo a pontuação ou velocidade) no Inspector: o jogo reage na hora. Um `bool` vira caixa de marcar; um enum, uma lista para escolher.
- **G3** Cole este código num jogo em branco (cabeçalho `using Lunet;` no topo) para testar os atributos:
  ```csharp
  public sealed class Teste : Game
  {
      [Range(0, 10), Tooltip("De 0 a 10")] public float Volume = 5;
      [ReadOnly] public int Quadro;
      [Hidden] public int Segredo = 1;
      [Group("Combate")] public int Dano = 2;
      [Multiline(3)] public string Notas = "Olá";
      protected override void Update(GameTime time) => Quadro++;
      protected override void Draw(GameTime time) => GraphicsDevice.Clear(Lunet.Graphics.Color.CornflowerBlue);
  }
  ```
  No Inspector: `Volume` tem barra deslizante limitada a 0–10; `Quadro` sobe e não pode ser editado; `Segredo` não aparece; `Dano` fica sob o título "Combate".
- **G4 Status após Run:** edite só o corpo de um método e aperte Run: a barra de status diz que só corpos mudaram. Adicione um campo novo e aperte Run: diz que é preciso reiniciar e indica o motivo.
- **G5 Preview isolado:** em **Configurações**, ligue "Preview isolado" e rode: o jogo abre normalmente. Aperte o botão Voltar: você volta ao IDE, e o console mostra as mensagens do jogo. Desligue a opção depois.
- **G6** Com o Preview isolado ligado, escreva `while (true) { }` dentro de `Update` e rode. O aparelho pode ficar lento no jogo, mas o IDE não deve travar junto; feche o Preview pelo botão Voltar ou pelos recentes. Ao voltar, o IDE avisa se o Preview foi encerrado inesperadamente. Se o aparelho inteiro travar, relate, mas isso é um limite conhecido (ADR 0006).

## H. Git

- **H1** **⋯ → Ferramentas → Git** → **Iniciar repositório Git**. Aparece a aba **Alterações** com os arquivos.
- **H2** Toque em **Commit…**: na primeira vez pede seu nome e e-mail; escreva uma mensagem e confirme. O commit aparece em **Histórico**.
- **H3** Edite um arquivo e volte ao Git: ele aparece como modificado. Toque nele → **Ver diff**: linhas novas em verde, removidas em vermelho.
- **H4** Em **Ramos**, crie `experimento`, mude o código, faça commit, volte para o ramo `main` (o código volta ao anterior) e troque de novo para `experimento`.
- **H5** Com uma alteração não commitada, tente trocar de ramo: o Lunet **recusa** dizendo qual arquivo seria perdido.
- **H6** Em **Histórico**, toque num commit → **Ações…** → **Reverter este commit**: um novo commit desfaz a mudança.
- **H7 GitHub (só se quiser):** crie um repositório **vazio e de teste** no GitHub e um token com permissão de repositório. Em **Remoto**, informe o endereço e o token em **Editar conta…**, depois **Push**. Confira no site que os arquivos chegaram. Edite no site e faça **Pull** no app. **Nunca use um repositório importante para este teste.**
- **H8** Na lista de projetos, **Clonar do Git** com o repositório do H7 (ele precisa ter `lunet.json` na raiz, que o Push do H7 já inclui).

## I. Recuperação de alterações

- **I1** Digite algo no editor e, sem sair do arquivo, deslize o app fora dos recentes. Reabra e entre no projeto: o Lunet oferece **Recuperar**; a edição volta.
- **I2** **⋯ → Ferramentas → Recuperar alterações não salvas** quando não há nada pendente diz isso claramente.

## J. Medição do editor (nos ajuda a decidir sobre arquivos enormes)

- **J1** **⋯ → Ferramentas → Medir o editor com arquivos grandes**. Espere alguns segundos. Aparece uma janela com tempos para 2 mil, 10 mil e 40 mil linhas. Toque em **Compartilhar** e me mande o texto. Depois disso seu arquivo original deve estar de volta, intacto.

## Ao terminar

Responda os códigos. Quando todos os itens tiverem passado (ou os que falharem estiverem corrigidos e retestados), digite explicitamente que **aprova a Fase 3**. Sem essa aprovação a Fase 4 não começa.
