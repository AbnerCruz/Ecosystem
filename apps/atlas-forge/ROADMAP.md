# Atlas Forge — escopo e próximos incrementos

Fonte do escopo completo: [MISSION.md](../../docs/proposals/atlas-forge/MISSION.md). Estado vivo: [Issue AF-001 #657](https://github.com/AbnerCruz/Ecosystem/issues/657); evidências: [VERIFICATION.md](../../docs/proposals/atlas-forge/docs/VERIFICATION.md) e handoffs `HO-20261022-atlas-forge-foundation` e `HO-20261022-atlas-forge-incorporation`. Não há marco M0–M8 globalmente declarado concluído. Os estados abaixo significam verificação local, aguardando revisão/integração e validação onde aplicável.

| Tarefa | Marco | Entrega / próximo critério | Evidência ou limite |
|---|---|---|---|
| AF-001 | M0/M1/M3 parcial | Corte vertical: seed, esfera, edição, clima/drenagem, notas, persistência e publicação | Implementado e verificado localmente; revisão/integração pendente na Issue #657 |
| AF-002 | M0 | Incorporação em `apps/atlas-forge` como aplicação independente | Autorizada em DEC-0054/ADD-0031; registro, boundary e CI integrados no PR #658; revisão do produto permanece separada. Sem capability global |
| AF-003 | M0/M1 | Receber Alpha e ampliar corpus real | Só mandato recebido; fixture sintética explicitamente identificada |
| AF-004 | M1 | Validar toque, teclado, acessibilidade e desempenho Android real | Pendente; Chromium móvel não é DEVICE |
| AF-005 | M1 | Tiles/LOD, métricas de frame e invalidação por região | AF-017 acrescenta LOD de visualização com relevo procedural até ≤1 m, Worker e LRU; tiles dos solvers e recálculo físico regional seguem pendentes |
| AF-006 | M2 | Placas persistentes e velocidade angular; convergência/divergência testáveis | Não implementado; não rebatizar ruído como tectônica |
| AF-007 | M3 | Lago com armazenamento, evaporação, transbordamento e drenagem em flats | Hoje sumidouros fechados; sem inundação dinâmica |
| AF-008 | M3 | Clima sazonal por insolação, transporte métrico e calibração | Clima atual é A; insolação é utilitário testado ainda não acoplado |
| AF-009 | M2/M3 | Erosão/transporte/deposição com balanços e overrides | AF-020: transporte regional conservativo, cobertura vegetal e overrides fixos; erosão física fina pendente |
| AF-010 | M4 | Catálogo/traços, produtor–herbívoro–predador e capacidade de suporte | AF-020: catálogo, dieta, estoques limitantes, dispersão e cenário de fome; ecologia fina pendente |
| AF-011 | M5 | Eras causais com escalas de tempo e diffs | AF-020: sucessores de até 10 mil anos com balanços e marcos; tectônica temporal pendente |
| AF-012 | M7 | Geradores de cidade/caverna/dungeon, hierarquia e edição vetorial | Hoje grafo de nove salas, persistente e conectado |
| AF-013 | M8 | Arquivo de projeto grande, hashes por campo, assets e adapter autorizado | JSON limitado a 24 MiB; sem escrever arquivos internos de outro aplicativo |
| AF-015 | M1 | Autoria explícita: abertura vazia, sem exemplos inseridos, inspeção local sem geração | Issue #660: implementação e regressões; revisão no [PR #661](https://github.com/AbnerCruz/Ecosystem/pull/661) |
| AF-016 | M3 | Entrada solar sazonal, W/m² e balanço global do modelo circular | Issue #664; não acopla automaticamente o clima heurístico |
| AF-017 | M1 | Planeta → terreno → chão: campo contínuo, tiles ≤1 m, cache e viewer | Issue #664; edição métrica persistente e acoplamento físico fino pendentes |
| AF-018 | M1 | Corrigir a troca visual no zoom: uma superfície do planeta ao chão, carregamento com ancestrais e ida/volta contínua | [Issue #687](https://github.com/AbnerCruz/Ecosystem/issues/687); evidências de wheel/globo/publicação no handoff |
| AF-014 | M8 | Dois consumidores reais, Extraction Review e capability se justificada | Sem extração antecipada; Runtime central nunca obrigatório |

Cada avanço deve atravessar modelo → UI → persistência → teste → publicação quando aplicável. Não declarar todos os gates por ter controles visuais. O pedido atual prioriza AF-017 (Issue #664): chegar ao chão, além da grade planetária. Depois: edição local versionada/desenho de restrições, AF-007/008 e os demais processos causais; AF-010 depende de recursos e habitats mensuráveis.

AF-021 ([Issue #720](https://github.com/AbnerCruz/Ecosystem/issues/720)): símbolos naturais locais por seed/material/cobertura vegetal e quadro de escala humana/edificação, sem mudança de JSON ou claims físicos. Revisão por PR e validação Android físico pendentes; vegetação individual, minerais geológicos e edição métrica persistente permanecem trabalho distinto.

AF-020 ([Issue #699](https://github.com/AbnerCruz/Ecosystem/issues/699)): ciclo causal descrito na SPEC, parcial de M2/M4/M5, sem declarar marcos completos. Prioridade atual é o pedido direto do proprietário; nenhum documento substitui a funcionalidade pedida.
