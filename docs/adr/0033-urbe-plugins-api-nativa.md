# ADR-0033 — Urbe C#: plugins por uma API nativa com permissões

## Status

Proposto — direção decidida pelo proprietário em **DEC-0044** ([ADD-0021](../governance/addenda/ADD-0021-urbe-plugins-api-nativa.md)); o desenho abaixo ainda **não** foi aceito e nada deve ser implementado antes disso (NN-011).

## Contexto

FATO: na 1.8.4, `src/customize/plugins.js` executa JavaScript do usuário com acesso total ao app. O cliente nativo (ADR-0032) é C# sem WebView e não roda JavaScript.

FATO: a Personalização nativa já lista, aprova (SHA-256, guardado no aparelho), cria e liga/desliga plugins, e avisa que não os executa.

FATO: o proprietário escolheu a API nativa de plugins em C# com permissões (DEC-0044, opção 2).

## Problema

Definir como um plugin nativo é escrito, carregado, autorizado e isolado, sem dar acesso irrestrito ao app nem ao vault (NN-016), sem acoplar o Urbe a hosts concretos (NN-007) e sem criar abstração compartilhada sem consumidor (NN-022).

## Questões em aberto (a decidir antes de aceitar)

1. **Formato e carga:** assembly .NET (DLL) carregado em tempo de execução, ou plugins declarativos (manifesto + ações predefinidas)? No Android, o carregamento dinâmico de código tem restrições de loja e de plataforma que precisam ser provadas.
2. **Superfície da API:** quais pontos de extensão existem (comandos da paleta, ferramentas da barra, painéis, transformações de nota, atalhos, textos de interface) e quais não existem.
3. **Modelo de permissões:** granularidade (ler notas, escrever notas, rede, pasta fora do vault), como o usuário concede, onde a concessão é guardada e como é revogada.
4. **Isolamento:** processo, `AssemblyLoadContext` ou nenhum; o que acontece com um plugin que trava ou consome CPU.
5. **Contrato (NN-006):** ID estável, versão, inputs/outputs, erros, permissões, lifecycle e política de compatibilidade da API.
6. **Distribuição e confiança:** origem do plugin, assinatura/hash (a aprovação por SHA-256 já existe) e o que muda para NN-023.
7. **Migração:** se haverá conversão assistida de plugins `.js` simples e qual o escopo.

## Opções

| Opção | Resultado |
|---|---|
| Só gerenciamento (sem executar) | Comportamento atual; rejeitada pelo proprietário como destino. |
| **API nativa em C# com permissões** | **Escolhida em DEC-0044.** |
| Motor de script isolado para os `.js` da 1.8.4 | Rejeitada: traz um motor JS ao app, com custo de segurança, tamanho e manutenção (NN-020/NN-022). |

## Decisão

Pendente de aceite. Direção: API nativa em C#, com permissões declaradas e deny-by-default. O desenho detalhado nasce das questões acima, em itens próprios, começando por uma prova mínima (um ponto de extensão, uma permissão) antes de uma API ampla.

## Consequências

- Nenhum código de plugin é escrito até este ADR ser aceito.
- Plugins `.js` da 1.8.4 não rodam no nativo.
- A API vive em `apps/urbe` (local-first, ADR-0011); só vira componente compartilhado com segundo consumidor real (NN-022).
- Validação em aparelho continua obrigatória para qualquer carga de código no Android (NN-017).

## Alternativas rejeitadas

- Motor de script isolado para os `.js` (ver tabela).
- Manter só o gerenciamento como destino final.

## Referências

DEC-0044; ADD-0021; ADR-0032; `apps/urbe/src/customize/plugins.js`; MANIFEST NN-006, NN-007, NN-011, NN-016, NN-017, NN-020, NN-022, NN-023.
