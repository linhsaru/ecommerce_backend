using Application.DTOs.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Recommendations
{
    public class BuildConfig
    {
        public CpuDto? Cpu { get; set; }
        public GpuDto? Gpu { get; set; }
        public RamDto? Ram { get; set; }
        public StorageDto? Storage { get; set; }
        public CaseDto? Case { get; set; }
        public MotherboardDto? Motherboard { get; set; }
        public CoolingDto? Cooling { get; set; }
        public PsuDto? Psu { get; set; }

        public decimal TotalPrice => (Cpu?.Price ?? 0) + (Gpu?.Price ?? 0) + (Ram?.Price ?? 0) + (Storage?.Price ?? 0) +
                                    (Case?.Price ?? 0) + (Motherboard?.Price ?? 0) + (Cooling?.Price ?? 0) + (Psu?.Price ?? 0);

        public double Score { get; set; }
    }
}
