using EcosystemAi.ProjectStore;

namespace EcosystemAi.Cli;

/// <summary>Encaminha integralmente objetivo e aceite ao Workspace existente.</summary>
public static class CliPlannedTaskRequest
{
    public static string Compose(PlannedTaskEntry task)
    {
        ArgumentNullException.ThrowIfNull(task);
        var goal = "OBJETIVO:\n" + task.Goal + "\n\nCRITÉRIO DE ACEITE:\n" + task.Acceptance;
        if (goal.Length > CliWebTaskRunner.MaxGoalLength)
            throw new ArgumentException("Objetivo e critério juntos excedem o limite de execução. Reduza a tarefa; nenhum trecho será descartado.");
        return goal;
    }
}
