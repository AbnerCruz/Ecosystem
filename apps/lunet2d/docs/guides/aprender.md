# Aprender — primeira missão

**Escopo previsto em LUNET-520/521:** aba Aprender (60 cursos planejados), somente o projeto inicial LRN-001-P1 disponível; IA geral na Home. Não declarar esta função disponível numa release anterior ao merge/CI.

## Fazer o primeiro jogo

1. Na tela inicial toque **Aprender** (quinto destino da barra).
2. Entre em **LRN-001 · Aprendendo movimento**, o primeiro curso. Os outros 59 aparecem **Disponível em breve**. O segundo projeto desse curso também está em breve.
3. Toque **Criar projeto para o desafio**. O Lunet cria um jogo normal com Game.cs inicial, sem resolver o desafio por você.
4. Volte a Aprender e abra **Aprendendo movimento** quando precisar revisar objetivos. São quatro etapas: desenhar quadrado, movê-lo por toque, desenhar círculo distante e vencer ao encostar com opção de reiniciar.
5. No editor, escreva o código do jogo, execute no Preview e depure. Para ajuda, na tela do desafio toque **Pedir ajuda da IA (pistas)** após configurar a IA na Home; o pedido inicial é explicativo e não solicita mudanças de arquivos.
6. Depois de testar um objetivo, marque sua caixa. O valor mostra quantos dos quatro objetivos você declarou concluídos; **não é verificação automática do jogo**. Pode desmarcar e rever.
7. Feche e reabra o aplicativo; o progresso fica no armazenamento privado da aplicação, separado do projeto. O mesmo jogo pode ser aberto pelos Projetos.

Também é possível associar um jogo existente, sem copiá-lo nem apagá-lo. Se o vínculo apontar para jogo excluído ou com GameId diferente, o aplicativo pedirá nova associação.

## IA da área de trabalho

Na Home, toque **Lunet IA**. É o chat geral para planejamento, estudo, ideação e dúvidas sobre o Lunet. No ícone de engrenagem dessa tela ficam configuração completa do provedor, modelo, chave, permissões de arquivo, pesquisa web e demais opções. O chat geral **não recebe arquivos nem ferramentas de projeto**; não pode afirmar que inspecionou ou alterou um jogo.

No editor de um projeto, abra o chat contextual. Ele guarda histórico separado por GameId e mantém as ferramentas de projeto existentes, sujeitas a permissões. Para alterar somente o modelo, toque no identificador do modelo ou use **Conversa → Trocar modelo**. Não há acesso à configuração completa de credenciais a partir do chat do projeto.

As credenciais continuam no Android Keystore; histórico geral fica separado do histórico dos projetos. Se ainda não configurou a IA, retorne à Home → Lunet IA → engrenagem.

## Comportamentos importantes

- Aprender e chat geral são duas superfícies; nenhuma substitui Editor, Preview, Studios ou Projetos.
- A barra da Home tem cinco destinos; IA geral é uma ação de Home, não uma sexta aba.
- Sem internet e sem chave, cursos, enunciados, jogo e progresso continuam utilizáveis; apenas respostas de IA online exigem provedor configurado.
- “Disponível em breve” não cria projeto nem oferece botão de início falso.
- Não existe certificado nem avaliação pedagógica automática no primeiro incremento.
- O curso LRN-001 não é considerado inteiro implementado enquanto LRN-001-P2 não estiver lançado.
- Corrupção do progresso/chat é informada sem sobrescrever o arquivo original.

## Roteiro de verificação após CI

1. Abra a Home, navegue pelos cinco destinos, feche o app e confira que Aprender reabre corretamente.
2. Abra Aprender e percorra os 60 títulos com Mostrar mais. Tente tocar num curso futuro; confira o aviso.
3. No primeiro curso, crie projeto e altere Game.cs. Volte; confira que o progresso é separado e o jogo não foi substituído.
4. Marque duas etapas, feche e reabra; confira 50%. Desmarque uma e confira 25%.
5. Associe projeto existente, depois volte e confira acesso ao mesmo GameId.
6. Abra Lunet IA na Home sem projeto, configure chave, envie pergunta e verifique resposta; reinicie e confira histórico. Nova conversa apaga só o chat geral.
7. Abra dois projetos diferentes e confira seus históricos distintos. O menu de projeto permite trocar modelo mas não alterar chave/permissões.
8. Com IA indisponível/erro de rede, o enunciado e o progresso do desafio continuam funcionando.

Esses passos são orientação para reproduzir defeitos, não gate manual obrigatório: a governança ADD-0035 dispensa validação em aparelho para integração, mas exige relatório honesto de verificações realmente executadas.
