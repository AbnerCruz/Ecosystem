# Auditoria pós-migração (P1-8)

> **Evidência**, não norma. Verifica NN-012 (histórico preservado) e NN-013 (migração sem refatoração) depois da importação de Lunet2D (P1-4) e Urbe (P1-5) e do corte (P1-6). Executada em **2026-10-01** sobre clones novos das origens (`git clone --bare`) e o Ecosystem em `547ef18`. O espelho posterior da origem (commits do bot) não afeta a contagem: a base são os commits da `main` original (`be30e62` e `662ca5b`).

## Resultado

| Verificação | Lunet2D | Urbe |
|-------------|---------|------|
| Commits da `main` original | 84 | 382 |
| Presentes no `commit-map` | **84 de 84** | **382 de 382** |
| Commits mapeados que são ancestrais da `main` do Ecosystem | **84 de 84** | **382 de 382** |
| Tags originais | 25 | 6 |
| Tags presentes no Ecosystem (`<id>/<tag>`) apontando para o commit mapeado | **25 de 25** | **6 de 6** |
| Conteúdo de `apps/<id>` × origem hoje (fora de `.github/`, `diff -r`) | **0 diferenças** | **0 diferenças** |
| Hash da árvore na importação × origem | `db9f6cd5…` = `db9f6cd5…` | `8f4f53db…` = `8f4f53db…` |
| Testes do produto no novo caminho | 286/286 | 56/56 |

**Conclusão (FATO):** nenhum commit, tag ou arquivo da origem foi perdido na importação (NN-012). Os dois únicos pontos em que o conteúdo de `apps/<id>` difere do que foi importado são mudanças **posteriores e documentadas**: o aviso de espelho no `README` do Lunet2D e o ajuste da checagem de workflows do Urbe (DEC-0016-A), ambos listados em [`import-plan.md`](import-plan.md) §8 (itens 8 e 9). A importação em si não alterou nenhum arquivo do produto (NN-013).

## Como repetir

```bash
git clone --bare https://github.com/AbnerCruz/<Repo> <Repo>.git
# 1) toda a história da main original está no commit-map e é ancestral da main do Ecosystem
git -C <Repo>.git rev-list <commit-original> | while read c; do grep -q "^$c " docs/migration/commit-map-<id>.txt; done
# 2) cada tag original existe como refs/tags/<id>/<tag> no commit mapeado
# 3) conteúdo
git -C <Repo>.git archive main | tar -x -C o --exclude='.github'
git archive origin/main:apps/<id> | tar -x -C e --exclude='.github'; diff -r o e
```

O `CHK-ARCH-REFS` (P1-7) cobre a parte contínua: nenhum produto referencia o outro nem o Hub no código real.

## Fora do escopo desta auditoria

Issues e PRs não fazem parte do histórico git (plano §9): continuam nas origens (DEC-0013-A). Validação em aparelho e do Urbe Web: handoff de P1-6.
