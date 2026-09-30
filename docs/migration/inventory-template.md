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

## 12. Riscos e ajustes técnicos inevitáveis previstos
- Cada ajuste necessário para funcionar em `apps/<id>/`, com justificativa (NN-013).

## 13. Decisões necessárias
- Referências a `DEC-XXXX`.
