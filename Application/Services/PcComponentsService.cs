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

        var variantIds = components
            .Where(x => x.ProductVariantId.HasValue)
            .Select(x => x.ProductVariantId!.Value)
            .Distinct()
            .ToList();

        var variantLookup = await _db.ProductVariants
            .AsNoTracking()
            .Where(x => variantIds.Contains(x.Id))
            .Select(x => new VariantLookupItem
            {
                VariantId = x.Id,
                ProductId = x.ProductId,
                ProductName = x.Product.Name,
                ProductSlug = x.Product.Slug,
                ProductThumbnailUrl = x.Product.ThumbnailUrl,
                Sku = x.Sku,
                VariantName = x.VariantName,
                VariantPrice = x.Price,
                CompareAt = x.CompareAt,
                VariantStatus = x.Status
            })
            .ToDictionaryAsync(x => x.VariantId, x => x, cancellationToken);

        var catalog = new PcComponentsCatalogDto();

        foreach (var c in components)
        {
            var type = (c.ComponentType ?? string.Empty).Trim().ToLowerInvariant();
            var sourceVariantId = c.ProductVariantId ?? c.Id;

            switch (type)
            {
                case "cpu":
                    var cpu = new CpuDto
                    {
                        Id = sourceVariantId,
                        Price = c.Price,
                        Brand = c.Brand ?? string.Empty,
                        Socket = c.Socket ?? string.Empty,
                        Tdp = c.Tdp ?? 0,
                        Cores = c.Cores ?? 0,
                        BoostClock = c.BoostClock ?? 0,
                    };
                    ApplyVariantMetadata(cpu, sourceVariantId, variantLookup);
                    catalog.Cpus.Add(cpu);
                    break;

                case "gpu":
                    var gpu = new GpuDto
                    {
                        Id = sourceVariantId,
                        Price = c.Price,
                        Brand = c.Brand ?? string.Empty,
                        Vram = c.Vram ?? 0,
                        Length = c.Length ?? 0,
                        PowerConsumption = c.PowerConsumption ?? 0,
                        RequiredConnectors = c.RequiredConnectors ?? new List<string>(),
                        RecommendedPsu = c.RecommendedPsu ?? 0,
                    };
                    ApplyVariantMetadata(gpu, sourceVariantId, variantLookup);
                    catalog.Gpus.Add(gpu);
                    break;

                case "motherboard":
                    var motherboard = new MotherboardDto
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
                    };
                    ApplyVariantMetadata(motherboard, sourceVariantId, variantLookup);
                    catalog.Motherboards.Add(motherboard);
                    break;

                case "ram":
                    var ram = new RamDto
                    {
                        Id = sourceVariantId,
                        Price = c.Price,
                        Brand = c.Brand ?? string.Empty,
                        Capacity = c.Capacity ?? 0,
                        Speed = c.Speed ?? 0,
                        Type = c.MemoryType ?? string.Empty,
                    };
                    ApplyVariantMetadata(ram, sourceVariantId, variantLookup);
                    catalog.Rams.Add(ram);
                    break;

                case "storage":
                    var storage = new StorageDto
                    {
                        Id = sourceVariantId,
                        Price = c.Price,
                        Brand = c.Brand ?? string.Empty,
                        Capacity = c.Capacity ?? 0,
                        Type = c.DriveFormFactor ?? string.Empty,
                        Interface = c.DriveInterface ?? string.Empty,
                    };
                    ApplyVariantMetadata(storage, sourceVariantId, variantLookup);
                    catalog.Storages.Add(storage);
                    break;

                case "psu":
                    var psu = new PsuDto
                    {
                        Id = sourceVariantId,
                        Price = c.Price,
                        Wattage = c.Wattage ?? 0,
                        Certification = c.Certification ?? string.Empty,
                        PcieConnectors = c.PcieConnectors ?? new List<string>(),
                        CpuConnector = c.CpuConnector,
                    };
                    ApplyVariantMetadata(psu, sourceVariantId, variantLookup);
                    catalog.Psus.Add(psu);
                    break;

                case "case":
                    var pcCase = new CaseDto
                    {
                        Id = sourceVariantId,
                        Price = c.Price,
                        FormFactor = c.FormFactor ?? string.Empty,
                        Material = c.Material ?? string.Empty,
                        MaxGpuLength = c.MaxGpuLength ?? 0,
                        MaxCoolerHeight = c.MaxCoolerHeight ?? 0,
                        SupportedFormFactors = c.SupportedFormFactors ?? new List<string>(),
                        PsuFormFactor = c.PsuFormFactor ?? string.Empty,
                    };
                    ApplyVariantMetadata(pcCase, sourceVariantId, variantLookup);
                    catalog.Cases.Add(pcCase);
                    break;

                case "cooling":
                    var cooling = new CoolingDto
                    {
                        Id = sourceVariantId,
                        Price = c.Price,
                        Type = c.CoolingType ?? string.Empty,
                        TdpSupport = c.TdpSupport ?? 0,
                        SupportedSockets = c.SupportedSockets ?? new List<string>(),
                        Height = c.Height ?? 0,
                    };
                    ApplyVariantMetadata(cooling, sourceVariantId, variantLookup);
                    catalog.Coolings.Add(cooling);
                    break;
            }
        }

        return Result<PcComponentsCatalogDto>.Ok(catalog);
    }

    private static void ApplyVariantMetadata(BaseComponentDto dto, Guid sourceVariantId, IReadOnlyDictionary<Guid, VariantLookupItem> variantLookup)
    {
        if (!variantLookup.TryGetValue(sourceVariantId, out var v))
            return;

        dto.ProductId = v.ProductId;
        dto.ProductName = v.ProductName;
        dto.ProductSlug = v.ProductSlug;
        dto.ProductThumbnailUrl = v.ProductThumbnailUrl;
        dto.Sku = v.Sku;
        dto.VariantName = v.VariantName;
        dto.VariantPrice = v.VariantPrice;
        dto.CompareAt = v.CompareAt;
        dto.VariantStatus = v.VariantStatus;
    }

    private sealed class VariantLookupItem
    {
        public Guid VariantId { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductSlug { get; set; } = string.Empty;
        public string? ProductThumbnailUrl { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string? VariantName { get; set; }
        public decimal VariantPrice { get; set; }
        public decimal? CompareAt { get; set; }
        public int VariantStatus { get; set; }
    }
}
