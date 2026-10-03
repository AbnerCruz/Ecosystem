# Ecosystem AI

AI Product do ecossistema: **consumidor** do Agent Runtime. Nasce com o runtime mínimo (slice **R1**) dentro dele, com **fronteira de extração** explícita; qualquer promoção para componente compartilhado passa por Extraction Review (NN-022).

Autoridades: [ADR-0017](../../docs/adr/0017-agent-runtime-execution-runtime-e-organizacoes.md) (arquitetura), [`docs/architecture/agent-runtime.md`](../../docs/architecture/agent-runtime.md) (plano e testes de enforcement), `ecosystem.json` (identidade, versão e permissões) e o ROADMAP (escopo, item P6-2). Este README não replica fase nem status.

## O que existe (R1)

| Projeto | Responsabilidade |
|---|---|
| `src/AgentRuntime.Core` | Primitivas e o laço do agente: Context, orçamento (ledger), ferramentas como capabilities, log de eventos, estado de tarefa com verificação, retomada. **Sem dependência alguma** além da biblioteca-base. |
| `src/AgentRuntime.Testing` | Implementações determinísticas das portas, para teste: provedores Scripted e Recorded, relógio, log (com injeção de falha), aprovadores. |
| `src/AgentRuntime.Tools.Files` | Tool de exemplo: `files.read`, `files.write` e `files.delete` presas à raiz do Context, mais o verificador de arquivos. |
| `tests/AgentRuntime.Tests` | Suíte de enforcement (T-1…T-15 do plano), incluindo testes de arquitetura lidos dos metadados do IL. |

## Garantias (cada uma tem teste)

- **Deny-by-default.** Ferramentas disponíveis = Host ∩ organização ∩ projeto ∩ agente; sem `agent.act` nenhuma. Fora do escopo de Context, a ferramenta não existe para o run.
- **Orçamento é primeira classe.** Toda chamada reserva antes e concilia depois; sem limite configurado, não gasta; estouro bloqueia, nunca é absorvido.
- **Saída ≠ sucesso.** Uma tarefa só chega a `Completed` com verificação independente aprovada e com evidência; critério que o verificador não entende falha.
- **Operação destrutiva exige aprovação.** Sem aprovador, o runtime escala em vez de agir.
- **Observável e auditável.** Todo evento responde quem, por quê, ferramenta, modelo, custo, contexto, resultado, verificação e aprovação. Não existe campo de raciocínio livre; o raciocínio privado do modelo é descartado.
- **Segredos nunca vazam.** Valores registrados são redigidos de eventos, artefatos e do que o modelo vê.
- **Retomável.** O estado do run é reconstruído do log; um log adulterado é recusado.
- **Laços têm limite.** Falhas equivalentes, repetição sem progresso, tentativas de verificação e passos têm teto.
- **Ocioso custa zero.** O host é dirigido por eventos: sem timer, sem espera ocupada, sem chamada a provedor.

## Fronteira de extração

O Core não referencia pacote nem outro projeto, e os testes de arquitetura (T-13) falham se ele passar a conhecer rede, processo, arquivo, timer, relógio direto, UI, SDK de provedor, outro Product ou o Control Plane do ecossistema. É essa fronteira que permite uma futura extração sem reescrita. Enquanto houver um único consumidor real, o código fica aqui.

## Como testar

```bash
dotnet test --project apps/ecosystem-ai/tests/AgentRuntime.Tests
```

Requer .NET SDK 10+. Não há provedor de modelo real, rede nem segredo: o R1 prova o runtime com provedores determinísticos.

## Coordenação local candidata

A prova de equipes, isolamento e integração está descrita em [docs-r2.md](docs-r2.md).
Autoridade do escopo: P6-3 no ROADMAP; contrato candidato: ADR-0020.
