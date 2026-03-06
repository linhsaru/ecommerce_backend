using Application.Common;
using Application.DTOs.Auth;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Services
{
    public interface IAuthService
    {
        Task<Result<LoginResponse>> LoginAsync(LoginRequest loginRequest, CancellationToken cancellationToken = default);
        Task<Result<string>> RegisterAsync(RegisterRequest registerRequest, CancellationToken cancellationToken = default);
    }
}
