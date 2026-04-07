using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Recommendations
{
    public class CompatibilityChecker
    {
        public bool IsCompatible(BuildConfig c)
        {
            // Required core parts
            if (c.Cpu is null || c.Motherboard is null || c.Ram is null || c.Psu is null || c.Storage is null)
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
    }
}
