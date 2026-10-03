# Hub

Control Plane do ecossistema (MANIFEST §5.3). Nesta fase (3) é **somente leitura**: consome as fontes canônicas do repositório e do GitHub e nunca é autoridade de estado (NN-001, NN-021). Não é dependência de nenhum Product (NN-003, NN-023).

- Decisão de tecnologia: Android nativo em C# (`net10.0-android`) — DEC-0022-A, [ADR-0013](../../docs/adr/0013-hub-read-only-fase-3.md).
- `src/Hub.Core`: leitura de dados, independente de UI (`net10.0`). Todo dado carrega sua origem; sem fonte, `NotAvailable`.
- `src/Hub.Android`: camada de UI Android (`net10.0-android`, P3-9). Só desenha o `HubScreen` e usa `SnapshotPolicy` (último estado bom marcado como tal); nenhuma regra de estado mora aqui. Fica fora de `Hub.slnx` porque compilar exige o workload `android`; o APK de depuração é gerado pelo job `android` do `hub-ci`.
- `tests/Hub.Tests`: testes do núcleo — `dotnet test --project tests/Hub.Tests` (a partir deste diretório).
- Versão: arquivo `VERSION` (autoridade declarada em `ecosystem.json`).
- Escopo, tarefas e gate: [`ROADMAP.md`](../../ROADMAP.md) (Fase 3).
