using EcosystemAi.ProjectStore;
using EcosystemAi.Product;

namespace EcosystemAi.Cli;

/// <summary>API CLI preservada; definição autoritativa local ao Product.</summary>
public static class CliPlannedTaskRequest
{
    public static string Compose(PlannedTaskEntry task) => ProductTaskRequest.Compose(task);
}
