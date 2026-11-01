# Runtime de agentes compartilhado — arquitetura e governança

## Fonte única, produtos independentes

O laço de execução, os contratos de agentes, tools/capabilities, escopos,
permissões, orçamento, eventos, retomada, revisão e providers **não** devem ser
reimplementados por produto. A fonte canônica atual está em
`apps/ecosystem-ai/src/AgentRuntime.Core` e
`apps/ecosystem-ai/src/AgentRuntime.Providers.ChatCompletions`.

O Lunet compila a projeção dos mesmos arquivos em
`apps/lunet2d/src/Lunet.Ai/Generated`. Não é um fork: o comando
`python3 platform/agent-runtime/projection.py --sync` gera a cópia, e a
verificação sem `--sync` falha se houver divergência byte a byte.
Esse mecanismo existe para manter build Android autocontido sem criar
referência direta entre projetos de produtos, bloqueada pela governança.

## Fronteiras e dependências

- **Core independente**: não conhece notas, vault, sprites, jogos ou UI.
- **Host de cada produto**: declara `ContextPath`, agentes, concessões,
  provider, histórico local, verificação e ferramentas autorizadas.
- **Ferramentas especializadas**: vivem no produto. Operam somente nos
  recursos do projeto e exigem concessões explícitas. Uma ferramenta
  que serve a vários produtos deve ser promovida ao runtime canônico,
  após testes de contrato, e não copiada entre aplicativos.
- **Dados separados**: mesma implementação não significa compartilhar
  conversas, arquivos, memórias, segredos, tokens ou permissões entre apps.
- **Evolução segura**: testes do runtime canônico, testes do adaptador,
  verificação da projeção e builds dos consumidores existentes devem
  passar antes de integrar alterações.

## Restrição explícita: Urbe intocável

Nesta iniciativa o proprietário **não autorizou modificar nenhum arquivo
ou comportamento do Urbe**. Não editar `apps/urbe/**`, suas configurações
nem seus workflows. Auditoria de código, caso necessária, é somente
leitura. A existência do núcleo compartilhado não autoriza migração,
refatoração ou alteração retroativa de um consumidor legado.

Como o Urbe ainda conserva implementação própria de partes da IA, a
**unificação funcional completa permanece bloqueada** até uma nova
autorização expressa. Fora dessa fronteira, é possível consolidar o
runtime, provar contratos e expandir as especializações do Lunet.

## Critérios para declarar unificação completa

1. Um único código canônico para cada mecanismo genérico.
2. Projeções/referências geradas e auditadas, sem alterações manuais.
3. Todos os consumidores usam os mecanismos compartilhados relevantes.
4. Conversas e segredos permanecem isolados por app/projeto.
5. Testes de compatibilidade, integração Android e regressão aprovados.

A conclusão do item 3 depende de trabalho no consumidor legado e não
pode ser declarada enquanto a restrição do proprietário existir.
