using Application.DTOs.Components;

namespace Application.DTOs.Recommendations;

public sealed class PcComponentsCatalogDto
{
    public List<CpuDto> Cpus { get; set; } = new();
    public List<GpuDto> Gpus { get; set; } = new();
    public List<RamDto> Rams { get; set; } = new();
    public List<StorageDto> Storages { get; set; } = new();
    public List<MotherboardDto> Motherboards { get; set; } = new();
    public List<PsuDto> Psus { get; set; } = new();
    public List<CaseDto> Cases { get; set; } = new();
    public List<CoolingDto> Coolings { get; set; } = new();
}

