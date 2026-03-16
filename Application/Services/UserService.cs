using Application.Common;
using Application.DTOs.Users;
using Application.Interfaces.Services;
using Domain.Entities;
using Domain.Helpers;
using Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _userRepo;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRoleRepository _roleRepo;

    public UserService(IUserRepository userRepo, IPasswordHasher passwordHasher, IRoleRepository roleRepo)
    {
        _userRepo = userRepo;
        _passwordHasher = passwordHasher;
        _roleRepo = roleRepo;
    }

    public async Task<Result<(List<UserDto> Items, long Total)>> GetPagedAsync(int page, int pageSize, string? search, int? status, CancellationToken cancellationToken = default)
    {
       IQueryable<User> query = _userRepo.GetQueryable().Include(u => u.Role);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => u.Email.Contains(search) || (u.FullName != null && u.FullName.Contains(search)) || (u.Username != null && u.Username.Contains(search)));
        if (status.HasValue)
            query = query.Where(u => u.Status == status.Value);

        var total = await query.LongCountAsync(cancellationToken);
        var skip = (Math.Max(1, page) - 1) * Math.Clamp(pageSize, 1, 100);
        var items = await query
            .OrderBy(u => u.CreatedAt)
            .Skip((int)skip)
            .Take(Math.Clamp(pageSize, 1, 100))
            .Select(u => new UserDto
            {
                Id = u.Id,
                RoleId = u.RoleId,
                RoleName = u.Role != null ? u.Role.RoleName : null,
                Email = u.Email,
                Phone = u.Phone,
                FullName = u.FullName,
                Username = u.Username,
                AvatarUrl = u.AvatarUrl,
                Status = u.Status,
                LastLogin = u.LastLogin,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return Result<(List<UserDto> Items, long Total)>.Ok((items, total));
    }

    public async Task<Result<UserDto?>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepo.GetByIdAsync(id, cancellationToken);
        if (user == null)
            return Result<UserDto?>.Fail("NOT_FOUND", "User not found.");
        return Result<UserDto?>.Ok(Map(user));
    }

    public async Task<Result<UserDto>> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        if (await _userRepo.ExistsByEmailAsync(request.Email, null, cancellationToken))
            return Result<UserDto>.Fail("VALIDATION_ERROR", "Email already exists.");

        var passwordHash = _passwordHasher.Hash(request.Password);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = passwordHash,
            FullName = request.FullName,
            Username = request.Username ?? request.Email,
            Phone = request.Phone,
            RoleId = request.RoleId ?? RoleHelper.GetId(Domain.Enums.UserRole.RoleUser),
            Status = request.Status
        };
        await _userRepo.AddAsync(user, cancellationToken);

        var created = await _userRepo.GetByIdAsync(user.Id, cancellationToken);
        return Result<UserDto>.Ok(Map(created!));
    }

    public async Task<Result<UserDto>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepo.GetByIdAsync(id, cancellationToken);
        if (user == null)
            return Result<UserDto>.Fail("NOT_FOUND", "User not found.");

        if (request.FullName != null) user.FullName = request.FullName;
        if (request.Username != null) user.Username = request.Username;
        if (request.Phone != null) user.Phone = request.Phone;
        if (request.AvatarUrl != null) user.AvatarUrl = request.AvatarUrl;
        if (request.RoleId.HasValue) user.RoleId = request.RoleId;
        if (request.Status.HasValue) user.Status = request.Status.Value;
        if (!string.IsNullOrEmpty(request.Password))
            user.PasswordHash = _passwordHasher.Hash(request.Password);

        _userRepo.Update(user);
        await _userRepo.SaveChangesAsync(cancellationToken);

        var updated = await _userRepo.GetByIdAsync(id, cancellationToken);
        return Result<UserDto>.Ok(Map(updated!));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepo.GetByIdAsync(id, cancellationToken);
        if (user == null)
            return Result.Fail("NOT_FOUND", "User not found.");

        user.MarkDeleted(null);
        _userRepo.Update(user);
        await _userRepo.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    private static UserDto Map(User u) => new()
    {
        Id = u.Id,
        RoleId = u.RoleId,
        RoleName = u.Role?.RoleName,
        Email = u.Email,
        Phone = u.Phone,
        FullName = u.FullName,
        Username = u.Username,
        AvatarUrl = u.AvatarUrl,
        Status = u.Status,
        LastLogin = u.LastLogin,
        CreatedAt = u.CreatedAt,
        UpdatedAt = u.UpdatedAt
    };
}
