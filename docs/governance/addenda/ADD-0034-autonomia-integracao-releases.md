# ADD-0034 — Autonomia por escopo, integração sem label e release por mudança

**Data:** 15/11/2026. **Autoridade:** ordem explícita do proprietário nesta conversa. **Registro:** DEC-0063. **Escopo:** todos os Products e agentes do Ecosystem. Substitui as exigências de aprovação por label de ADD-0012/ADR-0015, MANIFEST §22–§24/§27 e instruções derivadas; preserva testes, isolamento, rastreabilidade e segurança.

## Decisão do proprietário

- PR fora de rascunho + handoff de execução pronta declara que o agente terminou. Todos os testes automáticos aplicáveis no estado combinado com a main atual devem passar; integra automaticamente, sem a label `integrar`. Conflitos e testes falhos exigem correção, não aprovação humana.
- Mudança no código de qualquer app exige nova release daquele app após integração. Dependência compartilhada alterada inclui seus consumidores reais. Documentação e testes isolados não inventam alteração no binário. Falha de publicação fica pendente numa fila persistente e é tentada novamente; dispatch não é prova de release publicada.
- O estágio da versão (alpha/alfa, beta, dev ou estável) é exclusivamente do usuário. Agentes e automações apenas incrementam o número na autoridade canônica de cada app, preservando estágio, identidade, assinatura e dados. Um estágio já declarado continua até nova decisão do proprietário. Releases repetidas são imutáveis e um retry mantém a mesma versão. Os contadores Android também têm autoridade canônica e não dependem do número de execução do workflow.
- Aprovações/decisões pendentes bloqueiam somente a ação que realmente necessita delas. O agente registra motivo, impacto, proposta e próxima tarefa independente autorizada. Nunca interpreta silêncio como consentimento e nunca alega execução contínua sem executor real.
- Permissões são concedidas pelo usuário por agente e componente. Níveis: leitura; implementação no escopo aprovado; autonomia de produto; máxima dentro do produto. Máxima inclui decisões de produto e implementação no componente concedido, sem novas confirmações rotineiras. Não inclui alterar o estágio, governança do Ecosystem, própria concessão, outros Products ou fingir evidência humana.
- **Claude: autorização máxima no Urbe**, por concessão expressa do proprietário. Essa concessão não autoriza modificar o Ecosystem. O nome informado num handoff não autentica a pessoa nem pode conceder privilégio a si próprio. A concessão é autoridade de processo; credenciais GitHub compartilhadas não distinguem executores e não fornecem isolamento técnico por fornecedor.
- Regra que prejudica progresso: identificar a cláusula, apresentar caso concreto e mudança sugerida ao usuário, persistir a solicitação canônica e continuar trabalho independente. Nunca neutralizar checker, permissões, CI, manifesto ou workflow como expediente para produzir.

## Fronteira de governança

Autonomia de produto não autoriza mudar a constituição, mecanismos de segurança, integração, concessões ou regras globais. Essas mudanças dependem de decisão explícita do proprietário. O fluxo técnico passa a usar review APPROVED do proprietário vinculado ao head do PR, não uma label. Testes continuam obrigatórios inclusive nesse caso. Esta alteração de governança foi solicitada expressamente pelo proprietário; o registro é desta decisão, não uma autorização geral para agentes se autoalterarem.

## Critérios de aceitação

1. Product verde integra sem label, inclusive código de persistência/credenciais/distribuição dentro da direção autorizada.
2. Checker/política do PR não decidem sua própria autoridade; definição e configuração confiáveis vêm da main.
3. O commit publicado corresponde ao estado combinado testado; corridas de head/main/ref causam reavaliação.
4. Versões são preparadas antes dos testes, cada app mantém estágio e autoridade próprios; retries usam a origem imutável.
5. CI vermelho ou ausente para Product tocado impede integração; validação DEVICE continua distinguida de CI.
6. Uma decisão pendente não paralisa componentes ou tarefas independentes.
