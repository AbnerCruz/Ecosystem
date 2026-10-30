# Urbe — Notas da versão atual

**Versão canônica preparada:** 2.0.0-beta.37
**Canal:** Urbe 2.0 nativo, Android e Windows
**Publicação:** exclusivamente pelo workflow oficial, após a integração e os gates
**Identidade Android:** app.urbe.csharp

> Este arquivo descreve a beta.37 preparada. Não confirma a publicação: confira se existe a tag `urbe-csharp-v2.0.0-beta.37` com o APK Android assinado e o ZIP Windows. O histórico fica em `PATCHNOTES-HISTORY.md`.

## Notas, editor e arquivos

- **Tags do projeto:** as tags da nota da pasta (`Droerni/Droerni.md`) valem para tudo dentro da pasta. Nas propriedades elas aparecem em **herdadas**, com a origem; o × recusa uma tag para aquela nota.
- **Links que acompanham:** renomear ou mover uma nota, um arquivo ou uma pasta atualiza os `[[links]]`, as incorporações e os links Markdown `[texto](Nota.md)` nas outras notas.
- **Links e incorporações para qualquer arquivo:** `[[…]]` e `![[…]]` sugerem imagens, PDF, áudio, código, páginas e grafos, não só notas. Links para título e trecho (`[[Nota#Título]]`) também funcionam.
- **Barra de formatação pelo toque:** em Personalização → Editor você escolhe os botões e a ordem.
- **Horário de cada bloco**, opcional, em Personalização → Editor.
- **Ícone nas tags:** em Personalização → Tags, cada `#Tag:` pode ter um ícone antes do nome, além da cor.
- **Compartilhar:** mande um arquivo ou uma nota para o WhatsApp, o e-mail ou o Drive, pelas ações do arquivo ou por Mais ações da nota.
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
  - Um agente criado a partir de um modelo (Escritor, Organizador) mantém as ferramentas do modelo; antes saía só de leitura.
- **Resposta final** que diz o que foi feito; a revisão fica interna.
- **Consulta a especialistas:** perguntas, formulários e aprovações do especialista aparecem na conversa em que você está.
- **Permissões:**
  - Mudar para uma permissão mais restrita vale na hora.
  - Um especialista em automático pede aprovação quando quem pediu a consulta também pede.
  - Mudar `Personalização/AGENTS.md` sempre pede a sua aprovação.
- **Internet:**
  - Pesquisas longas não param mais em 12 buscas e 20 páginas por resposta.
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
- **Memória:**
  - Cada lembrança diz se é privada do agente ou compartilhada, e se foi registrada pelo app, declarada pelo agente, dita por você ou verificada.
  - Há filtros para ver só as privadas ou só as compartilhadas.
- **Relatório de erros:** não guarda chaves, trechos de notas nem caminhos de pastas, e apaga erros com mais de 30 dias.
- **Mover para:** a busca diz quando não acha nada.
- **Ver e alcançar tudo:**
  - O cartão de aprovação mostra todas as linhas da mudança, não só 160.
  - Qualquer tag filtra a Área de trabalho ("+N tags").
  - "Abrir ao lado" alcança qualquer nota pela busca.
  - Modelos de nota perguntam até 128 campos, não só 32.
- **Consultas:** quem pediu fica sabendo quais notas o especialista mudou.
- **Excluir um agente** leva as lembranças privadas dele, e a confirmação diz quantas são.

## Navegação

- Textos secundários (dicas, datas, contagens) mais legíveis em todos os temas, no nível de contraste recomendado (WCAG AA).
- Botões pequenos demais para o dedo ficaram maiores: Edição/Leitura no editor, "Incluir a nota aberta" no chat, chips e opções das pastas.
- Menu radial no celular.
- O Voltar do Android fecha primeiro o que está aberto e percorre as abas antes de sair.

## Compatibilidade

- A identidade e a assinatura beta não mudam: instala por cima da beta.36. As notas continuam na pasta que você escolheu.
- Faça backup antes de atualizar.
- Nada disto foi validado no aparelho ainda (NN-017). A lista do que conferir no moto g32 está nos handoffs das levas.

**Detalhes:** `PATCHNOTES-beta.37.md` e `apps/urbe/docs/csharp/CONSOLIDATION.md`.
