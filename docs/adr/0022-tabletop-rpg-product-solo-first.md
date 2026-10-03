# ADR-0022 — Tabletop RPG como Product C# solo-first

## Status

Aceito — DEC-0033 / ADD-0016.

## Contexto

O proprietário decidiu criar no Ecosystem uma plataforma completa de RPG de mesa
em que humanos e agentes de IA podem ocupar os papéis da mesa. Também decidiu que
o código novo é C#, a experiência nasce mobile-first e o MVP é solo; multiplayer
é requisito posterior. O MANIFEST exige independência entre Products, local-first
quando o domínio permite, autoridade clara de dados e promoção de abstrações
compartilhadas somente por evidência.

## Problema

É preciso introduzir o Product sem transformá-lo em módulo do Hub ou do Product
de IA, sem criar um backend antes de existir necessidade, e sem deixar um modelo
de linguagem controlar diretamente estado, regras ou informações secretas da
campanha.

## Opções

1. Criar `tabletop-rpg` como Product independente, com Core headless local em C#
   e MVP solo; integrações futuras entram por capabilities/contracts.
2. Implementar RPG como feature do `ecosystem-ai`, reutilizando sua identidade e
   lifecycle.
3. Começar pelo VTT multiplayer e servidor, deixando o modo solo/IA por cima da
   infraestrutura de rede.

## Decisão

Opção 1.

O componente possui ID estável `tabletop-rpg` e nome de exibição provisório
**Tabletop RPG**. O primeiro slice nasce em
`apps/tabletop-rpg/src/TabletopRpg.Core` e não referencia UI, rede, SDK de
modelo, Hub nem outro Product.

O Core estabelece quatro fronteiras:

1. **Campaign state:** verdade e eventos canônicos do jogo.
2. **Character perspective:** conhecimento e memória próprios, nunca projeção
   automática da verdade do mundo.
3. **Intent boundary:** humano ou agente solicita a mesma operação estruturada.
4. **Rules boundary:** resolução, dados e consequências pertencem ao motor.

O MVP não contém multiplayer. O produto final adicionará sincronização sobre as
operações/eventos do Core em vez de criar um segundo motor de jogo.

O uso futuro do Agent Runtime do Ecosystem é uma possibilidade, não dependência
assumida. Enquanto não houver capability/contrato aprovado e extração válida
segundo ADR-0011/NN-022, a necessidade de agentes permanece local ao Product.

## Consequências

- Campanhas solo podem evoluir sem servidor obrigatório.
- Player Agents podem receber visões filtradas sem acesso estrutural a segredos.
- Trocar modelo/provedor não muda as regras do jogo.
- UI mobile, persistência e multiplayer podem evoluir separadamente do domínio.
- O Product passa a ter lifecycle, versão, SPEC, ROADMAP e testes próprios.
- A entrada do novo Product em `ecosystem.json` é mudança arquitetural crítica e
  segue a autorização do integrador; isso não torna o Hub dependência.
- Nenhuma capability pública é criada neste ADR.

## Alternativas rejeitadas

A opção 2 mistura lifecycle e domínio de um jogo com o Product genérico de IA e
criaria dependência direta entre Products, contrariando a direção de composição.
A opção 3 adiciona servidor, sincronização e custo operacional antes de provar a
experiência que diferencia o produto: a mesa solo com agentes convincentes.

## Referências

ADD-0016; DEC-0033; MANIFEST NN-001/002/003/005/009/011/014/019/020/022/023;
ADR-0011; `apps/tabletop-rpg/SPEC.md`; `apps/tabletop-rpg/ROADMAP.md`;
RPG-001 / Issue #156.
