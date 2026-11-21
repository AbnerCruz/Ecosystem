using Avalonia.Controls;
using Avalonia.LogicalTree;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>CST-233: a PDF locked by a password is told as such, never as a broken or empty file.</summary>
    [Fact]
    public async Task ProtectedPdf_IsToldAsLockedByAPassword() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-pdfpw-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(vault);
            File.Copy(Path.Combine(AppContext.BaseDirectory, "fixtures", "protected.pdf"), Path.Combine(vault, "Contrato.pdf"));
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            try
            {
                await view.OpenVaultAsync();
                await Settle(window, view);
                await view.FilePreview.OpenAsync("Contrato.pdf");
                await Settle(window, view);
                var texts = string.Join(" | ", view.FilePreview.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t)));
                Assert.True(texts.Contains("senha", StringComparison.OrdinalIgnoreCase), texts);
                return true;
            }
            finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
