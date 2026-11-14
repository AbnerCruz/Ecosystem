# Multiplayer no Lunet

`Lunet.Networking` faz jogos multijogador em tempo real: o **host** (um celular que também joga) ou um **servidor dedicado** decide o que acontece, e os **clientes** enviam o que o jogador quer fazer. Os jogadores podem estar na mesma rede Wi-Fi ou no mesmo aparelho. Para a internet pública ainda falta autenticação e relay; use um servidor seu.

## Peças

| Peça | Para quê |
|---|---|
| `UdpTransport("meu-jogo")` | Rede real (UDP; LiteNetLib por dentro). A chave impede que outro jogo entre por engano (não é senha). |
| `LoopbackNetwork` | Vários jogadores no mesmo aparelho (tela dividida, hot-seat em rede) e testes. Endereço `"loopback"`. |
| `NetworkSimulator` | Simula rede ruim: latência, variação (jitter), perda, duplicação e banda. Presets em `NetworkConditions.Presets`. |
| `NetworkSession` | Entrada, versão e jogo conferidos, lista de jogadores, mensagens, relógio, proteção contra flood. |
| `NetworkObject` + `NetworkVariable<T>` | Objetos que aparecem em todos os aparelhos e seguem o servidor. |
| `ClientPrediction` | O próprio jogador se move na hora, sem esperar a rede; corrige quando o servidor responde. |
| `LanDiscovery` | Lista as partidas na rede local, sem digitar IP. |

## Exemplo completo: todos se movem na mesma arena

```csharp
using System;
using System.Collections.Generic;
using System.Numerics;
using Lunet;
using Lunet.Graphics;
using Lunet.Networking;

// Mensagem do cliente: a intenção (direção), nunca a posição final.
public sealed class Movimento : INetSerializable
{
    public uint Sequencia; public Vector2 Direcao; public float Dt;
    public void Serialize(NetWriter w) { w.WriteVarUInt(Sequencia); w.Write(Direcao); w.Write(Dt); }
    public void Deserialize(NetReader r) { Sequencia = r.ReadVarUInt(); Direcao = r.ReadVector2(); Dt = r.ReadSingle(); }
}

public sealed class Avatar : NetworkObject
{
    public readonly NetworkVariable<Vector2> Posicao = new();
    public readonly NetworkVariable<uint> UltimoInput = new();
    protected override IEnumerable<NetworkVariableBase> Variables => [Posicao, UltimoInput];

    public static Vector2 Mover(Vector2 p, Vector2 direcao, double dt) =>
        Vector2.Clamp(p + direcao * (float)(140 * dt), Vector2.Zero, new Vector2(360, 640));
}

public sealed class Arena : Game
{
    private NetworkSession _rede = null!;
    private ClientPrediction<Vector2, Vector2>? _eu;
    private readonly Dictionary<int, Avatar> _avatares = new();
    private SpriteBatch _batch = null!;
    private Texture2D _bola = null!;
    public bool SouHost = true;                 // troque por um botão "Criar/Entrar"
    public string EnderecoDoHost = "192.168.0.10";

    protected override void Initialize()
    {
        var opcoes = new NetworkSessionOptions { GameId = "arena", PlayerName = "Ana" };
        _rede = SouHost
            ? NetworkSession.Host(new UdpTransport("arena"), 7777, opcoes)
            : NetworkSession.Join(new UdpTransport("arena"), EnderecoDoHost, 7777, opcoes);
        _rede.Register<Movimento>(1);                          // mesmo id em todos os aparelhos
        _rede.Replication.Register("avatar", () => new Avatar());

        if (_rede.IsServer)
        {
            // O servidor cria um avatar para cada jogador que entra…
            _rede.PeerJoined += p => _avatares[p.Id] = _rede.Replication.Spawn<Avatar>("avatar", p.Id, a => a.Posicao.Value = new(180, 320));
            _rede.PeerLeft += p => { if (_avatares.Remove(p.Id, out var a)) _rede.Replication.Despawn(a); };
            // …e aplica as intenções, validando o que chega.
            _rede.On<Movimento>((p, m) =>
            {
                if (!_avatares.TryGetValue(p.Id, out var a) || m.Sequencia <= a.UltimoInput.Value) return;
                var dir = m.Direcao.Length() > 1 ? Vector2.Normalize(m.Direcao) : m.Direcao;
                a.Posicao.Value = Avatar.Mover(a.Posicao.Value, dir, Math.Clamp(m.Dt, 0, 0.1f));
                a.UltimoInput.Value = m.Sequencia;
            });
            foreach (var p in _rede.Peers) _avatares[p.Id] = _rede.Replication.Spawn<Avatar>("avatar", p.Id, a => a.Posicao.Value = new(180, 320));
        }
    }

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        _bola = Texture2D.CreateCircle(GraphicsDevice, 24, Color.White);
    }

    protected override void Update(GameTime time)
    {
        _rede.Update();
        var meu = MeuAvatar();
        if (meu is null) return;
        _eu ??= new ClientPrediction<Vector2, Vector2>(meu.Posicao.Value, Avatar.Mover);
        var direcao = Input.TryGetPointer(out var toque) ? Vector2.Normalize(toque - _eu.Current) : Vector2.Zero;
        if (float.IsNaN(direcao.X)) direcao = Vector2.Zero;
        var seq = _eu.Apply(direcao, time.DeltaSeconds);
        _rede.SendToServer(new Movimento { Sequencia = seq, Direcao = direcao, Dt = (float)time.DeltaSeconds });
        _eu.Reconcile(meu.UltimoInput.Value, meu.Posicao.Value);
    }

    private Avatar? MeuAvatar()
    {
        foreach (var a in _rede.Replication.All<Avatar>()) if (a.IsOwner) return a;
        return null;
    }

    protected override void Draw(GameTime time)
    {
        GraphicsDevice.Clear(Color.Black);
        _batch.Begin();
        foreach (var a in _rede.Replication.All<Avatar>())
        {
            var p = a.IsOwner && _eu is not null ? _eu.Current : a.Posicao.Interpolated; // eu: previsto; outros: suavizados
            _batch.Draw(_bola, p - new Vector2(12), a.IsOwner ? Color.Yellow : Color.CornflowerBlue);
        }
        _batch.End();
    }
}
```

## Regras que evitam trapaça e dessincronização

- **O servidor decide.** Clientes mandam intenções (direção, botão); o servidor valida (limites, tempo) e muda as `NetworkVariable`.
- **Mesmos ids e mesma ordem.** `Register<T>(id)` e `Variables` iguais em todos os aparelhos; mude `ProtocolVersion` ao alterar mensagens — versões diferentes são recusadas com aviso.
- **Escolha a entrega:** `ReliableOrdered` para eventos (pontos, chat, entrar na sala), `Sequenced`/`Unreliable` para o que é reenviado sempre (snapshots já usam `Sequenced`).
- **Nada de rede bloqueando o quadro:** tudo acontece em `session.Update()`, no `Update` do jogo.

## Descobrir partidas na rede local

```csharp
var lan = new LanDiscovery();
// host:
lan.Announce(new LobbyInfo("arena", 1, "Sala da Ana", "", 7777, _rede.Peers.Count, 8));
// todos, no Update:
lan.Update();
foreach (var sala in lan.Lobbies) { /* mostrar sala.Name, sala.Players/sala.MaxPlayers; entrar em sala.Address:sala.Port */ }
var melhor = new DirectoryMatchmaker(lan).Find("arena", 1); // a sala aberta mais cheia
```

Algumas redes (visitantes, corporativas) bloqueiam broadcast; digitar o IP do host continua funcionando.

## Testar sem dois celulares

```csharp
var rede = new LoopbackNetwork();
var ruim = NetworkConditions.Presets[3].Conditions; // "3G ruim"
var host = NetworkSession.Host(new NetworkSimulator(rede.CreateTransport(), ruim), 0, opcoes);
var cliente = NetworkSession.Join(new NetworkSimulator(rede.CreateTransport(), ruim), "loopback", host.Port, opcoes);
```

## Servidor dedicado

`NetworkSession.Host(transporte, porta, opcoes, dedicated: true)` roda sem jogador local, com a mesma lógica do host. Para rodar o **mesmo projeto** como servidor num PC (Windows, Linux ou macOS) com .NET 10:

```bash
# na pasta do Lunet (código-fonte), apontando para a pasta do projeto exportado (ZIP descompactado)
dotnet run --project src/Lunet.Server -- /caminho/MeuJogo            # roda até Ctrl+C
dotnet run --project src/Lunet.Server -- /caminho/MeuJogo --check    # só compila
```

O servidor não tem tela: `Game.IsHeadless` é `true`. Use isso para decidir o papel:

```csharp
protected override void Initialize()
{
    var opcoes = new NetworkSessionOptions { GameId = "arena" };
    _rede = IsHeadless
        ? NetworkSession.Host(new UdpTransport("arena"), 7777, opcoes, dedicated: true)
        : NetworkSession.Join(new UdpTransport("arena"), "meu-servidor.local", 7777, opcoes);
}
```

Saves do servidor ficam em `.lunet/server-saves/` (ou `--saves pasta`).

## Limites atuais

Sem autenticação, criptografia, relay/NAT traversal e diretório online; sem reconexão automática, delta compression e compensação de lag. Não exponha um host diretamente à internet pública.

## Validação no aparelho (DEVICE)

1. Dois celulares na mesma rede Wi-Fi: um cria (host), o outro entra pelo IP (ou pela lista LAN). Os dois se veem se movendo, sem travadas.
2. Entre e saia com um terceiro aparelho: os avatares aparecem e somem para todos.
3. Use apps de versões diferentes (`ProtocolVersion`): o segundo é recusado com aviso.
4. Afaste um aparelho do roteador (sinal fraco): o próprio avatar continua respondendo na hora; os outros seguem suaves.
5. Exporte o jogo como APK e repita o passo 1 com o APK instalado.
