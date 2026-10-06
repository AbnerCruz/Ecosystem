# UC-16 — investigação reproduzível de renderização C#

Escopo da Issue [#257](https://github.com/AbnerCruz/Ecosystem/issues/257), depois da primeira fatia integrada de edição. Fatos coletados em 2026-10-06; nenhuma biblioteca foi adotada no produto. Core/UI/Web/MAUI distribuídos não recebem referência a estes pacotes. O cliente JS continua distribuído até UC-31/G-C5.

## Experimento

`node csharp/tests/math-renderer-probe.mjs` cria projetos temporários, restaura versões exatas de pacotes, executa o renderizador real em .NET 10 e publica um experimento Blazor WASM. Chromium abre o publicado e renderiza outra fórmula depois de desligar a rede. Requer SDK .NET 10, dependências npm do Urbe e Chromium de Playwright. `--console-only` executa apenas a parte .NET. `URBE_DOTNET_EXE` pode indicar o executável do SDK.

A ferramenta conserva projetos, logs, SVGs/resultados e relatório no diretório temporário informado. Não substitui renderer por mock, não utiliza KaTeX, não adiciona projetos à solução e não integra UI M3. As fontes do experimento ficam em `csharp/tests/probes/math-renderer/`. O JS do framework Blazor corresponde à pilha já aceita pelo ADR-0025; nenhum interop com biblioteca matemática JS foi introduzido. O scanner legado é usado somente pelo tooling para extrair fórmulas do tutorial como corpus de investigação.

Entradas: 172 snippets do corpus suplementar da primeira fatia; sete fórmulas/erros/macros adicionais; 31 fórmulas reais extraídas de `tutorial/Matemática/Fórmulas em qualquer nota.md` e `Galeria de fórmulas.md`. O relatório registra SHA-256 dessas fontes e do corpus.

Relatório observado: [math-renderer-observations.json](math-renderer-observations.json); dados descritivos, sem comparação automática permissiva com rejeições conhecidas.

## Evidência observada

| Grupo | Resultado real |
|---|---|
| 172 snippets | 163 renderizados, 9 diagnósticos, nenhuma exceção |
| Sete entradas adicionais | Bhaskara, integral e matriz renderizados; `newcommand`, `def`, comando inexistente e fração incompleta diagnosticados |
| 31 fórmulas do tutorial | 30 renderizadas; `boxed` recusado |
| Export SVG | Fórmula de Bhaskara produz 16 paths; nenhum texto ou link externo. Corpus renderizado sem href externo |
| WebAssembly publicado | Renderização C# real e nova integral renderizada com rede desligada passaram em Chromium |
| Android/Windows instalados | Não executado; compatibilidade não é inferida do build do Core do Urbe, que não referencia esta biblioteca |

Não são goldens de paridade visual: layout, fidelidade tipográfica, acessibilidade e equivalência com o KaTeX real ainda não foram avaliados. São observações para escolher o caminho, não expectativa permissiva que transforma rejeições em sucesso. A prova Web é renderização após carregamento com rede desligada, não instalação PWA/cold start offline nem DEVICE. A publicação Blazor foi feita sem a otimização nativa opcional de `wasm-tools`.

## Dependências e licenças

Versões exatas executadas, confirmadas também no grafo restaurado e nuspec:

| Pacote | Versão | Licença declarada no pacote |
|---|---|---|
| CSharpMath | 1.0.0-pre.2 | MIT |
| CSharpMath.Rendering | 1.0.0-pre.2 | MIT |
| CSharpMath.VectSharp | 1.0.0-pre.2 | MIT |
| VectSharp | 2.6.1 | LGPL-3.0-only |
| VectSharp.SVG | 1.10.0 | LGPL-3.0-only |
| ExCSS | 4.2.5 | MIT |

O Rendering incorpora fontes Latin Modern Math, Cyrillic Modern e AMS Blackboard Bold. A documentação upstream declara GUST Font License para Latin Modern Math e SIL OFL para as outras duas. A expressão MIT do nuspec não substitui a auditoria dessas fontes. Não redistribuímos arquivos de fonte ou binários dos pacotes no repositório.

Fontes primárias: [CSharpMath License](https://github.com/verybadcat/CSharpMath/blob/67d5b47427a9480e4453bdf5c29164252b09e9ae/License), [lista de licenças/fontes](https://github.com/verybadcat/CSharpMath/blob/67d5b47427a9480e4453bdf5c29164252b09e9ae/ReadMe.md#license), [Rendering project](https://github.com/verybadcat/CSharpMath/blob/67d5b47427a9480e4453bdf5c29164252b09e9ae/CSharpMath.Rendering/CSharpMath.Rendering.csproj), [VectSharp](https://github.com/arklumpus/VectSharp), e os nuspec incluídos nos pacotes exatos restaurados. O commit upstream é referência da investigação; os bytes executados são os pacotes NuGet fixados, não uma compilação presumida de master.

## Lacunas e próximo passo

Sete comandos distintos falharam no catálogo: `dfrac`, `boldsymbol`, `lVert`/`rVert`, `overset`, `underset`, `boxed` e `cancel`. `newcommand` e `def` também não são implementados pelo parser testado. Não adotar o candidato como equivalente pronto; aliases/macros precisam preservar semântica, ter limites de expansão e diagnósticos, sem regravar TeX do usuário para disfarçar incompatibilidade.

Duas alternativas C# merecem continuidade: Rendering com backend SVG próprio do Urbe (licenças MIT/fontes e trabalho de implementação/teste); ou backend VectSharp com cumprimento explícito das obrigações LGPL e auditoria de distribuição. A prova reduz a incerteza de WASM, mas não decide arquitetura, licença ou adoção. Antes de incluir dependência em produção: ADR proposto, auditoria completa de pacotes/fontes, escolha fundamentada, PR crítico conforme política e corpus ampliado com renderer legado real como oráculo, sem copiar defeitos conhecidos.

UC-16 permanece aberta: macros/diagnósticos consistentes, renderização, exportação, corpus visual real e prova por superfície ainda necessários. UC-15 possui PRs #253/#254 abertos; não sobrepor seu renderer enquanto a reconciliação estiver pendente. G-C2 não foi aprovado, portanto M3 não foi antecipado.
