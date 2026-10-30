# Urbe 2.0.0-beta.37 — consolidação (levas 1 a 9)

Data: 30 de outubro de 2026
Tag prevista: `urbe-csharp-v2.0.0-beta.37`
Canal: beta nativo (`app.urbe.csharp`, mesmo certificado da beta.36; instala por cima)
Base: a beta.36 (selos de autoria) mais as levas 1 a 9 da consolidação Numa (PRs #847, #853, #855, #857 e #861).
Andamento item a item: `apps/urbe/docs/csharp/CONSOLIDATION.md`.

## Notas, editor e arquivos

- **Tags do projeto:** as tags da nota da pasta (`Droerni/Droerni.md`) valem para tudo dentro da pasta. Nas propriedades elas aparecem em **herdadas**, com a origem; o × recusa uma tag para aquela nota.
- **Links que acompanham:** renomear ou mover uma nota, um arquivo ou uma pasta atualiza os `[[links]]`, as incorporações e os links Markdown `[texto](Nota.md)` nas outras notas.
- **Links e incorporações para qualquer arquivo:** `[[…]]` e `![[…]]` sugerem imagens, PDF, áudio, código, páginas e grafos, não só notas. Links para título e trecho (`[[Nota#Título]]`) também funcionam.
- **Barra de formatação pelo toque:** em Personalização → Editor você escolhe os botões e a ordem.
- **Horário de cada bloco**, opcional, em Personalização → Editor.
- **Código com cores** e prévia HTML ao vivo ao lado do código.
- **Gráficos de funções** com parâmetros que você arrasta.
- **Sem negrito ou itálico** onde ninguém os escreveu. A saída do editor ficou mais rápida.
- **Gravação segura:**
  - Uma falha do armazenamento mostra o erro com "Tentar de novo", sem fingir que salvou.
  - Uma gravação cortada pelo app fechando é recuperada na abertura seguinte.
  - Uma nota alterada fora do Urbe não é sobrescrita.

## Captura rápida e voz

- Toda nota rápida vira um item de checklist na nota do mês, na seção do dia, com o horário.
- Gravação rápida sem pressa e gravações seguidas sem esperar a anterior.
- Transcrição de áudio longo, como uma aula, em uma nota.

## Assistente e agentes

- **Agentes:**
  - Instruções por blocos já na criação.
  - Idioma do sistema como padrão.
  - Guia do Urbe consultável pelo agente.
  - Conversas anteriores consultáveis.
- **Resposta final** que diz o que foi feito; a revisão fica interna.
- **Consulta a especialistas:** perguntas, formulários e aprovações do especialista aparecem na conversa em que você está.
- **Permissões:**
  - Mudar para uma permissão mais restrita vale na hora.
  - Um especialista em automático pede aprovação quando quem pediu a consulta também pede.
  - Mudar `Personalização/AGENTS.md` sempre pede a sua aprovação.
- **Internet:**
  - Uma página não consegue fazer o agente enviar trechos das suas notas.
  - Um endereço que você guardou numa nota abre normalmente.
- **Ordem das instruções:** quando duas divergem, vale a mais específica. Segurança e aprovações valem sempre.
- **Chat:**
  - O chat não trava ao expandir execuções longas.
  - A tela acompanha o que acontece sem fechar e reabrir.
  - Perguntas mostram o quanto o agente já entendeu.
  - O que você estava escrevendo para cada agente, com os anexos, volta mesmo depois de o Android encerrar o app.
  - Até 20 anexos por mensagem (antes, 4). Fotos grandes ou deitadas vão de pé e reduzidas, em vez de recusadas.
- **Métrica de cada resposta:** tempo, chamadas e onde o tempo foi gasto.

## Navegação

- Menu radial no celular.
- O Voltar do Android fecha primeiro o que está aberto e percorre as abas antes de sair.

## Compatibilidade

- A identidade e a assinatura beta não mudam: instala por cima da beta.36. As notas continuam na pasta que você escolheu.
- Faça backup antes de atualizar.
- Nada disto foi validado no aparelho ainda (NN-017). A lista do que conferir no moto g32 está nos handoffs das levas.
