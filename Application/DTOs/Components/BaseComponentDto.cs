using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Components
{
    //Lọc ra các thành phần linh kiện phục vụ cho việc xây dựng cấu hình máy tính
    //Nhóm filter
    //Nhóm compatibility
    //Nhóm scoring
    public abstract class BaseComponentDto
    {
        public Guid Id { get; set; }
        public decimal Price { get; set; }
        public Guid? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? ProductSlug { get; set; }
        public string? ProductThumbnailUrl { get; set; }
        public string? Sku { get; set; }
        public string? VariantName { get; set; }
        public decimal? VariantPrice { get; set; }
        public decimal? CompareAt { get; set; }
        public int? VariantStatus { get; set; }
    }

    public class CaseDto : BaseComponentDto
    {
        public string FormFactor { get; set; } = "";
        public string Material { get; set; } = "";
        public int MaxGpuLength { get; set; }
        public int MaxCoolerHeight { get; set; }
        public List<string> SupportedFormFactors { get; set; } = new List<string>(); // ATX, Micro-ATX, Mini-ITX
        public string PsuFormFactor { get; set; } = ""; // ATX, SFX
    }

    public class CoolingDto : BaseComponentDto
    {
        public string Type { get; set; } = "";
        public int TdpSupport { get; set; }
        public List<string>? SupportedSockets { get; set; }
        public int Height { get; set; }
    }

    public class CpuDto : BaseComponentDto
    {
        public string Socket { get; set; } = "";
        public string Brand { get; set; } = "";
        public int Tdp { get; set; }
        public int Cores { get; set; }
        public double BoostClock { get; set; }
    }

    public class GpuDto : BaseComponentDto
    {
        public string Brand { get; set; } = "";
        public int Vram { get; set; }
        public double Length { get; set; }
        public int PowerConsumption { get; set; }
        public List<string>? RequiredConnectors { get; set; }
        public int RecommendedPsu { get; set; }
    }

    public class MotherboardDto : BaseComponentDto
    {
        public string Socket { get; set; } = "";
        public string FormFactor { get; set; } = "";
        public int RamSlots { get; set; }
        public int MaxRam { get; set; }
        public string RamType { get; set; } = "";
        public string? Chipset { get; set; }
        public List<string>? SupportedCpuGenerations { get; set; }
        public int M2Slots { get; set; }
        public int SataPorts { get; set; }
        public string PcieVersion { get; set; }
    }

    public class PsuDto : BaseComponentDto
    {
        public int Wattage { get; set; }
        public string Certification { get; set; } = "";
        public List<string>? PcieConnectors { get; set; } // 8pin, 12VHPWR
        public string? CpuConnector { get; set; } // 8pin, 4+4
    }

    public class RamDto : BaseComponentDto
    {
        public string Brand { get; set; } = "";
        public int Capacity { get; set; }
        public int Speed { get; set; }
        public string Type { get; set; } = "";
    }

    public class StorageDto : BaseComponentDto
    {
        public string Brand { get; set; } = "";
        public int Capacity { get; set; }
        public string Type { get; set; } = "";
        public string Interface { get; set; } = ""; // SATA, NVMe
    }
}
