using EcosystemAi.ProjectStore;

namespace EcosystemAi.Product;

/// <summary>Entrada integral da tarefa planejada, sem tornar texto livre um verificador automático.</summary>
public static class ProductTaskRequest
{
    public static string Compose(PlannedTaskEntry task)
    {
        ArgumentNullException.ThrowIfNull(task);
        var goal = "OBJETIVO:\n" + task.Goal + "\n\nCRITÉRIO DE ACEITE:\n" + task.Acceptance;
        if (goal.Length > ProductExecutionHost.MaxText)
            throw new ArgumentException("Objetivo e critério juntos excedem 16384 caracteres; nenhum trecho foi descartado.");
        return goal;
    }
}
