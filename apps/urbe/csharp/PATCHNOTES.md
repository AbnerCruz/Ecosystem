# Urbe — Notas da versão atual

**Versão canônica preparada:** 2.0.0-beta.60
**Canal:** Urbe 2.0 nativo, Android e Windows
**Publicação:** pelo workflow oficial depois da integração.
**Atualização:** instala por cima da beta anterior (app.urbe.csharp, mesmo certificado).

## Etapas da IA

- **Painel de etapas.** Tocar na atividade do Assistente abre um painel que sobe da parte de baixo, com um X sempre visível no topo. Fechar o painel não interrompe a IA.
- **Sem travar com muitas etapas.** O painel desenha só as etapas mais recentes, em ordem; "Ver anteriores" e "Ver mais recentes" andam pelo histórico, que fica inteiro, sem desenhar tudo de uma vez.
- **O que o modelo está fazendo, à vista.** Enquanto a IA trabalha, o resumo de raciocínio que o modelo envia aparece no cartão da atividade e em cada etapa, sem precisar abrir nada.

## Abertura

- **Volta onde você estava só na mesma sessão.** Ao trocar de app ou voltar depois de pouco tempo, o Urbe continua na nota e na conversa em que você estava. Depois de uma pausa longa, abre na Área de trabalho e o Assistente começa uma conversa nova; a nota, os rascunhos e as conversas continuam salvos.
- **Você escolhe a pausa.** Personalização → Editor → "Ao voltar ao Urbe": 5 minutos, 30 minutos (padrão), 2 horas, 1 dia ou sempre reabrir onde parou. Uma execução da IA em andamento mantém a sessão.

## Não testado no aparelho

- Testado com testes automáticos (painel com 300 etapas, paginação, X, pausa curta e longa, reinício). O fechamento relatado no teste de motion graphics não foi reproduzido: falta o Relatório de erros do aparelho.
