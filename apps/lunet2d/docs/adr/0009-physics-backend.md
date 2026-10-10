# ADR 0009 — Física completa com Box2D.NET gerenciado

- Status: proposta em implementação (Fase 4)
- Fonte: SPEC §7, ROADMAP Fase 4 (física completa)
- Issue: #477

## Contexto

O Lunet requer simulação de corpos rígidos, contatos, joints e raycast, e não apenas AABB/SAT. O runtime é C#/.NET 10 com Android offline. A camada geométrica própria não substitui um solver maduro.

## Decisão

Usar **Box2D.NET 3.1.654** (port C# MIT de Box2D 3.x) como dependência de `Lunet.Framework`, sem P/Invoke nem biblioteca nativa Android. O Lunet expõe somente `Lunet.Physics` (`PhysicsWorld`, `RigidBody2D`, `Collider2D`, `Fixture2D`, `Joint2D`, `Contact`, `RaycastHit`). Tipos `B2*` jamais aparecem na API pública.

Primeiro incremento: world/step, gravity, corpos estáticos/cinemáticos/dinâmicos, círculo/caixa, fixture, material, sensor, impulso, junta de distância, eventos de início de contato e raycast closest. Entregas complementares exigidas para fechar o item: juntas avançadas (revoluta/prismática), múltiplas geometrias e filtros, sensores e contatos de fim, documentação/validação de performance e demonstração física real no Laboratório/Android.

## Consequências

- Licença MIT do backend deve ser preservada; conferir dependências transitivas e restore no CI.
- Etapa usa metros/unidades físicas com `Step(1/60)`; o usuário converte pixels para metros.
- Todas as alterações ficam no Framework e seus testes; jogos existentes não mudam.
- Não marcar Física completa [x] sem os complementos e validação DEVICE.
