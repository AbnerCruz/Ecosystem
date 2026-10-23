# Atlas Forge — escopo e próximos incrementos

Fonte do escopo completo: [MISSION.md](../../docs/proposals/atlas-forge/MISSION.md). Estado vivo: [Issue AF-001 #657](https://github.com/AbnerCruz/Ecosystem/issues/657); evidências: [VERIFICATION.md](../../docs/proposals/atlas-forge/docs/VERIFICATION.md) e handoffs `HO-20261022-atlas-forge-foundation` e `HO-20261022-atlas-forge-incorporation`. Não há marco M0–M8 globalmente declarado concluído. Os estados abaixo significam verificação local, aguardando revisão/integração e validação onde aplicável.

| Tarefa | Marco | Entrega / próximo critério | Evidência ou limite |
|---|---|---|---|
| AF-001 | M0/M1/M3 parcial | Corte vertical: seed, esfera, edição, clima/drenagem, notas, persistência e publicação | Implementado e verificado localmente; revisão/integração pendente na Issue #657 |
| AF-002 | M0 | Incorporação em `apps/atlas-forge` como aplicação independente | Autorizada em DEC-0054/ADD-0031; registro, boundary e CI preparados na Issue #659/PR #658; integração/revisão pendente. Sem capability global |
| AF-003 | M0/M1 | Receber Alpha e ampliar corpus real | Só mandato recebido; fixture sintética explicitamente identificada |
| AF-004 | M1 | Validar toque, teclado, acessibilidade e desempenho Android real | Pendente; Chromium móvel não é DEVICE |
| AF-005 | M1 | Tiles/LOD, métricas de frame e invalidação por região | AF-005/015: zoom 128×, HiDPI, curvas/escala, resolução 1.440 e refinamento implementados; tiles físicos, LOD e recálculo regional seguem pendentes |
| AF-006 | M2 | Placas persistentes e velocidade angular; convergência/divergência testáveis | Não implementado; não rebatizar ruído como tectônica |
| AF-007 | M3 | Lago com armazenamento, evaporação, transbordamento e drenagem em flats | Hoje sumidouros fechados; sem inundação dinâmica |
| AF-008 | M3 | Clima sazonal por insolação, transporte métrico e calibração | Clima atual é A; insolação é utilitário testado ainda não acoplado |
| AF-009 | M2/M3 | Erosão/transporte/deposição com balanços e overrides | Não implementado |
| AF-010 | M4 | Catálogo/traços, produtor–herbívoro–predador e capacidade de suporte | Central à missão; primeiro cenário: herbívoro sem alimento declina |
| AF-011 | M5 | Eras causais com escalas de tempo e diffs | Marcos atuais são snapshots, não simulação temporal |
| AF-012 | M6 | Geradores de cidade/caverna/dungeon, hierarquia e edição vetorial | Hoje grafo de nove salas, persistente e conectado |
| AF-013 | M7 | Arquivo de projeto grande, hashes por campo, assets e adapter autorizado | JSON limitado a 24 MiB; sem escrever arquivos internos de outro aplicativo |
| AF-015 | M1 | Autoria explícita: abertura vazia, sem exemplos inseridos, inspeção local sem geração | Issue #660: implementação e regressões; revisão no PR correspondente |
| AF-014 | M8 | Dois consumidores reais, Extraction Review e capability se justificada | Sem extração antecipada; Runtime central nunca obrigatório |

Cada avanço deve atravessar modelo → UI → persistência → teste → publicação quando aplicável. Não declarar todos os gates por ter controles visuais. A correção cartográfica solicitada pelo proprietário é prioritária em AF-005/015 (Issue #660); depois dela, retomar AF-007; AF-010 depende de recursos e habitats mensuráveis.
