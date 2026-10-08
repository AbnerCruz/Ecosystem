namespace TabletopRpg.Core;

// RPG-003: all state is campaign-owned, deterministic and independent of a ruleset,
// network service, AI provider, UI, or real-world time.
public readonly record struct SceneId(Guid Value)
{
    public static SceneId New() => new(Guid.NewGuid());
}
public readonly record struct QuestId(Guid Value)
{
    public static QuestId New() => new(Guid.NewGuid());
}
public readonly record struct InventoryId(Guid Value)
{
    public static InventoryId New() => new(Guid.NewGuid());
}

public enum QuestStatus { Open, Completed, Failed }

public sealed record CampaignScene(SceneId Id, string Title, string Description);
public sealed record CampaignQuest(QuestId Id, string Title, QuestStatus Status, SceneId? SceneId);
public sealed record InventoryEntry(InventoryId Id, CharacterId Owner, string ItemKey, int Quantity);
public sealed record CharacterCondition(CharacterId CharacterId, string Key, long? ExpiresAtMinute);
public sealed record CharacterResource(CharacterId CharacterId, string Key, int Current, int Maximum);

public sealed partial class Campaign
{
    private readonly Dictionary<SceneId, CampaignScene> _scenes = [];
    private readonly Dictionary<QuestId, CampaignQuest> _quests = [];
    private readonly Dictionary<InventoryId, InventoryEntry> _inventory = [];
    private readonly Dictionary<(CharacterId, string), CharacterCondition> _conditions = [];
    private readonly Dictionary<(CharacterId, string), CharacterResource> _resources = [];

    // Fictional elapsed minutes are *not* Clock: Clock orders audit events and
    // persists across player actions that do not advance fictional time.
    private long _fictionMinutes;
    private SceneId? _activeSceneId;

    public long FictionMinutes => _fictionMinutes;
    public SceneId? ActiveSceneId => _activeSceneId;
    public IReadOnlyList<CampaignScene> Scenes => _scenes.Values.OrderBy(x => x.Id.Value).ToArray();
    public IReadOnlyList<CampaignQuest> Quests => _quests.Values.OrderBy(x => x.Id.Value).ToArray();
    public IReadOnlyList<InventoryEntry> Inventory => _inventory.Values.OrderBy(x => x.Id.Value).ToArray();
    public IReadOnlyList<CharacterCondition> Conditions => _conditions.Values
        .OrderBy(x => x.CharacterId.Value).ThenBy(x => x.Key, StringComparer.Ordinal).ToArray();
    public IReadOnlyList<CharacterResource> Resources => _resources.Values
        .OrderBy(x => x.CharacterId.Value).ThenBy(x => x.Key, StringComparer.Ordinal).ToArray();

    public CampaignScene AddScene(SceneId id, string title, string description = "")
    {
        RequireId(id.Value, "Scene");
        title = RequireText(title, "Scene title");
        description = RequireDescription(description);
        var scene = new CampaignScene(id, title, description);
        if (!_scenes.TryAdd(id, scene)) throw new InvalidOperationException("Duplicate scene.");
        RecordEvent("scene-created", null, null, id.Value.ToString("N"));
        return scene;
    }

    public void EnterScene(SceneId id)
    {
        if (!_scenes.ContainsKey(id)) throw new KeyNotFoundException("Scene not found.");
        if (_activeSceneId == id) return;
        _activeSceneId = id;
        RecordEvent("scene-entered", null, null, id.Value.ToString("N"));
    }

    public void AdvanceTime(long minutes)
    {
        if (minutes <= 0 || minutes > 525600)
            throw new ArgumentOutOfRangeException(nameof(minutes), "Advance must be 1..525600 minutes.");
        var next = checked(_fictionMinutes + minutes);
        _fictionMinutes = next;
        // Expiration is deterministic, independent of frame rate and time zone.
        foreach (var key in _conditions.Keys.ToArray())
            if (_conditions[key].ExpiresAtMinute is { } expiry && expiry <= next)
                _conditions.Remove(key);
        RecordEvent("fiction-time-advanced", null, null, next.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public CampaignQuest AddQuest(QuestId id, string title, SceneId? sceneId = null)
    {
        RequireId(id.Value, "Quest");
        title = RequireText(title, "Quest title");
        if (sceneId is { } scene && !_scenes.ContainsKey(scene))
            throw new KeyNotFoundException("Quest scene not found.");
        var quest = new CampaignQuest(id, title, QuestStatus.Open, sceneId);
        if (!_quests.TryAdd(id, quest)) throw new InvalidOperationException("Duplicate quest.");
        RecordEvent("quest-created", null, null, id.Value.ToString("N"));
        return quest;
    }

    public CampaignQuest ResolveQuest(QuestId id, QuestStatus status)
    {
        if (status is not (QuestStatus.Completed or QuestStatus.Failed))
            throw new ArgumentOutOfRangeException(nameof(status));
        if (!_quests.TryGetValue(id, out var quest)) throw new KeyNotFoundException("Quest not found.");
        if (quest.Status != QuestStatus.Open) throw new InvalidOperationException("Quest is already resolved.");
        var updated = quest with { Status = status };
        _quests[id] = updated;
        RecordEvent("quest-resolved", null, null, id.Value.ToString("N") + ":" + status);
        return updated;
    }

    public InventoryEntry AddItem(InventoryId id, CharacterId owner, string itemKey, int quantity)
    {
        RequireId(id.Value, "Inventory entry");
        RequireCharacter(owner);
        itemKey = RequireKey(itemKey);
        if (quantity < 1 || quantity > 1_000_000) throw new ArgumentOutOfRangeException(nameof(quantity));
        var item = new InventoryEntry(id, owner, itemKey, quantity);
        if (!_inventory.TryAdd(id, item)) throw new InvalidOperationException("Duplicate inventory entry.");
        RecordEvent("inventory-added", null, owner, id.Value.ToString("N"));
        return item;
    }

    public InventoryEntry? AdjustItem(InventoryId id, int delta)
    {
        if (delta == 0) throw new ArgumentOutOfRangeException(nameof(delta));
        if (!_inventory.TryGetValue(id, out var item)) throw new KeyNotFoundException("Inventory entry not found.");
        var total = (long)item.Quantity + delta;
        if (total < 0 || total > 1_000_000) throw new ArgumentOutOfRangeException(nameof(delta));
        if (total == 0)
        {
            _inventory.Remove(id);
            RecordEvent("inventory-consumed", null, item.Owner, id.Value.ToString("N"));
            return null;
        }
        var updated = item with { Quantity = (int)total };
        _inventory[id] = updated;
        RecordEvent("inventory-adjusted", null, item.Owner, id.Value.ToString("N"));
        return updated;
    }

    public InventoryEntry TransferItem(InventoryId id, CharacterId destination)
    {
        RequireCharacter(destination);
        if (!_inventory.TryGetValue(id, out var item)) throw new KeyNotFoundException("Inventory entry not found.");
        if (destination == item.Owner) return item;
        var updated = item with { Owner = destination };
        _inventory[id] = updated;
        RecordEvent("inventory-transferred", null, destination, id.Value.ToString("N"));
        return updated;
    }

    public CharacterCondition SetCondition(CharacterId characterId, string key, long? durationMinutes = null)
    {
        RequireCharacter(characterId);
        key = RequireKey(key);
        if (durationMinutes is <= 0) throw new ArgumentOutOfRangeException(nameof(durationMinutes));
        long? expiry = durationMinutes is { } duration ? checked(_fictionMinutes + duration) : null;
        var value = new CharacterCondition(characterId, key, expiry);
        _conditions[(characterId, key)] = value;
        RecordEvent("condition-set", null, characterId, key);
        return value;
    }

    public bool ClearCondition(CharacterId characterId, string key)
    {
        RequireCharacter(characterId);
        key = RequireKey(key);
        if (!_conditions.Remove((characterId, key))) return false;
        RecordEvent("condition-cleared", null, characterId, key);
        return true;
    }

    public CharacterResource SetResource(CharacterId characterId, string key, int current, int maximum)
    {
        RequireCharacter(characterId);
        key = RequireKey(key);
        if (maximum < 1 || maximum > 1_000_000 || current < 0 || current > maximum)
            throw new ArgumentOutOfRangeException(nameof(current), "Require 0 <= current <= maximum <= 1000000.");
        var value = new CharacterResource(characterId, key, current, maximum);
        _resources[(characterId, key)] = value;
        RecordEvent("resource-set", null, characterId, key);
        return value;
    }

    public CharacterResource ChangeResource(CharacterId characterId, string key, int delta)
    {
        RequireCharacter(characterId);
        key = RequireKey(key);
        if (!_resources.TryGetValue((characterId, key), out var resource))
            throw new KeyNotFoundException("Resource not found.");
        long value = (long)resource.Current + delta;
        if (value < 0 || value > resource.Maximum) throw new ArgumentOutOfRangeException(nameof(delta));
        if (delta == 0) return resource;
        var updated = resource with { Current = (int)value };
        _resources[(characterId, key)] = updated;
        RecordEvent("resource-changed", null, characterId, key);
        return updated;
    }

    private void RequireCharacter(CharacterId id)
    {
        if (!_characters.ContainsKey(id)) throw new KeyNotFoundException("Character not found.");
    }

    private static void RequireId(Guid id, string kind)
    {
        if (id == Guid.Empty) throw new ArgumentException(kind + " id cannot be empty.");
    }

    private static string RequireText(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 256)
            throw new ArgumentException(label + " must have 1..256 characters.");
        return value.Trim();
    }

    private static string RequireDescription(string? value)
    {
        if (value is null || value.Length > 4096)
            throw new ArgumentException("Description must have 0..4096 characters.");
        return value;
    }

    private static string RequireKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 ||
            !value.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_'))
            throw new ArgumentException("Key must be lowercase ASCII slug with at most 128 characters.");
        return value;
    }
}
