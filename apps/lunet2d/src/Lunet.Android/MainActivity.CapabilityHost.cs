using Android.App;
using Android.Widget;
using System.Text.Json;
using Lunet.Core.Capabilities;

namespace Lunet.Android;

public sealed partial class MainActivity
{
    private void ShowConnections()
    {
        if (_project is null)
        {
            new AlertDialog.Builder(this)
                .SetTitle("Connections")
                .SetMessage("Abra um projeto para ver as capabilities no Context real do Lunet.")
                .SetPositiveButton("OK", (_, _) => { })
                .Show();
            return;
        }

        var host = LunetCapabilityHost.CreateDefault();
        using var session = host.OpenForProject(_project);
        var context = string.Join(" → ", session.Context.Path.Select(step => $"{step.Level}:{step.Id}"));
        var grants = session.Grants.Count == 0 ? "nenhum" : string.Join(", ", session.Grants);
        var lines = new List<string>
        {
            $"Context: {context}",
            $"Grants: {grants}",
            ""
        };

        var items = session.Connections();
        if (items.Count == 0)
            lines.Add("Nenhuma capability registrada neste Host.");
        else
            foreach (var connection in items)
            {
                var required = connection.RequiredPermissions.Count == 0 ? "nenhum" : string.Join(", ", connection.RequiredPermissions);
                lines.Add($"{(connection.Available ? "✓" : "×")} {connection.Capability}@{connection.Version} · {connection.Provider}");
                lines.Add($"  {connection.Status} · lifecycle {connection.Lifecycle} · grants exigidos: {required}");
                if (connection.MissingPermissions.Count > 0)
                    lines.Add($"  grants ausentes: {string.Join(", ", connection.MissingPermissions)}");
            }

        lines.Add("");
        lines.Add("Fonte: Registry do Host. Connections não mantém cadastro próprio.");
        new AlertDialog.Builder(this)
            .SetTitle("Connections · Registry")
            .SetMessage(string.Join("\n", lines))
            .SetPositiveButton("Fechar", (_, _) => { })
            .Show();
    }

    private async void InspectEditorWithLocalHost()
    {
        if (_project is null || _editor is null)
        {
            Toast.MakeText(this, "Abra um projeto primeiro.", ToastLength.Short)?.Show();
            return;
        }

        var document = _editor.Text ?? "";
        var start = Math.Clamp(Math.Min(_editor.SelectionStart, _editor.SelectionEnd), 0, document.Length);
        var end = Math.Clamp(Math.Max(_editor.SelectionStart, _editor.SelectionEnd), 0, document.Length);
        var selected = end > start;
        var text = selected ? document[start..end] : document;

        var host = LunetCapabilityHost.CreateDefault();
        using var session = host.OpenForProject(_project);
        var capability = session.Discover().FirstOrDefault(c =>
            c.Capability == LunetTextInspectionCapability.CapabilityId);

        if (capability is null)
        {
            new AlertDialog.Builder(this)
                .SetTitle("Host local")
                .SetMessage("text.inspect não está disponível neste Context.")
                .SetPositiveButton("OK", (_, _) => { })
                .Show();
            return;
        }

        var result = await session.InvokeAsync(
            capability.Capability,
            new Version(1, 0, 0),
            JsonSerializer.SerializeToElement(new { text }),
            CancellationToken.None);

        if (!result.Succeeded || result.Output is null)
        {
            new AlertDialog.Builder(this)
                .SetTitle("Host local")
                .SetMessage($"text.inspect falhou: {result.Error ?? "resposta inválida"}.")
                .SetPositiveButton("OK", (_, _) => { })
                .Show();
            return;
        }

        var output = result.Output.Value;
        var source = selected ? "seleção atual" : "documento atual";
        new AlertDialog.Builder(this)
            .SetTitle("text.inspect · Host Lunet")
            .SetMessage(
                $"Fonte: {source}\n" +
                $"Projeto: {_project.Name}\n" +
                $"Context project: {_project.Manifest.GameId}\n\n" +
                $"Caracteres: {output.GetProperty("characters").GetInt32()}\n" +
                $"Palavras: {output.GetProperty("words").GetInt32()}\n" +
                $"Linhas: {output.GetProperty("lines").GetInt32()}\n\n" +
                "Execução local: nenhum provider externo é necessário.")
            .SetPositiveButton("Fechar", (_, _) => { })
            .Show();
    }
}
