# AGENTS.md — math-authoring

Complementa `/AGENTS.md`; nunca o substitui.

## Fontes obrigatórias

Ler diretamente `/MANIFEST.md` (0.1), `/AGENTS.md`, `/ecosystem.json`, `SPEC.md`, `ROADMAP.md`, ADR-0029, ADR-0030 e DEC-0040.

## Invariantes locais

- **MA-I01 — Documento único.** `ProjectDocument` é a autoridade autoral; não existe estado visual paralelo.
- **MA-I02 — Matemática estruturada.** Relações são AST/nós tipados, não strings como autoridade.
- **MA-I03 — Tempo puro.** Estado em `t` independe do frame anterior.
- **MA-I04 — Renderer não decide matemática.** Ele apresenta snapshots avaliados.
- **MA-I05 — IA opcional.** Core/render/export não dependem de IA ou rede.
- **MA-I06 — Standalone.** Sem dependência direta de Hub, Urbe, Lunet2D ou Ecosystem AI.
- **MA-I07 — Local-first.** Projeto pertence ao usuário e evolui por schema versionado.
- **MA-I08 — Mobile-first profissional.** Core não conhece Activity/touch.
- **MA-I09 — Import é dado.** Projeto importado não executa código arbitrário.
- **MA-I10 — Sem extração especulativa.** Engine permanece no Product até segundo consumidor real + Extraction Review.

`MathAuthoring.Core` permanece headless, sem PackageReference e sem referência a outro Product.
