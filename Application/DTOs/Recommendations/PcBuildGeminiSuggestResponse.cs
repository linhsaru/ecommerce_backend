using Application.DTOs.Components;

namespace Application.DTOs.Recommendations;

public sealed class PcBuildGeminiSuggestResponse
{
    public string ModelUsed { get; set; } = "";
    public string? Raw { get; set; }
    public string? FinalReason { get; set; }
    public PcBuildCandidateDto? FinalBuild { get; set; }
    public List<PcBuildCandidateDto> ShortlistedBuilds { get; set; } = new();

    public List<CpuDto> Cpus { get; set; } = new();
    public List<GpuDto> Gpus { get; set; } = new();
    public List<RamDto> Rams { get; set; } = new();
    public List<StorageDto> Storages { get; set; } = new();
    public List<MotherboardDto> Motherboards { get; set; } = new();
    public List<PsuDto> Psus { get; set; } = new();
    public List<CaseDto> Cases { get; set; } = new();
    public List<CoolingDto> Coolings { get; set; } = new();
}

public sealed class PcBuildCandidateDto
{
    public decimal TotalPrice { get; set; }
    public double Score { get; set; }
    public CpuDto? Cpu { get; set; }
    public GpuDto? Gpu { get; set; }
    public RamDto? Ram { get; set; }
    public StorageDto? Storage { get; set; }
    public MotherboardDto? Motherboard { get; set; }
    public PsuDto? Psu { get; set; }
    public CaseDto? Case { get; set; }
    public CoolingDto? Cooling { get; set; }
}

