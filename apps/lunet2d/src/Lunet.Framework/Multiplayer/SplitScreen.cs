using System.Numerics;
using Lunet.Input;

namespace Lunet;

/// <summary>
/// Multijogador local no mesmo aparelho: divide a tela em regiões por jogador e atribui cada toque ao jogador da região
/// onde ele começou. Desenhe cada jogador com <c>batch.Begin(camera, regiao)</c>.
/// </summary>
/// <example>
/// <code>
/// var regioes = SplitScreen.Layout(2, GraphicsDevice.VirtualWidth, GraphicsDevice.VirtualHeight);
/// var batch = new Lunet.Graphics.SpriteBatch(GraphicsDevice);
/// var camera = new Lunet.Graphics.Camera2D();
/// for (int jogador = 0; jogador &lt; regioes.Length; jogador++)
/// {
///     batch.Begin(camera, regioes[jogador]);
///     batch.End();
///     foreach (var toque in SplitScreen.TouchesOf(jogador, Input.Touches, regioes)) Log.Info("jogador " + jogador + ": " + toque.Position);
/// }
/// </code>
/// </example>
public static class SplitScreen
{
    /// <summary>Máximo de jogadores numa tela.</summary>
    public const int MaxPlayers = 4;

    /// <summary>
    /// Regiões para <paramref name="players"/> jogadores (1 a 4): duas lado a lado na horizontal e empilhadas na vertical,
    /// três com o primeiro maior, quatro em grade. Ficam separadas por <paramref name="gap"/>.
    /// </summary>
    /// <param name="players">Quantidade de jogadores, de 1 a 4.</param>
    /// <param name="width">Largura da tela virtual.</param>
    /// <param name="height">Altura da tela virtual.</param>
    /// <param name="gap">Espaço entre as regiões.</param>
    /// <returns>Uma região por jogador, na ordem dos jogadores.</returns>
    public static RectangleF[] Layout(int players, float width, float height, float gap = 2)
    {
        if (players is < 1 or > MaxPlayers) throw new ArgumentOutOfRangeException(nameof(players), "De 1 a 4 jogadores.");
        if (!(width > 0 && height > 0) || gap < 0) throw new ArgumentOutOfRangeException(nameof(width), "Tamanho de tela inválido.");
        bool wide = width >= height;
        float half = gap / 2;
        RectangleF Box(float x, float y, float w, float h) => new(x, y, w, h);
        return players switch
        {
            1 => [Box(0, 0, width, height)],
            2 => wide
                ? [Box(0, 0, width / 2 - half, height), Box(width / 2 + half, 0, width / 2 - half, height)]
                : [Box(0, 0, width, height / 2 - half), Box(0, height / 2 + half, width, height / 2 - half)],
            3 => wide
                ? [Box(0, 0, width / 2 - half, height), Box(width / 2 + half, 0, width / 2 - half, height / 2 - half), Box(width / 2 + half, height / 2 + half, width / 2 - half, height / 2 - half)]
                : [Box(0, 0, width, height / 2 - half), Box(0, height / 2 + half, width / 2 - half, height / 2 - half), Box(width / 2 + half, height / 2 + half, width / 2 - half, height / 2 - half)],
            _ =>
            [
                Box(0, 0, width / 2 - half, height / 2 - half), Box(width / 2 + half, 0, width / 2 - half, height / 2 - half),
                Box(0, height / 2 + half, width / 2 - half, height / 2 - half), Box(width / 2 + half, height / 2 + half, width / 2 - half, height / 2 - half),
            ],
        };
    }

    /// <summary>O jogador dono do ponto (índice da região que o contém), ou -1 no espaço entre regiões.</summary>
    /// <param name="point">Ponto em coordenadas virtuais.</param>
    /// <param name="regions">Regiões de <see cref="Layout"/>.</param>
    /// <returns>Índice do jogador ou -1.</returns>
    public static int PlayerAt(Vector2 point, ReadOnlySpan<RectangleF> regions)
    {
        for (int i = 0; i < regions.Length; i++)
            if (regions[i].Contains(point)) return i;
        return -1;
    }

    /// <summary>Os toques que estão na região do jogador.</summary>
    /// <param name="player">Índice do jogador.</param>
    /// <param name="touches">Toques do quadro (<c>Input.Touches</c>).</param>
    /// <param name="regions">Regiões de <see cref="Layout"/>.</param>
    /// <returns>Os toques do jogador, em ordem.</returns>
    public static List<TouchPoint> TouchesOf(int player, ReadOnlySpan<TouchPoint> touches, ReadOnlySpan<RectangleF> regions)
    {
        var result = new List<TouchPoint>();
        foreach (var touch in touches)
            if (PlayerAt(touch.Position, regions) == player) result.Add(touch);
        return result;
    }

    /// <summary>Converte um ponto da tela para coordenadas locais da região (0,0 no canto da região).</summary>
    /// <param name="point">Ponto em coordenadas virtuais da tela.</param>
    /// <param name="region">Região do jogador.</param>
    /// <returns>O ponto relativo à região.</returns>
    public static Vector2 ToRegion(Vector2 point, RectangleF region) => point - region.Position;
}
