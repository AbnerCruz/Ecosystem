# Hub

Control Plane do ecossistema (MANIFEST §5.3). As leituras são **somente leitura**: consome as fontes canônicas do repositório e do GitHub e nunca é autoridade de estado (NN-001, NN-021). Não é dependência de nenhum Product (NN-003, NN-023).

- Decisão de tecnologia: Android nativo em C# (`net10.0-android`) — DEC-0022-A, [ADR-0013](../../docs/adr/0013-hub-read-only-fase-3.md).
- `src/Hub.Core`: leitura de dados, independente de UI (`net10.0`). Todo dado carrega sua origem; sem fonte, `NotAvailable`.
- `src/Hub.Android`: camada de UI Android (`net10.0-android`, P3-9). Só desenha o `HubScreen` e usa `SnapshotPolicy` (último estado bom marcado como tal); nenhuma regra de estado mora aqui. Fica fora de `Hub.slnx` porque compilar exige o workload `android`; o APK de depuração é gerado pelo job `android` do `hub-ci`.
- **Release (P3-7):** canal de desenvolvimento em GitHub Releases públicas deste repositório, tag `hub-vX.Y.Z-dev.N`, como pré-lançamento (DEC-0028-A), publicado pelo workflow `hub-release`. O APK é assinado com a chave de desenvolvimento **pública** e estável de `tools/` (DEC-0029-A): ver [`docs/assinatura-de-desenvolvimento.md`](docs/assinatura-de-desenvolvimento.md) — o que ela garante e o que não garante.
- `tests/Hub.Tests`: testes do núcleo — `dotnet test --project tests/Hub.Tests` (a partir deste diretório).
- Catálogo de releases e artefatos (P4-1): canais do perfil `current`, tags por componente no monorepo, metadados de APK e cache compatível. [Formato, limites e conferência visual](docs/catalogo-de-releases.md). SHA-256 informado pelo canal não significa bytes verificados.
- Download de APK (P4-2/P4-7): solicitação explícita, continuação em segundo plano com notificação/cancelamento, limite e comparação de tamanho/SHA-256 dos bytes; cache privado, erros e descarte explícitos. [Modelo de permissões e conferência em aparelho](docs/download-em-segundo-plano.md); [comportamento original dev.4](docs/download-e-integridade.md). Instalação/launcher seguem itens próprios.
- Patch Notes/histórico (P4-8): body da release, provider local, Markdown nativo/categorias técnicas expansíveis, histórico limitado e fallback do snapshot; versão instalada desconhecida até leitura real. [Fonte, comportamento e conferência mobile](docs/patch-notes.md).
- Versão: arquivo `VERSION` (autoridade declarada em `ecosystem.json`).
- Escopo, tarefas e gates: [`ROADMAP.md`](../../ROADMAP.md).
