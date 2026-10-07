# ADR-0012 — Renderização matemática C# com CSharpMath.Rendering e backend SVG próprio

- Status: Accepted
- Data: 2026-10-06
- Requisitos: REQ-007, REQ-027, REQ-064
- Decisores: proprietário do Ecosystem (autorização crítica de integração)

## Contexto

A UC-16 precisa substituir o caminho visual de matemática do cliente JavaScript sem
introduzir interop matemático em JavaScript no cliente C#. A investigação reproduzível
em `UC16-RENDERER-INVESTIGATION.md` executou 210 entradas reais com
`CSharpMath 1.0.0-pre.2` / `CSharpMath.Rendering 1.0.0-pre.2`, incluindo
publicação Blazor WASM e nova renderização com a rede desligada.

O backend usado apenas no probe, `CSharpMath.VectSharp`, traz VectSharp e
VectSharp.SVG sob LGPL-3.0-only. Essa dependência não é necessária para a
tipografia: `CSharpMath.Rendering` expõe `ICanvas` e `Path` justamente para
backends próprios.

## Forças

- C# puro e execução offline nas superfícies já escolhidas pelo ADR-0010;
- SVG autocontido para preview/export, sem rasterização nem engine nativa;
- preservar o TeX do usuário e transformar incompatibilidades em diagnósticos;
- evitar uma dependência LGPL quando uma fronteira MIT já oferece a extensão necessária;
- manter o renderer no Core, independente de DOM/WebView e de qualquer plano de controle externo;
- limitar entrada e evitar links/scripts/texto externo no SVG gerado.

## Alternativas consideradas

### A — CSharpMath.Rendering + backend SVG próprio do Urbe

Pacotes `CSharpMath` e `CSharpMath.Rendering` fixados em `1.0.0-pre.2`.
O Urbe implementa somente o canvas/path SVG, usando os outlines tipográficos
fornecidos pelo renderer. O SVG contém paths/linhas/retângulos, sem links externos.

Licenças observadas na versão avaliada:
- CSharpMath: MIT;
- Typography incorporado por CSharpMath.Rendering: MIT;
- Latin Modern Math: GUST Font License;
- Cyrillic Modern e AMS Blackboard Bold: SIL OFL.

### B — CSharpMath.VectSharp

Menos código de backend, mas adiciona VectSharp/VectSharp.SVG LGPL-3.0-only à
cadeia de distribuição. Rejeitada enquanto a alternativa A for suficiente.

### C — KaTeX/JavaScript por interop

Visual conhecido do cliente legado, porém viola o objetivo da migração C# e criaria
uma exceção arquitetural que ADR-0010/DEC-0024-B não autorizaram. Rejeitada.

## Decisão

Adotar a alternativa A. O PR crítico #274 recebeu a autorização canônica do proprietário e integrou em 2026-10-07; portanto a condição de aceitação definida por este ADR foi satisfeita.

O backend do Urbe deve:
1. produzir SVG autocontido sem `<script>`, `<text>` ou `href` externo;
2. nunca regravar o TeX para mascarar comando não suportado;
3. devolver diagnóstico em vez de propagar exceção de entrada ao Host;
4. impor limite explícito de tamanho/fonte;
5. manter aliases/macros futuros como camada separada, limitada e testada.

## Consequências positivas

- elimina VectSharp/LGPL do caminho proposto;
- usa a mesma tipografia C# em Web/Android/Windows;
- permite export SVG determinístico e offline;
- renderer permanece host-neutro e testável no Core.

## Consequências negativas / trade-offs

- `CSharpMath 1.0.0-pre.2` ainda é prerelease;
- o Urbe passa a manter um pequeno backend SVG;
- comandos sem suporte nativo (`boxed`, `overset`, `underset`, `cancel`, `boldsymbol`, `dfrac`, `lVert/rVert`) permanecem diagnósticos explícitos em vez de reescrita silenciosa;
- macros de string compatíveis com o contrato útil observado de `UrbeMath.setMacros` são expandidas apenas em memória, com limites e sem estado global; declarações dinâmicas `newcommand/def` continuam bloqueadas;
- licenças das fontes incorporadas precisam constar dos avisos de terceiros antes
  da distribuição do novo cliente.

## Migração

A dependência entra somente no `Urbe.Core`. O cliente JavaScript distribuído não
muda. UC-18 consumirá a API de renderização na UI C#; UC-31 continua sendo o único
corte de cliente autorizado.

## Validação

- corpus real do tutorial: piso de 30/31 fórmulas renderizadas, sem exceções;
- SVG sem texto, script ou referências externas;
- resultado determinístico para a mesma entrada;
- Core/Web/Android/Windows/E2E/checks/consistency verdes no estado combinado;
- macros limitadas e diagnósticos estruturados integrados;
- suíte final de G-C2 cruza edição, compatibilidade, macros e renderer, exige corpus real 30/31, SVG geométrico não vazio e determinismo;
- comparação visual humana de UI é deliberadamente G-C3/UC-18, não é inferida por este ADR.

## Relações

- substitui: nenhum ADR;
- substituída por: —
- documentos afetados:
  - `apps/urbe/docs/csharp/UC16-RENDERER-INVESTIGATION.md`;
  - `apps/urbe/docs/csharp/math-renderer-observations.json`;
  - `apps/urbe/csharp/src/Urbe.Core/Urbe.Core.csproj`.
