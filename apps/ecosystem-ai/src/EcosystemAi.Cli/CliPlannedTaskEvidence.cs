using System.Security.Cryptography;
using System.Text;
using EcosystemAi.ProjectStore;

namespace EcosystemAi.Cli;

/// <summary>
/// Projeta evidências de tarefas planejadas sobre o catálogo ATUAL.
/// Não altera receipts, não decide critérios de aceite nem conclui TaskRecord.
/// Uma anotação histórica positiva deixa de ser apresentada como comprovada
/// quando desaparecem seus comprovantes canônicos.
/// </summary>
public static class CliPlannedTaskEvidence
{
    public static bool IsCurrent(PlannedTaskEntry task, PlannedTaskAttempt attempt,
        SessionEntry session, TeamReviewBoard? reviews)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(session);

        bool Verified(string id) => session.Runs.Count(r =>
            r.RunId == id && r.Status == "succeeded" && r.Verified) == 1;

        if (attempt.Outcome == "response_verified")
            return attempt.RunIds.Count == 1 && Verified(attempt.RunIds[0]);

        if (attempt.Outcome != "review_recorded" || attempt.RunIds.Count != 2
            || attempt.RunIds[0] == attempt.RunIds[1]
            || !attempt.RunIds.All(Verified) || reviews is null)
            return false;

        return reviews.Reviews.Any(review =>
            review.ProjectId == task.ProjectId && review.SessionId == task.SessionId
            && review.ProducerRunId == attempt.RunIds[0]
            && review.ReviewerRunId == attempt.RunIds[1]
            && review.ProducerTurnIndex >= 0
            && review.ReviewerTurnIndex > review.ProducerTurnIndex
            && review.ReviewerTurnIndex < session.Turns.Count
            && session.Runs.ToList().FindIndex(r => r.RunId == review.ProducerRunId)
                < session.Runs.ToList().FindIndex(r => r.RunId == review.ReviewerRunId)
            && ValidTurn(session.Turns[review.ProducerTurnIndex], review.ProducerHash)
            && ValidTurn(session.Turns[review.ReviewerTurnIndex], review.ReviewerHash));
    }

    private static bool ValidTurn(ConversationTurn turn, string expectedDigest) =>
        turn.Role == "assistant" && !string.IsNullOrWhiteSpace(turn.Text)
        && string.Equals(expectedDigest, Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(turn.Text))).ToLowerInvariant(),
            StringComparison.Ordinal);
}
