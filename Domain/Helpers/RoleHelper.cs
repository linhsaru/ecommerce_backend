using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Helpers
{
    public static class RoleHelper
    {
        public static Guid GetId(UserRole role)
        {
            return role switch
            {
                UserRole.RoleAdmin => Guid.Parse("B7E3F2A1-1234-4A5B-8C9D-E1F2A3B4C5D6"),
                UserRole.RoleUser => Guid.Parse("C9D8E7F6-5678-4D3C-2B1A-F9E8D7C6B5A4"),
                _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
            };
        }
    }
}
