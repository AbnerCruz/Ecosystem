using Android.Widget;
using EcosystemAi.Product;

namespace EcosystemAi.Android;

public sealed partial class MainActivity
{
    private void ReviewProjectFile(string projectId, string sessionId, int turnIndex)
    {
        var host = new ReviewedProjectFileHost(_catalog, _journal);
        var naming = Column();
        var name = Input("Nome do arquivo .md ou .txt", "resposta.md");
        naming.AddView(name);
        naming.AddView(Text("Escolha o nome do novo arquivo. Você revisará o conteúdo antes de salvar.", 14));
        Dialog("Salvar no projeto", naming, "Revisar conteúdo", () =>
        {
            var proposal = host.Prepare(projectId, sessionId, turnIndex, name.Text ?? "");
            var body = Column();
            body.AddView(Text("Novo arquivo: " + proposal.FileName, 18, true));
            body.AddView(Text("Projeto: " + _store.Read().Projects.Single(p => p.Id == projectId).Name, 14));
            var content = Text(proposal.Content, 16);
            content.SetTextIsSelectable(true); body.AddView(content);
            body.AddView(Text("O arquivo será criado com este texto completo, sem alterar arquivos existentes. Confira o conteúdo antes de confirmar.", 14));
            Dialog("Revisar arquivo", body, "Confirmar e salvar", () =>
            {
                if (_active is not null) throw new InvalidOperationException("Aguarde a operação atual.");
                var cancel = new CancellationTokenSource(); _active = cancel;
                Render();
                _ = PublishAsync();
                async Task PublishAsync()
                {
                    string title = "Arquivo salvo no projeto";
                    string message;
                    try
                    {
                        var published = await Task.Run(() => host.PublishAsync(proposal, approved: true, cancel.Token));
                        message = published.FileName + "\nSHA-256: " + published.Sha256
                            + "\n\nDisponível em Arquivos e para leitura pelo agente. Salvar não aprova a qualidade da resposta.";
                    }
                    catch (OperationCanceledException)
                    { title = "Salvamento cancelado"; message = "Confira Arquivos antes de tentar novamente."; }
                    catch (Exception error) when (error is ArgumentException or InvalidOperationException
                        or IOException or UnauthorizedAccessException or KeyNotFoundException)
                    { title = "Arquivo não salvo"; message = error.Message; }
                    finally { _active = null; cancel.Dispose(); }
                    if (!IsFinishing && !IsDestroyed)
                    { Guard(Render); Notice(title, message); }
                }
            });
        });
    }
}
