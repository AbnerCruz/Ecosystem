# Handoff — LUNET-512: compatibilidade de referências curadas retiradas (16/11/2026)

- **Fonte canônica:** AbnerCruz/Ecosystem, apps/lunet2d. Base: main; branch: `fix/lunet-inspiration-retired-curated-20261116`. PR #958 integrado; PR #959 aberto, independente.
- **Risco corrigido:** um ID curado salvo e retirado do catálogo de uma versão posterior causava `InvalidDataException` ao abrir a biblioteca. O arquivo permanecia intacto, mas notas/favoritos ficavam inacessíveis.
- **Código:** `src/Lunet.Core/InspirationLibrary.cs` conserva entradas aposentadas com prefixo `ref.` como snapshots curados somente-leitura; dados pessoais seguem editáveis. Se o ID voltar ao catálogo, metadados instalados prevalecem e notas/favoritos continuam. IDs pessoais inválidos seguem recusados.
- **Regressão:** `tests/Lunet.Tests/InspirationLibraryTests.cs` injeta JSON de versão anterior com referência curada ausente, abre, salva notas e reabre sem perda; também verifica catálogo instalado.
- **Autoridades:** MANIFEST NN-001 (fonte local de dados pessoais), NN-005 (migração incremental), NN-010 (compatibilidade); AGENTS raiz/local; ROADMAP; auditorias Lunet de 15/11; ADD-0034/DEC-0064. Nenhum contrato compartilhado nem estágio de versão alterado.
- **Verificação:** teste foi registrado antes da correção em commit `60be512`. Ambiente desta execução sem .NET SDK e sem Android físico: xUnit, consistency, CI, release e DEVICE **não foram declarados aprovados**.
- **Próximos passos:** verificar checks do head do PR; corrigir falhas; no Android 13 carregar arquivo com referência aposentada, editar notas, reiniciar, confirmar persistência e preservação do JSON. Não marcar LUNET-512 como concluído antes do DEVICE.
