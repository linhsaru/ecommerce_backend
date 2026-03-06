using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Common
{
    public sealed record Error(string Code, string Message, string? Details = null)
    {

    }
}
