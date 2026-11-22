# Aprender — primeira trilha completa

A trilha **T01 — Primeiros jogos e orientação no Lunet** tem cinco cursos, com dois desafios cada, **10 projetos** disponíveis: Aprendendo movimento, Caça ao alvo, Fuja da borda, Desafio de três vidas, Colecionador cronometrado, Corrida de recordes, Labirinto de dedo, Arena com dois controles, Mini-arcade de um minuto e Microjogo autoral.

Os demais 55 cursos ficam marcados **Disponível em breve**. O catálogo exibe os 60 cursos em 12 trilhas com carregamento incremental.

## Aprender construindo

1. Na Home, toque **Aprender**. Na trilha inicial, abra um dos cinco cursos disponíveis e escolha o primeiro ou segundo projeto.
2. Leia a **missão**, os quatro **objetivos**, **Pesquisar** (referências à documentação offline do Framework), **Critérios de aceite** e **Desafio extra**. Nada exige conexão.
3. Toque **Criar projeto para este desafio**. Será criada uma pasta Lunet normal com `lunet.json` e um `Game.cs` inicial em branco; o jogo não é entregue resolvido. O desafio `LRN-004-P2` abre em orientação horizontal para dois controles.
4. No Editor, programe seu jogo em C# e use Preview/console para verificar cada comportamento. Se quiser, volte a Aprender e use **Pedir uma pista da IA**; a solicitação padrão é conceitual, não autoriza editar os arquivos.
5. Ao observar uma etapa funcionando, marque-a. O progresso de cada missão é uma **declaração pessoal**, não um teste automático de jogabilidade, autoria ou domínio. Desmarcar continua possível.
6. Toque **Continuar no editor** para retomar seu próprio Game.cs. O jogo continua visível em Projetos mesmo após completar o curso; você pode associar qualquer projeto existente à missão sem copiá-lo ou reescrevê-lo.
7. Após concluir quatro objetivos, abra a segunda missão do curso. O segundo projeto transfere conhecimentos e exige nova solução, não copiar código. No LRN-005-P2, desenvolva um microjogo de uma tela com regra própria.

### Acompanhamento e recuperação

- A aba Aprender mostra o total de **40 objetivos declarados** da trilha (0–100%) e o próximo projeto incompleto; cada curso mostra 0–8 objetivos e cada missão 0–4.
- Cada missão mantém associação por ID estável e `GameId`, sem interferir em outras. Fechar/reabrir preserva marcações; remover o jogo deixa o desafio disponível para nova associação.
- O progresso antigo da versão com apenas LRN-001-P1 é migrado de V1 para V2 na próxima gravação; nenhum objetivo anterior ou vínculo válido é apagado. Arquivos inválidos são preservados e geram erro, não reset silencioso.
- Aprender funciona sem conta, sem rede e sem IA. Imagens, sons e Studios são opcionais nesta trilha.

## IA da área de trabalho

Na Home, toque **Lunet IA**: chat geral para planejamento e estudo, sem acesso aos arquivos do jogo. A engrenagem nessa tela permite configurar provedor, modelo, chave e permissões. No editor de um jogo, o chat contextual utiliza conversa separada por `GameId`, mostra troca de modelo, mas não permite mudar configurações globais. Credenciais permanecem no Keystore. Pistas da IA são opcionais.

## Roteiro de verificação de release

1. Abra Aprender e confira 5 cursos disponíveis na primeira trilha e 55 cursos futuros; navegue e volte à Home.
2. Abra **LRN-001-P1** e **LRN-001-P2**. Cada um deve ter quatro objetivos, referências e rubrica diferentes. Crie projetos separados.
3. Marque uma etapa em cada um; feche e reabra. Confira 2/40, 1/4 em cada missão, sem interferência entre jogos.
4. Crie projeto em **LRN-004-P2**; confirme orientação horizontal no `lunet.json` e Preview. Abra todos os dez desafios, incluindo **LRN-005-P2**.
5. Associe projeto antigo ao primeiro desafio; verifique que `Game.cs` permanece intacto. Marque/desmarque e confira 40 etapas independentes.
6. Teste sem rede; catálogo e progresso continuam funcionais. Abra a IA por demanda, se configurada, e confira que ajuda não substitui o jogo do aluno.
7. Execute pelo menos um dos desafios no Preview (desenhe uma forma e teste entrada), feche o jogo e retorne pelo launcher.
8. Se já possuía progresso na versão anterior, confirme que suas etapas e o vínculo do primeiro desafio continuam presentes.

O roteiro é para teste reproduzível no Android 13, não substitui CI e não implica que testes físicos tenham sido realizados pelo agente.
