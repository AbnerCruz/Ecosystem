using System.Buffers.Binary;
using System.Text.RegularExpressions;

namespace Urbe.Core.Study;

/// <summary>
/// #956: a two-voice podcast (NotebookLM's Audio Overview, adapted). The script is an ordinary note, editable like any
/// other, with one turn per paragraph: <c>**Ana:** fala</c>. Each turn is spoken by the device's voice (the first
/// speaker gets voice 0, the second voice 1) and the parts are joined into one WAV.
/// </summary>
public static partial class Podcast
{
    public const string Suffix = ".podcast.md";

    public sealed record Turn(string Speaker, int Voice, string Text);

    public static IReadOnlyList<Turn> Turns(string markdown)
    {
        var speakers = new List<string>();
        var turns = new List<Turn>();
        foreach (Match m in TurnLine().Matches(AiAuthorship.Strip(markdown)))
        {
            var who = m.Groups[1].Value.Trim();
            var text = m.Groups[2].Value.Trim();
            if (text.Length == 0) continue;
            int voice = speakers.FindIndex(s => s.Equals(who, StringComparison.OrdinalIgnoreCase));
            if (voice < 0) { speakers.Add(who); voice = speakers.Count - 1; }
            turns.Add(new Turn(who, voice % 2, text));
        }
        return turns;
    }

    /// <summary>The script note a session's podcast starts from: the two hosts, the sources, and the request to the agent.</summary>
    public static string Draft(StudySession session, IEnumerable<string> sourcePaths) =>
        "# Podcast — " + session.Title + "\n\n" +
        "Duas vozes conversam sobre as fontes da sessão. Cada fala é um parágrafo `**Nome:** texto`; edite à vontade e use **Gerar áudio do podcast**.\n\n" +
        "Fontes: " + string.Join(", ", sourcePaths.Select(p => "[[" + Path.GetFileNameWithoutExtension(p) + "]]")) + "\n\n" +
        "&&: Escreva aqui um diálogo de podcast em português entre **Ana** e **Beto** sobre essas fontes" +
        (session.Objectives.Count > 0 ? " (objetivo: " + string.Join("; ", session.Objectives) + ")" : "") +
        ", só com o que está nelas, 12 a 20 falas, cada uma um parágrafo no formato **Ana:** texto.\n";

    /// <summary>Joins PCM WAV parts of the same format into one file, with a short pause between turns.</summary>
    public static void JoinWav(IReadOnlyList<string> parts, string target, double pauseSeconds = 0.35)
    {
        if (parts.Count == 0) throw new ArgumentException("Nenhuma fala.", nameof(parts));
        byte[]? format = null;
        using var output = File.Create(target);
        output.Write(new byte[44]);
        long data = 0;
        foreach (var part in parts)
        {
            var bytes = File.ReadAllBytes(part);
            var (fmt, start, length) = Chunks(bytes);
            if (format is null) format = fmt;
            else if (!format.AsSpan().SequenceEqual(fmt)) throw new InvalidDataException("As falas foram geradas em formatos diferentes.");
            if (data > 0)
            {
                int byteRate = BinaryPrimitives.ReadInt32LittleEndian(format.AsSpan(8)), align = BinaryPrimitives.ReadInt16LittleEndian(format.AsSpan(12));
                var pause = new byte[(int)(byteRate * pauseSeconds) / align * align];
                output.Write(pause);
                data += pause.Length;
            }
            output.Write(bytes, start, length);
            data += length;
        }
        output.Position = 0;
        var header = new byte[44];
        "RIFF"u8.CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), (int)(36 + data));
        "WAVEfmt "u8.CopyTo(header.AsSpan(8));
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(16), 16);
        format!.AsSpan(0, 16).CopyTo(header.AsSpan(20));
        "data"u8.CopyTo(header.AsSpan(36));
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(40), (int)data);
        output.Write(header);
    }

    private static (byte[] Format, int Start, int Length) Chunks(byte[] wav)
    {
        if (wav.Length < 12 || !wav.AsSpan(0, 4).SequenceEqual("RIFF"u8) || !wav.AsSpan(8, 4).SequenceEqual("WAVE"u8))
            throw new InvalidDataException("A fala não é um WAV.");
        byte[]? fmt = null;
        for (int at = 12; at + 8 <= wav.Length;)
        {
            int size = BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(at + 4));
            var id = wav.AsSpan(at, 4);
            if (id.SequenceEqual("fmt "u8)) fmt = wav.AsSpan(at + 8, 16).ToArray();
            else if (id.SequenceEqual("data"u8) && fmt is not null)
                return (fmt, at + 8, Math.Min(size < 0 ? int.MaxValue : size, wav.Length - at - 8));
            at += 8 + size + (size & 1);
        }
        throw new InvalidDataException("A fala não tem áudio.");
    }

    [GeneratedRegex(@"^\s*\*\*([^*\n:]{1,40}):\*\*[ \t]*(.+)$", RegexOptions.Multiline)]
    private static partial Regex TurnLine();
}
