using LuanVan.Contracts;
using LuanVan.Data;
using LuanVan.Models;
using LuanVan.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LuanVan.Controllers.Api;

[ApiController]
[Route("account-management")]
[Authorize(Policy = "ManageUser")]
public class AccountManagementController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IAuditLogService _auditLogService;
    private readonly IEmailService _emailService;
    private readonly ILogger<AccountManagementController> _logger;
    private readonly IConfiguration _configuration;

    private async Task<string?> ResolveRoleNameAsync(string? roleInput)
    {
        var roleName = (roleInput ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return null;
        }

        var normalizedRoleName = _roleManager.NormalizeKey(roleName);
        var role = await _roleManager.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.NormalizedName == normalizedRoleName);

        return role?.Name;
    }

    public AccountManagementController(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IAuditLogService auditLogService,
        IEmailService emailService,
        ILogger<AccountManagementController> logger,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _roleManager = roleManager;
        _auditLogService = auditLogService;
        _emailService = emailService;
        _logger = logger;
        _configuration = configuration;
    }

    private async Task<List<int>> GetAdminEmployeeIdsAsync()
    {
        return await (
            from nv in _dbContext.NhanViens.AsNoTracking()
            join ur in _dbContext.UserRoles.AsNoTracking() on nv.AspNetUserId equals ur.UserId
            join r in _dbContext.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where r.Name == Roles.Admin
            select nv.MaNhanVien
        ).Distinct().ToListAsync();
    }

    private async Task CreateAdminNotificationAsync(string loai, string noiDung)
    {
        try
        {
            var adminIds = await GetAdminEmployeeIdsAsync();
            if (adminIds.Count == 0)
            {
                return;
            }

            var loaiRow = await _dbContext.LoaiThongBaos.FirstOrDefaultAsync(x => x.TenLoai == loai);
            if (loaiRow == null)
            {
                var nextId = (await _dbContext.LoaiThongBaos.MaxAsync(x => (int?)x.MaLoai) ?? 0) + 1;
                loaiRow = new LoaiThongBao { MaLoai = nextId, TenLoai = loai };
                _dbContext.LoaiThongBaos.Add(loaiRow);
                await _dbContext.SaveChangesAsync();
            }

            var tb = new ThongBao
            {
                MaLoai = loaiRow.MaLoai,
                NoiDung = noiDung,
                ThoiGian = DateTime.Now
            };
            _dbContext.ThongBaos.Add(tb);
            await _dbContext.SaveChangesAsync();

            _dbContext.ThongBaoNhanViens.AddRange(adminIds.Select(id => new ThongBaoNhanVien
            {
                MaThongBao = tb.MaThongBao,
                MaNhanVien = id,
                DaDoc = false
            }));
            await _dbContext.SaveChangesAsync();
        }
        catch
        {
            // Không chặn luồng chính nếu tạo thông báo lỗi.
        }
    }

    private async Task SendEmployeeEmailAsync(string? recipientEmail, string recipientName, string subject, string bodyHtml)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            return;
        }

        try
        {
            await _emailService.SendSystemNotificationEmailAsync(recipientEmail, recipientName, subject, bodyHtml);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gửi email hệ thống thất bại tới {Email}. Subject: {Subject}", recipientEmail, subject);
        }
    }

    private void EnsureDbConnectionStringInitialized()
    {
        var connection = _dbContext.Database.GetDbConnection();
        if (!string.IsNullOrWhiteSpace(connection.ConnectionString))
        {
            return;
        }

        var configured = _configuration.GetConnectionString("DefaultConnection")
            ?? _configuration["ConnectionStrings:DefaultConnection"]
            ?? _configuration["ConnectionStrings__DefaultConnection"];

        if (string.IsNullOrWhiteSpace(configured))
        {
            return;
        }

        try
        {
            var builder = new SqlConnectionStringBuilder(configured);
            if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
            {
                builder.InitialCatalog = "LV2026";
            }

            _dbContext.Database.SetConnectionString(builder.ConnectionString);
        }
        catch
        {
            _dbContext.Database.SetConnectionString(configured);
        }
    }

    private async Task<ApplicationUser?> FindUserByRouteKeyAsync(string userKey)
    {
        EnsureDbConnectionStringInitialized();

        var key = (userKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var byId = await _userManager.FindByIdAsync(key);
        if (byId is not null)
        {
            return byId;
        }

        var byName = await _userManager.FindByNameAsync(key);
        if (byName is not null)
        {
            return byName;
        }

        if (key.Contains('@'))
        {
            var byEmail = await _userManager.FindByEmailAsync(key);
            if (byEmail is not null)
            {
                return byEmail;
            }
        }

        return null;
    }

    private async Task WriteAuditAsync(string action)
    {
        EnsureDbConnectionStringInitialized();
        var actorUserId = _userManager.GetUserId(User);
        await _auditLogService.LogByUserIdAsync(actorUserId, action);
    }

    private async Task WriteStructuredAuditAsync(
        string hanhDong,
        string doiTuong,
        object? duLieuCu = null,
        object? duLieuMoi = null,
        string? trangThai = "SUCCESS")
    {
        EnsureDbConnectionStringInitialized();
        var actorUserId = _userManager.GetUserId(User);
        await _auditLogService.LogStructuredByUserIdAsync(
            actorUserId,
            hanhDong,
            doiTuong,
            duLieuCu,
            duLieuMoi,
            trangThai);
    }

    [HttpGet("accounts")]
    public async Task<ActionResult<ApiResponse<List<AccountListItemDto>>>> GetAccounts(
        [FromQuery] string? keyword,
        [FromQuery] string? role,
        [FromQuery] string? status)
    {
        try
        {
            EnsureDbConnectionStringInitialized();
            var usersQuery = _dbContext.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var keywordTrim = keyword.Trim();
            usersQuery = usersQuery.Where(x =>
                (x.UserName != null && x.UserName.Contains(keywordTrim)) ||
                (x.Email != null && x.Email.Contains(keywordTrim)));
        }

        var users = await usersQuery
            .OrderBy(x => x.UserName)
            .Select(x => new AccountUserRaw
            {
                Id = x.Id,
                UserName = x.UserName,
                Email = x.Email,
                LockoutEnd = x.LockoutEnd,
                LockoutEnabled = x.LockoutEnabled,
                AccessFailedCount = x.AccessFailedCount
            })
            .ToListAsync();

        var userIds = users.Select(x => x.Id).ToList();

        var roleMap = await (
            from ur in _dbContext.Set<IdentityUserRole<string>>().AsNoTracking()
            join r in _dbContext.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where userIds.Contains(ur.UserId)
            select new { ur.UserId, RoleName = r.Name ?? string.Empty }
        )
        .GroupBy(x => x.UserId)
        .ToDictionaryAsync(g => g.Key, g => g.Select(x => x.RoleName).Distinct().OrderBy(x => x).ToList());

        var nhanVienLinkedRows = await (
            from nv in _dbContext.NhanViens.AsNoTracking()
            join pb in _dbContext.PhongBans.AsNoTracking() on nv.MaPhongBan equals pb.MaPhongBan into pbLeft
            from pb in pbLeft.DefaultIfEmpty()
            where nv.AspNetUserId != null && userIds.Contains(nv.AspNetUserId)
            select new
            {
                nv.AspNetUserId,
                nv.MaNhanVien,
                nv.HoTen,
                nv.MaPhongBan,
                TenPhongBan = pb != null ? pb.TenPhongBan : null,
                nv.Sdt
            }
        ).ToListAsync();

        var nhanVienMap = nhanVienLinkedRows
            .Where(x => x.AspNetUserId != null)
            .ToDictionary(x => x.AspNetUserId!, x => new EmployeeLiteDto
            {
                MaNhanVien = x.MaNhanVien,
                HoTen = x.HoTen,
                MaPhongBan = x.MaPhongBan,
                TenPhongBan = x.TenPhongBan,
                ChucVu = "Nhân viên",
                Sdt = x.Sdt
            });

        var employeeIds = nhanVienLinkedRows.Select(x => x.MaNhanVien).ToList();
        var lastActivityMap = await _dbContext.NhatKyHoatDongs
            .AsNoTracking()
            .Where(x => employeeIds.Contains(x.MaNhanVien) && x.ThoiGian.HasValue)
            .GroupBy(x => x.MaNhanVien)
            .Select(g => new { MaNhanVien = g.Key, LastTime = g.Max(x => x.ThoiGian) })
            .ToDictionaryAsync(x => x.MaNhanVien, x => x.LastTime);

        var now = DateTimeOffset.UtcNow;

        var result = users
            .Select((x, index) =>
            {
                var roles = roleMap.GetValueOrDefault(x.Id, new List<string>());
                var primaryRole = roles.FirstOrDefault() ?? "Employee";
                var isLocked = x.LockoutEnd.HasValue && x.LockoutEnd.Value > now;
                nhanVienMap.TryGetValue(x.Id, out var linkedEmployee);
                var lastActivity = linkedEmployee != null && lastActivityMap.TryGetValue(linkedEmployee.MaNhanVien, out var lastTime)
                    ? lastTime
                    : null;

                return new AccountListItemDto
                {
                    Stt = index + 1,
                    UserId = x.Id,
                    UserName = x.UserName,
                    Email = x.Email,
                    Role = primaryRole,
                    Roles = roles,
                    IsLocked = isLocked,
                    IsDisabled = !x.LockoutEnabled,
                    AccessFailedCount = x.AccessFailedCount,
                    LastActivity = lastActivity,
                    LinkedNhanVien = linkedEmployee
                };
            })
            .ToList();

        if (!string.IsNullOrWhiteSpace(role))
        {
            result = result
                .Where(x => string.Equals(x.Role, role.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
            {
                result = result.Where(x => !x.IsLocked).ToList();
            }
            else if (string.Equals(status, "lock", StringComparison.OrdinalIgnoreCase))
            {
                result = result.Where(x => x.IsLocked).ToList();
            }
        }

        for (var i = 0; i < result.Count; i++)
        {
            result[i].Stt = i + 1;
        }

            return Ok(ApiResponse<List<AccountListItemDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<List<AccountListItemDto>>.Fail($"Không tải được danh sách tài khoản: {ex.Message}"));
        }
    }

    [HttpGet("accounts/{userId}")]
    public async Task<ActionResult<ApiResponse<AccountDetailDto>>> GetAccountDetail(string userId)
    {
        EnsureDbConnectionStringInitialized();
        var resolvedUser = await FindUserByRouteKeyAsync(userId);
        if (resolvedUser is null)
        {
            return NotFound(ApiResponse<AccountDetailDto>.Fail("Không tìm thấy tài khoản."));
        }
        var resolvedUserId = resolvedUser.Id;

        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == resolvedUserId)
            .Select(x => new
            {
                x.Id,
                x.UserName,
                x.Email,
                x.LockoutEnd,
                x.LockoutEnabled,
                x.AccessFailedCount
            })
            .FirstOrDefaultAsync();

        var roles = await (
            from ur in _dbContext.Set<IdentityUserRole<string>>().AsNoTracking()
            join r in _dbContext.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where ur.UserId == resolvedUserId
            select r.Name ?? string.Empty
        ).Distinct().OrderBy(x => x).ToListAsync();

        var employee = await (
            from nv in _dbContext.NhanViens.AsNoTracking()
            join pb in _dbContext.PhongBans.AsNoTracking() on nv.MaPhongBan equals pb.MaPhongBan into pbLeft
            from pb in pbLeft.DefaultIfEmpty()
            where nv.AspNetUserId == resolvedUserId
            select new EmployeeLiteDto
            {
                MaNhanVien = nv.MaNhanVien,
                HoTen = nv.HoTen,
                MaPhongBan = nv.MaPhongBan,
                TenPhongBan = pb != null ? pb.TenPhongBan : null,
                ChucVu = "Nhân viên",
                Sdt = nv.Sdt
            }
        ).FirstOrDefaultAsync();

        DateTime? lastActivity = null;
        if (employee != null)
        {
            lastActivity = await _dbContext.NhatKyHoatDongs
                .AsNoTracking()
                .Where(x => x.MaNhanVien == employee.MaNhanVien && x.ThoiGian.HasValue)
                .MaxAsync(x => x.ThoiGian);
        }

        var dto = new AccountDetailDto
        {
            UserId = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            Roles = roles,
            Role = roles.FirstOrDefault() ?? "Employee",
            IsLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
            IsDisabled = !user.LockoutEnabled,
            AccessFailedCount = user.AccessFailedCount,
            LastActivity = lastActivity,
            LinkedNhanVien = employee
        };

        return Ok(ApiResponse<AccountDetailDto>.Ok(dto));
    }

    [HttpGet("employees")]
    public async Task<ActionResult<ApiResponse<List<EmployeeLiteDto>>>> GetAvailableNhanVien([FromQuery] bool includeLinked = false)
    {
        try
        {
            EnsureDbConnectionStringInitialized();
            var query = _dbContext.NhanViens.AsNoTracking();
            if (!includeLinked)
            {
                query = query.Where(x => x.AspNetUserId == null);
            }

            var employees = await (
                from nv in query.OrderBy(x => x.HoTen)
                join pb in _dbContext.PhongBans.AsNoTracking() on nv.MaPhongBan equals pb.MaPhongBan into pbLeft
                from pb in pbLeft.DefaultIfEmpty()
                select new EmployeeLiteDto
                {
                    MaNhanVien = nv.MaNhanVien,
                    HoTen = nv.HoTen,
                    MaPhongBan = nv.MaPhongBan,
                    TenPhongBan = pb != null ? pb.TenPhongBan : null,
                    ChucVu = "Nhân viên",
                    Sdt = nv.Sdt
                }
            ).ToListAsync();

            return Ok(ApiResponse<List<EmployeeLiteDto>>.Ok(employees));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<List<EmployeeLiteDto>>.Fail($"Không tải được danh sách nhân viên liên kết: {ex.Message}"));
        }
    }

    [HttpPost("accounts")]
    public async Task<ActionResult<ApiResponse<AccountDetailDto>>> CreateAccount([FromBody] CreateAccountRequest request)
    {
        EnsureDbConnectionStringInitialized();
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(ApiResponse<AccountDetailDto>.Fail("Vui lòng nhập đầy đủ thông tin bắt buộc."));
        }

        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return BadRequest(ApiResponse<AccountDetailDto>.Fail("Mật khẩu xác nhận không khớp."));
        }

        var normalizedUserName = request.UserName.Trim();
        var normalizedEmail = request.Email.Trim();

        var duplicateUserName = await _userManager.FindByNameAsync(normalizedUserName);
        if (duplicateUserName != null)
        {
            return BadRequest(ApiResponse<AccountDetailDto>.Fail("Username đã tồn tại."));
        }

        var duplicateEmail = await _userManager.FindByEmailAsync(normalizedEmail);
        if (duplicateEmail != null)
        {
            return BadRequest(ApiResponse<AccountDetailDto>.Fail("Email đã tồn tại."));
        }

        var normalizedRole = await ResolveRoleNameAsync(request.Role);
        if (string.IsNullOrWhiteSpace(normalizedRole))
        {
            return BadRequest(ApiResponse<AccountDetailDto>.Fail("Vai trò không hợp lệ hoặc chưa tồn tại trong hệ thống."));
        }

        NhanVien? nhanVien = null;
        if (request.MaNhanVien.HasValue)
        {
            nhanVien = await _dbContext.NhanViens.FirstOrDefaultAsync(x => x.MaNhanVien == request.MaNhanVien.Value);
            if (nhanVien is null)
            {
                return BadRequest(ApiResponse<AccountDetailDto>.Fail("Nhân viên liên kết không tồn tại."));
            }

            if (!string.IsNullOrWhiteSpace(nhanVien.AspNetUserId))
            {
                return BadRequest(ApiResponse<AccountDetailDto>.Fail("Nhân viên này đã được liên kết với tài khoản khác."));
            }
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();

        var newUser = new ApplicationUser
        {
            UserName = normalizedUserName,
            Email = normalizedEmail,
            EmailConfirmed = true,
            LockoutEnabled = true
        };

        var createResult = await _userManager.CreateAsync(newUser, request.Password);
        if (!createResult.Succeeded)
        {
            return BadRequest(ApiResponse<AccountDetailDto>.Fail(string.Join(" | ", createResult.Errors.Select(x => x.Description))));
        }

        var roleResult = await _userManager.AddToRoleAsync(newUser, normalizedRole);
        if (!roleResult.Succeeded)
        {
            await transaction.RollbackAsync();
            await _userManager.DeleteAsync(newUser);
            return BadRequest(ApiResponse<AccountDetailDto>.Fail(string.Join(" | ", roleResult.Errors.Select(x => x.Description))));
        }

        if (nhanVien != null)
        {
            nhanVien.AspNetUserId = newUser.Id;
            await _dbContext.SaveChangesAsync();
        }

        await transaction.CommitAsync();

        await WriteAuditAsync(nhanVien != null
            ? $"Tạo tài khoản {newUser.UserName} ({normalizedRole}) liên kết nhân viên {nhanVien.MaNhanVien}"
            : $"Tạo tài khoản {newUser.UserName} ({normalizedRole})");

        await SendEmployeeEmailAsync(
            newUser.Email,
            nhanVien?.HoTen ?? (newUser.UserName ?? "nhân viên"),
            "[LuanVan KPI] Tài khoản mới đã được tạo",
            $"""
            <p>Tài khoản của bạn đã được tạo thành công.</p>
            <ul>
                <li><strong>Tên đăng nhập:</strong> {System.Net.WebUtility.HtmlEncode(newUser.UserName ?? string.Empty)}</li>
                <li><strong>Vai trò:</strong> {System.Net.WebUtility.HtmlEncode(normalizedRole)}</li>
            </ul>
            <p>Bạn có thể đăng nhập vào hệ thống và đổi mật khẩu sau lần đăng nhập đầu tiên.</p>
            """);

        var dto = new AccountDetailDto
        {
            UserId = newUser.Id,
            UserName = newUser.UserName,
            Email = newUser.Email,
            Role = normalizedRole,
            Roles = new List<string> { normalizedRole },
            IsLocked = false,
            LinkedNhanVien = nhanVien != null
                ? new EmployeeLiteDto
                {
                    MaNhanVien = nhanVien.MaNhanVien,
                    HoTen = nhanVien.HoTen,
                    Sdt = nhanVien.Sdt
                }
                : null
        };

        return Ok(ApiResponse<AccountDetailDto>.Ok(dto, "Tạo tài khoản thành công."));
    }

    [HttpPut("accounts/{userId}/role")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateRole(string userId, [FromBody] UpdateRoleRequest request)
    {
        var normalizedRole = await ResolveRoleNameAsync(request.Role);
        if (string.IsNullOrWhiteSpace(normalizedRole))
        {
            return BadRequest(ApiResponse<object>.Fail("Vai trò không hợp lệ hoặc chưa tồn tại trong hệ thống."));
        }

        var user = await FindUserByRouteKeyAsync(userId);
        if (user is null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy tài khoản."));
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                return BadRequest(ApiResponse<object>.Fail(string.Join(" | ", removeResult.Errors.Select(x => x.Description))));
            }
        }

        var addResult = await _userManager.AddToRoleAsync(user, normalizedRole);
        if (!addResult.Succeeded)
        {
            return BadRequest(ApiResponse<object>.Fail(string.Join(" | ", addResult.Errors.Select(x => x.Description))));
        }

        await WriteStructuredAuditAsync(
            "Đổi vai trò tài khoản",
            $"ACCOUNT:{user.Id}",
            new { user.UserName, Roles = currentRoles },
            new { user.UserName, Role = normalizedRole });

        return Ok(ApiResponse<object>.Ok(null, "Cập nhật vai trò thành công."));
    }

    [HttpPut("accounts/{userId}/username")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateUserName(string userId, [FromBody] UpdateUserNameRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            return BadRequest(ApiResponse<object>.Fail("Username không hợp lệ."));
        }

        var user = await FindUserByRouteKeyAsync(userId);
        if (user is null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy tài khoản."));
        }

        var newUserName = request.UserName.Trim();
        var duplicateUser = await _userManager.FindByNameAsync(newUserName);
        if (duplicateUser != null && !string.Equals(duplicateUser.Id, user.Id, StringComparison.Ordinal))
        {
            return BadRequest(ApiResponse<object>.Fail("Username đã được sử dụng."));
        }

        var oldUserName = user.UserName;
        user.UserName = newUserName;
        user.NormalizedUserName = _userManager.NormalizeName(newUserName);

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(ApiResponse<object>.Fail(string.Join(" | ", result.Errors.Select(x => x.Description))));
        }

        await WriteStructuredAuditAsync(
            "Cập nhật username tài khoản",
            $"ACCOUNT:{user.Id}",
            new { UserName = oldUserName },
            new { UserName = newUserName });

        return Ok(ApiResponse<object>.Ok(null, "Cập nhật username thành công."));
    }

    [HttpPut("accounts/{userId}/email")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateEmail(string userId, [FromBody] UpdateEmailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(ApiResponse<object>.Fail("Email không hợp lệ."));
        }

        var user = await FindUserByRouteKeyAsync(userId);
        if (user is null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy tài khoản."));
        }

        var normalizedEmail = request.Email.Trim();
        var duplicateEmail = await _userManager.FindByEmailAsync(normalizedEmail);
        if (duplicateEmail != null && !string.Equals(duplicateEmail.Id, user.Id, StringComparison.Ordinal))
        {
            return BadRequest(ApiResponse<object>.Fail("Email đã được sử dụng."));
        }

        var oldEmail = user.Email;

        user.Email = normalizedEmail;
        user.NormalizedEmail = _userManager.NormalizeEmail(normalizedEmail);
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(ApiResponse<object>.Fail(string.Join(" | ", result.Errors.Select(x => x.Description))));
        }

        var linkedNhanVien = await _dbContext.NhanViens.FirstOrDefaultAsync(x => x.AspNetUserId == user.Id);
        if (linkedNhanVien != null)
        {
            linkedNhanVien.Email = normalizedEmail;
            await _dbContext.SaveChangesAsync();
        }

        await WriteStructuredAuditAsync(
            "Cập nhật email tài khoản",
            $"ACCOUNT:{user.Id}",
            new { user.UserName, Email = oldEmail },
            new { user.UserName, Email = normalizedEmail });

        return Ok(ApiResponse<object>.Ok(null, "Cập nhật email thành công."));
    }

    [HttpPut("accounts/{userId}/phone")]
    public async Task<ActionResult<ApiResponse<object>>> UpdatePhone(string userId, [FromBody] UpdatePhoneRequest request)
    {
        var user = await FindUserByRouteKeyAsync(userId);
        if (user is null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy tài khoản."));
        }

        var linkedNhanVien = await _dbContext.NhanViens.FirstOrDefaultAsync(x => x.AspNetUserId == user.Id);
        if (linkedNhanVien == null)
        {
            return BadRequest(ApiResponse<object>.Fail("Tài khoản chưa liên kết nhân viên để cập nhật số điện thoại."));
        }

        var normalizedPhone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        var oldPhone = linkedNhanVien.Sdt;

        linkedNhanVien.Sdt = normalizedPhone;
        user.PhoneNumber = normalizedPhone;

        await _dbContext.SaveChangesAsync();
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(ApiResponse<object>.Fail(string.Join(" | ", result.Errors.Select(x => x.Description))));
        }

        await WriteStructuredAuditAsync(
            "Cập nhật số điện thoại tài khoản",
            $"ACCOUNT:{user.Id}",
            new { user.UserName, Phone = oldPhone },
            new { user.UserName, Phone = normalizedPhone });

        return Ok(ApiResponse<object>.Ok(null, "Cập nhật số điện thoại thành công."));
    }

    [HttpPut("accounts/{userId}/link-employee")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateLinkedEmployee(string userId, [FromBody] UpdateLinkedEmployeeRequest request)
    {
        var user = await FindUserByRouteKeyAsync(userId);
        if (user is null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy tài khoản."));
        }

        var existingLinked = await _dbContext.NhanViens
            .FirstOrDefaultAsync(x => x.AspNetUserId == user.Id);

        if (!request.MaNhanVien.HasValue)
        {
            if (existingLinked != null)
            {
                existingLinked.AspNetUserId = null;
                await _dbContext.SaveChangesAsync();
                await WriteStructuredAuditAsync(
                    "Gỡ liên kết nhân viên khỏi tài khoản",
                    $"ACCOUNT:{user.Id}",
                    new { user.UserName, MaNhanVien = existingLinked.MaNhanVien },
                    new { user.UserName, MaNhanVien = (int?)null });
            }

            return Ok(ApiResponse<object>.Ok(null, "Đã gỡ liên kết nhân viên."));
        }

        var targetEmployee = await _dbContext.NhanViens.FirstOrDefaultAsync(x => x.MaNhanVien == request.MaNhanVien.Value);
        if (targetEmployee is null)
        {
            return BadRequest(ApiResponse<object>.Fail("Nhân viên không tồn tại."));
        }

        var linkedByAnotherAccount = await _dbContext.NhanViens
            .AnyAsync(x => x.MaNhanVien == request.MaNhanVien.Value && x.AspNetUserId != null && x.AspNetUserId != user.Id);
        if (linkedByAnotherAccount)
        {
            return BadRequest(ApiResponse<object>.Fail("Nhân viên này đã được liên kết với tài khoản khác."));
        }

        if (existingLinked != null)
        {
            existingLinked.AspNetUserId = null;
        }

        targetEmployee.AspNetUserId = user.Id;
        await _dbContext.SaveChangesAsync();

        await WriteStructuredAuditAsync(
            "Cập nhật liên kết tài khoản",
            $"ACCOUNT:{user.Id}",
            new { user.UserName, MaNhanVien = existingLinked?.MaNhanVien },
            new { user.UserName, MaNhanVien = targetEmployee.MaNhanVien });

        return Ok(ApiResponse<object>.Ok(null, "Cập nhật liên kết nhân viên thành công."));
    }

    [HttpPut("accounts/{userId}/lock")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateLockState(string userId, [FromBody] UpdateLockRequest request)
    {
        var user = await FindUserByRouteKeyAsync(userId);
        if (user is null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy tài khoản."));
        }

        IdentityResult result;
        if (request.IsLocked)
        {
            result = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
        }
        else
        {
            result = await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);
        }

        if (!result.Succeeded)
        {
            return BadRequest(ApiResponse<object>.Fail(string.Join(" | ", result.Errors.Select(x => x.Description))));
        }

        await WriteStructuredAuditAsync(
            request.IsLocked ? "Khóa tài khoản" : "Mở khóa tài khoản",
            $"ACCOUNT:{user.Id}",
            new
            {
                user.UserName,
                IsLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
                user.AccessFailedCount
            },
            new
            {
                user.UserName,
                IsLocked = request.IsLocked,
                AccessFailedCount = request.IsLocked ? user.AccessFailedCount : 0
            });

        if (request.IsLocked)
        {
            await CreateAdminNotificationAsync(
                "ACCOUNT_LOCK",
                $"Tài khoản {(user.UserName ?? user.Id)} vừa bị khóa bởi quản trị viên.");
        }
        else
        {
            var employeeName = await _dbContext.NhanViens
                .AsNoTracking()
                .Where(x => x.AspNetUserId == user.Id)
                .Select(x => x.HoTen)
                .FirstOrDefaultAsync();

            await SendEmployeeEmailAsync(
                user.Email,
                employeeName ?? (user.UserName ?? "nhân viên"),
                "[LuanVan KPI] Tài khoản đã được mở khóa",
                "<p>Tài khoản của bạn đã được mở khóa. Bạn có thể đăng nhập lại vào hệ thống.</p>");
        }

        return Ok(ApiResponse<object>.Ok(null, request.IsLocked ? "Đã khóa tài khoản." : "Đã mở khóa tài khoản."));
    }

    [HttpPut("accounts/{userId}/reset-password")]
    public async Task<ActionResult<ApiResponse<object>>> ResetPassword(string userId, [FromBody] ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || string.IsNullOrWhiteSpace(request.ConfirmPassword))
        {
            return BadRequest(ApiResponse<object>.Fail("Vui lòng nhập mật khẩu mới."));
        }

        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return BadRequest(ApiResponse<object>.Fail("Mật khẩu xác nhận không khớp."));
        }

        var user = await FindUserByRouteKeyAsync(userId);
        if (user is null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy tài khoản."));
        }

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await _userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);
        if (!resetResult.Succeeded)
        {
            return BadRequest(ApiResponse<object>.Fail(string.Join(" | ", resetResult.Errors.Select(x => x.Description))));
        }

        var actorName = User?.Identity?.Name ?? _userManager.GetUserId(User) ?? "Không xác định";
        await WriteStructuredAuditAsync(
            "Đặt lại mật khẩu tài khoản",
            $"ACCOUNT:{user.Id}",
            new
            {
                userName = user.UserName,
                passwordStatus = "Trước khi đặt lại",
                actionSource = "Quản lý tài khoản",
                changedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")
            },
            new
            {
                userName = user.UserName,
                passwordStatus = "Đã đặt lại thành công",
                actionSource = "Quản lý tài khoản",
                performedBy = actorName,
                changedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")
            });

        var linkedName = await _dbContext.NhanViens
            .AsNoTracking()
            .Where(x => x.AspNetUserId == user.Id)
            .Select(x => x.HoTen)
            .FirstOrDefaultAsync();

        await SendEmployeeEmailAsync(
            user.Email,
            linkedName ?? (user.UserName ?? "nhân viên"),
            "[LuanVan KPI] Mật khẩu tài khoản đã được cập nhật",
            "<p>Mật khẩu tài khoản của bạn vừa được cập nhật bởi quản trị viên. Nếu đây không phải yêu cầu của bạn, vui lòng liên hệ quản trị viên ngay.</p>");

        return Ok(ApiResponse<object>.Ok(null, "Đặt lại mật khẩu thành công."));
    }

    [HttpDelete("accounts/{userId}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAccount(string userId)
    {
        try
        {
            var user = await FindUserByRouteKeyAsync(userId);
            if (user is null)
            {
                return NotFound(ApiResponse<object>.Fail("Không tìm thấy tài khoản."));
            }

            var linkedNhanVienIds = await _dbContext.NhanViens
                .AsNoTracking()
                .Where(x => x.AspNetUserId == user.Id)
                .Select(x => x.MaNhanVien)
                .ToListAsync();

            if (linkedNhanVienIds.Count > 0)
            {
                var hasActiveProjectAssignments = await _dbContext.DuAnNhanViens
                    .AsNoTracking()
                    .AnyAsync(x => linkedNhanVienIds.Contains(x.MaNhanVien) && ((x.TrangThai ?? 1) == 1 || x.NgayRoi == null));

                if (hasActiveProjectAssignments)
                {
                    return BadRequest(ApiResponse<object>.Fail("Không thể xóa tài khoản vì nhân viên đang tham gia dự án."));
                }

                var hasOpenTaskAssignments = await _dbContext.PhanCongNhanViens
                    .AsNoTracking()
                    .AnyAsync(x => linkedNhanVienIds.Contains(x.MaNhanVien) && x.NgayKetThucThucTe == null);

                if (hasOpenTaskAssignments)
                {
                    return BadRequest(ApiResponse<object>.Fail("Không thể xóa tài khoản vì nhân viên đang có công việc chưa hoàn tất."));
                }
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            var linkedNhanVien = await _dbContext.NhanViens.Where(x => x.AspNetUserId == user.Id).ToListAsync();
            foreach (var nhanVien in linkedNhanVien)
            {
                nhanVien.AspNetUserId = null;
            }

            if (linkedNhanVien.Count > 0)
            {
                await _dbContext.SaveChangesAsync();
            }

            var deleteResult = await _userManager.DeleteAsync(user);
            if (!deleteResult.Succeeded)
            {
                await transaction.RollbackAsync();
                return BadRequest(ApiResponse<object>.Fail(string.Join(" | ", deleteResult.Errors.Select(x => x.Description))));
            }

            await transaction.CommitAsync();
            await WriteStructuredAuditAsync(
                "Xóa tài khoản",
                $"ACCOUNT:{user.Id}",
                new { user.UserName, user.Email, LinkedNhanVienIds = linkedNhanVienIds },
                new { Deleted = true });
            return Ok(ApiResponse<object>.Ok(null, "Xóa tài khoản thành công."));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail($"Không thể xóa tài khoản lúc này: {ex.Message}"));
        }
    }

    private sealed class AccountUserRaw
    {
        public string Id { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }
        public bool LockoutEnabled { get; set; }
        public int AccessFailedCount { get; set; }
    }

    public sealed class EmployeeLiteDto
    {
        public int MaNhanVien { get; set; }
        public string? HoTen { get; set; }
        public int? MaPhongBan { get; set; }
        public string? TenPhongBan { get; set; }
        public string? ChucVu { get; set; }
        public string? Sdt { get; set; }
    }

    public sealed class AccountListItemDto
    {
        public int Stt { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string Role { get; set; } = "Employee";
        public List<string> Roles { get; set; } = new();
        public bool IsLocked { get; set; }
        public bool IsDisabled { get; set; }
        public int AccessFailedCount { get; set; }
        public DateTime? LastActivity { get; set; }
        public EmployeeLiteDto? LinkedNhanVien { get; set; }
    }

    public sealed class AccountDetailDto
    {
        public string UserId { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string Role { get; set; } = "Employee";
        public List<string> Roles { get; set; } = new();
        public bool IsLocked { get; set; }
        public bool IsDisabled { get; set; }
        public int AccessFailedCount { get; set; }
        public DateTime? LastActivity { get; set; }
        public EmployeeLiteDto? LinkedNhanVien { get; set; }
    }

    public sealed class CreateAccountRequest
    {
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
        public string Role { get; set; } = "Employee";
        public int? MaNhanVien { get; set; }
    }

    public sealed class UpdateUserNameRequest
    {
        public string UserName { get; set; } = string.Empty;
    }

    public sealed class UpdateRoleRequest
    {
        public string Role { get; set; } = "Employee";
    }

    public sealed class UpdateEmailRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public sealed class UpdateLinkedEmployeeRequest
    {
        public int? MaNhanVien { get; set; }
    }

    public sealed class UpdatePhoneRequest
    {
        public string? PhoneNumber { get; set; }
    }

    public sealed class UpdateLockRequest
    {
        public bool IsLocked { get; set; }
    }

    public sealed class ResetPasswordRequest
    {
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}




