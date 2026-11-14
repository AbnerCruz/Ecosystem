using System.Text.Json;

namespace Lunet;

/// <summary>
/// Ordem de turnos para multijogador por vez: hot-seat (passar o aparelho) e partidas assíncronas em que o estado vai e
/// volta (arquivo, save, mensagem). Cada jogada aumenta <see cref="Version"/>; uma jogada feita sobre uma versão antiga
/// é recusada, então duas pessoas jogando ao mesmo tempo no mesmo turno nunca corrompem a partida.
/// </summary>
/// <example>
/// <code>
/// var turnos = new TurnOrder(new[] { "Ana", "Bia", "Caio" });
/// Log.Info("vez de " + turnos.Current);
/// turnos.EndTurn(turnos.Version);
/// turnos.Eliminate("Bia");
/// Save.Save("turnos", turnos.ToJson());
/// var depois = TurnOrder.FromJson(Save.Load("turnos", ""));
/// </code>
/// </example>
public sealed class TurnOrder
{
    private readonly List<string> _players;
    private readonly HashSet<string> _out = new(StringComparer.Ordinal);

    /// <summary>Cria a ordem com os jogadores na ordem em que jogam.</summary>
    /// <param name="players">Nomes únicos, de 1 a 64 jogadores.</param>
    public TurnOrder(IEnumerable<string> players)
    {
        _players = (players ?? throw new ArgumentNullException(nameof(players))).Select(p => (p ?? "").Trim()).ToList();
        if (_players.Count is 0 or > 64 || _players.Any(p => p.Length == 0) || _players.Distinct(StringComparer.Ordinal).Count() != _players.Count)
            throw new ArgumentException("Informe de 1 a 64 jogadores com nomes únicos.", nameof(players));
    }

    /// <summary>Todos os jogadores, na ordem.</summary>
    public IReadOnlyList<string> Players => _players;
    /// <summary>Jogadores ainda na partida.</summary>
    public IEnumerable<string> Active => _players.Where(p => !_out.Contains(p));
    /// <summary>Índice do jogador da vez em <see cref="Players"/>.</summary>
    public int CurrentIndex { get; private set; }
    /// <summary>Jogador da vez.</summary>
    public string Current => _players[CurrentIndex];
    /// <summary>Rodada atual (começa em 1 e sobe quando a vez volta ao primeiro jogador ativo).</summary>
    public int Round { get; private set; } = 1;
    /// <summary>Muda a cada jogada; use para detectar estados antigos.</summary>
    public int Version { get; private set; }
    /// <summary>Verdadeiro quando sobra um jogador ou nenhum.</summary>
    public bool IsOver => Active.Count() <= 1;

    /// <summary>Passa a vez ao próximo jogador ativo.</summary>
    /// <param name="expectedVersion">A <see cref="Version"/> que quem jogou viu; se mudou, a jogada é recusada.</param>
    /// <returns>Verdadeiro se a vez passou; falso se o estado já tinha mudado (jogada sobre versão antiga).</returns>
    public bool EndTurn(int expectedVersion)
    {
        if (expectedVersion != Version || IsOver) return false;
        Advance();
        Version++;
        return true;
    }

    private void Advance()
    {
        for (int step = 1; step <= _players.Count; step++)
        {
            int next = (CurrentIndex + step) % _players.Count;
            if (_out.Contains(_players[next])) continue;
            if (next <= CurrentIndex) Round++;
            CurrentIndex = next;
            return;
        }
    }

    /// <summary>Tira um jogador da partida; se era a vez dele, passa ao próximo.</summary>
    /// <param name="player">Nome do jogador.</param>
    public void Eliminate(string player)
    {
        if (!_players.Contains(player)) throw new ArgumentException("Jogador desconhecido: " + player, nameof(player));
        if (!_out.Add(player)) return;
        if (Current == player && Active.Any()) Advance();
        Version++;
    }

    /// <summary>Estado em JSON para salvar ou enviar.</summary>
    /// <returns>Texto JSON.</returns>
    public string ToJson() => JsonSerializer.Serialize(new Snapshot(_players, _out.ToList(), CurrentIndex, Round, Version));

    /// <summary>Recria a ordem a partir de <see cref="ToJson"/>, validando o conteúdo.</summary>
    /// <param name="json">Texto de <see cref="ToJson"/>.</param>
    /// <returns>A ordem de turnos.</returns>
    public static TurnOrder FromJson(string json)
    {
        Snapshot? s;
        try { s = JsonSerializer.Deserialize<Snapshot>(json); }
        catch (JsonException ex) { throw new InvalidDataException("Turnos inválidos: " + ex.Message, ex); }
        if (s is null || s.Players is null) throw new InvalidDataException("Turnos vazios.");
        var order = new TurnOrder(s.Players);
        if (s.Current < 0 || s.Current >= order._players.Count || s.Round < 1 || s.Version < 0 || (s.Out ?? []).Any(p => !order._players.Contains(p)))
            throw new InvalidDataException("Turnos inconsistentes.");
        foreach (var p in s.Out ?? []) order._out.Add(p);
        order.CurrentIndex = s.Current;
        order.Round = s.Round;
        order.Version = s.Version;
        return order;
    }

    private sealed record Snapshot(List<string> Players, List<string>? Out, int Current, int Round, int Version);
}
