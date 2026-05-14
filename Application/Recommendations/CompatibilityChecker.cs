using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using Application.DTOs.Components;

namespace Application.Recommendations
{
    public class CompatibilityChecker
    {
        public bool IsPartiallyCompatible(BuildConfig c)
        {
            // CPU - Mainboard
            if (c.Cpu is not null && c.Motherboard is not null && !EqualsIgnoreCase(c.Cpu.Socket, c.Motherboard.Socket))
                return false;

            // RAM - Mainboard
            if (c.Ram is not null && c.Motherboard is not null && !EqualsIgnoreCase(c.Ram.Type, c.Motherboard.RamType))
                return false;

            // Mainboard - Case form factor
            if (c.Motherboard is not null && c.Case is not null && !IsMotherboardCaseCompatible(c.Motherboard, c.Case))
                return false;

            // GPU - Case
            if (c.Gpu is not null && c.Case is not null && c.Case.MaxGpuLength > 0 && c.Gpu.Length > c.Case.MaxGpuLength)
                return false;

            // Cooler - Case
            if (c.Cooling is not null && c.Case is not null && !IsCoolingCaseCompatible(c.Cooling, c.Case))
                return false;

            // Storage - Mainboard slots (heuristic: match by slot type only)
            if (c.Storage is not null && c.Motherboard is not null && !IsStorageMotherboardCompatible(c.Storage, c.Motherboard))
                return false;

            // Cooler - CPU socket
            if (c.Cooling?.SupportedSockets is { Count: > 0 } && c.Cpu is not null)
            {
                var cpuSocket = c.Cpu.Socket?.Trim() ?? string.Empty;
                var supported = c.Cooling.SupportedSockets
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .ToList();
                if (!supported.Any(x => EqualsIgnoreCase(x, cpuSocket)))
                    return false;
            }

            // Cooler - CPU TDP
            if (c.Cooling is not null && c.Cpu is not null && c.Cooling.TdpSupport > 0 && c.Cpu.Tdp > 0)
            {
                if (c.Cooling.TdpSupport + 10 < c.Cpu.Tdp)
                    return false;
            }

            // PSU - GPU connectors
            if (c.Gpu is not null && c.Psu is not null && !HasRequiredPcieConnectors(c.Psu, c.Gpu))
                return false;

            return true;
        }

        public bool IsCompatible(BuildConfig c)
        {
            // Required core parts
            if (c.Cpu is null || c.Motherboard is null || c.Ram is null || c.Psu is null || c.Storage is null)
                return false;

            if (!IsPartiallyCompatible(c))
                return false;

            // CPU - Mainboard
            if (!EqualsIgnoreCase(c.Cpu.Socket, c.Motherboard.Socket))
                return false;

            // RAM - Mainboard
            if (!EqualsIgnoreCase(c.Ram.Type, c.Motherboard.RamType))
                return false;

            // GPU - Case
            if (c.Gpu is not null && c.Case is not null && c.Case.MaxGpuLength > 0 && c.Gpu.Length > c.Case.MaxGpuLength)
                return false;

            // Cooler - Case (if both exist and case has constraint)
            if (c.Cooling is not null && c.Case is not null && !IsCoolingCaseCompatible(c.Cooling, c.Case))
                return false;

            // Cooler - CPU socket (if cooling specifies supported sockets)
            if (c.Cooling?.SupportedSockets is { Count: > 0 })
            {
                var cpuSocket = c.Cpu.Socket?.Trim() ?? string.Empty;
                var supported = c.Cooling.SupportedSockets
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .ToList();
                if (!supported.Any(x => EqualsIgnoreCase(x, cpuSocket)))
                    return false;
            }

            // Cooler - CPU TDP safety margin
            if (c.Cooling is not null && c.Cooling.TdpSupport > 0 && c.Cpu.Tdp > 0)
            {
                if (c.Cooling.TdpSupport + 10 < c.Cpu.Tdp)
                    return false;
            }

            // PSU - GPU connectors
            if (c.Gpu is not null && !HasRequiredPcieConnectors(c.Psu, c.Gpu))
                return false;

            // Storage - Mainboard slots (do not compare PCIe generations)
            if (!IsStorageMotherboardCompatible(c.Storage, c.Motherboard))
                return false;

            // PSU power headroom: CPU + GPU + base 90W then x1.25
            var totalPower = (c.Cpu.Tdp > 0 ? c.Cpu.Tdp : 65) + (c.Gpu?.PowerConsumption ?? 0) + 90;
            if (c.Psu.Wattage <= 0 || c.Psu.Wattage < totalPower * 1.25)
                return false;

            return true;
        }

        private static bool IsCoolingCaseCompatible(CoolingDto cooling, CaseDto pcCase)
        {
            // Air cooler height check when available.
            if (pcCase.MaxCoolerHeight > 0 && cooling.Height > 0 && cooling.Height > pcCase.MaxCoolerHeight)
                return false;

            // AIO heuristic when case radiator metadata is missing.
            if (!IsAioCooling(cooling))
                return true;

            var radiatorSize = InferRadiatorSizeMm(cooling);
            if (radiatorSize <= 0)
                return true;

            var caseCaps = InferCaseRadiatorCapability(pcCase);
            if (radiatorSize >= 360)
                return caseCaps.Supports360;
            if (radiatorSize >= 240)
                return caseCaps.Supports240;
            return true;
        }

        private static bool IsStorageMotherboardCompatible(StorageDto storage, MotherboardDto motherboard)
        {
            var storageType = (storage.Type ?? string.Empty).Trim().ToUpperInvariant();
            var storageInterface = (storage.Interface ?? string.Empty).Trim().ToUpperInvariant();

            if (storageType.Contains("M.2") || storageType.Contains("M2"))
            {
                if (motherboard.M2Slots <= 0)
                    return false;
            }

            if (storageInterface.Contains("SATA"))
            {
                if (motherboard.SataPorts <= 0)
                    return false;
            }

            return true;
        }

        private static bool IsAioCooling(CoolingDto cooling)
        {
            var type = (cooling.Type ?? string.Empty).Trim().ToUpperInvariant();
            var text = $"{cooling.ProductName} {cooling.VariantName} {cooling.Type}".ToUpperInvariant();
            return type.Contains("AIO") || type.Contains("LIQUID") || text.Contains("AIO") || text.Contains("LIQUID");
        }

        private static int InferRadiatorSizeMm(CoolingDto cooling)
        {
            var text = $"{cooling.ProductName} {cooling.VariantName} {cooling.Type}";
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            var m = Regex.Match(text, @"(?<!\d)(120|140|240|280|360|420)\s*mm?(?!\d)", RegexOptions.IgnoreCase);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var mm))
                return mm;

            // Common naming like AIO 240 / 360 without "mm"
            m = Regex.Match(text, @"(?<!\d)(240|280|360|420)(?!\d)");
            if (m.Success && int.TryParse(m.Groups[1].Value, out mm))
                return mm;

            return 0;
        }

        private static (bool Supports240, bool Supports360) InferCaseRadiatorCapability(CaseDto pcCase)
        {
            var allForms = new List<string>();
            if (!string.IsNullOrWhiteSpace(pcCase.FormFactor))
                allForms.Add(pcCase.FormFactor);
            if (pcCase.SupportedFormFactors is { Count: > 0 })
                allForms.AddRange(pcCase.SupportedFormFactors.Where(x => !string.IsNullOrWhiteSpace(x)));

            var normalized = allForms
                .Select(x => x.Trim().ToUpperInvariant().Replace("-", string.Empty))
                .ToList();

            var supportsAtx = normalized.Any(x => x.Contains("ATX") && !x.Contains("MICRO"));
            var hasTowerHint = normalized.Any(x => x.Contains("MIDTOWER") || x.Contains("FULLTOWER"));
            var supportsMicroOnly = normalized.Any(x => x.Contains("MICROATX") || x.Contains("MATX") || x.Contains("MINIITX") || x == "ITX")
                                    && !supportsAtx && !hasTowerHint;

            if (supportsAtx || hasTowerHint)
                return (Supports240: true, Supports360: true);

            if (supportsMicroOnly)
                return (Supports240: true, Supports360: false);

            // Unknown case capability -> permissive to avoid false negatives from incomplete data.
            return (Supports240: true, Supports360: true);
        }

        private static bool EqualsIgnoreCase(string? a, string? b)
        {
            var x = (a ?? string.Empty).Trim();
            var y = (b ?? string.Empty).Trim();
            return string.Equals(x, y, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMotherboardCaseCompatible(MotherboardDto motherboard, CaseDto pcCase)
        {
            var mbForm = NormalizeFormFactor(motherboard.FormFactor);
            if (string.IsNullOrWhiteSpace(mbForm))
                return true;

            var supported = (pcCase.SupportedFormFactors ?? new List<string>())
                .Select(NormalizeFormFactor)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (supported.Count == 0)
            {
                var caseForm = NormalizeFormFactor(pcCase.FormFactor);
                if (string.IsNullOrWhiteSpace(caseForm))
                    return true;
                return string.Equals(caseForm, mbForm, StringComparison.OrdinalIgnoreCase);
            }

            return supported.Contains(mbForm);
        }

        private static bool HasRequiredPcieConnectors(PsuDto psu, GpuDto gpu)
        {
            if (gpu.RecommendedPsu > 0 && psu.Wattage > 0 && psu.Wattage < gpu.RecommendedPsu)
                return false;

            var required = (gpu.RequiredConnectors ?? new List<string>())
                .Select(NormalizeConnector)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            if (required.Count == 0)
                return true;

            var available = (psu.PcieConnectors ?? new List<string>())
                .Select(NormalizeConnector)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            if (available.Count == 0)
            {
                // Connector metadata may be missing in DB; trust recommended PSU when present.
                return gpu.RecommendedPsu > 0;
            }

            foreach (var req in required)
            {
                if (!available.Any(av => av.Contains(req, StringComparison.OrdinalIgnoreCase) || req.Contains(av, StringComparison.OrdinalIgnoreCase)))
                {
                    if (gpu.RecommendedPsu > 0)
                        return true;
                    return false;
                }
            }

            return true;
        }

        private static string NormalizeFormFactor(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var v = value.Trim().ToUpperInvariant().Replace(" ", string.Empty).Replace("-", string.Empty);
            if (v.Contains("MICROATX") || v == "MATX") return "MICROATX";
            if (v.Contains("MINIITX") || v == "ITX") return "MINIITX";
            if (v.Contains("ATX")) return "ATX";
            return v;
        }

        private static string NormalizeConnector(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Trim().ToUpperInvariant().Replace(" ", string.Empty).Replace("-", string.Empty);
        }
    }
}
