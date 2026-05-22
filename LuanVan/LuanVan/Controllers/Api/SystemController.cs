using LuanVan.Contracts;
using LuanVan.Data;
using LuanVan.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace LuanVan.Controllers.Api;

[ApiController]
[Route("system")]
[Authorize]
public class SystemController : ControllerBase
{
    private const string AiSettingsParamKey = "ui_ai_settings_json";
    private const string PermissionClaimType = Permissions.PermissionClaimType;
    private static readonly HashSet<string> AllowedPermissionClaims = new(Permissions.AllowedClaims, StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ProtectedSystemRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin",
        "Manager",
        "Employee"
    };

    private readonly AppDbContext _dbContext;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<SystemController> _logger;
    private readonly IConfiguration _configuration;

    public SystemController(AppDbContext dbContext, RoleManager<IdentityRole> roleManager, ILogger<SystemController> logger, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _roleManager = roleManager;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet("settings-overview")]
    [Authorize(Policy = Permissions.SettingsView)]
    public async Task<ActionResult<ApiResponse<SettingsOverviewDto>>> GetSettingsOverview()
    {
        try
        {
            var roles = await _dbContext.Roles
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new RoleItemDto
                {
                    Id = x.Id,
                    Name = x.Name ?? string.Empty
                })
                .ToListAsync();

            var users = await _dbContext.Users
                .AsNoTracking()
                .OrderBy(x => x.UserName)
                .Select(x => new UserItemDto
                {
                    Id = x.Id,
                    UserName = x.UserName,
                    Email = x.Email
                })
                .ToListAsync();

            var userRoleMap = await (
                from ur in _dbContext.Set<IdentityUserRole<string>>().AsNoTracking()
                join r in _dbContext.Roles.AsNoTracking() on ur.RoleId equals r.Id
                select new { ur.UserId, RoleName = r.Name ?? string.Empty }
            )
            .GroupBy(x => x.UserId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(x => x.RoleName).Distinct().OrderBy(x => x).ToList());

            foreach (var user in users)
            {
                user.Roles = userRoleMap.GetValueOrDefault(user.Id, new List<string>());
            }

            var loaiKpis = await _dbContext.LoaiKpis
                .AsNoTracking()
                .OrderBy(x => x.MaLoaiKpi)
                .Select(x => new MasterDataItemDto
                {
                    Id = x.MaLoaiKpi,
                    Name = x.TenLoaiKpi
                })
                .ToListAsync();

            var doKhos = await _dbContext.DoKhos
                .AsNoTracking()
                .OrderBy(x => x.MaDoKho)
                .Select(x => new MasterDataItemDto
                {
                    Id = x.MaDoKho,
                    Name = x.TenDoKho
                })
                .ToListAsync();

            var doUuTiens = await _dbContext.DoUuTiens
                .AsNoTracking()
                .OrderBy(x => x.MaDoUuTien)
                .Select(x => new MasterDataItemDto
                {
                    Id = x.MaDoUuTien,
                    Name = x.TenDoUuTien
                })
                .ToListAsync();

            var dto = new SettingsOverviewDto
            {
                Roles = roles,
                Users = users,
                LoaiKpis = loaiKpis,
                DoKhos = doKhos,
                DoUuTiens = doUuTiens
            };

            return Ok(ApiResponse<SettingsOverviewDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            var fallback = new SettingsOverviewDto();
            return Ok(ApiResponse<SettingsOverviewDto>.Ok(fallback, $"Settings overview fallback: {ex.Message}"));
        }
    }

    [HttpGet("roles")]
    [Authorize(Policy = Permissions.SettingsView)]
    public async Task<ActionResult<ApiResponse<List<RoleSummaryDto>>>> GetRoles()
    {
        try
        {
            EnsureDbConnectionStringInitialized();

            var roleRows = await _dbContext.Roles
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new RoleSummaryDto
                {
                    Id = x.Id,
                    Name = x.Name ?? string.Empty
                })
                .ToListAsync();

            var roleIds = roleRows.Select(x => x.Id).ToList();
            var userCountMap = await _dbContext.Set<IdentityUserRole<string>>()
                .AsNoTracking()
                .Where(x => roleIds.Contains(x.RoleId))
                .GroupBy(x => x.RoleId)
                .Select(g => new { RoleId = g.Key, UserCount = g.Count() })
                .ToDictionaryAsync(x => x.RoleId, x => x.UserCount);

            foreach (var role in roleRows)
            {
                role.UserCount = userCountMap.GetValueOrDefault(role.Id, 0);
            }

            return Ok(ApiResponse<List<RoleSummaryDto>>.Ok(roleRows));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<List<RoleSummaryDto>>.Ok(new List<RoleSummaryDto>(), $"Roles fallback: {ex.Message}"));
        }
    }

    [HttpPost("roles")]
    [Authorize(Policy = Permissions.SettingsEdit)]
    public async Task<ActionResult<ApiResponse<RoleSummaryDto>>> CreateRole([FromBody] SaveRoleRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(ApiResponse<RoleSummaryDto>.Fail("Tên role là bắt buộc."));
        }

        var roleName = request.Name.Trim();

        try
        {
            EnsureDbConnectionStringInitialized();

            var normalizedRoleName = roleName.ToUpperInvariant();
            var roleExists = await _dbContext.Roles
                .AsNoTracking()
                .AnyAsync(x => x.NormalizedName == normalizedRoleName);

            if (roleExists)
            {
                return Conflict(ApiResponse<RoleSummaryDto>.Fail("Role đã tồn tại."));
            }

            var role = new IdentityRole
            {
                Name = roleName,
                NormalizedName = normalizedRoleName,
                ConcurrencyStamp = Guid.NewGuid().ToString()
            };

            _dbContext.Roles.Add(role);
            await _dbContext.SaveChangesAsync();

            var dto = new RoleSummaryDto
            {
                Id = role.Id,
                Name = role.Name ?? roleName,
                UserCount = 0
            };

            return Ok(ApiResponse<RoleSummaryDto>.Ok(dto, "Tạo role thành công."));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<RoleSummaryDto>.Fail($"Không thể tạo role lúc này: {ex.Message}"));
        }
    }

    [HttpPut("roles/{roleId}")]
    [Authorize(Policy = Permissions.SettingsEdit)]
    public async Task<ActionResult<ApiResponse<RoleSummaryDto>>> UpdateRole(string roleId, [FromBody] SaveRoleRequest request)
    {
        try
        {
            EnsureDbConnectionStringInitialized();

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(ApiResponse<RoleSummaryDto>.Fail("Tên role là bắt buộc."));
            }

            var role = await _roleManager.FindByIdAsync(roleId);
            if (role is null)
            {
                return NotFound(ApiResponse<RoleSummaryDto>.Fail("Không tìm thấy role."));
            }

            if (ProtectedSystemRoles.Contains(role.Name ?? string.Empty)
                && !string.Equals(role.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(ApiResponse<RoleSummaryDto>.Fail("Không thể đổi tên role hệ thống mặc định."));
            }

            role.Name = request.Name.Trim();
            role.NormalizedName = request.Name.Trim().ToUpperInvariant();

            var updateResult = await _roleManager.UpdateAsync(role);
            if (!updateResult.Succeeded)
            {
                return BadRequest(ApiResponse<RoleSummaryDto>.Fail(string.Join(" | ", updateResult.Errors.Select(x => x.Description))));
            }

            var userCount = await _dbContext.Set<IdentityUserRole<string>>().AsNoTracking().CountAsync(x => x.RoleId == roleId);
            var dto = new RoleSummaryDto
            {
                Id = role.Id,
                Name = role.Name ?? string.Empty,
                UserCount = userCount
            };

            return Ok(ApiResponse<RoleSummaryDto>.Ok(dto, "Cập nhật role thành công."));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<RoleSummaryDto>.Fail($"Không thể cập nhật role lúc này: {ex.Message}"));
        }
    }

    [HttpDelete("roles/{roleId}")]
    [Authorize(Policy = Permissions.SettingsEdit)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteRole(string roleId)
    {
        try
        {
            EnsureDbConnectionStringInitialized();

            var role = await _roleManager.FindByIdAsync(roleId);
            if (role is null)
            {
                return NotFound(ApiResponse<object>.Fail("Không tìm thấy role."));
            }

            if (ProtectedSystemRoles.Contains(role.Name ?? string.Empty))
            {
                return BadRequest(ApiResponse<object>.Fail("Không thể xóa role hệ thống mặc định."));
            }

            var assignedUsers = await _dbContext.Set<IdentityUserRole<string>>().AsNoTracking().CountAsync(x => x.RoleId == roleId);
            if (assignedUsers > 0)
            {
                return BadRequest(ApiResponse<object>.Fail("Role dang được gan cho nguoi dung, không the xóa."));
            }

            var deleteResult = await _roleManager.DeleteAsync(role);
            if (!deleteResult.Succeeded)
            {
                return BadRequest(ApiResponse<object>.Fail(string.Join(" | ", deleteResult.Errors.Select(x => x.Description))));
            }

            return Ok(ApiResponse<object>.Ok(null, "Xóa role thành công."));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.Fail($"Không thể xóa role lúc này: {ex.Message}"));
        }
    }

    [HttpGet("roles/{roleId}/claims")]
    [Authorize(Policy = Permissions.SettingsView)]
    public async Task<ActionResult<ApiResponse<RoleClaimsDto>>> GetRoleClaims(string roleId)
    {
        try
        {
            EnsureDbConnectionStringInitialized();
            await EnsureIdentityRoleClaimsStorageAsync();

            var role = await _roleManager.FindByIdAsync(roleId);
            if (role is null)
            {
                return NotFound(ApiResponse<RoleClaimsDto>.Fail("Không tìm thấy role."));
            }

            var claims = await _roleManager.GetClaimsAsync(role);
            var permissionClaims = claims
                .Where(x => string.Equals(x.Type, PermissionClaimType, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            var dto = new RoleClaimsDto
            {
                RoleId = role.Id,
                RoleName = role.Name ?? string.Empty,
                PermissionClaims = permissionClaims
            };

            return Ok(ApiResponse<RoleClaimsDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<RoleClaimsDto>.Fail($"Không thể tải claim của role lúc này: {ex.Message}"));
        }
    }

    [HttpPut("roles/{roleId}/claims")]
    [Authorize(Policy = Permissions.SettingsEdit)]
    public async Task<ActionResult<ApiResponse<RoleClaimsDto>>> ReplaceRoleClaims(string roleId, [FromBody] UpdateRoleClaimsRequest request)
    {
        try
        {
            EnsureDbConnectionStringInitialized();
            await EnsureIdentityRoleClaimsStorageAsync();

            var role = await _roleManager.FindByIdAsync(roleId);
            if (role is null)
            {
                return NotFound(ApiResponse<RoleClaimsDto>.Fail("Không tìm thấy role."));
            }

            var requestedClaims = (request.PermissionClaims ?? new List<string>())
                .Select(x => (x ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var invalidClaims = requestedClaims
                .Where(x => !AllowedPermissionClaims.Contains(x))
                .ToList();

            if (invalidClaims.Count > 0)
            {
                return BadRequest(ApiResponse<RoleClaimsDto>.Fail($"Permission claim không hợp lệ: {string.Join(", ", invalidClaims)}"));
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            var currentClaims = await _roleManager.GetClaimsAsync(role);
            var currentPermissionClaims = currentClaims
                .Where(x => string.Equals(x.Type, PermissionClaimType, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var claim in currentPermissionClaims)
            {
                var removeResult = await _roleManager.RemoveClaimAsync(role, claim);
                if (!removeResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(ApiResponse<RoleClaimsDto>.Fail(string.Join(" | ", removeResult.Errors.Select(x => x.Description))));
                }
            }

            foreach (var claimValue in requestedClaims)
            {
                var addResult = await _roleManager.AddClaimAsync(role, new Claim(PermissionClaimType, claimValue));
                if (!addResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(ApiResponse<RoleClaimsDto>.Fail(string.Join(" | ", addResult.Errors.Select(x => x.Description))));
                }
            }

            await transaction.CommitAsync();

            var dto = new RoleClaimsDto
            {
                RoleId = role.Id,
                RoleName = role.Name ?? string.Empty,
                PermissionClaims = requestedClaims.OrderBy(x => x).ToList()
            };

            return Ok(ApiResponse<RoleClaimsDto>.Ok(dto, "Cập nhật permission claim cho role thành công."));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<RoleClaimsDto>.Fail($"Không thể cập nhật claim của role lúc này: {ex.Message}"));
        }
    }

    [HttpGet("identity-health")]
    [Authorize(Policy = Permissions.SettingsView)]
    public async Task<ActionResult<ApiResponse<IdentityHealthDto>>> GetIdentityHealth(CancellationToken cancellationToken = default)
    {
        await using var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var requiredTables = new[]
        {
            "AspNetRoles",
            "AspNetUserRoles",
            "AspNetRoleClaims"
        };

        var items = new List<IdentityTableHealthItemDto>();
        foreach (var tableName in requiredTables)
        {
            var exists = await IdentityTableExistsAsync(connection, tableName, cancellationToken);
            long? rowCount = null;
            if (exists)
            {
                rowCount = await IdentityTableCountAsync(connection, tableName, cancellationToken);
            }

            items.Add(new IdentityTableHealthItemDto
            {
                TableName = tableName,
                Exists = exists,
                RowCount = rowCount
            });
        }

        var dto = new IdentityHealthDto
        {
            ServerTimeUtc = DateTime.UtcNow,
            IsHealthy = items.All(x => x.Exists),
            Tables = items
        };

        var message = dto.IsHealthy
            ? "Identity tables are healthy."
            : "One or more Identity tables are missing.";

        return Ok(ApiResponse<IdentityHealthDto>.Ok(dto, message));
    }

    private async Task EnsureIdentityRoleClaimsStorageAsync()
    {
        await _dbContext.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[dbo].[AspNetRoleClaims]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AspNetRoleClaims]
    (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [RoleId] NVARCHAR(450) NOT NULL,
        [ClaimType] NVARCHAR(MAX) NULL,
        [ClaimValue] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END;

IF OBJECT_ID(N'[dbo].[AspNetRoleClaims]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[dbo].[AspNetRoles]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AspNetRoleClaims_AspNetRoles_RoleId')
BEGIN
    ALTER TABLE [dbo].[AspNetRoleClaims] WITH CHECK
    ADD CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId]
    FOREIGN KEY([RoleId]) REFERENCES [dbo].[AspNetRoles]([Id]) ON DELETE CASCADE;
END;

IF OBJECT_ID(N'[dbo].[AspNetRoleClaims]', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes
       WHERE name = N'IX_AspNetRoleClaims_RoleId'
         AND object_id = OBJECT_ID(N'[dbo].[AspNetRoleClaims]')
   )
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AspNetRoleClaims_RoleId]
    ON [dbo].[AspNetRoleClaims]([RoleId]);
END;");
    }

    private static async Task<bool> IdentityTableExistsAsync(DbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT CASE WHEN EXISTS (
    SELECT 1
    FROM sys.tables t
    INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE t.name = @tableName AND s.name = N'dbo'
) THEN 1 ELSE 0 END;";

        var tableParam = command.CreateParameter();
        tableParam.ParameterName = "@tableName";
        tableParam.DbType = DbType.String;
        tableParam.Value = tableName;
        command.Parameters.Add(tableParam);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result ?? 0) == 1;
    }

    private static async Task<long> IdentityTableCountAsync(DbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        var safeTableName = tableName switch
        {
            "AspNetRoles" => "[dbo].[AspNetRoles]",
            "AspNetUserRoles" => "[dbo].[AspNetUserRoles]",
            "AspNetRoleClaims" => "[dbo].[AspNetRoleClaims]",
            _ => throw new InvalidOperationException("Unsupported identity table for counting rows.")
        };

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT_BIG(1) FROM {safeTableName};";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null || result == DBNull.Value ? 0L : Convert.ToInt64(result);
    }

    [HttpGet("ai-settings")]
    [HttpGet("/settings/ai/config")]
    [Authorize(Policy = Permissions.SettingsView)]
    public async Task<ActionResult<ApiResponse<object>>> GetAiSettings(CancellationToken cancellationToken)
    {
        LogLegacyRouteUsage("/system/ai-settings");
        try
        {
            await using var connection = _dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await EnsureAiModelStorageAsync(connection, cancellationToken);
            await EnsureAiSettingsStorageAsync(connection, cancellationToken);
            var modelId = await GetOrCreateAiModelIdAsync(connection, cancellationToken);
            var settingsJson = await ReadAiSettingsJsonAsync(connection, modelId, cancellationToken);

            if (string.IsNullOrWhiteSpace(settingsJson))
            {
                return Ok(ApiResponse<object>.Ok(null));
            }

            using var doc = JsonDocument.Parse(settingsJson);
            return Ok(ApiResponse<object>.Ok(doc.RootElement.Clone()));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<object>.Ok(null, $"AI settings fallback: {ex.Message}"));
        }
    }

    [HttpPut("ai-settings")]
    [HttpPut("/settings/ai/config")]
    [Authorize(Policy = Permissions.SettingsEdit)]
    public async Task<ActionResult<ApiResponse<object>>> SaveAiSettings([FromBody] SaveAiSettingsRequest request, CancellationToken cancellationToken)
    {
        LogLegacyRouteUsage("/system/ai-settings");
        if (request.Settings.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return BadRequest(ApiResponse<object>.Fail("Dữ liệu cấu hình AI không hợp lệ."));
        }

        try
        {
            await using var connection = _dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await EnsureAiModelStorageAsync(connection, cancellationToken);
            await EnsureAiSettingsStorageAsync(connection, cancellationToken);
            await EnsureAiLogStorageAsync(connection, cancellationToken);
            var modelId = await GetOrCreateAiModelIdAsync(connection, cancellationToken);
            var json = request.Settings.GetRawText();
            var oldJson = await ReadAiSettingsJsonAsync(connection, modelId, cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = @"
IF EXISTS (
    SELECT 1 FROM dbo.THAMSOAI
    WHERE MAMODEL = @modelId AND TEN_THAMSO = @paramKey
)
BEGIN
    UPDATE dbo.THAMSOAI
    SET GIATRI = @jsonValue,
        MOTA = N'Cấu hình tham số AI từ giao diện Settings'
    WHERE MAMODEL = @modelId AND TEN_THAMSO = @paramKey;
END
ELSE
BEGIN
    INSERT INTO dbo.THAMSOAI(TEN_THAMSO, GIATRI, MOTA, MAMODEL)
    VALUES (@paramKey, @jsonValue, N'Cấu hình tham số AI từ giao diện Settings', @modelId);
END";

            var modelIdParam = command.CreateParameter();
            modelIdParam.ParameterName = "@modelId";
            modelIdParam.DbType = DbType.Int32;
            modelIdParam.Value = modelId;
            command.Parameters.Add(modelIdParam);

            var keyParam = command.CreateParameter();
            keyParam.ParameterName = "@paramKey";
            keyParam.DbType = DbType.String;
            keyParam.Value = AiSettingsParamKey;
            command.Parameters.Add(keyParam);

            var valueParam = command.CreateParameter();
            valueParam.ParameterName = "@jsonValue";
            valueParam.DbType = DbType.String;
            valueParam.Value = json;
            command.Parameters.Add(valueParam);

            await command.ExecuteNonQueryAsync(cancellationToken);

            var actor = User?.Identity?.Name;
            var diffSummary = BuildAiSettingsDiffSummary(oldJson, json);
            await WriteAiConfigAuditLogAsync(connection, modelId, actor, diffSummary, cancellationToken);
            await WriteSettingsAuditAsync("UPDATE_AI_CONFIG", "AI_SETTINGS", oldJson, json, "SUCCESS", cancellationToken);

            return Ok(ApiResponse<object>.Ok(null, "Lưu cấu hình AI thành công."));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<object>.Ok(null, $"AI settings save fallback: {ex.Message}"));
        }
    }

    [HttpGet("ai-settings/history")]
    [HttpGet("/settings/ai/history")]
    [Authorize(Policy = Permissions.SettingsView)]
    public async Task<ActionResult<ApiResponse<List<AiSettingsAuditItemDto>>>> GetAiSettingsHistory([FromQuery] int top = 8, CancellationToken cancellationToken = default)
    {
        LogLegacyRouteUsage("/system/ai-settings/history");
        try
        {
            var safeTop = Math.Clamp(top, 1, 50);

            await using var connection = _dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await EnsureAiLogStorageAsync(connection, cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT TOP (@top)
    MALOG,
    MAMODEL,
    LOAI_SUKIEN,
    KET_QUA,
    THOI_GIAN,
    NOI_DUNG
FROM dbo.LOG_AI
WHERE LOAI_SUKIEN = N'CONFIG_UPDATE'
ORDER BY MALOG DESC;";

            var topParam = command.CreateParameter();
            topParam.ParameterName = "@top";
            topParam.DbType = DbType.Int32;
            topParam.Value = safeTop;
            command.Parameters.Add(topParam);

            var rows = new List<AiSettingsAuditItemDto>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new AiSettingsAuditItemDto
                {
                    MaLog = reader.IsDBNull(0) ? 0 : reader.GetInt64(0),
                    MaModel = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                    LoaiSuKien = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    KetQua = reader.IsDBNull(3) ? null : reader.GetString(3),
                    ThoiGian = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                    NoiDung = reader.IsDBNull(5) ? null : reader.GetString(5)
                });
            }

            return Ok(ApiResponse<List<AiSettingsAuditItemDto>>.Ok(rows));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<List<AiSettingsAuditItemDto>>.Ok(new List<AiSettingsAuditItemDto>(), $"AI history fallback: {ex.Message}"));
        }
    }

    [HttpGet("ui-settings/{settingKey}")]
    [HttpGet("/settings/ui/{settingKey}")]
    [Authorize(Policy = Permissions.SettingsView)]
    public async Task<ActionResult<ApiResponse<object>>> GetUiSettings(string settingKey, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(settingKey))
            {
                return BadRequest(ApiResponse<object>.Fail("Khóa setting không hợp lệ."));
            }

            var normalizedKey = NormalizeUiSettingKey(settingKey, GetCurrentUserScopeKey());

            await using var connection = _dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await EnsureAiSettingsStorageAsync(connection, cancellationToken);
            var modelId = await GetOrCreateAiModelIdAsync(connection, cancellationToken);
            var savedJson = await ReadThamSoValueAsync(connection, modelId, normalizedKey, cancellationToken);

            if (string.IsNullOrWhiteSpace(savedJson))
            {
                return Ok(ApiResponse<object>.Ok(null));
            }

            using var doc = JsonDocument.Parse(savedJson);
            return Ok(ApiResponse<object>.Ok(doc.RootElement.Clone()));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<object>.Ok(null, $"UI settings fallback: {ex.Message}"));
        }
    }

    [HttpPut("ui-settings/{settingKey}")]
    [HttpPut("/settings/ui/{settingKey}")]
    [Authorize(Policy = Permissions.SettingsEdit)]
    public async Task<ActionResult<ApiResponse<object>>> SaveUiSettings(string settingKey, [FromBody] SaveUiSettingsRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(settingKey))
            {
                return BadRequest(ApiResponse<object>.Fail("Khóa setting không hợp lệ."));
            }

            if (request.Settings.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                return BadRequest(ApiResponse<object>.Fail("Dữ liệu setting không hợp lệ."));
            }

            var normalizedKey = NormalizeUiSettingKey(settingKey, GetCurrentUserScopeKey());

            await using var connection = _dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await EnsureAiSettingsStorageAsync(connection, cancellationToken);
            var modelId = await GetOrCreateAiModelIdAsync(connection, cancellationToken);

            await UpsertThamSoValueAsync(
                connection,
                modelId,
                normalizedKey,
                request.Settings.GetRawText(),
                "Cấu hình UI từ giao diện Portal",
                cancellationToken);

            return Ok(ApiResponse<object>.Ok(null, "Lưu setting thành công."));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<object>.Ok(null, $"UI settings save fallback: {ex.Message}"));
        }
    }

    [HttpGet("/settings/kpi-grades")]
    [Authorize(Policy = Permissions.SettingsView)]
    public async Task<ActionResult<ApiResponse<List<KpiGradeDto>>>> GetKpiGrades(CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await _dbContext.KpiXepLoais.AsNoTracking()
                .OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
                .Select(x => new KpiGradeDto
                {
                    Id = x.Id,
                    Code = x.Code,
                    Label = x.Label,
                    MoTa = x.MoTa,
                    MinScore = x.MinScore,
                    MaxScore = x.MaxScore,
                    ColorHex = x.ColorHex,
                    SortOrder = x.SortOrder,
                    IsActive = x.IsActive,
                    IsSystem = x.IsSystem
                })
                .ToListAsync(cancellationToken);

            return Ok(ApiResponse<List<KpiGradeDto>>.Ok(rows));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<List<KpiGradeDto>>.Ok(new List<KpiGradeDto>(), $"KPI grades fallback: {ex.Message}"));
        }
    }

    [HttpPost("/settings/kpi-grades")]
    [Authorize(Policy = Permissions.SettingsEdit)]
    public async Task<ActionResult<ApiResponse<KpiGradeDto>>> CreateKpiGrade([FromBody] SaveKpiGradeRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateKpiGradeAsync(request, null, cancellationToken);
        if (!validation.Ok)
        {
            return BadRequest(ApiResponse<KpiGradeDto>.Fail(validation.Message));
        }

        var entity = new KpiXepLoai
        {
            Code = request.Code.Trim().ToUpperInvariant(),
            Label = request.Label.Trim(),
            MoTa = string.IsNullOrWhiteSpace(request.MoTa) ? null : request.MoTa.Trim(),
            MinScore = request.MinScore,
            MaxScore = request.MaxScore,
            ColorHex = NormalizeColor(request.ColorHex),
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            IsSystem = false,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.KpiXepLoais.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await WriteSettingsAuditAsync("CREATE_KPI_GRADE", $"KPI_XEPLOAI:{entity.Id}", null, JsonSerializer.Serialize(entity), "SUCCESS", cancellationToken);

        return Ok(ApiResponse<KpiGradeDto>.Ok(ToKpiGradeDto(entity), validation.Warning));
    }

    [HttpPut("/settings/kpi-grades/{id:int}")]
    [Authorize(Policy = Permissions.SettingsEdit)]
    public async Task<ActionResult<ApiResponse<KpiGradeDto>>> UpdateKpiGrade(int id, [FromBody] SaveKpiGradeRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureDbConnectionStringInitialized();

            var entity = await _dbContext.KpiXepLoais.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (entity is null)
            {
                return NotFound(ApiResponse<KpiGradeDto>.Fail("Không tìm thấy mức xếp loại KPI."));
            }

            var requestCode = (request.Code ?? string.Empty).Trim().ToUpperInvariant();
            if (!string.Equals(requestCode, entity.Code, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(ApiResponse<KpiGradeDto>.Fail("Mã xếp loại không được thay đổi khi chỉnh sửa."));
            }

            var validation = await ValidateKpiGradeAsync(request, id, cancellationToken);
            if (!validation.Ok)
            {
                return BadRequest(ApiResponse<KpiGradeDto>.Fail(validation.Message));
            }

            var beforeJson = JsonSerializer.Serialize(entity);
            entity.Label = request.Label.Trim();
            entity.MoTa = string.IsNullOrWhiteSpace(request.MoTa) ? null : request.MoTa.Trim();
            entity.MinScore = request.MinScore;
            entity.MaxScore = request.MaxScore;
            entity.ColorHex = NormalizeColor(request.ColorHex);
            entity.SortOrder = request.SortOrder;
            entity.IsActive = request.IsActive;
            entity.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
            await WriteSettingsAuditAsync("UPDATE_KPI_GRADE", $"KPI_XEPLOAI:{entity.Id}", beforeJson, JsonSerializer.Serialize(entity), "SUCCESS", cancellationToken);

            return Ok(ApiResponse<KpiGradeDto>.Ok(ToKpiGradeDto(entity), validation.Warning));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<KpiGradeDto>.Fail($"Không thể cập nhật mức xếp loại KPI lúc này: {ex.Message}"));
        }
    }

    [HttpPatch("/settings/kpi-grades/{id:int}/toggle")]
    [Authorize(Policy = Permissions.SettingsEdit)]
    public async Task<ActionResult<ApiResponse<KpiGradeDto>>> ToggleKpiGrade(int id, [FromBody] ToggleActiveRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureDbConnectionStringInitialized();

            var entity = await _dbContext.KpiXepLoais.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (entity is null)
            {
                return NotFound(ApiResponse<KpiGradeDto>.Fail("Không tìm thấy mức xếp loại KPI."));
            }

            var beforeJson = JsonSerializer.Serialize(entity);
            entity.IsActive = request.IsActive;
            entity.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await WriteSettingsAuditAsync("TOGGLE_KPI_GRADE", $"KPI_XEPLOAI:{entity.Id}", beforeJson, JsonSerializer.Serialize(entity), "SUCCESS", cancellationToken);

            return Ok(ApiResponse<KpiGradeDto>.Ok(ToKpiGradeDto(entity), request.IsActive ? "Đã bật mức xếp loại." : "Đã ngưng áp dụng mức xếp loại."));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<KpiGradeDto>.Fail($"Không thể cập nhật trạng thái mức xếp loại lúc này: {ex.Message}"));
        }
    }

    [HttpGet("/settings/kpi-master/{masterType}")]
    [Authorize(Policy = Permissions.SettingsView)]
    public async Task<ActionResult<ApiResponse<List<KpiMasterItemDto>>>> GetKpiMaster(string masterType, CancellationToken cancellationToken = default)
    {
        EnsureDbConnectionStringInitialized();

        masterType = NormalizeMasterType(masterType);
        if (masterType == "invalid")
        {
            return BadRequest(ApiResponse<List<KpiMasterItemDto>>.Fail("Loại danh mục không hợp lệ."));
        }
        try
        {
            var items = masterType switch
            {
                "dokho" => await _dbContext.DoKhos.AsNoTracking().OrderBy(x => x.MaDoKho)
                    .Select(x => new KpiMasterItemDto { Id = x.MaDoKho, Name = x.TenDoKho ?? string.Empty, HeSo = x.HeSo, IsActive = x.IsActive })
                    .ToListAsync(cancellationToken),
                "douutien" => await _dbContext.DoUuTiens.AsNoTracking().OrderBy(x => x.MaDoUuTien)
                    .Select(x => new KpiMasterItemDto { Id = x.MaDoUuTien, Name = x.TenDoUuTien ?? string.Empty, HeSo = x.HeSo, IsActive = x.IsActive })
                    .ToListAsync(cancellationToken),
                _ => await _dbContext.LoaiKpis.AsNoTracking().OrderBy(x => x.MaLoaiKpi)
                    .Select(x => new KpiMasterItemDto { Id = x.MaLoaiKpi, Name = x.TenLoaiKpi ?? string.Empty, HeSo = x.HeSo, IsActive = x.IsActive })
                    .ToListAsync(cancellationToken)
            };

            return Ok(ApiResponse<List<KpiMasterItemDto>>.Ok(items));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<List<KpiMasterItemDto>>.Ok(new List<KpiMasterItemDto>(), $"KPI master fallback: {ex.Message}"));
        }
    }

    [HttpPatch("/settings/kpi-master/{masterType}/{id:int}/toggle")]
    [Authorize(Policy = Permissions.SettingsEdit)]
    public async Task<ActionResult<ApiResponse<KpiMasterItemDto>>> ToggleKpiMaster(string masterType, int id, [FromBody] ToggleActiveRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureDbConnectionStringInitialized();

            masterType = NormalizeMasterType(masterType);
            if (masterType == "invalid")
            {
                return BadRequest(ApiResponse<KpiMasterItemDto>.Fail("Loại danh mục không hợp lệ."));
            }

            string entityKey = $"{masterType}:{id}";
            string? beforeJson = null;
            string? afterJson = null;
            KpiMasterItemDto? dto = null;

            if (masterType == "dokho")
            {
                var row = await _dbContext.DoKhos.FirstOrDefaultAsync(x => x.MaDoKho == id, cancellationToken);
                if (row is null) return NotFound(ApiResponse<KpiMasterItemDto>.Fail("Không tìm thấy dữ liệu."));
                if (!request.IsActive && await _dbContext.CongViecs.AsNoTracking().AnyAsync(x => x.MaDoKho == id, cancellationToken))
                {
                    return BadRequest(ApiResponse<KpiMasterItemDto>.Fail("Độ khó đang được sử dụng, chỉ có thể ngưng áp dụng sau khi đánh giá ảnh hưởng."));
                }
                beforeJson = JsonSerializer.Serialize(row);
                row.IsActive = request.IsActive;
                afterJson = JsonSerializer.Serialize(row);
                dto = new KpiMasterItemDto { Id = row.MaDoKho, Name = row.TenDoKho ?? string.Empty, HeSo = row.HeSo, IsActive = row.IsActive };
            }
            else if (masterType == "douutien")
            {
                var row = await _dbContext.DoUuTiens.FirstOrDefaultAsync(x => x.MaDoUuTien == id, cancellationToken);
                if (row is null) return NotFound(ApiResponse<KpiMasterItemDto>.Fail("Không tìm thấy dữ liệu."));
                if (!request.IsActive && await _dbContext.CongViecs.AsNoTracking().AnyAsync(x => x.MaDoUuTien == id, cancellationToken))
                {
                    return BadRequest(ApiResponse<KpiMasterItemDto>.Fail("Độ ưu tiên đang được sử dụng, chỉ có thể ngưng áp dụng sau khi đánh giá ảnh hưởng."));
                }
                beforeJson = JsonSerializer.Serialize(row);
                row.IsActive = request.IsActive;
                afterJson = JsonSerializer.Serialize(row);
                dto = new KpiMasterItemDto { Id = row.MaDoUuTien, Name = row.TenDoUuTien ?? string.Empty, HeSo = row.HeSo, IsActive = row.IsActive };
            }
            else
            {
                var row = await _dbContext.LoaiKpis.FirstOrDefaultAsync(x => x.MaLoaiKpi == id, cancellationToken);
                if (row is null) return NotFound(ApiResponse<KpiMasterItemDto>.Fail("Không tìm thấy dữ liệu."));
                if (!request.IsActive && await _dbContext.DanhMucKpis.AsNoTracking().AnyAsync(x => x.MaLoaiKpi == id, cancellationToken))
                {
                    return BadRequest(ApiResponse<KpiMasterItemDto>.Fail("Loại KPI đang được sử dụng, chỉ có thể ngưng áp dụng sau khi đánh giá ảnh hưởng."));
                }
                beforeJson = JsonSerializer.Serialize(row);
                row.IsActive = request.IsActive;
                afterJson = JsonSerializer.Serialize(row);
                dto = new KpiMasterItemDto { Id = row.MaLoaiKpi, Name = row.TenLoaiKpi ?? string.Empty, HeSo = row.HeSo, IsActive = row.IsActive };
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await WriteSettingsAuditAsync("TOGGLE_KPI_MASTER", entityKey, beforeJson, afterJson, "SUCCESS", cancellationToken);
            return Ok(ApiResponse<KpiMasterItemDto>.Ok(dto!, request.IsActive ? "Đã bật danh mục." : "Đã ngưng áp dụng danh mục."));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<KpiMasterItemDto>.Fail($"Không thể cập nhật trạng thái danh mục lúc này: {ex.Message}"));
        }
    }

    [HttpPost("/settings/kpi-master/{masterType}")]
    [Authorize(Policy = Permissions.SettingsEdit)]
    public async Task<ActionResult<ApiResponse<KpiMasterItemDto>>> CreateKpiMaster(string masterType, [FromBody] SaveKpiMasterRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureDbConnectionStringInitialized();

            masterType = NormalizeMasterType(masterType);
            if (masterType == "invalid" || string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(ApiResponse<KpiMasterItemDto>.Fail("Dữ liệu không hợp lệ."));
            }

            KpiMasterItemDto dto;
            if (masterType == "dokho")
            {
                var row = new DoKho { TenDoKho = request.Name.Trim(), HeSo = NormalizeDoKhoHeSo(request.HeSo), IsActive = true };
                _dbContext.DoKhos.Add(row);
                await _dbContext.SaveChangesAsync(cancellationToken);
                dto = new KpiMasterItemDto { Id = row.MaDoKho, Name = row.TenDoKho ?? string.Empty, HeSo = row.HeSo, IsActive = row.IsActive };
                await WriteSettingsAuditAsync("CREATE_KPI_MASTER", $"dokho:{row.MaDoKho}", null, JsonSerializer.Serialize(row), "SUCCESS", cancellationToken);
            }
            else if (masterType == "douutien")
            {
                var row = new DoUuTien { TenDoUuTien = request.Name.Trim(), HeSo = NormalizeDoKhoHeSo(request.HeSo), IsActive = true };
                _dbContext.DoUuTiens.Add(row);
                await _dbContext.SaveChangesAsync(cancellationToken);
                dto = new KpiMasterItemDto { Id = row.MaDoUuTien, Name = row.TenDoUuTien ?? string.Empty, HeSo = row.HeSo, IsActive = row.IsActive };
                await WriteSettingsAuditAsync("CREATE_KPI_MASTER", $"douutien:{row.MaDoUuTien}", null, JsonSerializer.Serialize(row), "SUCCESS", cancellationToken);
            }
            else
            {
                var row = new LoaiKpi { TenLoaiKpi = request.Name.Trim(), HeSo = NormalizeDoKhoHeSo(request.HeSo), IsActive = true };
                _dbContext.LoaiKpis.Add(row);
                await _dbContext.SaveChangesAsync(cancellationToken);
                dto = new KpiMasterItemDto { Id = row.MaLoaiKpi, Name = row.TenLoaiKpi ?? string.Empty, HeSo = row.HeSo, IsActive = row.IsActive };
                await WriteSettingsAuditAsync("CREATE_KPI_MASTER", $"loaikpi:{row.MaLoaiKpi}", null, JsonSerializer.Serialize(row), "SUCCESS", cancellationToken);
            }

            return Ok(ApiResponse<KpiMasterItemDto>.Ok(dto, "Tạo danh mục thành công."));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<KpiMasterItemDto>.Fail($"Không thể tạo danh mục lúc này: {ex.Message}"));
        }
    }

    [HttpPut("/settings/kpi-master/{masterType}/{id:int}")]
    [Authorize(Policy = Permissions.SettingsEdit)]
    public async Task<ActionResult<ApiResponse<KpiMasterItemDto>>> UpdateKpiMaster(string masterType, int id, [FromBody] SaveKpiMasterRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureDbConnectionStringInitialized();

            masterType = NormalizeMasterType(masterType);
            if (masterType == "invalid" || string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(ApiResponse<KpiMasterItemDto>.Fail("Dữ liệu không hợp lệ."));
            }

            KpiMasterItemDto dto;
            string? beforeJson;
            string? afterJson;
            if (masterType == "dokho")
            {
                var row = await _dbContext.DoKhos.FirstOrDefaultAsync(x => x.MaDoKho == id, cancellationToken);
                if (row is null) return NotFound(ApiResponse<KpiMasterItemDto>.Fail("Không tìm thấy dữ liệu."));
                beforeJson = JsonSerializer.Serialize(row);
                row.TenDoKho = request.Name.Trim();
                row.HeSo = NormalizeDoKhoHeSo(request.HeSo);
                afterJson = JsonSerializer.Serialize(row);
                dto = new KpiMasterItemDto { Id = row.MaDoKho, Name = row.TenDoKho ?? string.Empty, HeSo = row.HeSo, IsActive = row.IsActive };
            }
            else if (masterType == "douutien")
            {
                var row = await _dbContext.DoUuTiens.FirstOrDefaultAsync(x => x.MaDoUuTien == id, cancellationToken);
                if (row is null) return NotFound(ApiResponse<KpiMasterItemDto>.Fail("Không tìm thấy dữ liệu."));
                beforeJson = JsonSerializer.Serialize(row);
                row.TenDoUuTien = request.Name.Trim();
                row.HeSo = NormalizeDoKhoHeSo(request.HeSo);
                afterJson = JsonSerializer.Serialize(row);
                dto = new KpiMasterItemDto { Id = row.MaDoUuTien, Name = row.TenDoUuTien ?? string.Empty, HeSo = row.HeSo, IsActive = row.IsActive };
            }
            else
            {
                var row = await _dbContext.LoaiKpis.FirstOrDefaultAsync(x => x.MaLoaiKpi == id, cancellationToken);
                if (row is null) return NotFound(ApiResponse<KpiMasterItemDto>.Fail("Không tìm thấy dữ liệu."));
                beforeJson = JsonSerializer.Serialize(row);
                row.TenLoaiKpi = request.Name.Trim();
                row.HeSo = NormalizeDoKhoHeSo(request.HeSo);
                afterJson = JsonSerializer.Serialize(row);
                dto = new KpiMasterItemDto { Id = row.MaLoaiKpi, Name = row.TenLoaiKpi ?? string.Empty, HeSo = row.HeSo, IsActive = row.IsActive };
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await WriteSettingsAuditAsync("UPDATE_KPI_MASTER", $"{masterType}:{id}", beforeJson, afterJson, "SUCCESS", cancellationToken);
            return Ok(ApiResponse<KpiMasterItemDto>.Ok(dto, "Cập nhật danh mục thành công."));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<KpiMasterItemDto>.Fail($"Không thể cập nhật danh mục lúc này: {ex.Message}"));
        }
    }

    private static async Task EnsureAiSettingsStorageAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"
IF OBJECT_ID(N'dbo.THAMSOAI', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[THAMSOAI]
    (
        [MATAMSO] INT IDENTITY(1,1) NOT NULL,
        [TEN_THAMSO] NVARCHAR(100) NOT NULL,
        [GIATRI] NVARCHAR(MAX) NOT NULL,
        [MOTA] NVARCHAR(300) NULL,
        [MAMODEL] INT NOT NULL,
        [NGAY_TAO] DATETIME2(0) NOT NULL CONSTRAINT [DF_THAMSOAI_NGAY_TAO] DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_THAMSOAI] PRIMARY KEY CLUSTERED ([MATAMSO] ASC)
    );
END;

IF OBJECT_ID(N'dbo.THAMSOAI', N'U') IS NOT NULL
   AND EXISTS (
       SELECT 1
       FROM sys.columns
       WHERE object_id = OBJECT_ID(N'dbo.THAMSOAI')
         AND name = N'GIATRI'
         AND max_length <> -1
   )
BEGIN
    ALTER TABLE dbo.THAMSOAI ALTER COLUMN GIATRI NVARCHAR(MAX) NOT NULL;
END;

IF OBJECT_ID(N'dbo.THAMSOAI', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.MOHINHAI', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_THAMSOAI_MOHINHAI')
BEGIN
    ALTER TABLE dbo.THAMSOAI WITH CHECK
    ADD CONSTRAINT [FK_THAMSOAI_MOHINHAI] FOREIGN KEY([MAMODEL]) REFERENCES dbo.MOHINHAI([MAMODEL]);
END;

IF OBJECT_ID(N'dbo.THAMSOAI', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'UQ_THAMSOAI_MAMODEL_TEN_THAMSO')
BEGIN
    ALTER TABLE dbo.THAMSOAI
    ADD CONSTRAINT [UQ_THAMSOAI_MAMODEL_TEN_THAMSO] UNIQUE ([MAMODEL], [TEN_THAMSO]);
END;";

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureAiModelStorageAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"
IF OBJECT_ID(N'dbo.MOHINHAI', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[MOHINHAI]
    (
        [MAMODEL] INT IDENTITY(1,1) NOT NULL,
        [TENMODEL] NVARCHAR(50) NULL,
        [VERSION] NVARCHAR(50) NULL,
        [NGAYTRAIN] DATETIME2(0) NULL,
        CONSTRAINT [PK_MOHINHAI] PRIMARY KEY CLUSTERED ([MAMODEL] ASC)
    );
END;

IF OBJECT_ID(N'dbo.MOHINHAI', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.columns
       WHERE object_id = OBJECT_ID(N'dbo.MOHINHAI')
         AND name = N'TENMODEL'
   )
BEGIN
    ALTER TABLE dbo.MOHINHAI ADD TENMODEL NVARCHAR(50) NULL;
END;

IF OBJECT_ID(N'dbo.MOHINHAI', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.columns
       WHERE object_id = OBJECT_ID(N'dbo.MOHINHAI')
         AND name = N'VERSION'
   )
BEGIN
    ALTER TABLE dbo.MOHINHAI ADD VERSION NVARCHAR(50) NULL;
END;

IF OBJECT_ID(N'dbo.MOHINHAI', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.columns
       WHERE object_id = OBJECT_ID(N'dbo.MOHINHAI')
         AND name = N'NGAYTRAIN'
   )
BEGIN
    ALTER TABLE dbo.MOHINHAI ADD NGAYTRAIN DATETIME2(0) NULL;
END;";

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> GetOrCreateAiModelIdAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var getCommand = connection.CreateCommand();
        getCommand.CommandText = @"
SELECT TOP (1) MAMODEL
FROM dbo.MOHINHAI
ORDER BY MAMODEL;";

        var existingId = await getCommand.ExecuteScalarAsync(cancellationToken);
        if (existingId is not null && existingId != DBNull.Value)
        {
            return Convert.ToInt32(existingId);
        }

        await using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = @"
INSERT INTO dbo.MOHINHAI(TENMODEL, VERSION, NGAYTRAIN)
VALUES (N'AI Settings Model', N'v1', SYSUTCDATETIME());
SELECT CAST(SCOPE_IDENTITY() AS INT);";

        var newId = await insertCommand.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(newId);
    }

    private static async Task EnsureAiLogStorageAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"
IF OBJECT_ID(N'dbo.LOG_AI', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[LOG_AI]
    (
        [MALOG] BIGINT IDENTITY(1,1) NOT NULL,
        [MAMODEL] INT NOT NULL,
        [LOAI_SUKIEN] NVARCHAR(30) NOT NULL,
        [KET_QUA] NVARCHAR(100) NULL,
        [THOI_GIAN] DATETIME2(0) NOT NULL CONSTRAINT [DF_LOG_AI_THOI_GIAN] DEFAULT SYSUTCDATETIME(),
        [NOI_DUNG] NVARCHAR(500) NULL,
        CONSTRAINT [PK_LOG_AI] PRIMARY KEY CLUSTERED ([MALOG] ASC)
    );
END;

IF OBJECT_ID(N'dbo.LOG_AI', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.MOHINHAI', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_LOG_AI_MOHINHAI')
BEGIN
    ALTER TABLE dbo.LOG_AI WITH CHECK
    ADD CONSTRAINT [FK_LOG_AI_MOHINHAI] FOREIGN KEY([MAMODEL]) REFERENCES dbo.MOHINHAI([MAMODEL]);
END;";

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task WriteAiConfigAuditLogAsync(DbConnection connection, int modelId, string? actor, string diffSummary, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO dbo.LOG_AI(MAMODEL, LOAI_SUKIEN, KET_QUA, THOI_GIAN, NOI_DUNG)
VALUES (@modelId, N'CONFIG_UPDATE', N'SUCCESS', SYSUTCDATETIME(), @content);";

        var modelIdParam = command.CreateParameter();
        modelIdParam.ParameterName = "@modelId";
        modelIdParam.DbType = DbType.Int32;
        modelIdParam.Value = modelId;
        command.Parameters.Add(modelIdParam);

        var contentParam = command.CreateParameter();
        contentParam.ParameterName = "@content";
        contentParam.DbType = DbType.String;
        var who = string.IsNullOrWhiteSpace(actor) ? "anonymous" : actor;
        var content = $"Cập nhật cấu hình AI bởi {who}. Thay đổi: {diffSummary}";
        contentParam.Value = content.Length > 500 ? content[..500] : content;
        command.Parameters.Add(contentParam);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string BuildAiSettingsDiffSummary(string? oldJson, string newJson)
    {
        if (string.IsNullOrWhiteSpace(oldJson))
        {
            return "Khởi tạo cấu hình AI lần đầu.";
        }

        try
        {
            using var oldDoc = JsonDocument.Parse(oldJson);
            using var newDoc = JsonDocument.Parse(newJson);

            var changes = new List<string>();
            CompareJsonElements(oldDoc.RootElement, newDoc.RootElement, "", changes);

            if (changes.Count == 0)
            {
                return "Không có thay đổi dữ liệu.";
            }

            const int maxShown = 8;
            var shown = changes.Take(maxShown).ToList();
            if (changes.Count > maxShown)
            {
                shown.Add($"... +{changes.Count - maxShown} mục");
            }

            return string.Join(", ", shown);
        }
        catch
        {
            return "Có thay đổi cấu hình (không phân tích được diff chi tiết).";
        }
    }

    private static void CompareJsonElements(JsonElement oldEl, JsonElement newEl, string path, List<string> changes)
    {
        if (oldEl.ValueKind != newEl.ValueKind)
        {
            changes.Add(string.IsNullOrWhiteSpace(path) ? "(root)" : path);
            return;
        }

        switch (oldEl.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var oldProps = oldEl.EnumerateObject().ToDictionary(x => x.Name, x => x.Value);
                var newProps = newEl.EnumerateObject().ToDictionary(x => x.Name, x => x.Value);
                var allKeys = oldProps.Keys.Union(newProps.Keys).OrderBy(x => x);

                foreach (var key in allKeys)
                {
                    var childPath = string.IsNullOrWhiteSpace(path) ? key : $"{path}.{key}";
                    var inOld = oldProps.TryGetValue(key, out var oldChild);
                    var inNew = newProps.TryGetValue(key, out var newChild);

                    if (!inOld || !inNew)
                    {
                        changes.Add(childPath);
                        continue;
                    }

                    CompareJsonElements(oldChild, newChild, childPath, changes);
                }
                break;
            }
            case JsonValueKind.Array:
                if (oldEl.GetRawText() != newEl.GetRawText())
                {
                    changes.Add(string.IsNullOrWhiteSpace(path) ? "(array)" : path);
                }
                break;
            default:
                if (oldEl.GetRawText() != newEl.GetRawText())
                {
                    changes.Add(string.IsNullOrWhiteSpace(path) ? "(value)" : path);
                }
                break;
        }
    }

    private static async Task<string?> ReadAiSettingsJsonAsync(DbConnection connection, int modelId, CancellationToken cancellationToken)
    {
        return await ReadThamSoValueAsync(connection, modelId, AiSettingsParamKey, cancellationToken);
    }

    private static async Task<string?> ReadThamSoValueAsync(DbConnection connection, int modelId, string paramKey, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT TOP (1) GIATRI
FROM dbo.THAMSOAI
WHERE TEN_THAMSO = @paramKey
  AND MAMODEL = @modelId
ORDER BY MATAMSO DESC;";

        var modelIdParam = command.CreateParameter();
        modelIdParam.ParameterName = "@modelId";
        modelIdParam.DbType = DbType.Int32;
        modelIdParam.Value = modelId;
        command.Parameters.Add(modelIdParam);

        var keyParam = command.CreateParameter();
        keyParam.ParameterName = "@paramKey";
        keyParam.DbType = DbType.String;
        keyParam.Value = paramKey;
        command.Parameters.Add(keyParam);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null || result == DBNull.Value ? null : Convert.ToString(result);
    }

    private static async Task UpsertThamSoValueAsync(DbConnection connection, int modelId, string paramKey, string jsonValue, string description, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"
IF EXISTS (
    SELECT 1 FROM dbo.THAMSOAI
    WHERE MAMODEL = @modelId AND TEN_THAMSO = @paramKey
)
BEGIN
    UPDATE dbo.THAMSOAI
    SET GIATRI = @jsonValue,
        MOTA = @description
    WHERE MAMODEL = @modelId AND TEN_THAMSO = @paramKey;
END
ELSE
BEGIN
    INSERT INTO dbo.THAMSOAI(TEN_THAMSO, GIATRI, MOTA, MAMODEL)
    VALUES (@paramKey, @jsonValue, @description, @modelId);
END";

        var modelIdParam = command.CreateParameter();
        modelIdParam.ParameterName = "@modelId";
        modelIdParam.DbType = DbType.Int32;
        modelIdParam.Value = modelId;
        command.Parameters.Add(modelIdParam);

        var keyParam = command.CreateParameter();
        keyParam.ParameterName = "@paramKey";
        keyParam.DbType = DbType.String;
        keyParam.Value = paramKey;
        command.Parameters.Add(keyParam);

        var valueParam = command.CreateParameter();
        valueParam.ParameterName = "@jsonValue";
        valueParam.DbType = DbType.String;
        valueParam.Value = jsonValue;
        command.Parameters.Add(valueParam);

        var descriptionParam = command.CreateParameter();
        descriptionParam.ParameterName = "@description";
        descriptionParam.DbType = DbType.String;
        descriptionParam.Value = description;
        command.Parameters.Add(descriptionParam);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private string GetCurrentUserScopeKey()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            return userId.Trim().ToLowerInvariant();
        }

        var userName = User?.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(userName))
        {
            return userName.Trim().ToLowerInvariant();
        }

        return "anonymous";
    }

    private void LogLegacyRouteUsage(string legacyRoutePrefix)
    {
        var path = Request?.Path.Value ?? string.Empty;
        if (!path.StartsWith("/system/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _logger.LogInformation("Legacy route used: {LegacyRoute}. Request: {RequestUri}", legacyRoutePrefix, Request.GetDisplayUrl());
    }

    private void EnsureDbConnectionStringInitialized()
    {
        var conn = _dbContext.Database.GetDbConnection();
        if (!string.IsNullOrWhiteSpace(conn.ConnectionString))
        {
            return;
        }

        var fallback =
            _configuration.GetConnectionString("DefaultConnection")
            ?? _configuration["ConnectionStrings:DefaultConnection"]
            ?? _configuration["ConnectionStrings__DefaultConnection"];

        if (string.IsNullOrWhiteSpace(fallback))
        {
            return;
        }

        try
        {
            var builder = new SqlConnectionStringBuilder(fallback);
            if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
            {
                builder.InitialCatalog = "LV2026";
            }
            conn.ConnectionString = builder.ConnectionString;
        }
        catch
        {
            conn.ConnectionString = fallback;
        }
    }

    private static string NormalizeUiSettingKey(string settingKey, string userScope)
    {
        var safeChars = settingKey
            .Trim()
            .ToLowerInvariant()
            .Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.')
            .ToArray();

        var normalized = new string(safeChars);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "ui_default";
        }

        return $"ui_{userScope}_{normalized}";
    }

    private static string NormalizeMasterType(string masterType)
    {
        var normalized = (masterType ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "dokho" => "dokho",
            "douutien" => "douutien",
            "loaikpi" => "loaikpi",
            _ => "invalid"
        };
    }

    private static decimal NormalizeDoKhoHeSo(decimal? heSo)
    {
        var value = heSo.GetValueOrDefault(1m);
        if (value <= 0m)
        {
            return 1m;
        }

        if (value > 100m)
        {
            return 100m;
        }

        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static string NormalizeColor(string? colorHex)
    {
        var value = (colorHex ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return "#64748B";
        }

        if (!value.StartsWith('#'))
        {
            value = $"#{value}";
        }

        return value.Length > 20 ? value[..20] : value;
    }

    private KpiGradeDto ToKpiGradeDto(KpiXepLoai x)
    {
        return new KpiGradeDto
        {
            Id = x.Id,
            Code = x.Code,
            Label = x.Label,
            MoTa = x.MoTa,
            MinScore = x.MinScore,
            MaxScore = x.MaxScore,
            ColorHex = x.ColorHex,
            SortOrder = x.SortOrder,
            IsActive = x.IsActive,
            IsSystem = x.IsSystem
        };
    }

    private async Task<(bool Ok, string Message, string? Warning)> ValidateKpiGradeAsync(SaveKpiGradeRequest request, int? excludeId, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return (false, "Dữ liệu không hợp lệ.", null);
        }

        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Label))
        {
            return (false, "Code và tên xếp loại là bắt buộc.", null);
        }

        if (request.MinScore > request.MaxScore)
        {
            return (false, "Khoảng điểm không hợp lệ: MinScore phải nhỏ hơn hoặc bằng MaxScore.", null);
        }

        var code = request.Code.Trim().ToUpperInvariant();
        var label = request.Label.Trim();

        var duplicatedCode = await _dbContext.KpiXepLoais.AsNoTracking()
            .AnyAsync(x => x.Id != excludeId && x.Code == code, cancellationToken);
        if (duplicatedCode)
        {
            return (false, "Code xếp loại đã tồn tại.", null);
        }

        var duplicatedLabel = await _dbContext.KpiXepLoais.AsNoTracking()
            .AnyAsync(x => x.Id != excludeId && x.IsActive && request.IsActive && x.Label == label, cancellationToken);
        if (duplicatedLabel)
        {
            return (false, "Tên xếp loại đã tồn tại trong nhóm đang áp dụng.", null);
        }

        var activeRows = await _dbContext.KpiXepLoais.AsNoTracking()
            .Where(x => x.Id != excludeId && x.IsActive)
            .Select(x => new { x.MinScore, x.MaxScore })
            .ToListAsync(cancellationToken);

        if (request.IsActive)
        {
            var overlaps = activeRows.Any(x => !(request.MaxScore < x.MinScore || request.MinScore > x.MaxScore));
            if (overlaps)
            {
                return (false, "Khoảng điểm bị chồng lấn với mức xếp loại đang áp dụng.", null);
            }
        }

        var allActive = activeRows.Select(x => (x.MinScore, x.MaxScore)).ToList();
        if (request.IsActive)
        {
            allActive.Add((request.MinScore, request.MaxScore));
        }

        var warning = CheckCoverageWarning(allActive);
        return (true, string.Empty, warning);
    }

    private static string? CheckCoverageWarning(List<(decimal Min, decimal Max)> ranges)
    {
        const decimal scoreStep = 0.01m;

        if (ranges.Count == 0)
        {
            return "Cảnh báo: chưa có mức xếp loại nào đang áp dụng.";
        }

        var ordered = ranges.OrderBy(x => x.Min).ToList();
        decimal cursor = 0m;
        foreach (var row in ordered)
        {
            if (row.Min > cursor + scoreStep)
            {
                return "Cảnh báo: cấu hình xếp loại chưa bao phủ toàn bộ dải điểm 0-100.";
            }
            cursor = Math.Max(cursor, row.Max);
        }

        if (cursor < 100m - scoreStep)
        {
            return "Cảnh báo: cấu hình xếp loại chưa bao phủ toàn bộ dải điểm 0-100.";
        }

        return null;
    }

    private async Task WriteSettingsAuditAsync(string action, string target, string? beforeJson, string? afterJson, string status, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var actor = await _dbContext.NhanViens.AsNoTracking().FirstOrDefaultAsync(x => x.AspNetUserId == userId, cancellationToken);
        if (actor is null)
        {
            return;
        }

        _dbContext.NhatKyHoatDongs.Add(new NhatKyHoatDong
        {
            MaNhanVien = actor.MaNhanVien,
            HanhDong = action,
            DoiTuong = target,
            DuLieuCu = beforeJson,
            DuLieuMoi = afterJson,
            ThoiGian = DateTime.Now,
            Ip = HttpContext?.Connection?.RemoteIpAddress?.ToString(),
            TrangThai = status
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public class SettingsOverviewDto
    {
        public List<RoleItemDto> Roles { get; set; } = new();
        public List<UserItemDto> Users { get; set; } = new();
        public List<MasterDataItemDto> LoaiKpis { get; set; } = new();
        public List<MasterDataItemDto> DoKhos { get; set; } = new();
        public List<MasterDataItemDto> DoUuTiens { get; set; } = new();
    }

    public class RoleItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class UserItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public List<string> Roles { get; set; } = new();
    }

    public class MasterDataItemDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    public class RoleSummaryDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int UserCount { get; set; }
    }

    public class SaveRoleRequest
    {
        public string Name { get; set; } = string.Empty;
    }

    public class UpdateRoleClaimsRequest
    {
        public List<string> PermissionClaims { get; set; } = new();
    }

    public class RoleClaimsDto
    {
        public string RoleId { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public List<string> PermissionClaims { get; set; } = new();
    }

    public class IdentityHealthDto
    {
        public DateTime ServerTimeUtc { get; set; }
        public bool IsHealthy { get; set; }
        public List<IdentityTableHealthItemDto> Tables { get; set; } = new();
    }

    public class IdentityTableHealthItemDto
    {
        public string TableName { get; set; } = string.Empty;
        public bool Exists { get; set; }
        public long? RowCount { get; set; }
    }

    public class SaveAiSettingsRequest
    {
        public JsonElement Settings { get; set; }
    }

    public class SaveUiSettingsRequest
    {
        public JsonElement Settings { get; set; }
    }

    public class AiSettingsAuditItemDto
    {
        public long MaLog { get; set; }
        public int MaModel { get; set; }
        public string LoaiSuKien { get; set; } = string.Empty;
        public string? KetQua { get; set; }
        public DateTime? ThoiGian { get; set; }
        public string? NoiDung { get; set; }
    }

    public class KpiGradeDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? MoTa { get; set; }
        public decimal MinScore { get; set; }
        public decimal MaxScore { get; set; }
        public string ColorHex { get; set; } = "#64748B";
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public bool IsSystem { get; set; }
    }

    public class SaveKpiGradeRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? MoTa { get; set; }
        public decimal MinScore { get; set; }
        public decimal MaxScore { get; set; }
        public string? ColorHex { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class ToggleActiveRequest
    {
        public bool IsActive { get; set; }
    }

    public class KpiMasterItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal? HeSo { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class SaveKpiMasterRequest
    {
        public string Name { get; set; } = string.Empty;
        public decimal? HeSo { get; set; }
    }
}



