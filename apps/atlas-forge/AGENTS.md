# AGENTS.md — Atlas Forge

Complementa o AGENTS.md raiz; a constituição, as decisões e a política de integração do repositório continuam valendo.

Antes de alterar este aplicativo, leia MANIFEST.md (incluindo NN), ecosystem.json, ADR-0035/DEC-0054, SPEC.md, ROADMAP.md e as notas do modelo/formato afetado. A fonte canônica está em apps/atlas-forge; a versão vem de package.json.

- Mantenha geração/solvers separados do editor e renderer. A grade física, o viewport e os caches são conceitos diferentes.
- Estado autoral em metros e overrides persistidos têm autoridade única; regeneração não remove edições sem ação explícita.
- Preserve determinismo por seed/configuração/versão; use streams independentes por subsistema.
- Cálculo pesado em Worker cancelável, sem commit parcial nem resultado tardio sobrescrevendo o mundo atual.
- Declare modelos como heurísticos, físicos simplificados ou validados, com unidades, equações e limites. Não chamar ruído de tectônica nem snapshots de simulação temporal.
- Arquivos importados são dados não confiáveis. Não executar código/HTML/URLs do projeto ou de notas; verificar snapshots SHA-256 e preservar arquivo original.
- Editor e publicação devem funcionar offline e sem outro aplicativo, serviço, conta ou runtime central. Não criar dependência Product → Product.
- O viewer publicado não inclui editor, storage ou solvers; teste o bundle e o fluxo file:// sem rede.
- Reuso potencial permanece local até consumidor real, Extraction Review e contrato; não registrar capability especulativa.
- Não alterar formato/algoritmo sem versão, migração e testes. Não perder dados desconhecidos silenciosamente.

Verificação: `npm run check`; para mudanças de desenho/desempenho, `npm run benchmark:render`; para solver, cenários sintéticos relevantes e `npm run benchmark`. Checks de consistência do repositório e handoff são obrigatórios. Emulação móvel não é validação física Android. CI seletivo fica em `.github/workflows/atlas-forge-checks.yml`; não publicar release ao integrar uma mudança rotineira.
