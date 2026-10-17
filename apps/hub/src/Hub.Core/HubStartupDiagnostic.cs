using System.Security.Cryptography;
using System.Text;

namespace Hub.Core;

/// <summary>
/// Diagnóstico opaco para uma falha gerenciada ao iniciar o cliente Android.
/// Nenhuma mensagem, caminho ou stack trace original é apresentado na interface.
/// </summary>
public sealed record HubStartupDiagnostic(string Explanation, string Id)
{
    public static HubStartupDiagnostic From(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var root = exception;
        for (var depth = 0; depth < 8 && root.InnerException is not null; depth++)
            root = root.InnerException;

        // Mensagem e stack aparecem apenas como entrada do hash, nunca na UI.
        var fingerprint = $"{root.GetType().FullName}|{root.HResult}|{root.StackTrace}";
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)))[..12];
        var explanation = root switch
        {
            InvalidDataException => "O estado local não pôde ser interpretado.",
            UnauthorizedAccessException => "O Android não permitiu acessar os dados locais.",
            IOException => "Não foi possível ler os dados locais.",
            _ => "O Hub não conseguiu concluir a inicialização."
        };
        return new HubStartupDiagnostic(explanation, id);
    }
}
