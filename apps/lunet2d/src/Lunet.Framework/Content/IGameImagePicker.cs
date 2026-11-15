namespace Lunet.Content;

/// <summary>Escolha de imagens dentro do jogo fornecida pelo host, sem permissões gerais de armazenamento.</summary>
/// <remarks>O seletor não bloqueia o jogo; consulte TryReceive em Update para obter a imagem.</remarks>
/// <example>
/// <code>
/// if (Services.TryGet&lt;IGameImagePicker&gt;(out var picker) &amp;&amp; picker.CanPick)
///     picker.Pick();
/// </code>
/// </example>
public interface IGameImagePicker
{
    /// <summary>Indica se o host oferece um seletor nativo de imagens.</summary>
    bool CanPick { get; }

    /// <summary>Abre o seletor. Chamadas repetidas enquanto já estiver aberto são ignoradas.</summary>
    void Pick();

    /// <summary>Consome um resultado pendente, incluindo cancelamento e falha.</summary>
    /// <param name="result">Resultado selecionado, ou cancelamento/falha com Rgba nulo.</param>
    /// <returns>Verdadeiro quando existe um resultado novo para consumir.</returns>
    bool TryReceive(out PickedGameImage result);
}

/// <summary>Imagem RGBA decodificada e limitada; crie a textura na thread gráfica usando Texture2D.FromPixels.</summary>
/// <param name="Width">Largura, em pixels.</param>
/// <param name="Height">Altura, em pixels.</param>
/// <param name="Rgba">Bytes R,G,B,A em ordem de linhas; nulo em cancelamento/falha.</param>
/// <param name="Error">Erro de leitura, ou nulo para sucesso/cancelamento.</param>
/// <example>
/// <code>
/// var result = new PickedGameImage(1, 1, new byte[] { 255, 255, 255, 255 });
/// </code>
/// </example>
public sealed record PickedGameImage(int Width, int Height, byte[]? Rgba, string? Error = null);
