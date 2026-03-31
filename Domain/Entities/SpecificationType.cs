using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//Bảng thông tin chi tiết về thông số kỹ thuật của sản phẩm
namespace Domain.Entities
{
    public class SpecificationType
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = null!;

        public string? Unit { get; set; }
    }
}
