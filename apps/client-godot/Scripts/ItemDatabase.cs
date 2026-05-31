using Godot;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Item database loader. Reads ItemDatabase.json and provides typed item lookups.
/// Every value is a vinculum ratio, not an absolute.
/// </summary>
public partial class ItemDatabase : Node
{
    private static ItemDatabase _instance;
    public static ItemDatabase Instance => _instance;

    private Dictionary<string, ItemDef> _items = new();
    private Dictionary<string, RecipeDef> _recipes = new();
    private Dictionary<string, CategoryDef> _categories = new();

    public override void _Ready()
    {
        _instance = this;
        LoadDatabase();
    }

    private void LoadDatabase()
    {
        string jsonPath = "res://Assets/item_database.json";
        using var file = FileAccess.Open(jsonPath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PrintErr($"ItemDatabase: Could not open {jsonPath}");
            return;
        }

        string json = file.GetAsText();
        var db = JsonSerializer.Deserialize<ItemDatabaseJson>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (db == null)
        {
            GD.PrintErr("ItemDatabase: Failed to parse JSON");
            return;
        }

        // Load categories
        foreach (var kvp in db.ItemCategories)
        {
            var cat = kvp.Value;
            cat.Id = kvp.Key;
            _categories[kvp.Key] = cat;
        }

        // Load items
        foreach (var kvp in db.Items)
        {
            var item = kvp.Value;
            item.Id = kvp.Key;
            item.CategoryDef = _categories.GetValueOrDefault(item.Category);
            _items[kvp.Key] = item;
        }

        // Load recipes
        if (db.Recipes != null)
        {
            foreach (var recipe in db.Recipes)
            {
                _recipes[recipe.Id] = recipe;
            }
        }

        GD.Print($"ItemDatabase: Loaded {_items.Count} items, {_recipes.Count} recipes, {_categories.Count} categories");
    }

    public ItemDef GetItem(string id)
    {
        return _items.GetValueOrDefault(id);
    }

    public RecipeDef GetRecipe(string id)
    {
        return _recipes.GetValueOrDefault(id);
    }

    public IEnumerable<ItemDef> AllItems() => _items.Values;
    public IEnumerable<RecipeDef> AllRecipes() => _recipes.Values;

    /// <summary>
    /// Get recipes craftable at a given station tier.
    /// </summary>
    public IEnumerable<RecipeDef> GetRecipesForStation(string station)
    {
        foreach (var recipe in _recipes.Values)
        {
            if (recipe.Station == station)
                yield return recipe;
        }
    }

    /// <summary>
    /// Get recipes the player can craft given their available items.
    /// </summary>
    public IEnumerable<RecipeDef> GetCraftableRecipes(Dictionary<string, int> inventoryItems)
    {
        foreach (var recipe in _recipes.Values)
        {
            bool canCraft = true;
            foreach (var input in recipe.Inputs)
            {
                if (!inventoryItems.TryGetValue(input.Item, out int qty) || qty < input.Qty)
                {
                    canCraft = false;
                    break;
                }
            }
            if (canCraft)
                yield return recipe;
        }
    }
}

// ─── JSON serialization types ───

public class ItemDatabaseJson
{
    [JsonPropertyName("item_categories")]
    public Dictionary<string, CategoryDef> ItemCategories { get; set; }

    [JsonPropertyName("items")]
    public Dictionary<string, ItemDef> Items { get; set; }

    [JsonPropertyName("recipes")]
    public List<RecipeDef> Recipes { get; set; }
}

public class CategoryDef
{
    [JsonIgnore]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("slot")]
    public string Slot { get; set; }

    [JsonPropertyName("stackable")]
    public bool Stackable { get; set; }

    [JsonPropertyName("max_stack")]
    public int MaxStack { get; set; } = 1;

    [JsonPropertyName("degradable")]
    public bool Degradable { get; set; }

    [JsonPropertyName("spoils")]
    public bool Spoils { get; set; }

    [JsonPropertyName("noisy")]
    public bool Noisy { get; set; }
}

public class ItemDef
{
    [JsonIgnore]
    public string Id { get; set; }

    [JsonIgnore]
    public CategoryDef CategoryDef { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("weight")]
    public float Weight { get; set; }

    [JsonPropertyName("volume")]
    public int Volume { get; set; }

    [JsonPropertyName("craftable")]
    public bool Craftable { get; set; }

    // Consumable
    [JsonPropertyName("restores")]
    public Dictionary<string, float> Restores { get; set; }

    [JsonPropertyName("side_effects")]
    public Dictionary<string, float> SideEffects { get; set; }

    [JsonPropertyName("spoil_time_days")]
    public float SpoilTimeDays { get; set; }

    // Weapon
    [JsonPropertyName("damage")]
    public float Damage { get; set; }

    [JsonPropertyName("stamina_cost")]
    public float StaminaCost { get; set; }

    [JsonPropertyName("range")]
    public float Range { get; set; } = 1.5f;

    [JsonPropertyName("noise")]
    public float Noise { get; set; }

    [JsonPropertyName("ammo_type")]
    public string AmmoType { get; set; }

    [JsonPropertyName("magazine_size")]
    public int MagazineSize { get; set; }

    [JsonPropertyName("reload_time")]
    public float ReloadTime { get; set; }

    // Durability
    [JsonPropertyName("durability")]
    public float Durability { get; set; } = 100;

    [JsonPropertyName("degradation_per_use")]
    public float DegradationPerUse { get; set; } = 0.1f;

    // Stacking
    [JsonPropertyName("stack_size")]
    public int StackSize { get; set; } = 1;

    // Components
    [JsonPropertyName("charge")]
    public float Charge { get; set; }

    [JsonPropertyName("power_output")]
    public float PowerOutput { get; set; }

    public bool IsWeaponMelee => CategoryDef?.Slot == "hand" && CategoryDef?.Noisy == false;
    public bool IsWeaponRanged => CategoryDef?.Slot == "hand" && CategoryDef?.Noisy == true;
    public bool IsConsumable => CategoryDef?.Slot == "consumable";
    public bool IsMaterial => CategoryDef?.Slot == "material" || CategoryDef?.Slot == "component";
}

public class RecipeDef
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("station")]
    public string Station { get; set; }

    [JsonPropertyName("time")]
    public int Time { get; set; }

    [JsonPropertyName("skill")]
    public string Skill { get; set; }

    [JsonPropertyName("inputs")]
    public List<RecipeItem> Inputs { get; set; }

    [JsonPropertyName("outputs")]
    public List<RecipeItem> Outputs { get; set; }
}

public class RecipeItem
{
    [JsonPropertyName("item")]
    public string Item { get; set; }

    [JsonPropertyName("qty")]
    public int Qty { get; set; }
}
