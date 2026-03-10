using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
    public class Role
    {
        public Guid Id { get; set; }
        public required string RoleName { get; set; }

        public string? Description { get; set; }

        public ICollection<User>? Users { get; set; } = new List<User>();
    }
}
