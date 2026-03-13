using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Auth
{
    public sealed class LoginResponse
    {
        public string AccessToken { get; set; } = "";

        public string RefreshToken { get; set; } = "";
        public string Username { get; set; } = "";

        public string? Role { get; set; } 

    }
}
