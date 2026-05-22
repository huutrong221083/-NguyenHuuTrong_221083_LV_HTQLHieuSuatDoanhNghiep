using LuanVan.Contracts;
using LuanVan.Data;
using LuanVan.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace LuanVan.Controllers.Api;

[ApiController]
[Authorize]
[Route("congviec")]
public class CongViecController : ControllerBase
{
    private const int TaskStatusNotStarted = 1;
    private const int TaskStatusInProgress = 2;

    private readonly AppDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;

    public CongViecController(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _configuration = configuration;
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

    private sealed class ActorContext
    {
        public int MaNhanVien { get; set; }
        public int? MaPhongBan { get; set; }
        public int? PhoMaPhongBan { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsManager { get; set; }
        public bool IsEmployee { get; set; }
    }
    private sealed class TaskProjectScope
    {
        public int MaCongViec { get; set; }
        public int MaDuAn { get; set; }
        public HashSet<int> ProjectDepartmentIds { get; set; } = new();
        public HashSet<int> ProjectEmployeeIds { get; set; } = new();
        public HashSet<int> AllowedEmployeeIds { get; set; } = new();
    }

    private async Task<ActorContext?> GetActorContextAsync()
    {
        EnsureDbConnectionStringInitialized();

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId) && User?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var actor = await _dbContext.NhanViens
            .AsNoTracking()
            .Where(x => x.AspNetUserId == userId)
            .Select(x => new ActorContext
            {
                MaNhanVien = x.MaNhanVien,
                MaPhongBan = x.MaPhongBan,
                PhoMaPhongBan = x.PhoMaPhongBan,
                IsAdmin = User.IsInRole(Roles.Admin),
                IsManager = User.IsInRole(Roles.Manager),
                IsEmployee = User.IsInRole(Roles.Employee)
            })
            .FirstOrDefaultAsync();

        if (actor != null)
        {
            return actor;
        }

        // Legacy compatibility: some data sets are not fully linked by AspNetUserId.
        // Try to resolve employee by numeric NameIdentifier claim (if present).
        var identifierClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(identifierClaim, out var claimMaNhanVien) && claimMaNhanVien > 0)
        {
            actor = await _dbContext.NhanViens
                .AsNoTracking()
                .Where(x => x.MaNhanVien == claimMaNhanVien)
                .Select(x => new ActorContext
                {
                    MaNhanVien = x.MaNhanVien,
                    MaPhongBan = x.MaPhongBan,
                    PhoMaPhongBan = x.PhoMaPhongBan,
                    IsAdmin = User.IsInRole(Roles.Admin),
                    IsManager = User.IsInRole(Roles.Manager),
                    IsEmployee = User.IsInRole(Roles.Employee)
                })
                .FirstOrDefaultAsync();

            if (actor != null)
            {
                return actor;
            }
        }

        // Fallback by email/name for environments where Identity id mapping is missing.
        var currentUser = string.IsNullOrWhiteSpace(userId)
            ? null
            : await _userManager.FindByIdAsync(userId);

        var email = User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirst("email")?.Value
            ?? currentUser?.Email;

        var userName = User.Identity?.Name ?? currentUser?.UserName;

        if (!string.IsNullOrWhiteSpace(email) || !string.IsNullOrWhiteSpace(userName))
        {
            actor = await _dbContext.NhanViens
                .AsNoTracking()
                .Where(x =>
                    (!string.IsNullOrWhiteSpace(email) && x.Email != null && x.Email == email)
                    || (!string.IsNullOrWhiteSpace(userName) && x.Email != null && x.Email == userName))
                .Select(x => new ActorContext
                {
                    MaNhanVien = x.MaNhanVien,
                    MaPhongBan = x.MaPhongBan,
                    PhoMaPhongBan = x.PhoMaPhongBan,
                    IsAdmin = User.IsInRole(Roles.Admin),
                    IsManager = User.IsInRole(Roles.Manager),
                    IsEmployee = User.IsInRole(Roles.Employee)
                })
                .FirstOrDefaultAsync();
        }

        return actor;
    }

    private IQueryable<CongViec> BuildTaskQuery()
    {
        return _dbContext.CongViecs
            .AsNoTracking()
            .Include(x => x.DuAn)
            .Include(x => x.CongViecCha)
            .Include(x => x.CongViecCon)
            .Include(x => x.PhanCongNhanViens).ThenInclude(x => x.NhanVien)
            .Include(x => x.PhanCongNhoms).ThenInclude(x => x.Nhom)
            .Include(x => x.PhanCongPhongBans).ThenInclude(x => x.PhongBan)
            .Include(x => x.NhatKyCongViecs).ThenInclude(x => x.NhanVien)
            // Note: don't eager-load TienDoCongViecs here for list queries because some DB rows
            // may contain NULLs for legacy columns that cause materialization errors.
            // Load progress history only when loading full details.
            ;
    }

    private static decimal ResolveProgress(CongViec task)
    {
        if (task.PhanTramHoanThanh.HasValue)
        {
            return task.PhanTramHoanThanh.Value;
        }

        var latestProgress = (task.TienDoCongViecs ?? Enumerable.Empty<TienDoCongViec>())
            .OrderByDescending(x => x.NgayCapNhat)
            .FirstOrDefault();

        return latestProgress?.PhanTramHoanThanh ?? 0m;
    }

    private static int ResolveTaskStatusForCreate(int? requestedStatus, DateTime? ngayBatDau, DateTime now)
    {
        var resolvedStatus = requestedStatus ?? TaskStatusNotStarted;

        // If start time is now/past and task is still marked as not started, auto switch to in-progress.
        if (resolvedStatus == TaskStatusNotStarted && ngayBatDau.HasValue && ngayBatDau.Value <= now)
        {
            return TaskStatusInProgress;
        }

        return resolvedStatus;
    }

    private static int ResolveTaskStatusForUpdate(int? existingStatus, int? requestedStatus, DateTime? ngayBatDau, DateTime now)
    {
        // If client requested an explicit status, respect it
        if (requestedStatus.HasValue)
        {
            return requestedStatus.Value;
        }

        var current = existingStatus ?? TaskStatusNotStarted;

        // If currently not started but start date is now/past, transition to in-progress
        if (current == TaskStatusNotStarted && ngayBatDau.HasValue && ngayBatDau.Value <= now)
        {
            return TaskStatusInProgress;
        }

        return current;
    }

    private static TaskAssigneeDto MapAssignee(PhanCongNhanVien item)
    {
        return new TaskAssigneeDto
        {
            MaNhanVien = item.MaNhanVien,
            HoTen = item.NhanVien?.HoTen
        };
    }

    private static TaskTeamDto MapTeam(PhanCongNhom item)
    {
        return new TaskTeamDto
        {
            MaNhom = item.MaNhom,
            TenNhom = item.Nhom?.TenNhom
        };
    }

    private static TaskDepartmentDto MapDepartment(PhanCongPhongBan item)
    {
        return new TaskDepartmentDto
        {
            MaPhongBan = item.MaPhongBan,
            TenPhongBan = item.PhongBan?.TenPhongBan
        };
    }

    private static TaskChildDto MapChild(CongViec item)
    {
        return new TaskChildDto
        {
            MaCongViec = item.MaCongViec,
            TenCongViec = item.TenCongViec,
            MaTrangThai = item.MaTrangThai ?? 0,
            HanHoanThanh = item.HanHoanThanh,
            TienDoPhanTram = item.PhanTramHoanThanh ?? 0m
        };
    }

    private static TaskActivityDto MapLogActivity(NhatKyCongViec item)
    {
        return new TaskActivityDto
        {
            Id = item.MaNhatKy,
            Loai = "NhatKy",
            NoiDung = item.GhiChu,
            PhanTramHoanThanh = item.PhanTramHoanThanh,
            NgayCapNhat = item.NgayCapNhat,
            MaNhanVien = item.MaNhanVien,
            HoTenNhanVien = item.NhanVien?.HoTen
        };
    }

    private static TaskActivityDto MapProgressActivity(TienDoCongViec item, int sequence)
    {
        return new TaskActivityDto
        {
            Id = sequence,
            Loai = "TienDo",
            NoiDung = "Cập nhật tiến độ",
            //GhiChu = item.MaTienDo,
            GhiChu = item.MaTienDo.ToString(),
            PhanTramHoanThanh = item.PhanTramHoanThanh,
            TrangThaiPheDuyet = item.TrangThaiPheDuyet,
            NguoiPheDuyet = item.NguoiPheDuyet,
            HoTenNguoiPheDuyet = item.NguoiPheDuyetNavigation?.HoTen,
            NgayPheDuyet = item.NgayPheDuyet,
            LyDoTuChoi = item.LyDoTuChoi,
            NgayCapNhat = item.NgayCapNhat
        };
    }

    private async Task<TaskProjectScope?> BuildTaskProjectScopeAsync(int maCongViec)
    {
        var taskProject = await _dbContext.CongViecs
            .AsNoTracking()
            .Where(x => x.MaCongViec == maCongViec)
            .Select(x => new { x.MaCongViec, x.MaDuAn })
            .FirstOrDefaultAsync();

        if (taskProject == null)
        {
            return null;
        }

        var maDuAn = taskProject.MaDuAn;

        var projectDepartmentIds = (await _dbContext.DuAnPhongBans
            .AsNoTracking()
            .Where(x => x.MaDuAn == maDuAn && (x.TrangThai ?? 1) == 1)
            .Select(x => x.MaPhongBan)
            .Distinct()
            .ToListAsync())
            .ToHashSet();

        var projectEmployeeIds = (await _dbContext.DuAnNhanViens
            .AsNoTracking()
            .Where(x => x.MaDuAn == maDuAn && (x.TrangThai ?? 1) == 1)
            .Select(x => x.MaNhanVien)
            .Distinct()
            .ToListAsync())
            .ToHashSet();

        var departmentEmployeeIds = projectDepartmentIds.Count == 0
            ? new HashSet<int>()
            : (await _dbContext.NhanViens
                .AsNoTracking()
                .Where(x => (x.TrangThai ?? 1) == 1
                    && ((x.MaPhongBan.HasValue && projectDepartmentIds.Contains(x.MaPhongBan.Value))
                        || (x.PhoMaPhongBan.HasValue && projectDepartmentIds.Contains(x.PhoMaPhongBan.Value))))
                .Select(x => x.MaNhanVien)
                .Distinct()
                .ToListAsync())
                .ToHashSet();

        var allowedEmployeeIds = new HashSet<int>(projectEmployeeIds);
        foreach (var employeeId in departmentEmployeeIds)
        {
            allowedEmployeeIds.Add(employeeId);
        }

        return new TaskProjectScope
        {
            MaCongViec = taskProject.MaCongViec,
            MaDuAn = maDuAn,
            ProjectDepartmentIds = projectDepartmentIds,
            ProjectEmployeeIds = projectEmployeeIds,
            AllowedEmployeeIds = allowedEmployeeIds
        };
    }

    private async Task<List<int>> GetManagerDepartmentIdsAsync(int maNhanVien)
    {
        return await _dbContext.PhongBans
            .AsNoTracking()
            .Where(x => x.MaTruongPhong == maNhanVien)
            .Select(x => x.MaPhongBan)
            .Distinct()
            .ToListAsync();
    }

    private static IQueryable<CongViec> ApplyManagerTaskScope(
        IQueryable<CongViec> query,
        int maNhanVien,
        List<int> managerDepartmentIds,
        AppDbContext dbContext)
    {
        // Get direct assignments
        var directAssignedTaskIds = dbContext.PhanCongNhanViens
            .AsNoTracking()
            .Where(x => x.MaNhanVien == maNhanVien)
            .Select(x => x.MaCongViec)
            .Distinct();

        // Get project assignments
        var activeProjectMemberTaskProjectIds = dbContext.DuAnNhanViens
            .AsNoTracking()
            .Where(x => x.MaNhanVien == maNhanVien && (x.TrangThai ?? 1) == 1)
            .Select(x => x.MaDuAn);

        // Get department-managed project assignments
        var managedDepartmentTaskProjectIds = dbContext.DuAnPhongBans
            .AsNoTracking()
            .Where(x => (x.TrangThai ?? 1) == 1 && managerDepartmentIds.Contains(x.MaPhongBan))
            .Select(x => x.MaDuAn);

        return query.Where(x =>
            directAssignedTaskIds.Contains(x.MaCongViec)
            || activeProjectMemberTaskProjectIds.Contains(x.MaDuAn)
            || managedDepartmentTaskProjectIds.Contains(x.MaDuAn));
    }

    private static IQueryable<TienDoCongViec> ApplyManagerProgressScope(
        IQueryable<TienDoCongViec> query,
        int maNhanVien,
        List<int> managerDepartmentIds,
        AppDbContext dbContext)
    {
        // Get direct assignments
        var directAssignedTaskIds = dbContext.PhanCongNhanViens
            .AsNoTracking()
            .Where(x => x.MaNhanVien == maNhanVien)
            .Select(x => x.MaCongViec)
            .Distinct();

        // Get project assignments
        var activeProjectMemberTaskProjectIds = dbContext.DuAnNhanViens
            .AsNoTracking()
            .Where(x => x.MaNhanVien == maNhanVien && (x.TrangThai ?? 1) == 1)
            .Select(x => x.MaDuAn);

        // Get department-managed project assignments
        var managedDepartmentTaskProjectIds = dbContext.DuAnPhongBans
            .AsNoTracking()
            .Where(x => (x.TrangThai ?? 1) == 1 && managerDepartmentIds.Contains(x.MaPhongBan))
            .Select(x => x.MaDuAn);

        return query.Where(x => x.CongViec != null && (
            directAssignedTaskIds.Contains(x.MaCongViec)
            || activeProjectMemberTaskProjectIds.Contains(x.CongViec.MaDuAn)
            || managedDepartmentTaskProjectIds.Contains(x.CongViec.MaDuAn)));
    }

    private static IQueryable<CongViec> ApplyEmployeeTaskScope(
        IQueryable<CongViec> query,
        ActorContext actor,
        AppDbContext dbContext)
    {
        return ApplySpecificEmployeeTaskScope(query, actor.MaNhanVien, actor.MaPhongBan, dbContext);
    }

    /// <summary>
    /// Filters tasks that are assigned directly to a specific employee.
    /// Matches the employee detail SQL view, which reads from PHANCONGNHANVIEN only.
    /// </summary>
    private static IQueryable<CongViec> ApplySpecificEmployeeTaskScope(
        IQueryable<CongViec> query,
        int maNhanVien,
        int? maPhongBan,
        AppDbContext dbContext)
    {
        var directAssignedTaskIds = dbContext.PhanCongNhanViens
            .AsNoTracking()
            .Where(x => x.MaNhanVien == maNhanVien)
            .Select(x => x.MaCongViec)
            .Distinct();

        return query.Where(x => directAssignedTaskIds.Contains(x.MaCongViec));
    }

    private static IQueryable<TienDoCongViec> ApplyEmployeeProgressScope(
        IQueryable<TienDoCongViec> query,
        ActorContext actor,
        AppDbContext dbContext)
    {
        // Get assigned task IDs from each source separately (same logic as task scope)
        
        // 1. Direct assignment
        var directAssignedTaskIds = dbContext.PhanCongNhanViens
            .AsNoTracking()
            .Where(x => x.MaNhanVien == actor.MaNhanVien && (x.TrangThai ?? 1) == 1)
            .Select(x => x.MaCongViec)
            .Distinct();

        // 2. Team assignment
        var teamIds = dbContext.ThanhVienNhoms
            .AsNoTracking()
            .Where(x => x.MaNhanVien == actor.MaNhanVien)
            .Select(x => x.MaNhom);

        var teamAssignedTaskIds = dbContext.PhanCongNhoms
            .AsNoTracking()
            .Where(x => (x.TrangThai ?? 1) == 1 && teamIds.Contains(x.MaNhom))
            .Select(x => x.MaCongViec)
            .Distinct();

        // 3. Project assignment (direct to employee)
        var directProjectAssignedProjectIds = dbContext.DuAnNhanViens
            .AsNoTracking()
            .Where(x => x.MaNhanVien == actor.MaNhanVien && (x.TrangThai ?? 1) == 1)
            .Select(x => x.MaDuAn)
            .Distinct();

        var directProjectAssignedTaskIds = dbContext.CongViecs
            .AsNoTracking()
            .Where(x => directProjectAssignedProjectIds.Contains(x.MaDuAn))
            .Select(x => x.MaCongViec)
            .Distinct();

        // 4. Project assignment (via department)
        var departmentPhongBanId = actor.MaPhongBan.GetValueOrDefault(0);
        var departmentProjectAssignedProjectIds = dbContext.DuAnPhongBans
            .AsNoTracking()
            .Where(x => (x.TrangThai ?? 1) == 1 && x.MaPhongBan == departmentPhongBanId)
            .Select(x => x.MaDuAn)
            .Distinct();

        var departmentProjectAssignedTaskIds = dbContext.CongViecs
            .AsNoTracking()
            .Where(x => departmentProjectAssignedProjectIds.Contains(x.MaDuAn))
            .Select(x => x.MaCongViec)
            .Distinct();

        // Combine all sources of task assignments:
        // direct task assignment, team assignment, direct project assignment, and department project assignment.
        var allTaskIds = directAssignedTaskIds
            .Union(teamAssignedTaskIds)
            .Union(directProjectAssignedTaskIds)
            .Union(departmentProjectAssignedTaskIds);

        return query.Where(x => allTaskIds.Contains(x.MaCongViec));
    }

    private async Task<bool> CanAccessProgressUpdateAsync(int maTienDo, ActorContext actor)
    {
        var query = _dbContext.TienDoCongViecs
            .AsNoTracking()
            .Where(x => x.MaTienDo == maTienDo);

        if (actor.IsAdmin)
        {
            return await query.AnyAsync();
        }

        if (actor.IsManager)
        {
            var managerDepartmentIds = await GetManagerDepartmentIdsAsync(actor.MaNhanVien);
            return await ApplyManagerProgressScope(query, actor.MaNhanVien, managerDepartmentIds, _dbContext).AnyAsync();
        }

        return await ApplyEmployeeProgressScope(query, actor, _dbContext).AnyAsync();
    }

    private TaskListItemDto MapTask(CongViec task)
    {
        var progress = ResolveProgress(task);
        var assignees = task.PhanCongNhanViens ?? Enumerable.Empty<PhanCongNhanVien>();
        var teams = task.PhanCongNhoms ?? Enumerable.Empty<PhanCongNhom>();
        var departments = task.PhanCongPhongBans ?? Enumerable.Empty<PhanCongPhongBan>();
        var children = task.CongViecCon ?? Enumerable.Empty<CongViec>();
        var logs = task.NhatKyCongViecs ?? Enumerable.Empty<NhatKyCongViec>();
        var progressHistory = task.TienDoCongViecs ?? Enumerable.Empty<TienDoCongViec>();

        return new TaskListItemDto
        {
            MaCongViec = task.MaCongViec,
            TenCongViec = task.TenCongViec,
            MoTa = task.MoTa,
            MaDuAn = task.MaDuAn,
            TenDuAn = task.DuAn?.TenDuAn,
            MaCongViecCha = task.MaCongViecCha,
            TenCongViecCha = task.CongViecCha?.TenCongViec,
            MaDoUuTien = task.MaDoUuTien ?? 0,
            TenDoUuTien = task.MaDoUuTien?.ToString(),
            MaDoKho = task.MaDoKho ?? 0,
            TenDoKho = task.MaDoKho?.ToString(),
            MaTrangThai = task.MaTrangThai ?? 0,
            TenTrangThai = task.MaTrangThai?.ToString(),
            NgayBatDau = task.NgayBatDau,
            HanHoanThanh = task.HanHoanThanh,
            DiemCongViec = task.DiemCongViec,
            TienDoPhanTram = progress,
            NguoiDuocGiao = assignees.Select(MapAssignee).ToList(),
            NhomDuocGiao = teams.Select(MapTeam).ToList(),
            PhongBanDuocGiao = departments.Select(MapDepartment).ToList(),
            CongViecCon = children.Select(MapChild).ToList(),
            HoatDongCongViecs = logs
                .Select(MapLogActivity)
                .OrderByDescending(x => x.NgayCapNhat)
                .ToList(),
            LichSuTienDo = progressHistory
                .Select((item, index) => MapProgressActivity(item, index + 1))
                .OrderByDescending(x => x.NgayCapNhat)
                .ToList(),
            BinhLuans = new List<TaskCommentDto>()
        };
    }

    private TaskDetailDto MapDetail(CongViec task, List<TaskCommentDto> comments)
    {
        var baseTask = MapTask(task);
        return new TaskDetailDto
        {
            MaCongViec = baseTask.MaCongViec,
            TenCongViec = baseTask.TenCongViec,
            MoTa = baseTask.MoTa,
            MaDuAn = baseTask.MaDuAn,
            TenDuAn = baseTask.TenDuAn,
            MaCongViecCha = baseTask.MaCongViecCha,
            TenCongViecCha = baseTask.TenCongViecCha,
            MaDoUuTien = baseTask.MaDoUuTien,
            TenDoUuTien = baseTask.TenDoUuTien,
            MaDoKho = baseTask.MaDoKho,
            TenDoKho = baseTask.TenDoKho,
            MaTrangThai = baseTask.MaTrangThai,
            TenTrangThai = baseTask.TenTrangThai,
            NgayBatDau = baseTask.NgayBatDau,
            HanHoanThanh = baseTask.HanHoanThanh,
            DiemCongViec = baseTask.DiemCongViec,
            TienDoPhanTram = baseTask.TienDoPhanTram,
            NguoiDuocGiao = baseTask.NguoiDuocGiao,
            NhomDuocGiao = baseTask.NhomDuocGiao,
            PhongBanDuocGiao = baseTask.PhongBanDuocGiao,
            CongViecCon = baseTask.CongViecCon,
            HoatDongCongViecs = baseTask.HoatDongCongViecs,
            LichSuTienDo = baseTask.LichSuTienDo,
            BinhLuans = comments
        };
    }

    private async Task<CongViec?> LoadTaskWithDetailsAsync(int id)
    {
        try
        {
            return await BuildTaskQuery()
                .Include(x => x.TienDoCongViecs).ThenInclude(x => x.NguoiPheDuyetNavigation)
                .FirstOrDefaultAsync(x => x.MaCongViec == id);
        }
        catch (System.Data.SqlTypes.SqlNullValueException sqlEx)
        {
            // Defensive fallback for legacy rows with NULLs in columns mapped to non-nullable CLR types.
            Console.Error.WriteLine(sqlEx);

            var task = await _dbContext.CongViecs
                .AsNoTracking()
                .Where(x => x.MaCongViec == id)
                .Select(x => new CongViec
                {
                    MaCongViec = x.MaCongViec,
                    MaDuAn = x.MaDuAn,
                    MaCongViecCha = x.MaCongViecCha,
                    TenCongViec = x.TenCongViec ?? string.Empty,
                    MoTa = x.MoTa ?? string.Empty,
                    NgayBatDau = x.NgayBatDau,
                    HanHoanThanh = x.HanHoanThanh,
                    MaTrangThai = x.MaTrangThai,
                    MaDoUuTien = x.MaDoUuTien,
                    MaDoKho = x.MaDoKho,
                    DiemCongViec = x.DiemCongViec,
                    PhanTramHoanThanh = x.PhanTramHoanThanh,
                    NgayTao = x.NgayTao,
                    NguoiTao = x.NguoiTao ?? string.Empty,
                    NgayCapNhat = x.NgayCapNhat,
                    NguoiCapNhat = x.NguoiCapNhat ?? string.Empty,
                    DaXoa = x.DaXoa
                })
                .FirstOrDefaultAsync();

            if (task == null) return null;

            // Load related collections with safe projections
            task.PhanCongNhanViens = await _dbContext.PhanCongNhanViens
                .AsNoTracking()
                .Where(p => p.MaCongViec == id)
                .Include(p => p.NhanVien)
                .ToListAsync();

            task.PhanCongNhoms = await _dbContext.PhanCongNhoms
                .AsNoTracking()
                .Where(p => p.MaCongViec == id)
                .Include(p => p.Nhom)
                .ToListAsync();

            task.PhanCongPhongBans = await _dbContext.PhanCongPhongBans
                .AsNoTracking()
                .Where(p => p.MaCongViec == id)
                .Include(p => p.PhongBan)
                .ToListAsync();

            task.NhatKyCongViecs = await _dbContext.NhatKyCongViecs
                .AsNoTracking()
                .Where(n => n.MaCongViec == id)
                .Include(n => n.NhanVien)
                .ToListAsync();

            task.TienDoCongViecs = await _dbContext.TienDoCongViecs
                .AsNoTracking()
                .Where(t => t.MaCongViec == id)
                .Include(t => t.NguoiPheDuyetNavigation)
                .OrderByDescending(t => t.NgayCapNhat)
                .ToListAsync();

            // Load the project name if available
            var project = await _dbContext.DuAns
                .AsNoTracking()
                .Where(d => d.MaDuAn == task.MaDuAn)
                .Select(d => new { d.MaDuAn, d.TenDuAn })
                .FirstOrDefaultAsync();

            if (project != null)
            {
                task.DuAn = new DuAn { MaDuAn = project.MaDuAn, TenDuAn = project.TenDuAn };
            }

            return task;
        }
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<TaskListItemDto>>>> GetTasks(
        [FromQuery] string? keyword,
        [FromQuery] int? duan,
        [FromQuery] int? trangthai,
        [FromQuery] int? nhanvien,
        [FromQuery] int? maNhanVien,
        [FromQuery] int page = 1,
        [FromQuery] int size = 200)
    {
        page = Math.Max(page, 1);
        size = Math.Clamp(size, 1, 200);

        var actor = await GetActorContextAsync();
        var isEmployeeScope = actor?.IsEmployee == true && actor.IsAdmin == false && actor.IsManager == false;
        var isManagerScope = actor?.IsManager == true && actor.IsAdmin == false;

        var query = BuildTaskQuery();

        if (isEmployeeScope)
        {
            if (actor == null)
            {
                return Ok(ApiResponse<PagedResult<TaskListItemDto>>.Ok(new PagedResult<TaskListItemDto>
                {
                    Items = new List<TaskListItemDto>(),
                    Page = page,
                    Size = size,
                    TotalItems = 0,
                    TotalPages = 0
                }));
            }

            query = ApplyEmployeeTaskScope(query, actor, _dbContext);
        }
        else if (isManagerScope)
        {
            if (actor == null)
            {
                return Ok(ApiResponse<PagedResult<TaskListItemDto>>.Ok(new PagedResult<TaskListItemDto>
                {
                    Items = new List<TaskListItemDto>(),
                    Page = page,
                    Size = size,
                    TotalItems = 0,
                    TotalPages = 0
                }));
            }

            var managerDepartmentIds = await GetManagerDepartmentIdsAsync(actor.MaNhanVien);
            query = ApplyManagerTaskScope(query, actor.MaNhanVien, managerDepartmentIds, _dbContext);
        }

        if (duan.HasValue)
        {
            query = query.Where(x => x.MaDuAn == duan.Value);
        }

        if (trangthai.HasValue)
        {
            query = query.Where(x => x.MaTrangThai == trangthai.Value);
        }

        // Support both 'nhanvien' and 'maNhanVien' parameter names for compatibility
        var targetNhanVienId = nhanvien ?? maNhanVien;
        if (targetNhanVienId.HasValue)
        {
            // Use comprehensive filtering that includes all assignment types
            // Get the target employee's department to support department-level assignments
            var targetEmployee = await _dbContext.NhanViens
                .AsNoTracking()
                .Where(x => x.MaNhanVien == targetNhanVienId.Value)
                .Select(x => new { x.MaNhanVien, x.MaPhongBan })
                .FirstOrDefaultAsync();
            
            if (targetEmployee != null)
            {
                query = ApplySpecificEmployeeTaskScope(query, targetEmployee.MaNhanVien, targetEmployee.MaPhongBan, _dbContext);
            }
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(x =>
                EF.Functions.Like(EF.Functions.Collate(x.TenCongViec, "Latin1_General_CI_AI"), $"%{term}%") ||
                (x.MoTa != null && EF.Functions.Like(EF.Functions.Collate(x.MoTa, "Latin1_General_CI_AI"), $"%{term}%")) ||
                (x.DuAn != null && x.DuAn.TenDuAn != null && EF.Functions.Like(EF.Functions.Collate(x.DuAn.TenDuAn, "Latin1_General_CI_AI"), $"%{term}%")));
        }

        var totalItems = await query.CountAsync();

        List<CongViec> items;
        try
        {
            items = await query
                .OrderByDescending(x => x.NgayCapNhat ?? x.NgayTao)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync();
        }
        catch (System.Data.SqlTypes.SqlNullValueException sqlEx)
        {
            // Defensive fallback: some legacy rows contain NULLs in columns mapped to non-nullable CLR types.
            // Return a minimal projection to avoid materialization errors.
            Console.Error.WriteLine(sqlEx);

            var fallbackQuery = _dbContext.CongViecs.AsNoTracking();
            if (isEmployeeScope && actor != null)
            {
                fallbackQuery = ApplyEmployeeTaskScope(fallbackQuery, actor, _dbContext);
            }
            else if (isManagerScope && actor != null)
            {
                var managerDepartmentIds = await GetManagerDepartmentIdsAsync(actor.MaNhanVien);
                fallbackQuery = ApplyManagerTaskScope(fallbackQuery, actor.MaNhanVien, managerDepartmentIds, _dbContext);
            }
            if (duan.HasValue) fallbackQuery = fallbackQuery.Where(x => x.MaDuAn == duan.Value);
            if (trangthai.HasValue) fallbackQuery = fallbackQuery.Where(x => x.MaTrangThai == trangthai.Value);
            
            if (targetNhanVienId.HasValue)
            {
                var targetEmployee = await _dbContext.NhanViens
                    .AsNoTracking()
                    .Where(x => x.MaNhanVien == targetNhanVienId.Value)
                    .Select(x => new { x.MaNhanVien, x.MaPhongBan })
                    .FirstOrDefaultAsync();
                
                if (targetEmployee != null)
                {
                    fallbackQuery = ApplySpecificEmployeeTaskScope(fallbackQuery, targetEmployee.MaNhanVien, targetEmployee.MaPhongBan, _dbContext);
                }
            }

            var fallbackItems = await fallbackQuery
                .OrderByDescending(x => x.NgayCapNhat ?? x.NgayTao)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(x => new CongViec {
                    MaCongViec = x.MaCongViec,
                    TenCongViec = x.TenCongViec,
                    MoTa = x.MoTa,
                    MaDuAn = x.MaDuAn,
                    MaCongViecCha = x.MaCongViecCha,
                    MaDoUuTien = x.MaDoUuTien,
                    MaDoKho = x.MaDoKho,
                    MaTrangThai = x.MaTrangThai,
                    NgayBatDau = x.NgayBatDau,
                    HanHoanThanh = x.HanHoanThanh,
                    DiemCongViec = x.DiemCongViec,
                    PhanTramHoanThanh = x.PhanTramHoanThanh
                })
                .ToListAsync();

            items = fallbackItems;
        }

        var dtoItems = items.Select(MapTask).ToList();

        return Ok(ApiResponse<PagedResult<TaskListItemDto>>.Ok(new PagedResult<TaskListItemDto>
        {
            Items = dtoItems,
            Page = page,
            Size = size,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)size)
        }));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<TaskDetailDto>>> GetTask(int id)
    {
        try
        {
            var task = await LoadTaskWithDetailsAsync(id);
            if (task == null)
            {
                return NotFound(ApiResponse<TaskDetailDto>.Fail("Không tìm thấy công việc."));
            }

            var dto = MapDetail(task, new List<TaskCommentDto>());

            return Ok(ApiResponse<TaskDetailDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            var message = $"Exception: {ex.Message}\n{ex.StackTrace}";
            return StatusCode(500, ApiResponse<TaskDetailDto>.Fail(message));
        }
    }

    [Authorize(Policy = Permissions.TasksCreate)]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> CreateTask([FromBody] CreateUpdateTaskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TenCongViec))
        {
            return BadRequest(ApiResponse<object>.Fail("Tên công việc là bắt buộc."));
        }

        if (request.NgayBatDau.HasValue && request.NgayBatDau.Value.Date < DateTime.Today)
        {
            return BadRequest(ApiResponse<object>.Fail("Ngày bắt đầu phải từ ngày hiện tại, không được chọn ngày trong quá khứ."));
        }

        if (request.NgayBatDau.HasValue && request.Deadline.HasValue && request.NgayBatDau.Value.Date >= request.Deadline.Value.Date)
        {
            return BadRequest(ApiResponse<object>.Fail("Deadline phải lớn hơn ngày bắt đầu."));
        }

        if (!request.MaDuAn.HasValue)
        {
            return BadRequest(ApiResponse<object>.Fail("Dự án là bắt buộc."));
        }

        var projectExists = await _dbContext.DuAns.AnyAsync(x => x.MaDuAn == request.MaDuAn.Value);
        if (!projectExists)
        {
            return BadRequest(ApiResponse<object>.Fail("Dự án không tồn tại."));
        }

        var normalizedTenCongViec = request.TenCongViec.Trim();
        var duplicateTaskName = await _dbContext.CongViecs.AnyAsync(x =>
            x.MaDuAn == request.MaDuAn.Value
            && x.TenCongViec != null
            && EF.Functions.Collate(x.TenCongViec, "Latin1_General_CI_AI") == normalizedTenCongViec);
        if (duplicateTaskName)
        {
            return Conflict(ApiResponse<object>.Fail("Tên công việc đã tồn tại trong dự án."));
        }

        if (request.MaCongViecCha.HasValue)
        {
            var parentExists = await _dbContext.CongViecs.AnyAsync(x => x.MaCongViec == request.MaCongViecCha.Value);
            if (!parentExists)
            {
                return BadRequest(ApiResponse<object>.Fail("Công việc cha không tồn tại."));
            }
        }

        // Database requires MADOUUTIEN (NOT NULL), so fallback to a default level if client omits it.
        var resolvedMaDoUuTien = request.MaDoUuTien ?? 1;
        var doUuTienExists = await _dbContext.DoUuTiens.AnyAsync(x => x.MaDoUuTien == resolvedMaDoUuTien);
        if (!doUuTienExists)
        {
            return BadRequest(ApiResponse<object>.Fail("Độ ưu tiên không tồn tại."));
        }

        // Database requires MADOKHO (NOT NULL), so fallback to a default level if client omits it.
        var resolvedMaDoKho = request.MaDoKho ?? 1;
        var doKhoExists = await _dbContext.DoKhos.AnyAsync(x => x.MaDoKho == resolvedMaDoKho);
        if (!doKhoExists)
        {
            return BadRequest(ApiResponse<object>.Fail("Độ khó không tồn tại."));
        }

        var now = DateTime.Now;
        var actor = await GetActorContextAsync();
        var resolvedTaskStatus = ResolveTaskStatusForCreate(request.MaTrangThai, request.NgayBatDau, now);

        var task = new CongViec
        {
            MaDuAn = request.MaDuAn.Value,
            MaCongViecCha = request.MaCongViecCha,
            TenCongViec = normalizedTenCongViec,
            MoTa = request.MoTa?.Trim() ?? string.Empty,
            NgayBatDau = request.NgayBatDau,
            HanHoanThanh = request.Deadline,
            MaTrangThai = resolvedTaskStatus,
            MaDoUuTien = resolvedMaDoUuTien,
            MaDoKho = resolvedMaDoKho,
            DiemCongViec = request.DiemCongViec,
            PhanTramHoanThanh = 0,
            NgayTao = now,
            NguoiTao = actor?.MaNhanVien.ToString() ?? User.Identity?.Name ?? string.Empty,
            NgayCapNhat = now,
            NguoiCapNhat = actor?.MaNhanVien.ToString() ?? User.Identity?.Name ?? string.Empty
        };

        _dbContext.CongViecs.Add(task);
        await _dbContext.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { task.MaCongViec }, "Tạo công việc thành công"));
    }

    [Authorize(Policy = Permissions.TasksEdit)]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateTask(int id, [FromBody] CreateUpdateTaskRequest request)
    {
        var task = await _dbContext.CongViecs.FirstOrDefaultAsync(x => x.MaCongViec == id);
        if (task == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy công việc."));
        }

        if (request.NgayBatDau.HasValue && request.Deadline.HasValue && request.NgayBatDau.Value.Date >= request.Deadline.Value.Date)
        {
            return BadRequest(ApiResponse<object>.Fail("Deadline phải lớn hơn ngày bắt đầu."));
        }

        if (string.IsNullOrWhiteSpace(request.TenCongViec))
        {
            return BadRequest(ApiResponse<object>.Fail("Tên công việc là bắt buộc."));
        }

        if (request.MaDuAn.HasValue)
        {
            var projectExists = await _dbContext.DuAns.AnyAsync(x => x.MaDuAn == request.MaDuAn.Value);
            if (!projectExists)
            {
                return BadRequest(ApiResponse<object>.Fail("Dự án không tồn tại."));
            }

            task.MaDuAn = request.MaDuAn.Value;
        }

        if (request.MaCongViecCha.HasValue)
        {
            var parentExists = await _dbContext.CongViecs.AnyAsync(x => x.MaCongViec == request.MaCongViecCha.Value && x.MaCongViec != id);
            if (!parentExists)
            {
                return BadRequest(ApiResponse<object>.Fail("Công việc cha không tồn tại."));
            }

            task.MaCongViecCha = request.MaCongViecCha;
        }
        else
        {
            task.MaCongViecCha = null;
        }

        var normalizedTaskName = request.TenCongViec.Trim();
        var targetProjectId = request.MaDuAn ?? task.MaDuAn;
        var duplicateOnUpdate = await _dbContext.CongViecs.AnyAsync(x =>
            x.MaCongViec != id
            && x.MaDuAn == targetProjectId
            && x.TenCongViec != null
            && EF.Functions.Collate(x.TenCongViec, "Latin1_General_CI_AI") == normalizedTaskName);
        if (duplicateOnUpdate)
        {
            return Conflict(ApiResponse<object>.Fail("Tên công việc đã tồn tại trong dự án."));
        }

        task.TenCongViec = normalizedTaskName;
        task.MoTa = request.MoTa?.Trim() ?? string.Empty;
        task.NgayBatDau = request.NgayBatDau;
        task.HanHoanThanh = request.Deadline;

        // Normalize status on update: respect explicit request, otherwise auto-transition
        task.MaTrangThai = ResolveTaskStatusForUpdate(task.MaTrangThai, request.MaTrangThai, request.NgayBatDau, DateTime.Now);

        if (request.MaDoUuTien.HasValue)
        {
            var doUuTienExists = await _dbContext.DoUuTiens.AnyAsync(x => x.MaDoUuTien == request.MaDoUuTien.Value);
            if (!doUuTienExists)
            {
                return BadRequest(ApiResponse<object>.Fail("Độ ưu tiên không tồn tại."));
            }

            task.MaDoUuTien = request.MaDoUuTien;
        }

        if (request.MaDoKho.HasValue)
        {
            var doKhoExists = await _dbContext.DoKhos.AnyAsync(x => x.MaDoKho == request.MaDoKho.Value);
            if (!doKhoExists)
            {
                return BadRequest(ApiResponse<object>.Fail("Độ khó không tồn tại."));
            }

            task.MaDoKho = request.MaDoKho;
        }

        if (request.DiemCongViec.HasValue)
        {
            task.DiemCongViec = request.DiemCongViec;
        }

        task.NgayCapNhat = DateTime.Now;
        task.NguoiCapNhat = User.Identity?.Name ?? string.Empty;

        await _dbContext.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { task.MaCongViec }, "Cập nhật công việc thành công"));
    }

    [Authorize(Policy = Permissions.TasksDelete)]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteTask(int id)
    {
        var task = await _dbContext.CongViecs.FirstOrDefaultAsync(x => x.MaCongViec == id);
        if (task == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy công việc."));
        }

        _dbContext.CongViecs.Remove(task);

        await _dbContext.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(null, "Xóa công việc thành công"));
    }

    [Authorize(Policy = Permissions.TasksEdit)]
    [HttpPut("{id:int}/status")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateStatus(int id, [FromBody] UpdateTaskStatusRequest request)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Tài khoản chưa được liên kết."));
        }

        var task = await _dbContext.CongViecs.FirstOrDefaultAsync(x => x.MaCongViec == id);
        if (task == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy công việc."));
        }

        var now = DateTime.Now;
        var isEmployeeOnly = actor.IsEmployee && !actor.IsAdmin && !actor.IsManager;

        // Employee tự chuyển sang hoàn thành thì tạo yêu cầu duyệt, chưa đổi trạng thái ngay.
        if (request.MaTrangThai == 3 && isEmployeeOnly)
        {
            var pendingCompletion = new TienDoCongViec
            {
                MaCongViec = id,
                PhanTramHoanThanh = 100,
                TrangThaiHienTai = 3,
                NgayCapNhat = now,
                TrangThaiPheDuyet = "Chờ duyệt"
            };
            _dbContext.TienDoCongViecs.Add(pendingCompletion);
            await _dbContext.SaveChangesAsync();

            try
            {
                await NotifyManagersByTaskAsync(
                    id,
                    $"Công việc \"{task.TenCongViec}\" đã được nhân viên đề xuất hoàn thành 100% và đang chờ quản lý duyệt.",
                    "Công việc");
            }
            catch
            {
                // keep business flow unaffected by notification errors
            }

            return Ok(ApiResponse<object>.Ok(new { task.MaCongViec, TrangThaiChoDuyet = true }, "Đã gửi yêu cầu duyệt hoàn thành công việc."));
        }

        task.MaTrangThai = request.MaTrangThai;
        if (request.MaTrangThai == 3)
        {
            task.PhanTramHoanThanh = 100;
        }

        task.NgayCapNhat = now;
        task.NguoiCapNhat = User.Identity?.Name ?? string.Empty;
        await _dbContext.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { task.MaCongViec, task.MaTrangThai }, "Cập nhật trạng thái thành công"));
    }

    [HttpPost("/tiendo")]
    [Authorize(Policy = Permissions.TasksEdit)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateProgress([FromBody] UpdateProgressRequest request)
    {
        var task = await _dbContext.CongViecs.FirstOrDefaultAsync(x => x.MaCongViec == request.MaCongViec);
        if (task == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy công việc."));
        }

        var now = DateTime.Now;
        var progress = Math.Clamp(request.PhanTramHoanThanh, 0, 100);
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Tài khoản chưa được liên kết."));
        }

        var isEmployeeOnly = actor.IsEmployee && !actor.IsAdmin && !actor.IsManager;
        var requiresManagerApproval = isEmployeeOnly && progress >= 100;
        var approvalStatus = requiresManagerApproval ? "Chờ duyệt" : "Đã duyệt";

        var newProgressUpdate = new TienDoCongViec
        {
            MaCongViec = request.MaCongViec,
            PhanTramHoanThanh = progress,
            TrangThaiHienTai = progress >= 100 ? 3 : progress > 0 ? 2 : 1,
            NgayCapNhat = now,
            TrangThaiPheDuyet = approvalStatus,
            NguoiPheDuyet = requiresManagerApproval ? null : actor.MaNhanVien,
            NgayPheDuyet = requiresManagerApproval ? null : now
        };
        _dbContext.TienDoCongViecs.Add(newProgressUpdate);

        _dbContext.NhatKyCongViecs.Add(new NhatKyCongViec
        {
            MaCongViec = request.MaCongViec,
            MaNhanVien = actor?.MaNhanVien ?? request.MaNhanVien,
            PhanTramHoanThanh = progress,
            NgayCapNhat = now,
            GhiChu = request.GhiChu?.Trim() ?? string.Empty
        });

        // Cập nhật ngay task nếu không cần duyệt.
        if (!requiresManagerApproval)
        {
            task.PhanTramHoanThanh = progress;
            task.MaTrangThai = progress >= 100 ? 3 : progress > 0 ? 2 : 1;
            task.NgayCapNhat = now;
            task.NguoiCapNhat = User.Identity?.Name ?? string.Empty;
        }

        await _dbContext.SaveChangesAsync();

        if (requiresManagerApproval)
        {
            try
            {
                await NotifyManagersByTaskAsync(
                    request.MaCongViec,
                    $"Nhân viên đã cập nhật tiến độ công việc \"{task.TenCongViec}\" lên {progress}% và đang chờ quản lý duyệt hoàn thành.",
                    "Công việc");
            }
            catch
            {
                // keep business flow unaffected by notification errors
            }
        }

        return Ok(ApiResponse<object>.Ok(new { request.MaCongViec, PhanTramHoanThanh = progress, MaTienDo = newProgressUpdate.MaTienDo }, "Cập nhật tiến độ thành công"));
    }

    [HttpPost("/phancong/nhanvien")]
    [Authorize(Policy = Permissions.TasksAssign)]
    public async Task<ActionResult<ApiResponse<object>>> AssignEmployee([FromBody] TaskAssignmentRequest request)
    {
        if (!request.MaCongViec.HasValue || !request.MaNhanVien.HasValue)
        {
            return BadRequest(ApiResponse<object>.Fail("Thiếu thông tin phân công."));
        }

        var taskExists = await _dbContext.CongViecs.AnyAsync(x => x.MaCongViec == request.MaCongViec.Value);
        if (!taskExists)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy công việc."));
        }

        var employeeExists = await _dbContext.NhanViens.AnyAsync(x => x.MaNhanVien == request.MaNhanVien.Value);
        if (!employeeExists)
        {
            return BadRequest(ApiResponse<object>.Fail("Nhân viên không tồn tại."));
        }
        var scope = await BuildTaskProjectScopeAsync(request.MaCongViec.Value);
        if (scope == null)
        {
            return BadRequest(ApiResponse<object>.Fail("Công việc chưa được liên kết với dự án hợp lệ."));
        }

        if (!scope.AllowedEmployeeIds.Contains(request.MaNhanVien.Value))
        {
            return BadRequest(ApiResponse<object>.Fail("Nhân viên không thuộc phạm vi được giao của dự án."));
        }

        var existing = await _dbContext.PhanCongNhanViens.FirstOrDefaultAsync(x => x.MaCongViec == request.MaCongViec.Value && x.MaNhanVien == request.MaNhanVien.Value);
        if (existing == null)
        {
            existing = new PhanCongNhanVien
            {
                MaCongViec = request.MaCongViec.Value,
                MaNhanVien = request.MaNhanVien.Value,
                NgayBatDauDuKien = DateTime.Now,
                TrangThai = 1
            };
            _dbContext.PhanCongNhanViens.Add(existing);
        }

        await _dbContext.SaveChangesAsync();

        try
        {
            var taskName = await _dbContext.CongViecs
                .AsNoTracking()
                .Where(x => x.MaCongViec == request.MaCongViec.Value)
                .Select(x => x.TenCongViec)
                .FirstOrDefaultAsync();

            var employeeName = await _dbContext.NhanViens
                .AsNoTracking()
                .Where(x => x.MaNhanVien == request.MaNhanVien.Value)
                .Select(x => x.HoTen)
                .FirstOrDefaultAsync();

            await NotifyEmployeeAndManagersAsync(
                request.MaNhanVien.Value,
                request.MaCongViec.Value,
                $"Bạn được giao công việc mới: \"{taskName}\".",
                $"Công việc \"{taskName}\" đã được giao cho {employeeName}.",
                "Công việc");
        }
        catch
        {
            // keep assignment flow unaffected by notification errors
        }

        return Ok(ApiResponse<object>.Ok(new { request.MaCongViec, request.MaNhanVien }, "Phân công nhân viên thành công"));
    }

    private async Task NotifyManagersByTaskAsync(int maCongViec, string message, string loaiThongBao)
    {
        var managerEmployeeIds = await (
            from nv in _dbContext.NhanViens.AsNoTracking()
            join ur in _dbContext.UserRoles.AsNoTracking() on nv.AspNetUserId equals ur.UserId
            join r in _dbContext.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where r.Name == Roles.Manager
            where _dbContext.PhanCongPhongBans.Any(pc => pc.MaCongViec == maCongViec && pc.MaPhongBan == nv.MaPhongBan)
               || _dbContext.PhanCongNhoms.Any(pn => pn.MaCongViec == maCongViec && _dbContext.ThanhVienNhoms.Any(tv => tv.MaNhom == pn.MaNhom && tv.MaNhanVien == nv.MaNhanVien))
            select nv.MaNhanVien
        ).Distinct().ToListAsync();

        await CreateNotificationAsync(loaiThongBao, message, managerEmployeeIds);
    }

    private async Task NotifyEmployeeAndManagersAsync(int maNhanVien, int maCongViec, string employeeMessage, string managerMessage, string loaiThongBao)
    {
        var recipientIds = new HashSet<int> { maNhanVien };
        var managerIds = await (
            from nv in _dbContext.NhanViens.AsNoTracking()
            join ur in _dbContext.UserRoles.AsNoTracking() on nv.AspNetUserId equals ur.UserId
            join r in _dbContext.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where r.Name == Roles.Manager
            where _dbContext.PhanCongPhongBans.Any(pc => pc.MaCongViec == maCongViec && pc.MaPhongBan == nv.MaPhongBan)
               || _dbContext.PhanCongNhoms.Any(pn => pn.MaCongViec == maCongViec && _dbContext.ThanhVienNhoms.Any(tv => tv.MaNhom == pn.MaNhom && tv.MaNhanVien == nv.MaNhanVien))
            select nv.MaNhanVien
        ).Distinct().ToListAsync();

        foreach (var id in managerIds)
        {
            recipientIds.Add(id);
        }

        await CreateNotificationAsync(loaiThongBao, employeeMessage, new[] { maNhanVien });
        if (managerIds.Count > 0)
        {
            await CreateNotificationAsync(loaiThongBao, managerMessage, managerIds);
        }
    }

    private async Task CreateNotificationAsync(string loai, string noiDung, IEnumerable<int> maNhanVienIds)
    {
        var ids = maNhanVienIds.Distinct().ToList();
        if (ids.Count == 0) return;

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

        var links = ids.Select(id => new ThongBaoNhanVien
        {
            MaThongBao = tb.MaThongBao,
            MaNhanVien = id,
            DaDoc = false
        });
        _dbContext.ThongBaoNhanViens.AddRange(links);
        await _dbContext.SaveChangesAsync();
    }

    [HttpPost("/congviec/{id:int}/checklist/toggle")]
    [Authorize(Policy = Permissions.TasksEdit)]
    public async Task<ActionResult<ApiResponse<object>>> ToggleChildChecklist(int id, [FromBody] ToggleChildCompletionRequest request)
    {
        var child = await _dbContext.CongViecs.FirstOrDefaultAsync(x => x.MaCongViec == id);
        if (child == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy công việc con."));
        }

        var now = DateTime.Now;
        child.PhanTramHoanThanh = request.IsDone ? 100 : 0;
        child.NgayCapNhat = now;
        child.NguoiCapNhat = User.Identity?.Name ?? string.Empty;

        var actor = await GetActorContextAsync();
        _dbContext.NhatKyCongViecs.Add(new NhatKyCongViec
        {
            MaCongViec = child.MaCongViec,
            MaNhanVien = request.MaNhanVien ?? actor?.MaNhanVien ?? 0,
            PhanTramHoanThanh = child.PhanTramHoanThanh,
            NgayCapNhat = now,
            GhiChu = request.GhiChu?.Trim() ?? (request.IsDone ? "Hoàn thành (Checklist)" : "Mở lại (Checklist)")
        });

        // If the child belongs to a parent, recalc parent's progress
        if (child.MaCongViecCha.HasValue)
        {
            var parentId = child.MaCongViecCha.Value;
            var siblings = await _dbContext.CongViecs
                .Where(x => x.MaCongViecCha == parentId && (x.DaXoa == null || x.DaXoa == false))
                .ToListAsync();

            decimal newProgress = 0;
            if (siblings.Count == 0)
            {
                newProgress = child.PhanTramHoanThanh ?? 0;
            }
            else
            {
                var useWeight = siblings.Any(s => s.DiemCongViec.HasValue && s.DiemCongViec.Value > 0);
                if (useWeight)
                {
                    var totalWeight = siblings.Sum(s => s.DiemCongViec ?? 0);
                    if (totalWeight == 0)
                    {
                        var done = siblings.Count(s => (s.PhanTramHoanThanh ?? 0) >= 100);
                        newProgress = Math.Round(100m * done / siblings.Count, 2);
                    }
                    else
                    {
                        var doneWeight = siblings.Sum(s => ((s.PhanTramHoanThanh ?? 0) >= 100) ? (s.DiemCongViec ?? 0) : 0);
                        newProgress = Math.Round(100m * doneWeight / totalWeight, 2);
                    }
                }
                else
                {
                    var done = siblings.Count(s => (s.PhanTramHoanThanh ?? 0) >= 100);
                    newProgress = Math.Round(100m * done / siblings.Count, 2);
                }
            }

            var parent = await _dbContext.CongViecs.FirstOrDefaultAsync(x => x.MaCongViec == parentId);
            if (parent != null)
            {
                parent.PhanTramHoanThanh = newProgress;
                parent.MaTrangThai = newProgress >= 100 ? 3 : newProgress > 0 ? 2 : parent.MaTrangThai;
                parent.NgayCapNhat = now;
                parent.NguoiCapNhat = User.Identity?.Name ?? string.Empty;

                var tiendo = new TienDoCongViec
                {
                    MaCongViec = parentId,
                    PhanTramHoanThanh = newProgress,
                    TrangThaiHienTai = parent.MaTrangThai,
                    NgayCapNhat = now
                };
                _dbContext.TienDoCongViecs.Add(tiendo);
            }
        }

        await _dbContext.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { child.MaCongViec, child.PhanTramHoanThanh }, "Cập nhật checklist thành công"));
    }

    [HttpPost("{id:int}/assignments/nhom")]
    [Authorize(Policy = Permissions.TasksAssign)]
    public async Task<ActionResult<ApiResponse<object>>> AssignTeam(int id, [FromBody] TaskAssignmentRequest request)
    {
        if (!request.MaNhom.HasValue)
        {
            return BadRequest(ApiResponse<object>.Fail("Thiếu thông tin nhóm."));
        }

        var taskExists = await _dbContext.CongViecs.AnyAsync(x => x.MaCongViec == id);
        var teamExists = await _dbContext.Nhoms.AnyAsync(x => x.MaNhom == request.MaNhom.Value);
        if (!taskExists || !teamExists)
        {
            return BadRequest(ApiResponse<object>.Fail("Công việc hoặc nhóm không tồn tại."));
        }
        var scope = await BuildTaskProjectScopeAsync(id);
        if (scope == null)
        {
            return BadRequest(ApiResponse<object>.Fail("Công việc chưa được liên kết với dự án hợp lệ."));
        }

        var isProjectTeam = await _dbContext.DuAnNhoms
            .AsNoTracking()
            .AnyAsync(x => x.MaDuAn == scope.MaDuAn && x.MaNhom == request.MaNhom.Value && (x.TrangThai ?? 1) == 1);

        if (!isProjectTeam)
        {
            var memberIds = (await _dbContext.ThanhVienNhoms
                .AsNoTracking()
                .Where(x => x.MaNhom == request.MaNhom.Value)
                .Select(x => x.MaNhanVien)
                .Distinct()
                .ToListAsync())
                .ToHashSet();

            var teamLeaderId = await _dbContext.Nhoms
                .AsNoTracking()
                .Where(x => x.MaNhom == request.MaNhom.Value)
                .Select(x => x.TruongNhom)
                .FirstOrDefaultAsync();

            if (teamLeaderId.HasValue)
            {
                memberIds.Add(teamLeaderId.Value);
            }

            if (memberIds.Count == 0)
            {
                return BadRequest(ApiResponse<object>.Fail("Nhóm chưa có thành viên hợp lệ để giao việc."));
            }

            var allMembersInScope = memberIds.All(x => scope.AllowedEmployeeIds.Contains(x));
            if (!allMembersInScope)
            {
                return BadRequest(ApiResponse<object>.Fail("Nhóm không thuộc phạm vi được giao của dự án."));
            }
        }

        var existing = await _dbContext.PhanCongNhoms.FirstOrDefaultAsync(x => x.MaCongViec == id && x.MaNhom == request.MaNhom.Value);
        if (existing == null)
        {
            _dbContext.PhanCongNhoms.Add(new PhanCongNhom
            {
                MaCongViec = id,
                MaNhom = request.MaNhom.Value,
                NgayGiao = DateTime.Now,
                TrangThai = 1
            });
        }

        await _dbContext.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { MaCongViec = id, request.MaNhom }, "Phân công nhóm thành công"));
    }

    [HttpDelete("{id:int}/assignments/nhom/{maNhom:int}")]
    [Authorize(Policy = Permissions.TasksAssign)]
    public async Task<ActionResult<ApiResponse<object>>> RemoveTeamAssignment(int id, int maNhom)
    {
        var existing = await _dbContext.PhanCongNhoms.FirstOrDefaultAsync(x => x.MaCongViec == id && x.MaNhom == maNhom);
        if (existing == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy phân công nhóm."));
        }

        _dbContext.PhanCongNhoms.Remove(existing);
        await _dbContext.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(null, "Đã gỡ phân công nhóm"));
    }

    [HttpPost("{id:int}/assignments/phongban")]
    [Authorize(Policy = Permissions.TasksAssign)]
    public async Task<ActionResult<ApiResponse<object>>> AssignDepartment(int id, [FromBody] TaskAssignmentRequest request)
    {
        if (!request.MaPhongBan.HasValue)
        {
            return BadRequest(ApiResponse<object>.Fail("Thiếu thông tin phòng ban."));
        }

        var taskExists = await _dbContext.CongViecs.AnyAsync(x => x.MaCongViec == id);
        var departmentExists = await _dbContext.PhongBans.AnyAsync(x => x.MaPhongBan == request.MaPhongBan.Value);
        if (!taskExists || !departmentExists)
        {
            return BadRequest(ApiResponse<object>.Fail("Công việc hoặc phòng ban không tồn tại."));
        }
        var scope = await BuildTaskProjectScopeAsync(id);
        if (scope == null)
        {
            return BadRequest(ApiResponse<object>.Fail("Công việc chưa được liên kết với dự án hợp lệ."));
        }

        if (!scope.ProjectDepartmentIds.Contains(request.MaPhongBan.Value))
        {
            return BadRequest(ApiResponse<object>.Fail("Phòng ban không thuộc phạm vi được giao của dự án."));
        }

        var existing = await _dbContext.PhanCongPhongBans.FirstOrDefaultAsync(x => x.MaCongViec == id && x.MaPhongBan == request.MaPhongBan.Value);
        if (existing == null)
        {
            _dbContext.PhanCongPhongBans.Add(new PhanCongPhongBan
            {
                MaCongViec = id,
                MaPhongBan = request.MaPhongBan.Value,
                NgayPhanCong = DateTime.Now,
                TrangThai = 1
            });
        }

        await _dbContext.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { MaCongViec = id, request.MaPhongBan }, "Phân công phòng ban thành công"));
    }

    [HttpDelete("{id:int}/assignments/phongban/{maPhongBan:int}")]
    [Authorize(Policy = Permissions.TasksAssign)]
    public async Task<ActionResult<ApiResponse<object>>> RemoveDepartmentAssignment(int id, int maPhongBan)
    {
        var existing = await _dbContext.PhanCongPhongBans.FirstOrDefaultAsync(x => x.MaCongViec == id && x.MaPhongBan == maPhongBan);
        if (existing == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy phân công phòng ban."));
        }

        _dbContext.PhanCongPhongBans.Remove(existing);
        await _dbContext.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(null, "Đã gỡ phân công phòng ban"));
    }

    // ========== Progress Approval Endpoints ==========

    [HttpGet("/tiendo")]
    [Authorize(Policy = Permissions.TasksEdit)]
    public async Task<ActionResult<ApiResponse<PagedResult<ProgressUpdateDto>>>> GetProgressUpdates(
        [FromQuery] string? trangthai,
        [FromQuery] int? duan,
        [FromQuery] int? maCongViec,
        [FromQuery] DateTime? tuNgay,
        [FromQuery] DateTime? denNgay,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        try
        {
            page = Math.Max(page, 1);
            size = Math.Clamp(size, 1, 100);

            var actor = await GetActorContextAsync();
            if (actor == null)
            {
                return Unauthorized(ApiResponse<PagedResult<ProgressUpdateDto>>.Fail("Tài khoản chưa được liên kết."));
            }

            var query = _dbContext.TienDoCongViecs
                .AsNoTracking()
                .Include(x => x.CongViec).ThenInclude(x => x.DuAn)
                .Include(x => x.NguoiPheDuyetNavigation)
                .AsQueryable();

            if (actor.IsManager && !actor.IsAdmin)
            {
                var managerDepartmentIds = await GetManagerDepartmentIdsAsync(actor.MaNhanVien);
                query = ApplyManagerProgressScope(query, actor.MaNhanVien, managerDepartmentIds, _dbContext);
            }
            else if (!actor.IsAdmin && !actor.IsManager)
            {
                query = ApplyEmployeeProgressScope(query, actor, _dbContext);
            }

            if (!string.IsNullOrWhiteSpace(trangthai))
            {
                query = query.Where(x => x.TrangThaiPheDuyet == trangthai.Trim());
            }

            if (duan.HasValue)
            {
                query = query.Where(x => x.CongViec != null && x.CongViec.MaDuAn == duan.Value);
            }

            if (maCongViec.HasValue)
            {
                query = query.Where(x => x.MaCongViec == maCongViec.Value);
            }

            if (tuNgay.HasValue)
            {
                query = query.Where(x => x.NgayCapNhat.HasValue && x.NgayCapNhat.Value.Date >= tuNgay.Value.Date);
            }

            if (denNgay.HasValue)
            {
                query = query.Where(x => x.NgayCapNhat.HasValue && x.NgayCapNhat.Value.Date <= denNgay.Value.Date);
            }

            var totalItems = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.NgayCapNhat)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(x => new ProgressUpdateDto
                {
                    MaTienDo = x.MaTienDo,
                    MaCongViec = x.MaCongViec,
                    TenCongViec = x.CongViec != null ? x.CongViec.TenCongViec : "(Công việc đã xóa)",
                    MaDuAn = x.CongViec != null ? x.CongViec.MaDuAn : 0,
                    TenDuAn = x.CongViec != null && x.CongViec.DuAn != null ? x.CongViec.DuAn.TenDuAn : "-",
                    PhanTramHoanThanh = x.PhanTramHoanThanh,
                    TrangThaiHienTai = x.TrangThaiHienTai,
                    NgayCapNhat = x.NgayCapNhat,
                    TrangThaiPheDuyet = x.TrangThaiPheDuyet,
                    NguoiPheDuyet = x.NguoiPheDuyet,
                    HoTenNguoiPheDuyet = x.NguoiPheDuyetNavigation != null ? x.NguoiPheDuyetNavigation.HoTen : null,
                    NgayPheDuyet = x.NgayPheDuyet,
                    LyDoTuChoi = x.LyDoTuChoi
                })
                .ToListAsync();

            return Ok(ApiResponse<PagedResult<ProgressUpdateDto>>.Ok(new PagedResult<ProgressUpdateDto>
            {
                Items = items,
                Page = page,
                Size = size,
                TotalItems = totalItems,
                TotalPages = (int)Math.Ceiling(totalItems / (double)size)
            }));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return StatusCode(500, ApiResponse<PagedResult<ProgressUpdateDto>>.Fail($"Không thể tải dữ liệu tiến độ: {ex.Message}"));
        }
    }

    [HttpGet("/tiendo/{id:int}")]
    [Authorize(Policy = Permissions.TasksEdit)]
    public async Task<ActionResult<ApiResponse<ProgressUpdateDto>>> GetProgressUpdateDetail(int id)
    {
        try
        {
            var actor = await GetActorContextAsync();
            if (actor == null)
            {
                return Unauthorized(ApiResponse<ProgressUpdateDto>.Fail("Tài khoản chưa được liên kết."));
            }

            var canAccess = await CanAccessProgressUpdateAsync(id, actor);
            if (!canAccess)
            {
                return StatusCode(403, ApiResponse<ProgressUpdateDto>.Fail("Bạn không có quyền truy cập cập nhật tiến độ này."));
            }

            var update = await _dbContext.TienDoCongViecs
                .AsNoTracking()
                .Include(x => x.CongViec).ThenInclude(x => x.DuAn)
                .Include(x => x.NguoiPheDuyetNavigation)
                .FirstOrDefaultAsync(x => x.MaTienDo == id);

            if (update == null)
            {
                return NotFound(ApiResponse<ProgressUpdateDto>.Fail("Không tìm thấy cập nhật tiến độ."));
            }

            var dto = new ProgressUpdateDto
            {
                MaTienDo = update.MaTienDo,
                MaCongViec = update.MaCongViec,
                TenCongViec = update.CongViec != null ? update.CongViec.TenCongViec : "(Công việc đã xóa)",
                MaDuAn = update.CongViec != null ? update.CongViec.MaDuAn : 0,
                TenDuAn = update.CongViec != null && update.CongViec.DuAn != null ? update.CongViec.DuAn.TenDuAn : "-",
                PhanTramHoanThanh = update.PhanTramHoanThanh,
                TrangThaiHienTai = update.TrangThaiHienTai,
                NgayCapNhat = update.NgayCapNhat,
                TrangThaiPheDuyet = update.TrangThaiPheDuyet,
                NguoiPheDuyet = update.NguoiPheDuyet,
                HoTenNguoiPheDuyet = update.NguoiPheDuyetNavigation?.HoTen,
                NgayPheDuyet = update.NgayPheDuyet,
                LyDoTuChoi = update.LyDoTuChoi
            };

            return Ok(ApiResponse<ProgressUpdateDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return StatusCode(500, ApiResponse<ProgressUpdateDto>.Fail($"Không thể tải chi tiết tiến độ: {ex.Message}"));
        }
    }

    [HttpPut("/tiendo/{id:int}/approve")]
    [Authorize(Policy = Permissions.TasksApprove)]
    public async Task<ActionResult<ApiResponse<object>>> ApproveProgress(int id, [FromBody] ApproveProgressRequest request)
    {
        var update = await _dbContext.TienDoCongViecs.FirstOrDefaultAsync(x => x.MaTienDo == id);
        if (update == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy cập nhật tiến độ."));
        }

        if (update.TrangThaiPheDuyet != "Chờ duyệt")
        {
            return BadRequest(ApiResponse<object>.Fail($"Cập nhật tiến độ này đã có trạng thái: {update.TrangThaiPheDuyet}"));
        }

        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Tài khoản chưa được liên kết."));
        }

        var canAccess = await CanAccessProgressUpdateAsync(id, actor);
        if (!canAccess)
        {
            return StatusCode(403, ApiResponse<object>.Fail("Bạn không có quyền duyệt tiến độ cho công việc ngoài phạm vi quản lý."));
        }

        update.TrangThaiPheDuyet = "Đã duyệt";
        update.NguoiPheDuyet = actor.MaNhanVien;
        update.NgayPheDuyet = DateTime.Now;

        // Apply progress to actual task when approved
        var congViec = await _dbContext.CongViecs.FirstOrDefaultAsync(x => x.MaCongViec == update.MaCongViec);
        if (congViec != null && update.PhanTramHoanThanh.HasValue)
        {
            congViec.PhanTramHoanThanh = update.PhanTramHoanThanh;
            congViec.MaTrangThai = update.PhanTramHoanThanh.Value >= 100 ? 3 : update.PhanTramHoanThanh.Value > 0 ? 2 : 1;
            congViec.NgayCapNhat = DateTime.Now;
            congViec.NguoiCapNhat = User.Identity?.Name ?? string.Empty;
        }

        await _dbContext.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { update.MaTienDo }, "Đã phê duyệt tiến độ"));
    }

    [HttpPut("/tiendo/{id:int}/reject")]
    [Authorize(Policy = Permissions.TasksApprove)]
    public async Task<ActionResult<ApiResponse<object>>> RejectProgress(int id, [FromBody] RejectProgressRequest request)
    {
        var update = await _dbContext.TienDoCongViecs.FirstOrDefaultAsync(x => x.MaTienDo == id);
        if (update == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy cập nhật tiến độ."));
        }

        if (update.TrangThaiPheDuyet != "Chờ duyệt")
        {
            return BadRequest(ApiResponse<object>.Fail($"Cập nhật tiến độ này đã có trạng thái: {update.TrangThaiPheDuyet}"));
        }

        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Tài khoản chưa được liên kết."));
        }

        var canAccess = await CanAccessProgressUpdateAsync(id, actor);
        if (!canAccess)
        {
            return StatusCode(403, ApiResponse<object>.Fail("Bạn không có quyền từ chối tiến độ cho công việc ngoài phạm vi quản lý."));
        }

        update.TrangThaiPheDuyet = "Từ chối";
        update.NguoiPheDuyet = actor.MaNhanVien;
        update.NgayPheDuyet = DateTime.Now;
        update.LyDoTuChoi = request.LyDoTuChoi?.Trim() ?? "Không có lý do";

        await _dbContext.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(new { update.MaTienDo }, "Đã từ chối tiến độ"));
    }
}
