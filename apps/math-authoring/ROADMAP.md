# ROADMAP — Autoria matemática temporal

> Autoridade de escopo/IDs do Product `math-authoring`; estado vivo em Issues e evidências em handoffs.
> DEC-0040 alternativa A foi aplicada: o ID técnico permanente é `math-authoring`.
> O nome público permanece em aberto. Este roadmap não cria fase global nem altera o roadmap de outros Products.

## Preparação e constituição

- [x] MSP-001 — Preparação de constituição concluída: auditoria, SPEC/plano e ADRs foram integrados; DEC-0040 foi decidida pelo proprietário em favor de `math-authoring`. Issue #260 acompanha o fechamento da constituição efetiva.
- [~] MA-001 — Aplicar a alternativa escolhida, aceitar ADRs na forma legitimada,
  migrar SPEC/ROADMAP para `apps/<id>`, criar AGENTS local, VERSION, Core C# mínimo
  com build/test reais, registro e CI seletivo. Gate: boundaries/testes standalone
  e consistency; PR de efeito arquitetural/control plane segue aprovação crítica
  do integrador, sem DEC adicional para a mesma direção. Sem pasta vazia como DoD.

## Documento e persistência

- [ ] MA-002 — Documento tipado, schema v1, parser/serializer/formatter,
  validação/referências/limites, comandos/transações/undo/redo, migration hooks.
  Gate: round-trip preserva semântica e IDs; parse inválido não altera revisão;
  versão futura falha sem sobrescrever dados; mudanças textuais/visuais usam
  as mesmas transações. Depende de MA-001 e ADR-0030 legitimado.
- [ ] MA-003 — Arquivo portável, assets por ID/hash, save/autosave, backup e
  recovery. Gate: falhas injetadas antes/depois de replace preservam ao menos
  uma revisão válida; reopen completo, migrations, import/export, traversal,
  tamanho e corrupção testados. Efeito em dados do usuário escala como crítico.

## Engine, Stage e tempo

- [ ] MA-004 — Math AST e grafo de dependências: funções 1D, variáveis, pontos,
  retas/segmentos e diagnósticos. Gate: Δx atualiza somente dependentes, topologia
  e ciclos testados; resultados iguais em repetição e culturas diferentes;
  dx=0 retorna domínio inválido. Depende de MA-002.
- [ ] MA-005 — Scene graph 2D e display list: transformações, grupos/z-order,
  plano/eixos/plot/pontos/linhas/shapes, texto e câmera. Gate: dois projetos
  distintos renderizam pela mesma engine; world↔screen e hit tests corretos;
  estrutura determinística sem golden frágil de antialiasing. Depende de MA-004.
- [ ] MA-006 — Layout de fórmulas estruturado e nítido. Gate: fração/exponente,
  tokens/baselines/medidas offline e export alta resolução; análise de licença,
  Android, tamanho e performance antes de qualquer biblioteca. Depende de MA-005.
- [ ] MA-007 — Timeline pura: tracks/clips/keyframes, easing, variáveis,
  propriedades/câmera, composição entre cenas. Gate: avaliar t fora de ordem
  equivale a avaliar sequencialmente; seek, bordas e conflitos de tracks testados.
  Depende de MA-004/005; não exige áudio nesta fase.

## Produto autoral

- [ ] MA-008 — Editor Android C# mobile-first: biblioteca/projeto, Stage,
  hierarquia, inspector, timeline, seleção/handles e pan/pinch. Gate: professor
  monta eixos/função/pontos/secante e anima variável sem programação; play/seek,
  undo/redo e save usam o documento real. CI + DEVICE com build exata.
  Depende de MA-003/005/006/007; identidade de pacote/assinatura/canal formalizada
  antes de publicação. Desktop segue o modelo adaptável, sem novo runtime global.
- [ ] MA-009 — Visão textual com erros/source spans e feedback no Stage.
  Gate: edits nas duas visões comparam a mesma revisão canônica; conflito de
  rascunho é explícito; texto inválido conserva a última cena válida; undo/redo
  cobre as duas origens. Depende de MA-002/008.

## Saída real e prova do produto

- [ ] MA-010 — Export de PNG e MP4 H.264 em Android. Render offscreen da revisão
  congelada, MediaCodec/MediaMuxer avaliados primeiro, progress/cancel, perfis,
  descarte de saída parcial e recuperação. Gate: arquivo real 1280×720/30 fps
  ou perfil equivalente justificado, decode/PTS/metadados, duração/frames e
  reprodução/seek Android verificados. Não marcar pronto só pelo container.
  Depende de MA-005/006/007/008; estratégia técnica em IMPLEMENTATION §4.
- [ ] MA-011 — Projeto Δx → 0 autoral real + tutorial/regressão + evidência
  completa. Gate: sete passos da SPEC, projeto reaberto e MP4/Android validados,
  CI/consistency verdes e PR entregue ao integrador. Medir MA-REQ-014 no aparelho
  identificado; nenhum epsilon oculto ou hardcode. Depende de MA-009/010.

## Como executar os milestones

Cada item gera Issue própria ao iniciar e PR pequeno consistente. Separar dados,
engine, Stage, editor e encoder permite revisão incremental; exemplos e integração
devem acompanhar cada módulo para evitar semanas de classes sem prova de uso.
Não esperar MA-011 para tentar salvar/reabrir ou produzir o primeiro MP4 técnico.
MA-010 pode ter um spike local assim que MA-005/007 permitirem frames offscreen;
o encoder inicial é genérico e a validação final permanece no item de export.

Teste estrutural/determinístico, positivo e negativo quando há risco real,
precede DEVICE. Nenhum `[x]` sem evidência e integração. Rotina autorizada avança
autonomamente; só decisão nova, aprovação crítica de efeito ou DEVICE real
interrompe. Não criar perguntas informais para substituir o portal.

## Evoluções posteriores (sem autorização de implementação nesta preparação)

Correspondência semântica de termos/CAS mínimo; geometria; vetores/matrizes;
datasets/estatística; áudio/narração/legendas; Presenter; experiências interativas;
templates; extensões C# com trust model próprio; IA opcional por capability válida;
3D e colaboração somente com justificativa e roadmap específico.
