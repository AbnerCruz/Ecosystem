using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using TabletopRpg.Persistence;

namespace TabletopRpg.Core.Tests;

public sealed class PersistenceFormatTests
{
    [Fact]
    public void Tampering_with_payload_is_detected_by_checksum()
    {
        var fixture = PersistenceTestFixture.CreateCampaign();
        var codec = new CampaignCodec(new PersistenceTestFixture.FixedTimeProvider());
        var json = JsonNode.Parse(codec.Encode(fixture.Campaign))!.AsObject();
        json["payload"]!.AsObject()["name"] = "Tampered without updating checksum";

        var tampered = Encoding.UTF8.GetBytes(json.ToJsonString());

        var error = Assert.Throws<CampaignFormatException>(() => codec.Decode(tampered));
        Assert.Contains("checksum", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Future_schema_version_is_rejected_fail_closed()
    {
        var fixture = PersistenceTestFixture.CreateCampaign();
        var codec = new CampaignCodec(new PersistenceTestFixture.FixedTimeProvider());
        var json = JsonNode.Parse(codec.Encode(fixture.Campaign))!.AsObject();
        json["schemaVersion"] = CampaignCodec.CurrentSchemaVersion + 1;

        var future = Encoding.UTF8.GetBytes(json.ToJsonString());

        var error = Assert.Throws<CampaignFormatException>(() => codec.Decode(future));
        Assert.Contains("schema version", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Structurally_incomplete_payload_is_rejected_even_with_a_valid_checksum()
    {
        var fixture = PersistenceTestFixture.CreateCampaign();
        var codec = new CampaignCodec(new PersistenceTestFixture.FixedTimeProvider());
        var json = JsonNode.Parse(codec.Encode(fixture.Campaign))!.AsObject();

        json["payload"]!.AsObject().Remove("characters");
        ResignPayload(json);

        var invalid = Encoding.UTF8.GetBytes(json.ToJsonString());
        var error = Assert.Throws<CampaignFormatException>(() => codec.Decode(invalid));

        Assert.Contains("characters", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Overlapping_sessions_are_rejected_even_with_a_valid_checksum()
    {
        var fixture = PersistenceTestFixture.CreateCampaign();
        var codec = new CampaignCodec(new PersistenceTestFixture.FixedTimeProvider());
        var json = JsonNode.Parse(codec.Encode(fixture.Campaign))!.AsObject();
        var payload = json["payload"]!.AsObject();
        var sessions = payload["sessions"]!.AsArray();

        payload["clock"] = 7;
        payload["activeSessionId"] = null;
        sessions[0]!.AsObject()["endedAt"] = 6;
        sessions.Add(new JsonObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "Overlapping Session",
            ["startedAt"] = 6,
            ["endedAt"] = 7
        });
        ResignPayload(json);

        var invalid = Encoding.UTF8.GetBytes(json.ToJsonString());
        var error = Assert.Throws<CampaignFormatException>(() => codec.Decode(invalid));

        Assert.Contains("overlap", error.Message, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public void Duplicate_controlled_character_ids_are_rejected()
    {
        var fixture = PersistenceTestFixture.CreateCampaign();
        var codec = new CampaignCodec(new PersistenceTestFixture.FixedTimeProvider());
        var json = JsonNode.Parse(codec.Encode(fixture.Campaign))!.AsObject();
        var participants = json["payload"]!.AsObject()["participants"]!.AsArray();
        var controlled = participants[0]!.AsObject()["controlledCharacterIds"]!.AsArray();

        controlled.Add(controlled[0]!.DeepClone());
        ResignPayload(json);

        var invalid = Encoding.UTF8.GetBytes(json.ToJsonString());
        var error = Assert.Throws<CampaignFormatException>(() => codec.Decode(invalid));

        Assert.Contains("duplicate", error.Message, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public void Case_insensitive_duplicate_attribute_names_are_rejected()
    {
        var fixture = PersistenceTestFixture.CreateCampaign();
        var codec = new CampaignCodec(new PersistenceTestFixture.FixedTimeProvider());
        var json = JsonNode.Parse(codec.Encode(fixture.Campaign))!.AsObject();
        var characters = json["payload"]!.AsObject()["characters"]!.AsArray();
        var attributes = characters
            .Select(node => node!.AsObject())
            .First(character => character["name"]!.GetValue<string>() == "Hero")
            ["attributes"]!.AsObject();

        attributes["Strength"] = 99;
        ResignPayload(json);

        var invalid = Encoding.UTF8.GetBytes(json.ToJsonString());
        var error = Assert.Throws<CampaignFormatException>(() => codec.Decode(invalid));

        Assert.Contains("attributes", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static void ResignPayload(JsonObject envelope)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(envelope["payload"]!.ToJsonString());
        envelope["payloadSha256"] =
            Convert.ToHexString(SHA256.HashData(payloadBytes)).ToLowerInvariant();
    }
}
