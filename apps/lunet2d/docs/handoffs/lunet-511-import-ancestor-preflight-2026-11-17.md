# LUNET-511 — Preflight de ancestral ocupado (17/11/2026)

- Escopo: apenas `apps/lunet2d`, PR #962, branch `feat/lunet-511-versioned-asset-library-20261116`.
- Problema: `FindImportConflicts` detectava `Content/Art` como arquivo, mas `ImportToProject` verificava apenas o destino final e chegava ao staging antes de falhar.
- Implementação: `ReusableAssetLibrary.ImportToProject` verifica cada ancestral contra arquivos, antes de staging, e rejeita o pacote sem sobrescrita.
- Regressão: `ImportToProject_AncestorFileConflictFailsBeforeCreatingStaging` valida colisão, Game.cs intacto e conteúdo existente preservado.
- Referências: auditoria `docs/architecture/audits/lunet-home-studios-explorar-2026-11-15.md`, preflight e cópia offline; documentação oficial de segurança de caminhos e I/O do .NET.
- Evidência: commits 30e49e43 e a5b45952; execução local .NET indisponível neste ambiente. Head novo aguarda CI. DEVICE Android 13 pendente.
- Próximos passos: acompanhar CI e corrigir falhas; implementar edição direta em Studios fora do projeto, com testes e validação física. Sem merge forçado nem alteração de estágio.
