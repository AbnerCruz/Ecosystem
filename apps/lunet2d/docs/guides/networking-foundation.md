# Networking do Lunet — primeira fatia (F7, candidato)

> **Estado:** primeiro incremento experimental, não é multiplayer completo. Nenhum componente passa a ser `[x]` até testes, CI e validação em dispositivos. Jogos sem multiplayer continuam offline.

## Pesquisa e referências verificadas (29/10/2026)

| Referência | Aprendizado aplicado | Erro a evitar |
|---|---|---|
| [Godot — High-level multiplayer](https://docs.godotengine.org/en/4.6/tutorials/networking/high_level_multiplayer.html) | Transporte separado do gameplay, peers e autoridade | Tratar endereço local como acessível na internet; autenticação inexistente |
| [Mirror — General](https://mirror-networking.gitbook.io/docs/manual/general) | Host dedicado ou host-jogador; mensagens, estado e identidade em camadas | Replicar ações sem validação do host |
| [Unity — Netcode](https://docs.unity.com/en-us/multiplayer/netcode/netcode) | Sistemas para escalas e requisitos diferentes | Uma implementação única para turnos e ação em tempo real |
| [FishNet — Prediction](https://fish-networking.gitbook.io/docs/guides/features/prediction/creating-code/controlling-an-object) | Separar input, predição e reconciliação | Confiar em colisões ou dano relatados pelo cliente |
| [LiteNetLib 2](https://github.com/RevenantX/LiteNetLib) | UDP confiável selecionável, fragmentação, canais, NAT punching e simulador | Reimplementar protocolo UDP confiável sem justificar |
| [Gaffer — UDP vs TCP](https://gafferongames.com/post/udp_vs_tcp/) | TCP tem head-of-line blocking; usar UDP para snapshots realtime | Enviar posição e física a 60 FPS por TCP |
| [Android — Connecting to network](https://developer.android.com/develop/connectivity/network-ops/connecting) | APK autônomo precisa de `INTERNET`; não requer solicitação interativa | Editor com rede mas jogo exportado sem permissão |

## Escopo realmente implementado neste incremento

- `Lunet.Networking.TcpGameHost`: host com ID positivo único atribuído na conexão, eventos `Joined`, `Input`, `Left`; entrada bounded de 512 mensagens.
- `Lunet.Networking.TcpGameClient`: conexão e recepção de `State`, envio de intenção `Input`.
- `NetworkFrameCodec` interno: frames tipados e limitados a 8 KiB, proteção contra mensagens truncadas, inválidas e com tamanho forjado.
- Escrita serializada por peer para impedir frames sobrepostos, TCP_NODELAY, rede assíncrona; `TryReceive` para não bloquear o loop gráfico.
- O host rejeita frames de estado vindos de clientes; somente o próprio host publica o estado.
- Manifesto do APK standalone declara as permissões normais `INTERNET` e `ACCESS_NETWORK_STATE`.
- Testes de protocolo, dois peers, estado, isolamento de autoridade, mensagem malformada, excesso de tamanho.

**Não implementado:** UDP/LiteNetLib, transporte intercambiável `INetworkTransport`, criptografia/TLS, autenticação, login, descoberta LAN, NAT traversal, relay, lobby, matchmaking, previsão e reconciliação, objetos replicados, RPC, limite de taxa por cliente, timeout de sessão ociosa, servidores headless, saves multiplayer, teste em dois aparelhos, MMO e Bluetooth. Não comercializar ou apresentar como multiplayer online seguro.

## Uso inicial, em C# fora da renderização

```csharp
using System.Net;
using Lunet.Networking;

// Teste seguro na mesma máquina; host e cliente só aceitam loopback aqui.
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
await using var host = await TcpGameHost.StartAsync(IPAddress.Loopback, 0, cts.Token);
await using var client = await TcpGameClient.ConnectAsync("127.0.0.1", host.Port, cts.Token);

// O host recebe JOIN + INPUT, e decide o estado (nunca confia em dados enviados).
var joined = await host.ReceiveAsync(cts.Token);
await client.SendInputAsync(new byte[] { 1 }, cts.Token);
var input = await host.ReceiveAsync(cts.Token);
if (input.Kind == NetworkHostEventKind.Input)
    await host.BroadcastStateAsync(new byte[] { 100 }, cts.Token);

var state = await client.ReceiveAsync(cts.Token);
```

No jogo real, inicie a conexão de maneira assíncrona e leia os eventos com `TryReceive` no `Update`; não bloqueie o frame com `.Result`, `.Wait()` ou chamadas sincronizadas de rede. A thread do jogo é a única que atualiza o estado do gameplay; os callbacks da rede só enfileiram mensagens.

Para testes LAN entre dois aparelhos de confiança, o host pode vincular `IPAddress.Any` e o segundo cliente se conectar ao IP do host na rede local. **NÃO exponha este servidor diretamente à internet pública:** não há autenticação, criptografia nem proteção contra abuso no estágio atual.

## Passos seguintes e gates

1. Validar o projeto `Lunet.Networking` independente no compilador Roslyn e no APK standalone; introduzir `INetworkTransport` substituível.
2. Transport realtime LiteNetLib 2 e canais reliable/unreliable, simulação de loss/jitter/latência.
3. Replicação com autoridade do servidor, `NetworkIdentity`, snapshots, interpolação/predição/reconciliação, validação e limites.
4. Demo de dois clientes em Android com movimento e colisões sincronizados, testes automatizados e validação física.
5. Multiplayer local, split screen, LAN discovery, Wi-Fi Direct, Bluetooth, lobby, relay, online PvP/co-op, servidores headless, jogo assíncrono, escala persistente, segurança operacional.

A primeira fatia **não encerra a Fase 7**. CI verde não equivale a testes DEVICE.
