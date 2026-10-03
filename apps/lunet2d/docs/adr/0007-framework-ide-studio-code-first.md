# ADR 0007 — Lunet Framework / IDE / Studio code-first

## Status

Aceito — direção explicitamente instruída pelo proprietário no documento
`lunet-framework-ide-studio-continuar-desenvolvimento.md`, entregue para execução
em 2026-10-03. Registro canônico da direção em SPEC §1/§13/§16/§37; tarefa
LUNET-302, Issue #149. Não inventa resposta pelo portal nem nova decisão pendente.

## Contexto

Já existem Framework, Editor, Compiler, Runtime e Android, com testes de
boundary. Ferramentas visuais futuras poderiam inverter a autoridade e esconder
lógica. O proprietário determinou um Product Lunet com Framework/IDE/Studio.

## Problema

Manter desenvolvimento integral via C# e ferramentas visuais opcionais sem
fragmentar projetos, duplicar infraestrutura nem transformar a IDE em Inspector.

## Opções

1. Um Product, três camadas, source como autoridade.
2. Três aplicativos/projetos de jogo distintos.
3. Editor visual obrigatório, lógica e metadata privadas.

## Decisão

Opção 1. CODE IS THE SOURCE OF TRUTH; NO HIDDEN GAME LOGIC;
EVERY VISUAL ACTION MUST HAVE AN INSPECTABLE SOURCE REPRESENTATION.
Framework independente de Editor, Android, Studio, IA e control plane; Runtime executa,
Compiler compila, Editor fornece IDE, Android compõe. Studio é opcional, edita
os mesmos arquivos e não supera a autoridade do source. Comportamento em C#;
dados declarativos em texto aberto/documentado/determinístico. Assets binários
permitidos. Scene2D/ECS/nodes opcionais. SDK público adequado comum a ferramentas
oficiais/plugins. Packages estendem jogo; plugins estendem Lunet.

## Consequências

Nenhum rename massivo ou csproj vazio. Boundary `Lunet.Studio` nasce junto da
primeira ferramenta real; Framework/Runtime/Compiler nunca o referenciam.
Roundtrip visual/fonte quando razoável, preservando código não suportado;
limitações explícitas. Agent Workspace consome Runtime compartilhado Ecosystem.
Planejamento futuro permanece classificado e não altera o gate da Fase 3.

## Alternativas rejeitadas

Apps independentes fragmentam o mesmo projeto; visual obrigatório aprisiona
lógica e prejudica diffs, manutenção e trabalho offline. `LunetEngine` como nome
do visual confunde runtime com ferramenta.

## Enforcement

ArchitectureTests existentes protegem independência de Framework e Compiler,
Runtime → Framework e Editor → Compiler. Teste adicional verifica dependências
transitivas contra Studio/PluginHost/Editor SDK; valerá também quando existirem.
Cada Studio exige contrato textual, roundtrip e compilação sem Studio na sua
entrega real. SPEC §13 define as seis perguntas de inspeção/versionamento/edição.
