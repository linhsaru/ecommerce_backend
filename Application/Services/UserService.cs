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

        if (request.Addresses != null)
        {
            var hasManyDefault = request.Addresses.Count(a => a.IsDefault) > 1;
            if (hasManyDefault)
                return Result<UserDto>.Fail("VALIDATION_ERROR", "Chỉ được có 1 địa chỉ mặc định.");

            var now = DateTimeOffset.UtcNow;
            var existingActiveAddresses = user.Addresses
                .Where(a => !a.IsDeleted)
                .OrderBy(a => a.CreatedAt)
                .ToList();

            var maxCount = Math.Max(existingActiveAddresses.Count, request.Addresses.Count);
            for (var i = 0; i < maxCount; i++)
            {
                var hasExisting = i < existingActiveAddresses.Count;
                var hasIncoming = i < request.Addresses.Count;

                if (hasExisting && hasIncoming)
                {
                    // Update existing address if user already has one at this position.
                    var existing = existingActiveAddresses[i];
                    var incoming = request.Addresses[i];
                    existing.Recipient = incoming.Recipient;
                    existing.Phone = incoming.Phone;
                    existing.Line1 = incoming.Line1;
                    existing.Line2 = incoming.Line2;
                    existing.Ward = incoming.Ward;
                    existing.District = incoming.District;
                    existing.Province = incoming.Province;
                    existing.Country = string.IsNullOrWhiteSpace(incoming.Country) ? "VN" : incoming.Country;
                    existing.PostalCode = incoming.PostalCode;
                    existing.IsDefault = incoming.IsDefault;
                    existing.UpdatedAt = now;
                    continue;
                }

                if (!hasExisting && hasIncoming)
                {
                    var incoming = request.Addresses[i];
                    user.Addresses.Add(new UserAddress
                    {
                        UserId = user.Id,
                        Recipient = incoming.Recipient,
                        Phone = incoming.Phone,
                        Line1 = incoming.Line1,
                        Line2 = incoming.Line2,
                        Ward = incoming.Ward,
                        District = incoming.District,
                        Province = incoming.Province,
                        Country = string.IsNullOrWhiteSpace(incoming.Country) ? "VN" : incoming.Country,
                        PostalCode = incoming.PostalCode,
                        IsDefault = incoming.IsDefault,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                    continue;
                }

                if (hasExisting && !hasIncoming)
                {
                    existingActiveAddresses[i].MarkDeleted();
                }
            }
        }

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
        await _userRepo.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result<UserDto>> UpdateUserRoleAsync(Guid id, Guid roleId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepo.GetByIdAsync(id, cancellationToken);
        if (user == null)
            return Result<UserDto>.Fail("NOT_FOUND", "User not found.");

        // Validate role exists
        try
        {
            await _roleRepo.GetRoleAsync(roleId, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return Result<UserDto>.Fail("NOT_FOUND", "Role not found.");
        }

        user.RoleId = roleId;

        await _userRepo.SaveChangesAsync(cancellationToken);

        var updated = await _userRepo.GetByIdAsync(id, cancellationToken);
        return Result<UserDto>.Ok(Map(updated!));
    }

    public async Task<Result<UserDto>> RemoveUserRoleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userRepo.GetByIdAsync(id, cancellationToken);
        if (user == null)
            return Result<UserDto>.Fail("NOT_FOUND", "User not found.");

        user.RoleId = null;

        await _userRepo.SaveChangesAsync(cancellationToken);

        var updated = await _userRepo.GetByIdAsync(id, cancellationToken);
        return Result<UserDto>.Ok(Map(updated!));
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
        UpdatedAt = u.UpdatedAt,
        Addresses = u.Addresses
            .Where(a => !a.IsDeleted)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .Select(a => new UserAddressDto
            {
                Id = a.Id,
                Recipient = a.Recipient,
                Phone = a.Phone,
                Line1 = a.Line1,
                Line2 = a.Line2,
                Ward = a.Ward,
                District = a.District,
                Province = a.Province,
                Country = a.Country,
                PostalCode = a.PostalCode,
                IsDefault = a.IsDefault
            })
            .ToList()
    };
}
