# ADR-0048 — Urbe: cliente MCP para ferramentas externas opcionais

## Status

Aceito — DEC-0061, alternativa C (14/11/2026): servidores remotos no celular e no computador, e servidores locais no computador.

Primeiro incremento (15/11/2026, beta.56): cliente remoto (Streamable HTTP) com o SDK oficial `ModelContextProtocol.Core` 2.4.0 (Apache-2.0), no Android e no Windows. Cada servidor é cadastrado pela pessoa (endereço, nome, chave opcional); as ferramentas descobertas começam desligadas e são liberadas uma a uma como leitura (como a pesquisa web, sem aprovação no modo "perguntar") ou escrita (aprovação a cada uso). Elas entram no mesmo catálogo, nas mesmas políticas por agente e nas mesmas aprovações; a resposta de um servidor chega marcada como dado de terceiros, não instrução. Falta desta decisão: servidores locais (stdio) no computador, e validação no aparelho.

## Contexto

O plano Numa 2.6 (CST-215) pede avaliar e implementar um cliente do Model Context Protocol (MCP) em C#. Com ele, o Assistente usaria ferramentas, recursos e prompts de servidores externos, com descoberta sob demanda, permissões por ferramenta e servidor, limites, cancelamento, validação de esquemas e revogação. O plano também diz que MCP é opcional: não substitui ferramentas locais, não cria um segundo catálogo de permissões e não expõe arquivos, rede ou credenciais só porque um servidor foi instalado.

Hoje o Assistente só chama ferramentas próprias do Urbe (notas, documentos, cálculo, dados, skills, pesquisa web), todas com a política de aprovação atual.

## Problema

Ligar o Urbe a servidores MCP abre uma fronteira de confiança nova:
- conteúdo e instruções vindos de um processo ou serviço de terceiros;
- tráfego de rede para destinos escolhidos pela pessoa;
- credenciais (tokens OAuth ou chaves) guardadas pelo app;
- ferramentas que podem alterar dados fora do vault.

O NN-011 e o NN-016 exigem decisão explícita antes de mudar uma fronteira de confiança e um modelo de permissões. O plano (§1.4) exige ADR quando uma persistência ou biblioteca nova afeta o contrato durável ou a fronteira de confiança.

## Opções

1. **Não implementar agora.** O Urbe continua só com as ferramentas próprias, e as skills cobrem instruções especializadas.
2. **Cliente MCP só por HTTP (Streamable HTTP) e só no Windows/desktop.** Usa o SDK oficial `ModelContextProtocol` (C#, MIT). Cada servidor é cadastrado pela pessoa com a URL e uma permissão por ferramenta (leitura, escrita ou ação). Cada chamada passa pela aprovação atual. Não há stdio no Android, onde processos locais não são viáveis.
3. **Cliente MCP por HTTP no Android e no Windows, mais stdio no Windows.** Cobre servidores locais no computador e remotos no celular, com os mesmos grants. Tem custo maior de empacotamento e de segurança, porque stdio executa um programa local.
4. **Implementar um cliente próprio, sem o SDK.** Evita a dependência, mas reimplementa o protocolo e as atualizações dele.

## Decisão

Pendente (DEC-0061). Recomendação técnica: a opção 2 como primeiro incremento.
- Usar o SDK oficial, fixando a versão.
- Os servidores ficam na Personalização, nunca no vault de notas.
- Nenhuma ferramenta MCP é chamada sem grant da pessoa.
- O conteúdo devolvido é tratado como não confiável, como as páginas da web hoje.
- Desconectar ou revogar um servidor não afeta a escrita do vault.

## Consequências

- **Se aceito (opção 2 ou 3):**
  - uma dependência nova (SDK MCP) e uma tela de servidores com grants;
  - testes de desconexão, timeout, revogação e de esquemas inválidos;
  - tokens só no armazenamento seguro da plataforma.
- **Se rejeitado:** CST-215 fica registrado como "não adotado" no plano, com a razão.

## Alternativas rejeitadas

Ainda não há alternativas rejeitadas: todas estão em aberto até a DEC-0061.

## Referências

- Plano Numa 2.6, CST-215, CST-072, CST-084 e CST-214.
- NN-011 e NN-016; MANIFEST §22.
- ADR-0033 (IA unificada) e ADD-0027.
- SDK: https://github.com/modelcontextprotocol/csharp-sdk
