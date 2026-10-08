using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TabletopRpg.Persistence;

namespace TabletopRpg.Core.Tests;

public sealed class WorldStateTests
{
    private static (Campaign Campaign, CharacterId Hero, CharacterId Ally, ParticipantId Human) Create()
    {
        var campaign = new Campaign(CampaignId.New(), "Frontier");
        var hero = CharacterId.New();
        var ally = CharacterId.New();
        var human = ParticipantId.New();
        campaign.AddCharacter(new Character(hero, "Hero"));
        campaign.AddCharacter(new Character(ally, "Ally"));
        campaign.AddParticipant(new Participant(human, "Human", ControllerKind.Human, SeatRole.Player, [hero]));
        return (campaign, hero, ally, human);
    }

    [Fact]
    public void Scene_quest_time_and_expiring_condition_are_deterministic()
    {
        var (campaign, hero, _, _) = Create();
        var scene = campaign.AddScene(SceneId.New(), "Dungeon", "Cave below the town");
        campaign.EnterScene(scene.Id);
        var quest = campaign.AddQuest(QuestId.New(), "Recover the map", scene.Id);
        campaign.SetCondition(hero, "torch-lit", 15);
        var firstClock = campaign.Clock;
        campaign.AdvanceTime(14);
        Assert.Single(campaign.Conditions);
        campaign.AdvanceTime(1);
        Assert.Empty(campaign.Conditions);
        Assert.Equal(15, campaign.FictionMinutes);
        Assert.True(campaign.Clock > firstClock);
        Assert.Equal(scene.Id, campaign.ActiveSceneId);
        Assert.Equal(QuestStatus.Completed, campaign.ResolveQuest(quest.Id, QuestStatus.Completed).Status);
        Assert.Throws<InvalidOperationException>(() => campaign.ResolveQuest(quest.Id, QuestStatus.Failed));
        Assert.Equal(campaign.Events.Count, campaign.Events.Select(x => x.Sequence).Distinct().Count());
    }

    [Fact]
    public void Inventory_resource_and_condition_reject_invalid_actions_without_events()
    {
        var (campaign, hero, ally, _) = Create();
        var id = InventoryId.New();
        campaign.AddItem(id, hero, "rope", 3);
        campaign.TransferItem(id, ally);
        Assert.Equal(ally, Assert.Single(campaign.Inventory).Owner);
        campaign.AdjustItem(id, -2);
        Assert.Equal(1, Assert.Single(campaign.Inventory).Quantity);
        campaign.SetResource(hero, "mana", 5, 10);
        Assert.Equal(2, campaign.ChangeResource(hero, "mana", -3).Current);
        var before = campaign.Events.Count;

        Assert.Throws<ArgumentOutOfRangeException>(() => campaign.AdjustItem(id, -2));
        Assert.Throws<ArgumentOutOfRangeException>(() => campaign.ChangeResource(hero, "mana", -3));
        Assert.Throws<KeyNotFoundException>(() => campaign.SetCondition(CharacterId.New(), "poisoned"));
        Assert.Throws<KeyNotFoundException>(() => campaign.AddQuest(QuestId.New(), "Missing scene", SceneId.New()));
        Assert.Throws<ArgumentException>(() => campaign.AddScene(SceneId.New(), "", ""));
        Assert.Equal(before, campaign.Events.Count);
        Assert.Equal(1, Assert.Single(campaign.Inventory).Quantity);

        Assert.Null(campaign.AdjustItem(id, -1));
        Assert.Empty(campaign.Inventory);
    }

    [Fact]
    public void New_world_state_round_trips_through_persisted_schema_v2_and_player_view_stays_private()
    {
        var (campaign, hero, ally, human) = Create();
        var scene = campaign.AddScene(SceneId.New(), "Secret chamber", "The duke's vault.");
        campaign.EnterScene(scene.Id);
        campaign.AddQuest(QuestId.New(), "Find the duke", scene.Id);
        var item = campaign.AddItem(InventoryId.New(), ally, "amulet", 2);
        campaign.SetCondition(ally, "invisible", 10);
        campaign.SetResource(hero, "stamina", 8, 12);
        campaign.AdvanceTime(3);
        var codec = new CampaignCodec(new PersistenceTestFixture.FixedTimeProvider());

        var serialized = codec.Encode(campaign);
        var restored = codec.Decode(serialized);
        var encodedAgain = codec.Encode(restored);
        var parsed = JsonNode.Parse(serialized)!;

        Assert.Equal(2, parsed["schemaVersion"]!.GetValue<int>());
        Assert.Equal(campaign.Clock, restored.Clock);
        Assert.Equal(3, restored.FictionMinutes);
        Assert.Equal(scene.Id, restored.ActiveSceneId);
        Assert.Equal("Secret chamber", Assert.Single(restored.Scenes).Title);
        Assert.Equal("Find the duke", Assert.Single(restored.Quests).Title);
        Assert.Equal(item.Id, Assert.Single(restored.Inventory).Id);
        Assert.Equal(10L, Assert.Single(restored.Conditions).ExpiresAtMinute);
        Assert.Equal(8, Assert.Single(restored.Resources).Current);
        Assert.Equal(serialized, encodedAgain);
        Assert.Equal(campaign.Events.Select(x => x.Kind), restored.Events.Select(x => x.Kind));

        var playerView = restored.CreatePlayerView(human);
        Assert.Null(typeof(PlayerView).GetProperty("Quests"));
        Assert.Null(typeof(PlayerView).GetProperty("Scenes"));
        Assert.Null(typeof(PlayerView).GetProperty("Inventory"));
        Assert.Single(playerView.Characters);
        Assert.Empty(playerView.Characters.Single().Knowledge);
    }

    [Fact]
    public void V1_campaigns_load_with_empty_world_state_and_upgrade_on_next_save()
    {
        var (campaign, _, _, _) = Create();
        var codec = new CampaignCodec(new PersistenceTestFixture.FixedTimeProvider());
        var envelope = JsonNode.Parse(codec.Encode(campaign))!.AsObject();
        envelope["schemaVersion"] = 1;
        envelope["payload"]!.AsObject().Remove("world");
        Resign(envelope);

        var legacy = Encoding.UTF8.GetBytes(envelope.ToJsonString());
        var restored = codec.Decode(legacy);
        Assert.Equal(campaign.Id, restored.Id);
        Assert.Empty(restored.Scenes);
        Assert.Empty(restored.Quests);
        Assert.Equal(0, restored.FictionMinutes);
        Assert.Equal(campaign.Clock, restored.Clock);
        Assert.Equal(2, JsonNode.Parse(codec.Encode(restored))!["schemaVersion"]!.GetValue<int>());

        envelope["payload"]!.AsObject()["world"] = new JsonObject();
        Resign(envelope);
        Assert.Throws<CampaignFormatException>(() => codec.Decode(Encoding.UTF8.GetBytes(envelope.ToJsonString())));
    }

    [Theory]
    [InlineData("activeSceneId", "unknown")]
    [InlineData("fictionMinutes", "negative")]
    [InlineData("inventory", "unknown-owner")]
    [InlineData("resources", "over-capacity")]
    [InlineData("quests", "missing-scene")]
    [InlineData("conditions", "expired")]
    public void Tampered_world_fails_even_if_payload_is_resigned(string field, string kind)
    {
        var (campaign, hero, _, _) = Create();
        var scene = campaign.AddScene(SceneId.New(), "Dungeon");
        campaign.AddQuest(QuestId.New(), "Locate entrance", scene.Id);
        campaign.AddItem(InventoryId.New(), hero, "rope", 2);
        campaign.SetCondition(hero, "poisoned", 5);
        campaign.SetResource(hero, "hp", 6, 10);
        var codec = new CampaignCodec(new PersistenceTestFixture.FixedTimeProvider());
        var envelope = JsonNode.Parse(codec.Encode(campaign))!.AsObject();
        var world = envelope["payload"]!["world"]!.AsObject();

        switch (kind)
        {
            case "unknown":
                world[field] = Guid.NewGuid().ToString();
                break;
            case "negative":
                world[field] = -1;
                break;
            case "unknown-owner":
                world["inventory"]![0]!["owner"] = Guid.NewGuid().ToString();
                break;
            case "over-capacity":
                world["resources"]![0]!["current"] = 11;
                break;
            case "missing-scene":
                world["quests"]![0]!["sceneId"] = Guid.NewGuid().ToString();
                break;
            case "expired":
                world["conditions"]![0]!["expiresAtMinute"] = 0;
                break;
        }
        Resign(envelope);
        var bytes = Encoding.UTF8.GetBytes(envelope.ToJsonString());
        Assert.Throws<CampaignFormatException>(() => codec.Decode(bytes));
    }

    [Fact]
    public void Schema_v2_rejects_partial_world_and_future_schema()
    {
        var (campaign, _, _, _) = Create();
        var codec = new CampaignCodec(new PersistenceTestFixture.FixedTimeProvider());
        var envelope = JsonNode.Parse(codec.Encode(campaign))!.AsObject();
        envelope["payload"]!.AsObject().Remove("world");
        Resign(envelope);
        Assert.Throws<CampaignFormatException>(() => codec.Decode(Encoding.UTF8.GetBytes(envelope.ToJsonString())));

        envelope["schemaVersion"] = 3;
        Assert.Throws<CampaignFormatException>(() => codec.Decode(Encoding.UTF8.GetBytes(envelope.ToJsonString())));
    }

    [Fact]
    public void Missing_required_world_properties_cannot_be_recovered_by_defaults()
    {
        var (campaign, _, _, _) = Create();
        var codec = new CampaignCodec(new PersistenceTestFixture.FixedTimeProvider());
        var envelope = JsonNode.Parse(codec.Encode(campaign))!.AsObject();
        envelope["payload"]!["world"]!.AsObject().Remove("fictionMinutes");
        Resign(envelope);
        Assert.Throws<CampaignFormatException>(() => codec.Decode(Encoding.UTF8.GetBytes(envelope.ToJsonString())));
    }

    private static void Resign(JsonObject envelope)
    {
        var bytes = Encoding.UTF8.GetBytes(envelope["payload"]!.ToJsonString());
        envelope["payloadSha256"] = Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
