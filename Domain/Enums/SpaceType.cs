using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Enums
{
    public enum SpecType
    {

        Socket,
        Cores,
        Threads,
        BaseClock,
        BoostClock,
        Tdp,

        Chipset,
        Vram,
        GpuLength,
        PowerConsumption,

        RamType,
        RamSpeed,
        RamCapacity,
        RamModules,
        MaxRam,

        PcieVersion,

        StorageType,
        StorageInterface,
        StorageFormFactor,
        StorageCapacity,
        ReadSpeed,
        WriteSpeed,

        PsuWattage,
        PsuEfficiency,
        PsuModular,

        CaseFormFactorSupport,
        CaseMaxGpuLength,

        CoolerType
    }
}
