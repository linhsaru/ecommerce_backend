using Application.Common;
using Application.DTOs.Components;
using Application.DTOs.Recommendations;
using Application.Interfaces;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class PcComponentsService : IPcComponentsService
{
    private readonly IAppDbContext _db;

    public PcComponentsService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<PcComponentsCatalogDto>> GetPcComponentsCatalogAsync(CancellationToken cancellationToken = default)
    {
        var components = await _db.Components
            .AsNoTracking()
            .Where(x => x.DeletedAt == null)
            .OrderBy(x => x.Price)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var catalog = new PcComponentsCatalogDto();

        foreach (var c in components)
        {
            var type = (c.ComponentType ?? string.Empty).Trim().ToLowerInvariant();
            var sourceVariantId = c.ProductVariantId ?? c.Id;

            switch (type)
            {
                case "cpu":
                    catalog.Cpus.Add(new CpuDto
                    {
                        Id = sourceVariantId,
                        Price = c.Price,
                        Brand = c.Brand ?? string.Empty,
                        Socket = c.Socket ?? string.Empty,
                        Tdp = c.Tdp ?? 0,
                        Cores = c.Cores ?? 0,
                        BoostClock = c.BoostClock ?? 0,
                    });
                    break;

                case "gpu":
                    catalog.Gpus.Add(new GpuDto
                    {
                        Id = sourceVariantId,
                        Price = c.Price,
                        Brand = c.Brand ?? string.Empty,
                        Vram = c.Vram ?? 0,
                        Length = c.Length ?? 0,
                        PowerConsumption = c.PowerConsumption ?? 0,
                        RequiredConnectors = c.RequiredConnectors ?? new List<string>(),
                        RecommendedPsu = c.RecommendedPsu ?? 0,
                    });
                    break;

                case "motherboard":
                    catalog.Motherboards.Add(new MotherboardDto
                    {
                        Id = sourceVariantId,
                        Price = c.Price,
                        Socket = c.Socket ?? string.Empty,
                        FormFactor = c.FormFactor ?? string.Empty,
                        RamSlots = c.RamSlots ?? 0,
                        MaxRam = c.MaxRam ?? 0,
                        RamType = c.RamType ?? string.Empty,
                        Chipset = c.Chipset,
                        SupportedCpuGenerations = c.SupportedCpuGenerations ?? new List<string>(),
                        M2Slots = c.M2Slots ?? 0,
                        SataPorts = c.SataPorts ?? 0,
                        PcieVersion = c.PcieVersion ?? string.Empty,
                    });
                    break;

                case "ram":
                    catalog.Rams.Add(new RamDto
                    {
                        Id = sourceVariantId,
                        Price = c.Price,
                        Brand = c.Brand ?? string.Empty,
                        Capacity = c.Capacity ?? 0,
                        Speed = c.Speed ?? 0,
                        Type = c.MemoryType ?? string.Empty,
                    });
                    break;

                case "storage":
                    catalog.Storages.Add(new StorageDto
                    {
                        Id = sourceVariantId,
                        Price = c.Price,
                        Brand = c.Brand ?? string.Empty,
                        Capacity = c.Capacity ?? 0,
                        Type = c.DriveFormFactor ?? string.Empty,
                        Interface = c.DriveInterface ?? string.Empty,
                    });
                    break;

                case "psu":
                    catalog.Psus.Add(new PsuDto
                    {
                        Id = sourceVariantId,
                        Price = c.Price,
                        Wattage = c.Wattage ?? 0,
                        Certification = c.Certification ?? string.Empty,
                        PcieConnectors = c.PcieConnectors ?? new List<string>(),
                        CpuConnector = c.CpuConnector,
                    });
                    break;

                case "case":
                    catalog.Cases.Add(new CaseDto
                    {
                        Id = sourceVariantId,
                        Price = c.Price,
                        FormFactor = c.FormFactor ?? string.Empty,
                        Material = c.Material ?? string.Empty,
                        MaxGpuLength = c.MaxGpuLength ?? 0,
                        MaxCoolerHeight = c.MaxCoolerHeight ?? 0,
                        SupportedFormFactors = c.SupportedFormFactors ?? new List<string>(),
                        PsuFormFactor = c.PsuFormFactor ?? string.Empty,
                    });
                    break;

                case "cooling":
                    catalog.Coolings.Add(new CoolingDto
                    {
                        Id = sourceVariantId,
                        Price = c.Price,
                        Type = c.CoolingType ?? string.Empty,
                        TdpSupport = c.TdpSupport ?? 0,
                        SupportedSockets = c.SupportedSockets ?? new List<string>(),
                        Height = c.Height ?? 0,
                    });
                    break;
            }
        }

        return Result<PcComponentsCatalogDto>.Ok(catalog);
    }
}
