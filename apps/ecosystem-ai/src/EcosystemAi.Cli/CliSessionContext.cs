using EcosystemAi.ProjectStore;
using EcosystemAi.Product;

namespace EcosystemAi.Cli;

/// <summary>API CLI preservada; composição canônica local ao Product, utilizável no Android.</summary>
public static class CliSessionContext
{
    public const int MaxPriorMessages = ProductConversationContext.MaxPriorMessages;
    public const int MaxHistoryCharacters = ProductConversationContext.MaxHistoryCharacters;
    public static string Compose(ProjectCatalog catalog, string projectId, string sessionId, string goal) =>
        ProductConversationContext.Compose(catalog, projectId, sessionId, goal);
}
