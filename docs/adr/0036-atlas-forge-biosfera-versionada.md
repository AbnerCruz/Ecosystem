# ADR-0036 — Atlas Forge: biosfera persistente e sucessores temporais

## Status

Proposto — implementação revisável no AF-020, Issue #699. Não registra aprovação arquitetural ou integração que ainda não ocorreu.

## Contexto

O proprietário solicitou espécies configuráveis, cadeias alimentares, influência da biosfera no terreno e eras causais, com persistência e publicação offline. Atlas Forge permanece aplicação independente conforme ADR-0035. O formato v2 reservava `species` vazio e não representava estoques nem tempo simulado.

## Problema

Manter continuidade de mundos existentes enquanto novos projetos armazenam recursos/populações e produzem sucessores causais. Snapshots sozinhos não atendem simulação temporal. Viewer não pode depender dos solvers.

## Opções

1. Formato v3 com catálogo e estoques autoritativos, migração aditiva v2, modelo explícito e solver cancelável local.
2. Gravar populações em campos reservados de v2, rompendo o significado do leitor existente.
3. Manter somente visualização ou guardar biosfera em storage separado sem roundtrip integral.

## Decisão

Proposta implementada para revisão: opção 1. v2 migra em memória, com biosfera nula e inicialização explícita; v3 tem `resources-trophic-1`, campos em unidades físicas e catálogo validado. Todo arquivo de entrada permanece intacto. Versões/modelos desconhecidos são recusados. Marcos carregam estado completo e o Worker só entrega sucessor após validação. Grade ecológica regional independente evita confundir detalhe visual com física fina. Viewer recebe snapshot e consultas, sem solver.

## Consequências

Leitores antigos recusam novos projetos; cópia original é preservada, sem downgrade destrutivo. Era armazena marco anterior, sujeito aos limites existentes de 24 MiB e 12 marcos. A revisão é crítica por formato/migração de dados do usuário. Testes cobrem roundtrip, migração, cancelamento, restauração, balanços e HTML offline. Parâmetros ecológicos/erosivos são simplificados, não calibração científica ou tectônica.

## Alternativas rejeitadas

Reutilizar v2 esconderia uma mudança de contrato. Armazenamento separado permitiria exportações incompletas. Introduzir Tool global/integração direta no Urbe não tem segundo consumidor autorizado e contraria boundaries existentes.

## Referências

MANIFEST NN-001, NN-002, NN-003, NN-010, NN-011, NN-017, NN-018, NN-022; ADR-0035; `apps/atlas-forge/docs/FORMAT.md`, `MODELS.md`, `SPEC.md`; Issue #699.
