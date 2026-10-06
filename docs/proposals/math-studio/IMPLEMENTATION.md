# Preparação técnica e auditoria — codinome Math Studio

> Proposta: nada deste plano é implementação ou validação de build.
> Base auditada: `43ccb54e328b7b503d3e07adfde36180014e9964` (main, 2026-10-06).
> Branch de trabalho: `codex/math-authoring-proposal`; Issue #260 / MSP-001.

## 1. Fatos observados, inferências e gate

**Fatos:** árvore completa da main lida pelo GitHub; Products registrados:
Hub, Lunet2D, Urbe, Ecosystem AI e Tabletop RPG. Há também portal/automações/checks
e `platform/text-inspection`, extraído por DEC-0038 para dois consumidores reais.
Não há diretório de Product matemático, decisão matemática em DEC-0001..0039,
ADR específico (índice termina em 0028), Issue de matemática encontrada ou branch
de matemática nas duas páginas consultadas (172 branches no total).

A main não corresponde integralmente ao briefing nem aos resumos históricos:
Fase 5 já foi aprovada; Fase 6/P6-4 espera o anfitrião real sem antecipar o roadmap
do Lunet/Urbe. Urbe está em UC-15 C#, com PRs #253/#254 em andamento. PR #256
reconcilia P4-9 e o candidato distribuído. Issues abertas observadas #249, #200,
#166, #159 e #144 têm escopos distintos; #159 permanece aberto apesar de branches
de closeout. Isso não autoriza encerrar trabalho de terceiros.

**Sobreposição:** #256 toca ROADMAP e handoffs, sem mudar DEC/ADRs desta proposta.
Evitar modificar estado de fases/produtos. Numeração ADR/DEC é recurso concorrente:
revalidar main/branches antes de publicar; se colidir, renumerar somente nossos
novos registros e referências. Consulta de Issue search não substitui leitura
dos registros; existência de branch histórica não prova trabalho ativo.

**Inferência normativa:** a intenção do novo Product está autorizada na delegação,
mas sua identidade técnica não. AGENTS §3, NN-011/019 e o precedente DEC-0027
exigem decisão explícita antes de tornar o ID permanente. Propor arquitetura e
perguntar é rotina pelo ADR-0015; constituir/registrar/ativar CI é efeito crítico.
Não pedir novamente a tese do produto nem classificar Tool/Library pelo usuário.

**Gate preparado:** DEC-0040 sob componente existente `ecosystem` (o checker não
aceita componente inexistente). Alternativas escolhem `math-authoring` ou
`math-composer` como ID técnico, mantendo marca em aberto e ratificando ADR-0029/
0030, ou adiam a identidade. Não registrar `math-studio` em ecosystem.json.
Uma decisão no portal e sua aplicação canônica precedem MA-001. O PR de proposta
pode integrar automaticamente e publicar a pendência; não precisa de aprovação
crítica só por apresentar opções.

## 2. Caminho após decisão: arquitetura local C#

Estrutura ilustrativa sob `apps/<id-decidido>`; nomes de assemblies e paths locais
são detalhes a ajustar na constituição. Não criar todos os projetos vazios.

```text
Project model + typed Math AST + Validation
             ↓
Commands/Transactions → published document revision
             ↓                   ↓
Math compilation + graph   Timeline evaluation(t)
             └──────────┬────────┘
                 Scene evaluation(t)
                        ↓
                  Display list
                   ↙         ↘
           Stage/selection   Offscreen/export

Persistence ↔ validated document + asset catalog
Visual UI / text view → same command/transaction boundary
```

Core headless em .NET 10: modelo, matemática, tempo, operações e validação.
Persistence: save/load/recovery/migrations. Rendering: geometria/layout/display
list independente de Activity. Adapter Android C# `net10.0-android`: Canvas,
toque/lifecycle, storage e codec. Editor coordena transações, não contém evaluator.
Sem referência a assemblies em `apps/hub`, `apps/lunet2d`, `apps/urbe` ou IA.
Reusar contratos reais somente se houver consumidor; nenhuma capability nova
é necessária para a primeira engine local.

O controller publica revisão validada; operações editoriais usam IDs e transações
atômicas. Texto inválido não altera a revisão. History referencia revisões/comandos
inversos validados; agrupamento de drag é uma transação. Recovery não depende da
Activity nem é o undo log em memória. Export congela revisão e assets, permitindo
editar o próximo estado sem mudar o vídeo em andamento.

Math compile constrói DAG tipado e ordem estável por ID; AST compila uma vez por
revisão afetada. Mudança de variável marca apenas dependentes via reverse edges.
Detectar ciclos e refs/tipos errados antes de execução. Cache explicita chave
revisão/nó/inputs e política de invalidação; nenhum cache é autoridade autoral.
Erros distinguem domínio inválido, referência ausente, ciclo, overflow e limite.

Timeline amostra tracks diretamente em t e sobrepõe valores à base, antes de
avaliar o DAG; não muta cumulativamente o estado da cena. Tempos em ticks/racionais
permitem frames i/fps e PTS estáveis; sem acumular dt de UI. Keyframes têm ordenação,
política de empates e valores de borda explícitos. Um target tem um escritor por
intervalo ou composição determinística documentada; conflito não é last-writer
implícito. O renderer consome snapshots avaliados e não calcula matemática.

Determinismo semântico usa cultura invariável, ordem de operações estável e
diagnósticos definidos. Não prometer bytes raster idênticos entre GPUs/fonts/SOs:
teste primeiro display list/geometria/tempo. Golden só com ambiente/fontes fixos.
Hit testing reutiliza geometria e inversas das transformações, em ordem visual.

## 3. Persistência, imports e tipografia

Formato físico a detalhar em MA-003: pacote com manifesto JSON versionado e assets
referenciados por IDs/hashes relativos, nunca caminhos absolutos. Export/import
leva os bytes necessários. Save produz revisão temporária validada, flush/replace
e backup da revisão anterior com marcador/hash. Autosave/journal declara a revisão
base e checkpoints; recovery verifica integridade e escolhe último estado válido
sem destruir o arquivo original. Limites e atomicidade são testados no storage
Android efetivo; não extrapolar garantia de rename desktop para providers SAF.

Import é dados: limites de bytes, expansão de pacote, contagem/profundidade AST,
nós/tracks/assets, dimensões de imagem e duração; reject path traversal/symlinks
e payload executável. Número não finito e versão desconhecida são erros claros.
Migrações copy-on-write validam antes/depois e preservam backup; versão futura
não é normalizada e regravada. Não introduzir extensão executável escondida.

Tipografia inicial: layout local de tokens/glyphs, baseline, sobrescrito e fração
sobre AST, com métricas de fonte e desenho nítido em escala do render. O slice
precisa de x², Δx, razão de diferenças e reta. Guardar texto/layout e IDs de termos,
não bitmaps pré-renderizados como estrutura matemática. LaTeX completo não é
requisito da v1; entrada familiar precisa de gramática/diagnóstico declarados.

| Candidato | Avaliação antes de escolha |
|---|---|
| Layout mínimo C# + fontes/glyphs locais | Cobertura pequena e previsível, offline, licença das fontes e medidas controladas; custo de crescer em símbolos |
| Biblioteca math typesetting .NET | Licença/transitivos, Android/AOT, bundle, token boxes, medição, vetor/export e benchmark no aparelho |
| KaTeX/MathJax/WebView | Avaliar offline/bundle e acesso à estrutura; acrescenta runtime JS ao caminho central e não é assumido por existir no Urbe |
| CAS externo | Só quando necessário; licença, tamanho, offline, .NET/Android, determinismo, testabilidade e substituição; sem dependência agora |

Nenhuma biblioteca externa foi selecionada/instalada nesta preparação. O spike
com evidências escolhe tecnologia; nova dependência estrutural exige ADR quando
aplicável. Não reportar licença/tamanho/performance não medidos como fatos.

## 4. MP4 Android: plano tecnicamente verificável

Primeiro avaliar APIs de plataforma acessíveis por C#:
`MediaCodec` AVC (`video/avc`) e `MediaMuxer` MP4. Usar o mesmo estado/display list
do Stage no renderer offscreen, não capturar a tela do editor. Caminho preferido
a provar: render offscreen + input Surface do codec, adaptada por EGL/GLES com
presentation timestamp explícito. Se for raster para textura, reutilizar buffers
e medir custo; nada de screenshot da UI ou arquivos intermediários por frame.
Não assumir que `Surface.lockCanvas` funciona no encoder.

Alternativa de spike: input buffers/Images YUV negociados pelo codec; exige
validar formatos, strides/planes, conversão de cor e limites por fabricante.
Não pressupor um layout YUV universal. CodecSurface e YUV ficam em adapters locais,
com decisão baseada em DEVICE/medidas; não são novo engine global.

Validar codec disponível, dimensões/alinhamento, frame rate/profile/bitrate e
memória antes de iniciar. Frames i=0..N-1 usam t=i/30, PTS monotônico em micros/ns
conforme API; começar muxer após output format, drenar codec e EOS, finalizar
track/arquivo. Cancel/falha descarta temporário e libera codec/Surface/GL/muxer.
Publicação do arquivo só após finalização válida. UI recebe progress, não trabalho
de encoding. Lifecycle/background precisa de estratégia explícita e tested;
process death não deixa arquivo parcial marcado como pronto.

Gate: produzir bytes 1280×720/30 fps com duration/PTS/codec/perfil conferidos,
decode de começo/meio/fim, quantidade de frames tolerando arredondamento declarado,
metadados, reprodução e seek em Android identificado. Evidência inclui projeto,
revisão, build, configuração e hash do MP4. Inspector desktop pode conferir
metadados/decodificar, mas não substitui a engine ou teste Android.

APIs de plataforma evitam empacotar FFmpeg/libx264 preventivamente. Avaliar
compatibilidade/distribuição/licenças aplicáveis do codec e fontes; nenhuma
conclusão jurídica ou licença de bundle novo é presumida. FFmpeg/biblioteca
externa só com necessidade demonstrada, tamanho/licença/Android e ADR adequados.

## 5. Testes, performance e preparação do PR de constituição

| Milestone | Checks que comprovam comportamento |
|---|---|
| MA-001 | Build headless, ausência de refs Product/Hub, teste standalone, VERSION único, seletividade CI |
| MA-002/003 | Round-trip/refs/limites/migrations; transações e undo; crash windows, backup e recovery; pacote portável |
| MA-004 | Reatividade incremental, ciclos/domínio/ref inválida, f(x)=x² e slope=2+dx, culturas/repetição |
| MA-005/006 | Transformações/câmera/hit testing; display list determinística; métricas/glyphs e alta resolução |
| MA-007/009 | Seek fora de ordem, interpolação/empates; edição textual/visual mesma revisão e conflito de rascunho |
| MA-008/010/011 | Fluxo no app e process death; vídeo real/metadata/decode/reprodução; tutorial e dispositivo exatos |

Medir tempo de compilação, avaliação de nós dirty, custo de frame p50/p95,
alocações/GC e memória da cena/alvos de export. Alvo inicial de preview 30 fps
(33.3 ms por frame) deve ser medido no aparelho/candidato, sem inventar hardware
ou alegar performance desktop como DEVICE. Export fora da UI; clipping de viewport
e caches explícitos; quality knobs não podem mudar o estado matemático.

Após aprovação A/B, MA-001 deve incluir schema/modelo mínimo usado em um teste
real, AGENTS local subordinado à raiz, SPEC/ROADMAP transferidos, VERSION e teste
de boundary. Preparar workflow seletivo e participação nos checks do integrador
para o Product real; esse control plane é crítico pela política da main. Não
alterar workflows/integrador nesta preparação e não introduzir CI falso.

## 6. Verificação desta preparação e limitações do ambiente

O workspace inicial não tinha checkout Git. `git clone` pelo proxy herdado falhou:
`Failed to connect to proxy port 8080`. Acesso read/write ao GitHub funciona pelo
conector, usado para árvore/arquivos/branches/Issues/PRs e Git data em branch própria.
Arquivos locais são cópia de auditoria com refs explícitas, não clone nem migração
de histórico; commit remoto preserva a árvore/histórico do repositório.

`dotnet run tests/consistency/Check.cs` foi tentado e retornou exit 127:
`dotnet: command not found`. Não declarar consistency local verde. Schemas e links
locais são conferidos separadamente; o PR dispara o consistency existente e o
integrador testa o estado combinado. Resultado de CI deve ser conferido antes
de relatar verde; não enfraquecer os checks por limitação deste ambiente.

## 7. Fontes normativas consultadas

MANIFEST integral, AGENTS integral, ARCHITECTURE, ecosystem.json, ROADMAP,
enforcement-matrix, decisions, comunicação/DoD/state-drift/multi-agent/política de
integração, schemas de decisions/handoff; índice e ADRs 0001/0006/0011/0015/0022;
product-model/distribution/local-first; adendos 0002/0009/0010/0011/0012/0015/0016/
0017; handoffs de constituição RPG, Fase 5 e P4-9; arquivos normativos dos Products
consultados para escopo/conflitos. Referências exatas estão no handoff MSP-001.
Estado histórico de documentação conceitual não substitui decisões posteriores
ou current.profile para distribuição; não reconciliar essas divergências fora
do escopo autorizado de autoria matemática.
