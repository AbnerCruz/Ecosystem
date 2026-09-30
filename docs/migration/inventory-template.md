# Inventário de migração — <Produto> (`<id>`)

> Tarefa: P1-? · Agente: … · Data: AAAA-MM-DD · Commit de origem inventariado: `<sha>`
> Marque cada afirmação como FATO OBSERVADO (com comando/evidência) ou INFERÊNCIA (MANIFEST §53).

## 1. Repositórios de origem
- URL, visibilidade, branch padrão, forks relevantes.

## 2. Branches relevantes
| Branch | Último commit | Situação (ativa/merge pendente/abandonada) | Preservar? |
|--------|---------------|--------------------------------------------|------------|

## 3. Histórico
- Total de commits na branch padrão (`git rev-list --count`), primeiro commit, autores.

## 4. Tags e releases
| Tag | Release? | Artefatos | Observações |
|-----|----------|-----------|-------------|

## 5. Workflows (GitHub Actions)
| Arquivo | Gatilhos | O que faz | Paths/secrets que dependem da raiz |
|---------|----------|-----------|-------------------------------------|

## 6. GitHub Pages
- Existe? Fonte, domínio, impacto da mudança de path.

## 7. Secrets e configuração
- **Apenas nomes**, nunca valores (MANIFEST §30.2).

## 8. Dependências externas
- SDKs, pacotes, serviços, versões.

## 9. Arquivos normativos
- SPEC, ROADMAP, AGENTS, ADRs, changelog — caminhos e papel.

## 10. Versão
- Onde vive a versão hoje (autoridade única — NN-001) e como é incrementada.

## 11. Linha de base (antes)
| Comando | Resultado | Evidência |
|---------|-----------|-----------|
| build | | |
| testes | | |

## 12. Mapa funcional e arquitetural (ADD-0002 §16)

> **Escopo:** descrever o que **existe hoje**, com evidência (FATO OBSERVADO). A classificação futura é **PROPOSTA / INVENTÁRIO**; não autoriza extrair, mover nem refatorar nada (NN-013, `docs/migration/README.md` §7). Não presumir que existem módulos da visão de produto (`docs/architecture/product-vision.md`).

Repita o bloco abaixo para cada subsistema importante:

```text
Nome:
Responsabilidade atual:
Arquivos/diretórios:
Dependências:
Dados que possui:
UI:
Pode funcionar standalone hoje?
É específico do Product?
Dependências externas:
Riscos:

Candidato futuro (PROPOSTA — não autoriza extração):
[ ] Product Core
[ ] Product Shell
[ ] Tool
[ ] Workspace
[ ] Service
[ ] Library
[ ] Adapter
[ ] Ainda indeterminado
```

Resumo ao final do mapa: subsistemas inventariados, quantos por classificação proposta, dependências entre subsistemas que tornariam uma extração cara, e dependência de qualquer subsistema em relação ao Hub ou a outro Product (deve ser nenhuma — NN-002, NN-003, NN-023).

## 13. Riscos e ajustes técnicos inevitáveis previstos
- Cada ajuste necessário para funcionar em `apps/<id>/`, com justificativa (NN-013).

## 14. Decisões necessárias
- Referências a `DEC-XXXX`.
