namespace Application.DTOs.Recommendations;

public sealed class PcBuildGeminiSuggestRequest
{
    /// <summary>
    /// Optional free text. If empty, backend will generate a prompt from the selections below.
    /// </summary>
    public string? Message { get; set; }

    // UI selections (matching your builder screen)
    public string? Budget { get; set; }          // e.g. "Dưới 15 triệu", "15 - 25 triệu", "25 - 40 triệu", "Trên 40 triệu"
    public string? Usage { get; set; }           // e.g. "Văn phòng / học tập", "Gaming", "Design / Edit video", "AI / Lập trình"
    public List<string> PerformanceTags { get; set; } = new(); // e.g. ["Ưu tiên FPS cao", "Ưu tiên đa nhiệm"]
    public string? BrandPreference { get; set; } // e.g. "Intel + NVIDIA", "Full AMD", "Pha trộn tối ưu giá"

    public int PerTypeCandidates { get; set; } = 12;
    public int PerTypeReturn { get; set; } = 5;
    public int ShortlistSize { get; set; } = 6; // Top N builds after scoring (5-10 recommended)
    public int MaxGeneratedBuilds { get; set; } = 500; // Safety cap for compatible outputs
    public int MaxCheckedCombinations { get; set; } = 60000; // Safety cap for compatibility checks
    public string? Model { get; set; }
}

