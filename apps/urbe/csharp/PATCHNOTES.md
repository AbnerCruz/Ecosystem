# Urbe — Notas da versão atual

**Versão canônica preparada:** 2.0.0-beta.41
**Canal:** Urbe 2.0 nativo, Android e Windows
**Publicação:** somente pelo workflow oficial após CI e integração autorizada
**Identidade Android:** app.urbe.csharp, mesmo certificado da beta.40; atualização direta quando a release oficial existir

## Planejamento em camadas — correção do defeito reportado

- Corrigido o rebaixamento silencioso do plano estruturado para o modo simples quando a IA omite goal durante as atualizações. O contexto inicial permanece vinculado à mesma execução de agente, sem vazamento entre chats.
- Tentativas de enviar objetivos, IDs, dependências ou revisões finais sem contexto válido não podem mais contornar o validador do grafo: a ferramenta informa o erro.
- Textos como "Ainda pendente" e "A conferir" não são aceitos como prova declarada de nó concluído. As provas continuam declarações textuais; a ferramenta NÃO autentica testes externos ou verificação por outro agente.
- Preservados os testes e critérios, dependências, revisões globais e o caminho simples para pedidos que não ativaram o planejamento.

## Editor — abas mais compactas

- Cápsulas de abas de 40dp de altura, títulos até 124dp, tipografia de 12dp, cantos e espaçamentos reduzidos.
- A área principal de toque mantém ao menos 48dp, sem modificar a semântica de substituição da aba atual e criação explícita pelo +.
- Adicionada regressão headless para densidade visual, uso em 320dp e controle do botão de nova aba.

## Paridade e limitações de consolidação

- Auditoria contrastada em `apps/urbe/docs/csharp/PARIDADE-CONSOLIDACAO-BETA41.md` com o plano normativo Numa 2.5 e referências open source.
- Os bloqueadores CST-016 (teclado/foco), CST-142 (equações), CST-143 (menu radial) e CST-144 (perda de texto) continuam ABERTOS até validação física reproduzível; testes headless da beta.39/40 não substituem aceitação no moto g32.
- Publicações: HTML de três capítulos foi testado pelo usuário; PDF, edição de fontes vivas e formatação final ainda precisam de testes no aparelho. Não foram anunciados nesta beta como novos recursos.
- Não há executor de grafos durável, recibos de verificação independentes ou retomada automática após morte do processo.
