# Auditoria e limpeza de branches (RM-F0-16, REQ-018/067)

> Data: 2026-09-30. Critério: branch removida somente se seu conteúdo está na `main` (ancestral da `main`, ou commit duplicado por rebase/squash com o mesmo efeito). Os SHAs das pontas ficam aqui para restauração (`git branch <nome> <sha>` após `git fetch origin <sha>`, enquanto o GitHub retiver o objeto).

| Branch | Ponta (SHA) | Incorporada por |
|---|---|---|
| `chore/urbe-2-foundation` | `13d043c446005a4a302dd5327b36075c52d1d001` | ancestral da `main` |
| `claude/abertura-rapida` | `1cceda5c049237a659a9511ac18f85271cfbad52` | ancestral da `main` |
| `claude/agente-melhor` | `1f52bcb1fcd1d2f38a1a5e122106c5f73e91f341` | ancestral da `main` |
| `claude/agentes-ia` | `99d635c1e60b8e014a3f64ca2ba95adde1b4848d` | ancestral da `main` |
| `claude/ambiente-paginas` | `87db173a2e624f8fe50ef824188532a61de45bca` | ancestral da `main` |
| `claude/analise-geral-funcionamento-yj098x` | `d5f4888b6eaf7b078edab819a5c83df5daac803b` | ancestral da `main` |
| `claude/app-desempenho` | `165d930f68050a5b6916cbdc56d8e4df9a516c4f` | ancestral da `main` |
| `claude/app-nativo` | `6216e8881d42472d9d6d3dc3b5c844612fca123f` | ancestral da `main` |
| `claude/aquario-vivo` | `7bb81442d512903b97099f373d70120672723f86` | ancestral da `main` |
| `claude/bairros-buraco` | `70fd3acaec751eb01751e6f1f2814a6c2ce1a6d6` | ancestral da `main` |
| `claude/bairros-casas` | `b6aed4e18972e13d04034df9da0a7955c65e07ec` | ancestral da `main` |
| `claude/celular-mundo` | `c04939ee7721f892049998e8355cc107da425437` | ancestral da `main` |
| `claude/estudio-paginas` | `22920e6c2bdadc9f37f233dac1f0da087dd2ccc6` | ancestral da `main` |
| `claude/fix-abertura-mundo` | `376bfba85e477cbab7f7140013f5a044a15bac54` | ancestral da `main` |
| `claude/fix-explorer-abas` | `92e823818cd09ec40f0a7380cb241830976e29a0` | ancestral da `main` |
| `claude/fix-migracao-oceano` | `ac75d36d538f44374a299e49468e9f8657be68ff` | ancestral da `main` |
| `claude/layout-livre` | `de854212675f8b34070180c4015ec15ba4b1ad90` | ancestral da `main` |
| `claude/matematica` | `48c2cd8b9e314509214b121b966a8ea027f60a6a` | ancestral da `main` |
| `claude/moradores` | `ac3a3afe614f59e349a581daa48e8b219a74f5ae` | ancestral da `main` |
| `claude/mundo-cheio` | `69742814188da6063652b7a48ac872d8e7960051` | ancestral da `main` |
| `claude/mundo-vivo` | `8d11584b27ac0435780bd44f3cf8e2a7a5945848` | ancestral da `main` |
| `claude/paginas-html` | `0abdb9403eae8c37279f0dfc43ffd9acd83d2262` | ancestral da `main` |
| `claude/paginas-livro` | `163677bb4270108fc5dcbcc739ff26f4eea4b9c2` | ancestral da `main` |
| `claude/primeiro-acesso` | `83ee36c58d9e0327578b7b298bb6f9f2821a6a05` | ancestral da `main` |
| `claude/regioes-visual` | `7981c6184fb793ac299af4834ff536abc900032d` | ancestral da `main` |
| `claude/ux-fase-1` | `d97e7b0148d81edf9f8be52815448f54f58b2abf` | ancestral da `main` |
| `claude/ux-fase-2` | `0b145a4565c913438dac9d75b8f6d2fe7f1c7331` | ancestral da `main` |
| `claude/ux-fase-3` | `9f7e359a6a41233faf754ce446ea4004d5be5dcc` | ancestral da `main` |
| `claude/beta-v1` | `4bc9ec85a5bee87c706bbde970d627fda472e02c` | só commits de merge de PR (#16/#17); conteúdo na `main` |
| `claude/mundo-real` | `9106eea3a96368334ab53e5a94063f7830cc4b91` | só commits de merge de PR (#16/#17); conteúdo na `main` |
| `claude/fix-explorer-celular` | `58b66ded1e7198350fe2d6eef57e1fe399b667fe` | commit duplicado na `main` (`0cf0da8`, mesma mensagem; `explorer.deleteFolder` e CHANGELOG presentes) |

Mantidas: `main` e `claude/new-session-eh5rwa` (branch de trabalho desta sessão).

## Estado da remoção
A verificação está concluída (todas as branches acima estão incorporadas). **A remoção das branches remotas não foi executada:** apagar refs remotas exige autorização explícita do proprietário (a ação foi bloqueada na sessão). Para concluir o item `RM-F0-16`: o proprietário autoriza a remoção, ou executa `git push origin --delete <branches da tabela>`. Sem risco de perda: o conteúdo está na `main`.
