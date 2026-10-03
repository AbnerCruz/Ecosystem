# Tabletop RPG

Product do Ecosystem para RPG de mesa com humanos e agentes de IA como participantes de primeira classe.

Identidade estável: `tabletop-rpg`. O nome de exibição é provisório e pode mudar sem alterar o ID.

## Direção

- C# integral para o código novo.
- Mobile-first.
- MVP solo: um humano pode jogar com Mestre e jogadores controlados por agentes.
- Multiplayer é requisito do produto final, mas não pertence ao MVP.
- O motor do jogo, não o modelo, é autoridade de estado, regras e resultados.
- Estado real do mundo, conhecimento de cada personagem e memória subjetiva são domínios separados.
- Agentes recebem visão filtrada e devolvem intents; não recebem uma referência mutável ao estado.
- Local-first e Product independente: nenhum outro Product é requisito de funcionamento ou distribuição.

## Estrutura inicial

- `src/TabletopRpg.Core`: domínio e runtime headless, sem UI, rede ou SDK de provedor.
- `src/TabletopRpg.Persistence`: codec versionado e store local com save atômico, backup e recuperação.
- `tests/TabletopRpg.Core.Tests`: testes de domínio e persistência.
- `SPEC.md`: requisitos do Product.
- `ROADMAP.md`: fases e gates próprios.
- `docs/persistence-v1.md`: formato local/exportável e política de recuperação.

## Teste

```bash
cd apps/tabletop-rpg
dotnet test --project tests/TabletopRpg.Core.Tests
```

Requer .NET SDK 10+. O Product ainda não faz chamadas de rede nem contém provedor LLM real.
