using System;
using System.Collections.Generic;
using System.Linq;
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
            if (c.Cooling is not null && c.Case is not null && c.Case.MaxCoolerHeight > 0 && c.Cooling.Height > c.Case.MaxCoolerHeight)
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
            if (c.Cooling is not null && c.Case is not null && c.Case.MaxCoolerHeight > 0 && c.Cooling.Height > c.Case.MaxCoolerHeight)
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

            // PSU power headroom: CPU + GPU + base 90W then x1.25
            var totalPower = (c.Cpu.Tdp > 0 ? c.Cpu.Tdp : 65) + (c.Gpu?.PowerConsumption ?? 0) + 90;
            if (c.Psu.Wattage <= 0 || c.Psu.Wattage < totalPower * 1.25)
                return false;

            return true;
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
                return false;

            var supported = (pcCase.SupportedFormFactors ?? new List<string>())
                .Select(NormalizeFormFactor)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (supported.Count == 0)
            {
                var caseForm = NormalizeFormFactor(pcCase.FormFactor);
                return !string.IsNullOrWhiteSpace(caseForm) && string.Equals(caseForm, mbForm, StringComparison.OrdinalIgnoreCase);
            }

            return supported.Contains(mbForm);
        }

        private static bool HasRequiredPcieConnectors(PsuDto psu, GpuDto gpu)
        {
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
                return false;

            foreach (var req in required)
            {
                if (!available.Any(av => av.Contains(req, StringComparison.OrdinalIgnoreCase) || req.Contains(av, StringComparison.OrdinalIgnoreCase)))
                    return false;
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
