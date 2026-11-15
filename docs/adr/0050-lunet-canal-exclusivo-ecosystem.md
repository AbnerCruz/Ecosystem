# ADR-0050 — Lunet com autoridade e distribuição exclusivas no Ecosystem

## Status

Aceito — decisão expressa do proprietário (DEC-0064 / ADD-0035), 15/11/2026.

## Contexto

A migração para `apps/lunet2d` já consolidou o código e as releases no Ecosystem, mas persistiam uma sincronização agendada no antigo repositório, `origin-sync` e canais alternativos nos perfis. Isso multiplica pipelines e induz agentes a tratar um CI incapaz de encontrar contratos do monorepo como autoridade.

## Problema

O pipeline legado e a declaração de um canal alternativo criavam autoridade concorrente e falhas de build fora do monorepo, apesar de o Lunet já ser publicado pelo Ecosystem.

## Opções

1. Permanecer com dois repositórios ativos e reparar a sincronização.
2. Tornar o Ecosystem o único repositório operacional, preservando a proveniência histórica (decisão do proprietário).
3. Apagar imediatamente o histórico importado e o repositório antigo.

## Decisão

Somente `AbnerCruz/Ecosystem` desenvolve, testa, integra e distribui o Lunet, por `lunet2d-ci.yml` e `lunet2d-release.yml`. Desativar sincronização externa e remover componente operacional `origin-sync`, scripts/templates e canais alternativos `source.repository`. Manter a proveniência Git histórica, IDs, tags importadas e código funcional. Arquivar/apagar o repositório antigo na interface GitHub é decisão administrativa separada, não fallback operacional.

## Consequências

Nenhuma modificação em binário, projeto, identidade de pacote ou versão; documentos, perfis e automações deixam de oferecer o repositório legado como destino. O código histórico fica rastreável no monorepo. O fechamento desta alteração continua sujeito a CI e gate de governança.

## Alternativas rejeitadas

Espelho ativo não acrescenta valor e multiplica CI divergente. Apagar o histórico de migração violaria os princípios de rastreabilidade sem necessidade. A remoção administrativa da conta GitHub não é pré-condição para encerrar a operação.

## Verificação

- O perfil current mantém apenas o canal oficial Ecosystem para o Lunet; target mantém apenas a plataforma first-party futura, sem espelho.
- Pipeline direto permanece intacto; assinaturas, versionCode e identidade não mudam.
- O relatório de 15/11 substitui como referência de estado a fotografia de 14/11, sem apagar o histórico.
- Rodar checks de consistência e CI do Ecosystem no estado combinado. Não declarar DEVICE aprovado nem contornar revisão vinculada ao head para alterações de governança (NN-011/016/017/018).
