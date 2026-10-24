using System.Text.Json.Serialization;

namespace KayoneZSoundpad.Models;

/// <summary>library.json kök nesnesi.</summary>
public class LibraryData
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("categories")]
    public List<Category> Categories { get; set; } = new();

    [JsonPropertyName("sounds")]
    public List<SoundItem> Sounds { get; set; } = new();

    [JsonPropertyName("nextOrder")]
    public int NextOrder { get; set; } = 1;
}
