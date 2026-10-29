using System.Text;
using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// CST-006: before a file is overwritten, the storages keep its previous bytes next to it in
/// "<c>name.urbe-recovery-&lt;32 hex&gt;</c>" and remove that copy once the new text is verified. The app closing (killed,
/// battery, crash) half-way leaves the copy, and the file may be empty or cut. At the next opening each copy is
/// resolved here, never losing a text: what the person sees decides nothing silently.
/// </summary>
public static partial class InterruptedWrites
{
    public const string Marker = ".urbe-recovery-";

    public enum Resolution
    {
        /// <summary>The file is as the copy: the write never started. The copy goes.</summary>
        Unchanged,
        /// <summary>The file is gone or empty (cut at the start): the copy's text goes back into it.</summary>
        Restore,
        /// <summary>Both hold different texts: the copy becomes a note next to it (<see cref="Recovery.KeepAs"/>), so the person compares them.</summary>
        KeepBoth
    }

    public sealed record Recovery(string Copy, string Target, string Text, Resolution Resolution, string? KeepAs);

    [GeneratedRegex(@"^(?<target>.+)\.urbe-recovery-[0-9a-f]{32}$")]
    private static partial Regex CopyName();

    public static bool IsCopy(string path) => CopyName().IsMatch(path);

    public static IReadOnlyList<Recovery> Plan(IReadOnlyDictionary<string, VaultFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var taken = new HashSet<string>(files.Keys, StringComparer.OrdinalIgnoreCase);
        var plan = new List<Recovery>();
        foreach (var (path, copy) in files.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            if (CopyName().Match(path) is not { Success: true } match) continue;
            var target = match.Groups["target"].Value;
            // the app's own files (.urbe/) are not the person's notes: their readers keep the vault protected when one is cut
            if (target.StartsWith(".urbe/", StringComparison.Ordinal) || !VaultTextFiles.IsText(target)) continue;
            var text = Decode(copy.Bytes.Span);
            files.TryGetValue(target, out var current);
            if (current is not null && current.Bytes.Span.SequenceEqual(copy.Bytes.Span))
                plan.Add(new Recovery(path, target, text, Resolution.Unchanged, null));
            else if (current is null || current.Bytes.Length == 0)
                plan.Add(new Recovery(path, target, text, Resolution.Restore, null));
            else
            {
                var keep = Free(target, taken);
                taken.Add(keep);
                plan.Add(new Recovery(path, target, text, Resolution.KeepBoth, keep));
            }
        }
        return plan;
    }

    /// <summary>"Pasta/Nota (antes da gravação interrompida).md", free in the vault.</summary>
    private static string Free(string target, HashSet<string> taken)
    {
        int slash = target.LastIndexOf('/');
        var folder = slash >= 0 ? target[..(slash + 1)] : "";
        var file = target[(slash + 1)..];
        int dot = file.LastIndexOf('.');
        var (name, extension) = dot > 0 ? (file[..dot], file[dot..]) : (file, "");
        var candidate = folder + name + " (antes da gravação interrompida)" + extension;
        for (int n = 2; taken.Contains(candidate); n++) candidate = folder + name + " (antes da gravação interrompida " + n + ")" + extension;
        return candidate;
    }

    private static string Decode(ReadOnlySpan<byte> bytes) => Encoding.UTF8.GetString(bytes).TrimStart('﻿');
}
