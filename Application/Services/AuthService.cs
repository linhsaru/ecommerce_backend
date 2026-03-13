using Application.Common;
using Application.DTOs.Auth;
using Application.Interfaces;
using Application.Interfaces.Services;
using Domain.Entities;
using Domain.Enums;
using Domain.Helpers;
using Domain.Interfaces.Repositories;

namespace Application.Services
{
    public sealed class AuthService : IAuthService
    {
        private readonly IRoleRepository _roleRepository;
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IJwtTokenGenerator _jwtTokenGenerator;

        public AuthService(
            IRoleRepository roleRepository,
            IUserRepository userRepository,
            IPasswordHasher passwordHasher,
            IJwtTokenGenerator jwtTokenGenerator)
        {
            _roleRepository = roleRepository;
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
            _jwtTokenGenerator = jwtTokenGenerator;
        }

        public async Task<Result<LoginResponse>> LoginAsync(LoginRequest loginRequest, CancellationToken cancellationToken = default)
        {
            // 1. Tìm user
            var user = await _userRepository.GetUserAsync(loginRequest.Email, cancellationToken);

            if (user == null)
            {
                return Result<LoginResponse>.Fail("AUTH_001", "Email hoặc mật khẩu không chính xác.");
            }

            // 2. Kiểm tra trạng thái
            if (user.Status != 1)
            {
                return Result<LoginResponse>.Fail("AUTH_002", "Tài khoản đang bị khóa hoặc chưa kích hoạt.");
            }

            // 3. Kiểm tra mật khẩu
            bool isPasswordValid = _passwordHasher.Verify(loginRequest.Password, user.PasswordHash!);
            if (!isPasswordValid)
            {
                return Result<LoginResponse>.Fail("AUTH_001", "Email hoặc mật khẩu không chính xác.");
            }

            // 4. Tạo access token, refresh token và trả về LoginResponse
            var accessToken = _jwtTokenGenerator.GenerateToken(user.Username);
            var refreshToken = _jwtTokenGenerator.GenerateRefreshToken(user.Username);
                
            var role = await _roleRepository.GetRoleAsync(user.RoleId ?? Guid.Empty, cancellationToken);
                
            var loginResponse = new LoginResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                Username = user.Username,
                Role = role.RoleName
            };
            return Result<LoginResponse>.Ok(loginResponse);
        }

        public async Task<Result<string>> RegisterAsync(RegisterRequest registerRequest, CancellationToken cancellationToken = default)
        {
            // 1. Check trùng Email
            var existingUser = await _userRepository.GetUserAsync(registerRequest.Email, cancellationToken);
            if (existingUser != null)
            {
                return Result<string>.Fail("AUTH_003", "Email này đã tồn tại trong hệ thống.");
            }

            if(registerRequest.Password != registerRequest.ConfirmPassword)
            {
                return Result<string>.Fail("AUTH_004", "Mật khẩu và xác nhận mật khẩu không khớp.");
            }
            if(string.IsNullOrWhiteSpace(registerRequest.UserName))
            {
                return Result<string>.Fail("AUTH_005", "Username không được để trống.");
            }
            if(string.IsNullOrWhiteSpace(registerRequest.FullName))
            {
                return Result<string>.Fail("AUTH_006", "FullName không được để trống.");
            }
             if(string.IsNullOrWhiteSpace(registerRequest.PhoneNumber))
            {
                return Result<string>.Fail("AUTH_007", "PhoneNumber không được để trống.");
            }

            // 2. Hash mật khẩu & Tạo User
            var passwordHash = _passwordHasher.Hash(registerRequest.Password);

            var newUser = new User
            {
                Id = Guid.NewGuid(),
                Email = registerRequest.Email,
                FullName = registerRequest.FullName,
                Username = registerRequest.UserName,
                Phone = registerRequest.PhoneNumber,
                PasswordHash = passwordHash,
                Status = 1,
                RoleId = RoleHelper.GetId(UserRole.RoleUser)
            };

            // 3. Lưu vào DB
            await _userRepository.AddAsync(newUser, cancellationToken);

            // Vì trả về Result<string>, bạn có thể trả về Email hoặc message thành công
            return Result<string>.Ok(newUser.Email);
        }
    }
}