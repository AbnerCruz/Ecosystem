using Avalonia.Controls;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>#956: a note with sections becomes Nome.infografico.svg and .png beside it, and the PNG opens.</summary>
    [Fact]
    public async Task Infographic_NoteBecomesSvgAndPngBesideIt() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-infographic-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Água.md"), "# A água no Brasil\n\nO país tem 12% da água doce do mundo.\n\n## Rios\n\nO Amazonas é o maior rio do mundo em volume. Ele corre por 6.992 km.\n\n## Uso\n\nA irrigação usa 50% da água retirada. O resto vai para cidades e indústrias.\n\n## Cuidados\n\nPreservar nascentes e matas ciliares protege os rios.\n");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            Assert.Equal("Água.infografico.png", await view.MakeInfographicAsync("Água.md"));
            Assert.StartsWith("<svg", File.ReadAllText(Path.Combine(vault, "Água.infografico.svg")));
            var png = File.ReadAllBytes(Path.Combine(vault, "Água.infografico.png"));
            Assert.Equal(0x89, png[0]);
            if (Environment.GetEnvironmentVariable("URBE_CAPTURE") is { Length: > 0 } capture) File.WriteAllBytes(capture, png);
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
