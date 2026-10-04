using TabletopRpg.Core;

namespace TabletopRpg.Core.Tests;

internal static class PersistenceTestFixture
{
    internal static Fixture CreateCampaign()
    {
        var campaign = new Campaign(CampaignId.New(), "Persistence Test");
        var hero = new Character(
            CharacterId.New(),
            "Hero",
            new Dictionary<string, int> { ["strength"] = 3 });
        var companion = new Character(CharacterId.New(), "Companion");

        campaign.AddCharacter(hero);
        campaign.AddCharacter(companion);

        var human = new Participant(
            ParticipantId.New(),
            "Human",
            ControllerKind.Human,
            SeatRole.Player,
            [hero.Id]);
        var agent = new Participant(
            ParticipantId.New(),
            "Agent",
            ControllerKind.Agent,
            SeatRole.Player,
            [companion.Id]);

        campaign.AddParticipant(human);
        campaign.AddParticipant(agent);

        var secret = campaign.AddWorldFact("The duke is a vampire.");
        var known = campaign.AddWorldFact("The old bridge is unsafe.");
        campaign.RevealFactTo(hero.Id, known.Id, "scout report");
        campaign.AddBelief(hero.Id, "The guard looks nervous.", "intuition", 0.65);
        campaign.Remember(hero.Id, "We promised to return before dawn.");

        var sessionId = SessionId.New();
        campaign.StartSession(sessionId, "Session One");

        var engine = new SessionEngine(
            campaign,
            new BasicD20RuleSystem(),
            new DiceRoller(new FixedRandomSource(10)));
        var speech = engine.Execute(new SpeakIntent(human.Id, hero.Id, "We move before sunrise."));
        Assert.True(speech.Accepted);

        return new Fixture(
            campaign,
            hero.Id,
            human.Id,
            secret.Id,
            known.Id,
            sessionId);
    }

    internal static string PrimaryPath(string root, CampaignId id) =>
        Path.Combine(root, $"{id.Value:N}.campaign.json");

    internal sealed record Fixture(
        Campaign Campaign,
        CharacterId HeroId,
        ParticipantId HumanId,
        FactId SecretFactId,
        FactId KnownFactId,
        SessionId SessionId);

    internal sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 10, 3, 21, 0, 0, TimeSpan.Zero);
    }

    internal sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "tabletop-rpg-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private sealed class FixedRandomSource : IRandomSource
    {
        private readonly int _value;

        public FixedRandomSource(int value)
        {
            _value = value;
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            if (_value < minInclusive || _value >= maxExclusive)
                throw new InvalidOperationException("Fixed random value is outside requested range.");
            return _value;
        }
    }
}
