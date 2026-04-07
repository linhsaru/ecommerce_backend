using System.Collections.Generic;
using Domain.Common;

namespace Domain.Entities;

public class Component : SoftDeleteEntity<Guid>
{
    public Guid? ProductVariantId { get; set; }

    //CPU, GPU, Motherboard, RAM, PSU, Storage, Cooling, Case
    public required string ComponentType { get; set; }

    public required string Name { get; set; }

    public decimal Price { get; set; }

    public string? Brand { get; set; }

    // --- CPU & Motherboard ---
    public string? Socket { get; set; }

    // --- CPU ---
    public int? Tdp { get; set; }

    public int? Cores { get; set; }

    public double? BoostClock { get; set; }

    // --- GPU ---
    public int? Vram { get; set; }

    //Chiều dài card (mm).
    public int? Length { get; set; }

    public int? PowerConsumption { get; set; }

    public List<string>? RequiredConnectors { get; set; }

    public int? RecommendedPsu { get; set; }

    // --- Motherboard & Case (cùng tên JSON; ý nghĩa tùy ComponentType) ---
    public string? FormFactor { get; set; }

    // --- Motherboard ---
    public int? RamSlots { get; set; }

    public int? MaxRam { get; set; }

    public string? RamType { get; set; }

    public string? Chipset { get; set; }

    public List<string>? SupportedCpuGenerations { get; set; }

    public int? M2Slots { get; set; }

    public int? SataPorts { get; set; }

    public string? PcieVersion { get; set; }

    // --- RAM & Storage (Capacity: GB) ---
    public int? Capacity { get; set; }

    // --- RAM ---
    public int? Speed { get; set; }

    //JSON RAM field <c>Type</c> (DDR4, DDR5, …).
    public string? MemoryType { get; set; }

    public string? Timings { get; set; }

    public string? Voltage { get; set; }

    // --- PSU ---
    public int? Wattage { get; set; }

    public string? Certification { get; set; }

    public List<string>? PcieConnectors { get; set; }

    public string? CpuConnector { get; set; }

    // --- Storage ---
    //JSON Storage field <c>Interface</c> (PCIe Gen4 x4, …)
    public string? DriveInterface { get; set; }

    //JSON Storage field <c>Type</c> (M.2 2280, …).
    public string? DriveFormFactor { get; set; }

    // --- Cooling ---
    //JSON Cooling field <c>Type</c> (fan, air, liquid).
    public string? CoolingType { get; set; }

    public int? TdpSupport { get; set; }

    public List<string>? SupportedSockets { get; set; }

    //Chiều cao tản (mm).
    public int? Height { get; set; }

    // --- Case ---
    public string? Material { get; set; }

    public int? MaxGpuLength { get; set; }

    public int? MaxCoolerHeight { get; set; }

    public List<string>? SupportedFormFactors { get; set; }

    public string? PsuFormFactor { get; set; }

    public ProductVariant? ProductVariant { get; set; }
}
