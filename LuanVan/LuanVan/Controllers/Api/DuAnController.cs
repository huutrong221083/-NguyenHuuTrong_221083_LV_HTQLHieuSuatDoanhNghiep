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
[Route("duan")]
[Authorize]
public class DuAnController : ControllerBase
{
    private const int ProjectStatusNotStarted = 0;
    private const int ProjectStatusInProgress = 1;
    private const int ProjectStatusCompleted = 2;
    private const int ProjectStatusPendingCompletionApproval = 3;
    private const int ProjectStatusDeleted = -1;
    private const int TaskStatusCompleted = 3;

    private readonly AppDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogService _auditLogService;
    private readonly IConfiguration _configuration;

    public DuAnController(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IAuditLogService auditLogService,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _auditLogService = auditLogService;
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
        public string UserId { get; set; } = string.Empty;
        public int MaNhanVien { get; set; }
        public int? MaPhongBan { get; set; }
        public int? PhoMaPhongBan { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsManager { get; set; }
        public bool IsEmployee { get; set; }
    }

    private sealed class ProjectTaskAssigneeRow
    {
        public int MaCongViec { get; set; }
        public string HoTen { get; set; } = string.Empty;
    }

    private sealed class ProjectTaskProgressRow
    {
        public int MaCongViec { get; set; }
        public double PhanTram { get; set; }
    }

    private async Task<ActorContext?> GetActorContextAsync()
    {
        EnsureDbConnectionStringInitialized();

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        return await _dbContext.NhanViens
            .AsNoTracking()
            .Where(x => x.AspNetUserId == userId)
            .Select(x => new ActorContext
            {
                UserId = userId,
                MaNhanVien = x.MaNhanVien,
                MaPhongBan = x.MaPhongBan,
                PhoMaPhongBan = x.PhoMaPhongBan,
                IsAdmin = User.IsInRole(Roles.Admin),
                IsManager = User.IsInRole(Roles.Manager),
                IsEmployee = User.IsInRole(Roles.Employee)
            })
            .FirstOrDefaultAsync();
    }

    private async Task<HashSet<int>> GetManagerDepartmentIdsAsync(ActorContext actor)
    {
        var departmentIds = new HashSet<int>();

        if (actor.MaPhongBan.HasValue)
        {
            departmentIds.Add(actor.MaPhongBan.Value);
        }

        var ledDepartmentIds = await _dbContext.PhongBans
            .AsNoTracking()
            .Where(x => x.MaTruongPhong == actor.MaNhanVien)
            .Select(x => x.MaPhongBan)
            .ToListAsync();

        foreach (var departmentId in ledDepartmentIds)
        {
            departmentIds.Add(departmentId);
        }

        return departmentIds;
    }

    private async Task<bool> CanAccessProjectAsync(ActorContext actor, int maDuAn)
    {
        if (actor.IsAdmin)
        {
            return true;
        }

        if (actor.IsManager)
        {
            var managerDepartmentIds = await GetManagerDepartmentIdsAsync(actor);
            var inDepartmentScope = managerDepartmentIds.Any() && await _dbContext.DuAnPhongBans
                .AsNoTracking()
                .AnyAsync(x => x.MaDuAn == maDuAn && managerDepartmentIds.Contains(x.MaPhongBan) && (x.TrangThai ?? 1) == 1);

            if (inDepartmentScope)
            {
                return true;
            }

            return await _dbContext.DuAnNhanViens
                .AsNoTracking()
                .AnyAsync(x => x.MaDuAn == maDuAn && x.MaNhanVien == actor.MaNhanVien && (x.TrangThai ?? 1) == 1);
        }

        if (actor.IsEmployee)
        {
            // Check direct project assignment
            var isProjectMember = await _dbContext.DuAnNhanViens
                .AsNoTracking()
                .AnyAsync(x => x.MaDuAn == maDuAn && x.MaNhanVien == actor.MaNhanVien && (x.TrangThai ?? 1) == 1);

            if (isProjectMember)
            {
                return true;
            }

            // Check department-level project assignment
            var actorDepartmentId = actor.MaPhongBan.GetValueOrDefault(0);
            if (actorDepartmentId != 0)
            {
                var isDepartmentProjectMember = await _dbContext.DuAnPhongBans
                    .AsNoTracking()
                    .AnyAsync(x => x.MaDuAn == maDuAn && x.MaPhongBan == actorDepartmentId && (x.TrangThai ?? 1) == 1);

                if (isDepartmentProjectMember)
                {
                    return true;
                }
            }

            // Check team-level project assignment
            var teamIds = _dbContext.ThanhVienNhoms
                .AsNoTracking()
                .Where(x => x.MaNhanVien == actor.MaNhanVien)
                .Select(x => x.MaNhom);

            var isTeamProjectMember = await _dbContext.DuAnNhoms
                .AsNoTracking()
                .AnyAsync(x => x.MaDuAn == maDuAn && (x.TrangThai ?? 1) == 1 && teamIds.Contains(x.MaNhom));

            if (isTeamProjectMember)
            {
                return true;
            }

            // Also check if has any task assignment on the project (legacy support)
            var actorSecondaryDepartmentId = actor.PhoMaPhongBan;

            return await _dbContext.CongViecs
                .AsNoTracking()
                .AnyAsync(x => x.MaDuAn == maDuAn && (
                    x.PhanCongNhanViens.Any(p => p.MaNhanVien == actor.MaNhanVien && (p.TrangThai ?? 1) == 1)
                    || x.PhanCongNhoms.Any(p => (p.TrangThai ?? 1) == 1 && teamIds.Contains(p.MaNhom))
                    || x.PhanCongPhongBans.Any(p => (p.TrangThai ?? 1) == 1
                        && ((actorDepartmentId != 0 && p.MaPhongBan == actorDepartmentId)
                            || (actorSecondaryDepartmentId.HasValue && p.MaPhongBan == actorSecondaryDepartmentId.Value)))));
        }

        return false;
    }

    private static string? ValidateProjectRequest(string? tenDuAn, DateTime? ngayBatDau, DateTime? ngayKetThuc, int? trangThai)
    {
        if (string.IsNullOrWhiteSpace(tenDuAn))
        {
            return "Tên dự án là bắt buộc.";
        }

        if (!ngayBatDau.HasValue || !ngayKetThuc.HasValue)
        {
            return "Ngày bắt đầu và ngày kết thúc là bắt buộc.";
        }

        if (ngayBatDau.Value.Date < DateTime.Today)
        {
            return "Ngày bắt đầu phải từ ngày hiện tại, không được chọn ngày trong quá khứ.";
        }

        if (ngayKetThuc.Value.Date <= ngayBatDau.Value.Date)
        {
            return "Ngày kết thúc phải lớn hơn ngày bắt đầu.";
        }

        if (trangThai.HasValue && trangThai.Value is not (ProjectStatusNotStarted or ProjectStatusInProgress or ProjectStatusCompleted))
        {
            return "Trạng thái dự án không hợp lệ.";
        }

        return null;
    }

    private static int ResolveProjectStatusForCreate(int? requestedStatus, DateTime? ngayBatDau, DateTime now)
    {
        var resolvedStatus = requestedStatus ?? ProjectStatusNotStarted;

        // If start time is now/past and project is still marked as not started, auto switch to in-progress.
        if (resolvedStatus == ProjectStatusNotStarted && ngayBatDau.HasValue && ngayBatDau.Value <= now)
        {
            return ProjectStatusInProgress;
        }

        return resolvedStatus;
    }

    private async Task EnsureDepartmentLinkAsync(int maDuAn, int maPhongBan)
    {
        var existing = await _dbContext.DuAnPhongBans
            .FirstOrDefaultAsync(x => x.MaDuAn == maDuAn && x.MaPhongBan == maPhongBan);

        if (existing == null)
        {
            _dbContext.DuAnPhongBans.Add(new DuAnPhongBan
            {
                MaDuAn = maDuAn,
                MaPhongBan = maPhongBan,
                NgayThamGia = DateTime.Now,
                TrangThai = 1
            });
            return;
        }

        existing.TrangThai = 1;
        existing.NgayThamGia ??= DateTime.Now;
    }

    private async Task MarkProjectTasksCompletedAsync(int maDuAn)
    {
        var tasks = await _dbContext.CongViecs
            .Where(x => x.MaDuAn == maDuAn)
            .ToListAsync();

        if (tasks.Count == 0)
        {
            return;
        }

        var taskIds = tasks.Select(x => x.MaCongViec).ToList();
        var progressByTask = await _dbContext.TienDoCongViecs
            .Where(x => taskIds.Contains(x.MaCongViec))
            .ToDictionaryAsync(x => x.MaCongViec, x => x);

        foreach (var task in tasks)
        {
            task.MaTrangThai = TaskStatusCompleted;

            if (progressByTask.TryGetValue(task.MaCongViec, out var progress))
            {
                progress.PhanTramHoanThanh = 100;
                progress.TrangThaiHienTai = TaskStatusCompleted;
                progress.NgayCapNhat = DateTime.Now;
                continue;
            }

            _dbContext.TienDoCongViecs.Add(new TienDoCongViec
            {
                //MaTienDo = Guid.NewGuid().ToString("N"),
                MaCongViec = task.MaCongViec,
                PhanTramHoanThanh = 100,
                TrangThaiHienTai = TaskStatusCompleted,
                NgayCapNhat = DateTime.Now
            });
        }
    }

    [HttpPost]
    [Authorize(Policy = Permissions.ProjectsCreate)]
    public async Task<ActionResult<ApiResponse<DuAnDetailDto>>> CreateDuAn([FromBody] UpsertDuAnRequest request)
    {
        var now = DateTime.Now;
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<DuAnDetailDto>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        var validationError = ValidateProjectRequest(request.TenDuAn, request.NgayBatDau, request.NgayKetThuc, request.TrangThai);
        if (validationError != null)
        {
            return BadRequest(ApiResponse<DuAnDetailDto>.Fail(validationError));
        }

        var normalizedTenDuAn = request.TenDuAn!.Trim();
        var projectNameExists = await _dbContext.DuAns.AnyAsync(x =>
            x.TenDuAn != null
            && (x.TrangThai ?? 0) != ProjectStatusDeleted
            && EF.Functions.Collate(x.TenDuAn, "Latin1_General_CI_AI") == normalizedTenDuAn);
        if (projectNameExists)
        {
            return Conflict(ApiResponse<DuAnDetailDto>.Fail("Tên dự án đã tồn tại."));
        }

        var maPhongBan = request.MaPhongBan;
        if (actor.IsManager)
        {
            var managerDepartmentIds = await GetManagerDepartmentIdsAsync(actor);
            if (!managerDepartmentIds.Any())
            {
                return Forbid();
            }

            if (!maPhongBan.HasValue)
            {
                maPhongBan = actor.MaPhongBan ?? managerDepartmentIds.First();
            }
            else if (!managerDepartmentIds.Contains(maPhongBan.Value))
            {
                return Forbid();
            }
        }

        var resolvedProjectStatus = ResolveProjectStatusForCreate(request.TrangThai, request.NgayBatDau, now);

        var duAn = new DuAn
        {
            TenDuAn = normalizedTenDuAn,
            MoTa = request.MoTa?.Trim(),
            NgayBatDau = request.NgayBatDau,
            NgayKetThuc = request.NgayKetThuc,
            TrangThai = resolvedProjectStatus
        };

        await using var tx = await _dbContext.Database.BeginTransactionAsync();

        _dbContext.DuAns.Add(duAn);
        await _dbContext.SaveChangesAsync();

        if (maPhongBan.HasValue)
        {
            await EnsureDepartmentLinkAsync(duAn.MaDuAn, maPhongBan.Value);
            await _dbContext.SaveChangesAsync();
        }

        await _auditLogService.LogByUserIdAsync(actor.UserId, $"Tạo dự án {duAn.TenDuAn} (Mã: {duAn.MaDuAn})");
        await tx.CommitAsync();

        var detail = await BuildProjectDetailAsync(duAn.MaDuAn);
        return CreatedAtAction(nameof(GetDuAnById), new { id = duAn.MaDuAn }, ApiResponse<DuAnDetailDto>.Ok(detail, "Tạo dự án thành công"));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.ProjectsEdit)]
    public async Task<ActionResult<ApiResponse<DuAnDetailDto>>> UpdateDuAn(int id, [FromBody] UpsertDuAnRequest request)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<DuAnDetailDto>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        var duAn = await _dbContext.DuAns.FirstOrDefaultAsync(x => x.MaDuAn == id && (x.TrangThai ?? 0) != ProjectStatusDeleted);
        if (duAn == null)
        {
            return NotFound(ApiResponse<DuAnDetailDto>.Fail("Không tìm thấy dự án."));
        }

        if (!await CanAccessProjectAsync(actor, id))
        {
            return Forbid();
        }

        var validationError = ValidateProjectRequest(request.TenDuAn, request.NgayBatDau, request.NgayKetThuc, request.TrangThai);
        if (validationError != null)
        {
            return BadRequest(ApiResponse<DuAnDetailDto>.Fail(validationError));
        }

        var normalizedTenDuAn = request.TenDuAn!.Trim();
        var duplicateProjectName = await _dbContext.DuAns.AnyAsync(x =>
            x.MaDuAn != id
            && x.TenDuAn != null
            && (x.TrangThai ?? 0) != ProjectStatusDeleted
            && EF.Functions.Collate(x.TenDuAn, "Latin1_General_CI_AI") == normalizedTenDuAn);
        if (duplicateProjectName)
        {
            return Conflict(ApiResponse<DuAnDetailDto>.Fail("Tên dự án đã tồn tại."));
        }

        if (actor.IsManager && request.MaPhongBan.HasValue)
        {
            var managerDepartmentIds = await GetManagerDepartmentIdsAsync(actor);
            if (!managerDepartmentIds.Contains(request.MaPhongBan.Value))
            {
                return Forbid();
            }
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync();

        var oldStatus = duAn.TrangThai;

        duAn.TenDuAn = normalizedTenDuAn;
        duAn.MoTa = request.MoTa?.Trim();
        duAn.NgayBatDau = request.NgayBatDau;
        duAn.NgayKetThuc = request.NgayKetThuc;

        // Normalize project status on update: respect explicit request, otherwise auto-transition when start date reached
        var requestedStatus = ResolveProjectStatusForCreate(request.TrangThai, request.NgayBatDau, DateTime.Now);
        var requiresAdminApprovalForCompletion = actor.IsManager && !actor.IsAdmin && oldStatus != ProjectStatusCompleted && requestedStatus == ProjectStatusCompleted;

        // Manager xác nhận hoàn thành dự án cần Admin duyệt: giữ trạng thái hiện tại, gửi thông báo.
        duAn.TrangThai = requiresAdminApprovalForCompletion ? ProjectStatusPendingCompletionApproval : requestedStatus;

        if (request.MaPhongBan.HasValue)
        {
            await EnsureDepartmentLinkAsync(id, request.MaPhongBan.Value);
        }

        // Khi dự án hoàn thành, đồng bộ trạng thái công việc liên quan.
        if (oldStatus != ProjectStatusCompleted && duAn.TrangThai == ProjectStatusCompleted)
        {
            await MarkProjectTasksCompletedAsync(id);
        }

        await _dbContext.SaveChangesAsync();

        if (requiresAdminApprovalForCompletion)
        {
            try
            {
                await NotifyAdminsForProjectCompletionApprovalAsync(id, duAn.TenDuAn ?? $"Dự án {id}", actor.MaNhanVien);
            }
            catch
            {
                // keep business flow unaffected by notification errors
            }
        }

        await _auditLogService.LogByUserIdAsync(actor.UserId, $"Cập nhật dự án {duAn.TenDuAn} (Mã: {id})");
        await tx.CommitAsync();

        var detail = await BuildProjectDetailAsync(id);
        var message = requiresAdminApprovalForCompletion
            ? "Đã gửi yêu cầu Admin duyệt hoàn thành dự án."
            : "Cập nhật dự án thành công";
        return Ok(ApiResponse<DuAnDetailDto>.Ok(detail, message));
    }

    [HttpGet("hoanthanh-cho-duyet")]
    [Authorize(Policy = Permissions.ProjectsView)]
    public async Task<ActionResult<ApiResponse<List<ProjectCompletionApprovalItemDto>>>> GetPendingProjectCompletionApprovals()
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<List<ProjectCompletionApprovalItemDto>>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        if (!actor.IsAdmin && !actor.IsManager)
        {
            return Forbid();
        }

        IQueryable<DuAn> query = _dbContext.DuAns.AsNoTracking()
            .Where(x => (x.TrangThai ?? 0) == ProjectStatusPendingCompletionApproval);

        if (actor.IsManager && !actor.IsAdmin)
        {
            var managerDepartmentIds = await GetManagerDepartmentIdsAsync(actor);
            query = query.Where(x =>
                (managerDepartmentIds.Any() && x.DuAnPhongBans.Any(pb => managerDepartmentIds.Contains(pb.MaPhongBan) && (pb.TrangThai ?? 1) == 1))
                || x.DuAnNhanViens.Any(nv => nv.MaNhanVien == actor.MaNhanVien && (nv.TrangThai ?? 1) == 1));
        }

        var items = await query
            .OrderByDescending(x => x.MaDuAn)
            .Select(x => new ProjectCompletionApprovalItemDto
            {
                MaDuAn = x.MaDuAn,
                TenDuAn = x.TenDuAn ?? $"Dự án {x.MaDuAn}",
                MoTa = x.MoTa,
                NgayBatDau = x.NgayBatDau,
                NgayKetThuc = x.NgayKetThuc,
                TongCongViec = x.CongViecs.Count(cv => (cv.DaXoa ?? false) == false),
                CongViecHoanThanh = x.CongViecs.Count(cv => (cv.DaXoa ?? false) == false && cv.MaTrangThai == TaskStatusCompleted)
            })
            .ToListAsync();

        return Ok(ApiResponse<List<ProjectCompletionApprovalItemDto>>.Ok(items));
    }

    [HttpPut("{id:int}/approve-completion")]
    [Authorize(Policy = Permissions.ProjectsEdit)]
    public async Task<ActionResult<ApiResponse<object>>> ApproveProjectCompletion(int id)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }
        if (!actor.IsAdmin)
        {
            return Forbid();
        }

        var duAn = await _dbContext.DuAns.FirstOrDefaultAsync(x => x.MaDuAn == id && (x.TrangThai ?? 0) != ProjectStatusDeleted);
        if (duAn == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy dự án."));
        }
        if ((duAn.TrangThai ?? 0) != ProjectStatusPendingCompletionApproval)
        {
            return BadRequest(ApiResponse<object>.Fail("Dự án không ở trạng thái chờ Admin duyệt hoàn thành."));
        }

        duAn.TrangThai = ProjectStatusCompleted;
        await MarkProjectTasksCompletedAsync(id);
        await _dbContext.SaveChangesAsync();

        try
        {
            await NotifyManagersByProjectAsync(id, $"Admin đã duyệt hoàn thành dự án \"{duAn.TenDuAn}\".");
        }
        catch
        {
            // keep business flow unaffected by notification errors
        }

        return Ok(ApiResponse<object>.Ok(new { MaDuAn = id }, "Đã duyệt hoàn thành dự án."));
    }

    [HttpPut("{id:int}/reject-completion")]
    [Authorize(Policy = Permissions.ProjectsEdit)]
    public async Task<ActionResult<ApiResponse<object>>> RejectProjectCompletion(int id, [FromBody] RejectProjectCompletionRequest? request)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }
        if (!actor.IsAdmin)
        {
            return Forbid();
        }

        var duAn = await _dbContext.DuAns.FirstOrDefaultAsync(x => x.MaDuAn == id && (x.TrangThai ?? 0) != ProjectStatusDeleted);
        if (duAn == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy dự án."));
        }
        if ((duAn.TrangThai ?? 0) != ProjectStatusPendingCompletionApproval)
        {
            return BadRequest(ApiResponse<object>.Fail("Dự án không ở trạng thái chờ Admin duyệt hoàn thành."));
        }

        duAn.TrangThai = ProjectStatusInProgress;
        await _dbContext.SaveChangesAsync();

        var reason = string.IsNullOrWhiteSpace(request?.LyDoTuChoi) ? "Chưa đạt điều kiện hoàn thành." : request!.LyDoTuChoi!.Trim();
        try
        {
            await NotifyManagersByProjectAsync(id, $"Admin đã từ chối hoàn thành dự án \"{duAn.TenDuAn}\". Lý do: {reason}");
        }
        catch
        {
            // keep business flow unaffected by notification errors
        }

        return Ok(ApiResponse<object>.Ok(new { MaDuAn = id }, "Đã từ chối hoàn thành dự án."));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.ProjectsDelete)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteDuAn(int id)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        var duAn = await _dbContext.DuAns.FirstOrDefaultAsync(x => x.MaDuAn == id && (x.TrangThai ?? 0) != ProjectStatusDeleted);
        if (duAn == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy dự án."));
        }

        if (!await CanAccessProjectAsync(actor, id))
        {
            return Forbid();
        }

        var hasTasks = await _dbContext.CongViecs.AnyAsync(x => x.MaDuAn == id);
        var hasMembers = await _dbContext.DuAnNhanViens.AnyAsync(x => x.MaDuAn == id && (x.TrangThai ?? 1) == 1);

        duAn.TrangThai = ProjectStatusDeleted;
        await _dbContext.SaveChangesAsync();

        await _auditLogService.LogByUserIdAsync(actor.UserId, $"Xóa mềm dự án {duAn.TenDuAn} (Mã: {id})");

        var message = hasTasks || hasMembers
            ? "Dự án đã được chuyển trạng thái inactive (xóa mềm) do đang có dữ liệu liên kết."
            : "Xóa mềm dự án thành công.";

        return Ok(ApiResponse<object>.Ok(null, message));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<DuAnListItemDto>>>> GetDuAns(
        [FromQuery] int? trangThai,
        [FromQuery] string? keyword,
        [FromQuery] int? month,
        [FromQuery] int? year,
        [FromQuery] DateTime? tuNgay,
        [FromQuery] DateTime? denNgay,
        [FromQuery] int page = 1,
        [FromQuery] int size = 10)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<PagedResult<DuAnListItemDto>>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 100);

        IQueryable<DuAn> query = _dbContext.DuAns.AsNoTracking().Where(x => (x.TrangThai ?? 0) != ProjectStatusDeleted);

        if (!actor.IsAdmin)
        {
            if (actor.IsManager)
            {
                var managerDepartmentIds = await GetManagerDepartmentIdsAsync(actor);
                query = query.Where(x =>
                    (managerDepartmentIds.Any() && x.DuAnPhongBans.Any(pb => managerDepartmentIds.Contains(pb.MaPhongBan) && (pb.TrangThai ?? 1) == 1))
                    || x.DuAnNhanViens.Any(nv => nv.MaNhanVien == actor.MaNhanVien && (nv.TrangThai ?? 1) == 1));
            }
            else
            {
                // Employee scope: see projects from direct assignment, team membership, or department assignment
                var employeeTeamIds = _dbContext.ThanhVienNhoms
                    .AsNoTracking()
                    .Where(x => x.MaNhanVien == actor.MaNhanVien)
                    .Select(x => x.MaNhom);

                var employeePhongBanId = actor.MaPhongBan.GetValueOrDefault(0);

                query = query.Where(x => 
                    x.DuAnNhanViens.Any(nv => nv.MaNhanVien == actor.MaNhanVien && (nv.TrangThai ?? 1) == 1)
                    || (employeePhongBanId != 0 && x.DuAnPhongBans.Any(pb => pb.MaPhongBan == employeePhongBanId && (pb.TrangThai ?? 1) == 1))
                    || x.DuAnNhoms.Any(dn => (dn.TrangThai ?? 1) == 1 && employeeTeamIds.Contains(dn.MaNhom)));
            }
        }

        if (trangThai.HasValue)
        {
            query = query.Where(x => x.TrangThai == trangThai.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(x =>
                (x.TenDuAn != null && EF.Functions.Like(EF.Functions.Collate(x.TenDuAn, "Latin1_General_CI_AI"), $"%{k}%"))
                || (x.MoTa != null && EF.Functions.Like(EF.Functions.Collate(x.MoTa, "Latin1_General_CI_AI"), $"%{k}%")));
        }

        if (month.HasValue && month.Value is >= 1 and <= 12)
        {
            query = query.Where(x => x.NgayBatDau.HasValue && x.NgayBatDau.Value.Month == month.Value);
        }

        if (year.HasValue)
        {
            query = query.Where(x => x.NgayBatDau.HasValue && x.NgayBatDau.Value.Year == year.Value);
        }

        if (tuNgay.HasValue)
        {
            query = query.Where(x => x.NgayBatDau.HasValue && x.NgayBatDau.Value.Date >= tuNgay.Value.Date);
        }

        if (denNgay.HasValue)
        {
            query = query.Where(x => x.NgayKetThuc.HasValue && x.NgayKetThuc.Value.Date <= denNgay.Value.Date);
        }

        var totalItems = await query.CountAsync();

        var pageRows = await query
            .OrderByDescending(x => x.NgayBatDau)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(x => new
            {
                x.MaDuAn,
                x.TenDuAn,
                x.MoTa,
                x.NgayBatDau,
                x.NgayKetThuc,
                x.TrangThai,
                TotalTasks = x.CongViecs.Count,
                DoneTasks = x.CongViecs.Count(cv => cv.MaTrangThai == TaskStatusCompleted),
                LateTasks = x.CongViecs.Count(cv => cv.MaTrangThai != TaskStatusCompleted && cv.HanHoanThanh.HasValue && cv.HanHoanThanh.Value.Date < DateTime.Now.Date)
            })
            .ToListAsync();

        var items = pageRows.Select(x =>
        {
            var percent = x.TotalTasks == 0 ? 0 : Math.Round((double)x.DoneTasks * 100 / x.TotalTasks, 2);
            return new DuAnListItemDto
            {
                MaDuAn = x.MaDuAn,
                TenDuAn = x.TenDuAn,
                MoTa = x.MoTa,
                NgayBatDau = x.NgayBatDau,
                NgayKetThuc = x.NgayKetThuc,
                TrangThai = x.TrangThai,
                TongCongViec = x.TotalTasks,
                CongViecHoanThanh = x.DoneTasks,
                CongViecTreHan = x.LateTasks,
                PhanTramHoanThanh = percent
            };
        }).ToList();

        var result = new PagedResult<DuAnListItemDto>
        {
            Items = items,
            Page = page,
            Size = size,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)size)
        };

        return Ok(ApiResponse<PagedResult<DuAnListItemDto>>.Ok(result));
    }

    [HttpGet("list")]
    public async Task<ActionResult<ApiResponse<List<DuAnListItemDto>>>> GetDuAnList()
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<List<DuAnListItemDto>>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        IQueryable<DuAn> query = _dbContext.DuAns.AsNoTracking().Where(x => (x.TrangThai ?? 0) != ProjectStatusDeleted);

        if (!actor.IsAdmin)
        {
            if (actor.IsManager)
            {
                var maPhongBan = actor.MaPhongBan;
                query = query.Where(x =>
                    (maPhongBan.HasValue && x.DuAnPhongBans.Any(pb => pb.MaPhongBan == maPhongBan && (pb.TrangThai ?? 1) == 1))
                    || x.DuAnNhanViens.Any(nv => nv.MaNhanVien == actor.MaNhanVien && (nv.TrangThai ?? 1) == 1));
            }
            else
            {
                // Employee scope: see projects from direct assignment, team membership, or department assignment
                var employeeTeamIds = _dbContext.ThanhVienNhoms
                    .AsNoTracking()
                    .Where(x => x.MaNhanVien == actor.MaNhanVien)
                    .Select(x => x.MaNhom);

                var employeePhongBanId = actor.MaPhongBan.GetValueOrDefault(0);

                query = query.Where(x => 
                    x.DuAnNhanViens.Any(nv => nv.MaNhanVien == actor.MaNhanVien && (nv.TrangThai ?? 1) == 1)
                    || (employeePhongBanId != 0 && x.DuAnPhongBans.Any(pb => pb.MaPhongBan == employeePhongBanId && (pb.TrangThai ?? 1) == 1))
                    || x.DuAnNhoms.Any(dn => (dn.TrangThai ?? 1) == 1 && employeeTeamIds.Contains(dn.MaNhom)));
            }
        }

        var items = await query
            .OrderBy(x => x.TenDuAn)
            .Select(x => new DuAnListItemDto
            {
                MaDuAn = x.MaDuAn,
                TenDuAn = x.TenDuAn
            })
            .ToListAsync();

        return Ok(ApiResponse<List<DuAnListItemDto>>.Ok(items));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<DuAnDetailDto>>> GetDuAnById(int id)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<DuAnDetailDto>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        if (!await CanAccessProjectAsync(actor, id))
        {
            return Forbid();
        }

        var detail = await BuildProjectDetailAsync(id);
        if (detail == null)
        {
            return NotFound(ApiResponse<DuAnDetailDto>.Fail("Không tìm thấy dự án."));
        }

        return Ok(ApiResponse<DuAnDetailDto>.Ok(detail));
    }

    [HttpGet("{id:int}/tasks")]
    public async Task<ActionResult<ApiResponse<List<ProjectTaskDto>>>> GetProjectTasks(int id)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<List<ProjectTaskDto>>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        if (!await CanAccessProjectAsync(actor, id))
        {
            return Forbid();
        }

        var taskRows = await _dbContext.CongViecs
            .AsNoTracking()
            .Where(x => x.MaDuAn == id)
            .OrderBy(x => x.HanHoanThanh)
            .Select(x => new
            {
                x.MaCongViec,
                x.TenCongViec,
                x.HanHoanThanh,
                x.MaTrangThai
            })
            .ToListAsync();

        var taskIds = taskRows.Select(x => x.MaCongViec).ToList();

        var assigneeRows = taskIds.Count == 0
            ? new List<ProjectTaskAssigneeRow>()
            : await _dbContext.PhanCongNhanViens
                .AsNoTracking()
                .Where(pc => taskIds.Contains(pc.MaCongViec) && (pc.TrangThai ?? 1) == 1)
                .Select(pc => new ProjectTaskAssigneeRow
                {
                    MaCongViec = pc.MaCongViec,
                    HoTen = pc.NhanVien != null && pc.NhanVien.HoTen != null ? pc.NhanVien.HoTen : string.Empty
                })
                .ToListAsync();

        var teamAssigneeRows = taskIds.Count == 0
            ? new List<ProjectTaskAssigneeRow>()
            : await (
                from pn in _dbContext.PhanCongNhoms.AsNoTracking()
                join tv in _dbContext.ThanhVienNhoms.AsNoTracking() on pn.MaNhom equals tv.MaNhom
                join nv in _dbContext.NhanViens.AsNoTracking() on tv.MaNhanVien equals nv.MaNhanVien
                where taskIds.Contains(pn.MaCongViec)
                      && (pn.TrangThai ?? 1) == 1
                      && !string.IsNullOrWhiteSpace(nv.HoTen)
                select new ProjectTaskAssigneeRow
                {
                    MaCongViec = pn.MaCongViec,
                    HoTen = nv.HoTen!
                }
            ).ToListAsync();

        if (teamAssigneeRows.Count > 0)
        {
            assigneeRows.AddRange(teamAssigneeRows);
        }

        var progressRows = taskIds.Count == 0
            ? new List<ProjectTaskProgressRow>()
            : await _dbContext.TienDoCongViecs
                .AsNoTracking()
                .Where(td => taskIds.Contains(td.MaCongViec))
                .Select(td => new ProjectTaskProgressRow
                {
                    MaCongViec = td.MaCongViec,
                    PhanTram = (double)(td.PhanTramHoanThanh ?? 0)
                })
                .ToListAsync();

        var assigneeMap = new Dictionary<int, List<string>>();
        foreach (var row in assigneeRows)
        {
            if (string.IsNullOrWhiteSpace(row.HoTen))
            {
                continue;
            }

            if (!assigneeMap.TryGetValue(row.MaCongViec, out var names))
            {
                names = new List<string>();
                assigneeMap[row.MaCongViec] = names;
            }

            if (!names.Contains(row.HoTen))
            {
                names.Add(row.HoTen);
            }
        }

        var progressMap = progressRows
            .GroupBy(x => x.MaCongViec)
            .ToDictionary(
                g => g.Key,
                g => Math.Round(g.Select(x => x.PhanTram).DefaultIfEmpty(0).Average(), 2));

        var result = taskRows.Select(x =>
        {
            var assignees = new List<string>();
            if (assigneeMap.TryGetValue(x.MaCongViec, out var foundAssignees) && foundAssignees != null)
            {
                assignees = foundAssignees;
            }

            return new ProjectTaskDto
            {
                MaCongViec = x.MaCongViec,
                TenCongViec = x.TenCongViec,
                HanHoanThanh = x.HanHoanThanh,
                MaTrangThai = x.MaTrangThai ?? 0,
                NguoiThucHien = assignees,
                PhanTramHoanThanh = progressMap.TryGetValue(x.MaCongViec, out var progress) ? progress : 0,
                LaTreHan = x.MaTrangThai != TaskStatusCompleted && x.HanHoanThanh.HasValue && x.HanHoanThanh.Value.Date < DateTime.Now.Date
            };
        }).ToList();

        return Ok(ApiResponse<List<ProjectTaskDto>>.Ok(result));
    }

    [HttpGet("{id:int}/tracking")]
    public async Task<ActionResult<ApiResponse<ProjectTrackingDto>>> GetProjectTracking(int id)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<ProjectTrackingDto>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        if (!await CanAccessProjectAsync(actor, id))
        {
            return Forbid();
        }

        var exists = await _dbContext.DuAns.AsNoTracking().AnyAsync(x => x.MaDuAn == id && (x.TrangThai ?? 0) != ProjectStatusDeleted);
        if (!exists)
        {
            return NotFound(ApiResponse<ProjectTrackingDto>.Fail("Không tìm thấy dự án."));
        }

        var taskRows = await _dbContext.CongViecs
            .AsNoTracking()
            .Where(x => x.MaDuAn == id)
            .Select(x => new
            {
                x.MaCongViec,
                x.MaTrangThai,
                x.HanHoanThanh
            })
            .ToListAsync();

        var total = taskRows.Count;
        var done = taskRows.Count(x => x.MaTrangThai == TaskStatusCompleted);
        var late = taskRows.Count(x => x.MaTrangThai != TaskStatusCompleted && x.HanHoanThanh.HasValue && x.HanHoanThanh.Value.Date < DateTime.Now.Date);
        var completionPercent = total == 0 ? 0 : Math.Round((double)done * 100 / total, 2);

        var mucDo = completionPercent switch
        {
            <= 30 => "Chậm",
            <= 70 => "Đang tiến triển",
            _ => "Tốt"
        };

        var memberIds = await _dbContext.DuAnNhanViens
            .AsNoTracking()
            .Where(x => x.MaDuAn == id && (x.TrangThai ?? 1) == 1)
            .Select(x => x.MaNhanVien)
            .Distinct()
            .ToListAsync();

        var aiRows = await _dbContext.DuDoanAis
            .AsNoTracking()
            .Where(x => memberIds.Contains(x.MaNhanVien) && x.XacSuatTreHan.HasValue)
            .OrderByDescending(x => x.ThoiGianDuDoan)
            .Select(x => new
            {
                x.MaNhanVien,
                XacSuat = (double)(x.XacSuatTreHan ?? 0),
                x.ThoiGianDuDoan
            })
            .ToListAsync();

        var latestAiByEmployee = aiRows
            .GroupBy(x => x.MaNhanVien)
            .Select(g => g.First())
            .ToList();

        var avgProbability = latestAiByEmployee.Count == 0
            ? 0
            : Math.Round(latestAiByEmployee.Average(x => x.XacSuat) * 100, 2);

        var highRisk = latestAiByEmployee.Count(x => x.XacSuat >= 0.7);
        var warningLevel = avgProbability switch
        {
            < 30 => "Thấp",
            < 60 => "Trung bình",
            _ => "Cao"
        };

        var tracking = new ProjectTrackingDto
        {
            TongCongViec = total,
            CongViecHoanThanh = done,
            CongViecTreHan = late,
            PhanTramHoanThanh = completionPercent,
            PhanLoaiTienDo = mucDo,
            AiCanhBao = new ProjectAiWarningDto
            {
                TyLeRuiRoTreHan = avgProbability,
                SoNhanVienRuiRoCao = highRisk,
                MucCanhBao = warningLevel,
                NgayTreDuKien = late > 0
                    ? DateTime.Now.Date.AddDays(1)
                    : (avgProbability >= 60 ? DateTime.Now.Date.AddDays(3) : null)
            }
        };

        return Ok(ApiResponse<ProjectTrackingDto>.Ok(tracking));
    }

    [HttpPost("{id:int}/nhanvien")]
    [Authorize(Policy = Permissions.ProjectsEdit)]
    public async Task<ActionResult<ApiResponse<object>>> AssignEmployeeToProject(int id, [FromBody] AssignEmployeeRequest request)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        if (!await CanAccessProjectAsync(actor, id))
        {
            return Forbid();
        }

        var duAn = await _dbContext.DuAns.FirstOrDefaultAsync(x => x.MaDuAn == id && (x.TrangThai ?? 0) != ProjectStatusDeleted);
        if (duAn == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy dự án."));
        }

        var nhanVien = await _dbContext.NhanViens.FirstOrDefaultAsync(x => x.MaNhanVien == request.MaNhanVien && x.TrangThai == 1);
        if (nhanVien == null)
        {
            return BadRequest(ApiResponse<object>.Fail("Nhân viên không tồn tại hoặc đã ngừng hoạt động."));
        }

        var existing = await _dbContext.DuAnNhanViens
            .FirstOrDefaultAsync(x => x.MaDuAn == id && x.MaNhanVien == request.MaNhanVien);

        if (existing == null)
        {
            _dbContext.DuAnNhanViens.Add(new DuAnNhanVien
            {
                MaDuAn = id,
                MaNhanVien = request.MaNhanVien,
                VaiTro = string.IsNullOrWhiteSpace(request.VaiTro) ? "Member" : request.VaiTro.Trim(),
                NgayThamGia = DateTime.Now,
                TrangThai = 1
            });
        }
        else
        {
            existing.TrangThai = 1;
            existing.NgayRoi = null;
            existing.NgayThamGia ??= DateTime.Now;
            existing.VaiTro = string.IsNullOrWhiteSpace(request.VaiTro) ? existing.VaiTro : request.VaiTro.Trim();
        }

        await _dbContext.SaveChangesAsync();
        await _auditLogService.LogByUserIdAsync(actor.UserId, $"Gán nhân viên {nhanVien.HoTen} vào dự án {duAn.TenDuAn}");

        try
        {
            await CreateProjectNotificationAsync("Dự án", $"Bạn được thêm vào dự án mới: \"{duAn.TenDuAn}\".", new[] { nhanVien.MaNhanVien });

            var managerIds = await (
                from nv in _dbContext.NhanViens.AsNoTracking()
                join ur in _dbContext.UserRoles.AsNoTracking() on nv.AspNetUserId equals ur.UserId
                join r in _dbContext.Roles.AsNoTracking() on ur.RoleId equals r.Id
                where r.Name == Roles.Manager
                where nv.MaPhongBan.HasValue
                   && _dbContext.DuAnPhongBans.Any(dp => dp.MaDuAn == id && dp.MaPhongBan == nv.MaPhongBan && (dp.TrangThai ?? 1) == 1)
                select nv.MaNhanVien
            ).Distinct().ToListAsync();

            if (managerIds.Count > 0)
            {
                await CreateProjectNotificationAsync("Dự án", $"Nhân viên {nhanVien.HoTen} vừa được gán vào dự án \"{duAn.TenDuAn}\".", managerIds);
            }
        }
        catch
        {
            // keep assignment flow unaffected by notification errors
        }

        return Ok(ApiResponse<object>.Ok(null, "Gán nhân sự thành công."));
    }

    private async Task CreateProjectNotificationAsync(string loai, string noiDung, IEnumerable<int> maNhanVienIds)
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

        _dbContext.ThongBaoNhanViens.AddRange(ids.Select(id => new ThongBaoNhanVien
        {
            MaThongBao = tb.MaThongBao,
            MaNhanVien = id,
            DaDoc = false
        }));
        await _dbContext.SaveChangesAsync();
    }

    private async Task NotifyAdminsForProjectCompletionApprovalAsync(int maDuAn, string tenDuAn, int maQuanLy)
    {
        var adminIds = await (
            from nv in _dbContext.NhanViens.AsNoTracking()
            join ur in _dbContext.UserRoles.AsNoTracking() on nv.AspNetUserId equals ur.UserId
            join r in _dbContext.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where r.Name == Roles.Admin
            select nv.MaNhanVien
        ).Distinct().ToListAsync();

        if (adminIds.Count == 0)
        {
            return;
        }

        var managerName = await _dbContext.NhanViens.AsNoTracking()
            .Where(x => x.MaNhanVien == maQuanLy)
            .Select(x => x.HoTen)
            .FirstOrDefaultAsync();

        var requester = string.IsNullOrWhiteSpace(managerName) ? $"QL #{maQuanLy}" : managerName;
        await CreateProjectNotificationAsync(
            "Dự án",
            $"Quản lý {requester} đã đề xuất xác nhận hoàn thành dự án \"{tenDuAn}\" và đang chờ Admin duyệt.",
            adminIds);
    }

    private async Task NotifyManagersByProjectAsync(int maDuAn, string message)
    {
        var managerIds = await (
            from nv in _dbContext.NhanViens.AsNoTracking()
            join ur in _dbContext.UserRoles.AsNoTracking() on nv.AspNetUserId equals ur.UserId
            join r in _dbContext.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where r.Name == Roles.Manager
            where nv.MaPhongBan.HasValue
               && _dbContext.DuAnPhongBans.Any(dp => dp.MaDuAn == maDuAn && dp.MaPhongBan == nv.MaPhongBan && (dp.TrangThai ?? 1) == 1)
            select nv.MaNhanVien
        ).Distinct().ToListAsync();

        if (managerIds.Count == 0)
        {
            return;
        }

        await CreateProjectNotificationAsync("Dự án", message, managerIds);
    }

    [HttpGet("{id:int}/nhanvien")]
    public async Task<ActionResult<ApiResponse<List<DuAnNhanVienDto>>>> GetProjectEmployees(int id)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<List<DuAnNhanVienDto>>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        if (!await CanAccessProjectAsync(actor, id))
        {
            return Forbid();
        }

        var employees = await _dbContext.DuAnNhanViens
            .AsNoTracking()
            .Where(x => x.MaDuAn == id && (x.TrangThai ?? 1) == 1)
            .OrderBy(x => x.NhanVien.HoTen)
            .Select(x => new DuAnNhanVienDto
            {
                MaNhanVien = x.MaNhanVien,
                HoTen = x.NhanVien.HoTen,
                VaiTro = x.VaiTro,
                NgayThamGia = x.NgayThamGia
            })
            .ToListAsync();

        return Ok(ApiResponse<List<DuAnNhanVienDto>>.Ok(employees));
    }

    [HttpDelete("{id:int}/nhanvien/{maNhanVien:int}")]
    [Authorize(Policy = Permissions.ProjectsEdit)]
    public async Task<ActionResult<ApiResponse<object>>> RemoveEmployeeFromProject(int id, int maNhanVien)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        if (!await CanAccessProjectAsync(actor, id))
        {
            return Forbid();
        }

        var item = await _dbContext.DuAnNhanViens
            .FirstOrDefaultAsync(x => x.MaDuAn == id && x.MaNhanVien == maNhanVien && (x.TrangThai ?? 1) == 1);

        if (item == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy bản ghi gán nhân sự."));
        }

        item.TrangThai = 0;
        item.NgayRoi = DateTime.Now;

        await _dbContext.SaveChangesAsync();
        await _auditLogService.LogByUserIdAsync(actor.UserId, $"Hủy gán nhân viên {maNhanVien} khỏi dự án {id}");

        return Ok(ApiResponse<object>.Ok(null, "Hủy gán nhân sự thành công."));
    }

    [HttpPost("{id:int}/nhom")]
    [Authorize(Policy = Permissions.ProjectsEdit)]
    public async Task<ActionResult<ApiResponse<object>>> AssignTeamToProject(int id, [FromBody] AssignTeamRequest request)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        if (!await CanAccessProjectAsync(actor, id))
        {
            return Forbid();
        }

        var team = await _dbContext.Nhoms.FirstOrDefaultAsync(x => x.MaNhom == request.MaNhom);
        if (team == null)
        {
            return BadRequest(ApiResponse<object>.Fail("Không tìm thấy nhóm."));
        }

        var existing = await _dbContext.DuAnNhoms.FirstOrDefaultAsync(x => x.MaDuAn == id && x.MaNhom == request.MaNhom);
        if (existing == null)
        {
            _dbContext.DuAnNhoms.Add(new DuAnNhom
            {
                MaDuAn = id,
                MaNhom = request.MaNhom,
                NgayThamGia = DateTime.Now,
                TrangThai = 1
            });
        }
        else
        {
            existing.TrangThai = 1;
            existing.NgayThamGia ??= DateTime.Now;
        }

        if (request.TuDongThemThanhVienNhom)
        {
            var memberIds = await _dbContext.ThanhVienNhoms
                .AsNoTracking()
                .Where(x => x.MaNhom == request.MaNhom)
                .Select(x => x.MaNhanVien)
                .Distinct()
                .ToListAsync();

            foreach (var memberId in memberIds)
            {
                var memberProject = await _dbContext.DuAnNhanViens
                    .FirstOrDefaultAsync(x => x.MaDuAn == id && x.MaNhanVien == memberId);

                if (memberProject == null)
                {
                    _dbContext.DuAnNhanViens.Add(new DuAnNhanVien
                    {
                        MaDuAn = id,
                        MaNhanVien = memberId,
                        VaiTro = "Member",
                        NgayThamGia = DateTime.Now,
                        TrangThai = 1
                    });
                }
                else if ((memberProject.TrangThai ?? 1) != 1)
                {
                    memberProject.TrangThai = 1;
                    memberProject.NgayRoi = null;
                    memberProject.NgayThamGia ??= DateTime.Now;
                }
            }
        }

        await _dbContext.SaveChangesAsync();
        await _auditLogService.LogByUserIdAsync(actor.UserId, $"Gán nhóm {team.TenNhom} vào dự án {id}");

        return Ok(ApiResponse<object>.Ok(null, "Gán nhóm thành công."));
    }

    [HttpGet("{id:int}/nhom")]
    public async Task<ActionResult<ApiResponse<List<DuAnNhomDto>>>> GetProjectTeams(int id)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<List<DuAnNhomDto>>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        if (!await CanAccessProjectAsync(actor, id))
        {
            return Forbid();
        }

        var teams = await _dbContext.DuAnNhoms
            .AsNoTracking()
            .Where(x => x.MaDuAn == id && (x.TrangThai ?? 1) == 1)
            .OrderBy(x => x.Nhom.TenNhom)
            .Select(x => new DuAnNhomDto
            {
                MaNhom = x.MaNhom,
                TenNhom = x.Nhom.TenNhom,
                NgayThamGia = x.NgayThamGia
            })
            .ToListAsync();

        return Ok(ApiResponse<List<DuAnNhomDto>>.Ok(teams));
    }

    [HttpDelete("{id:int}/nhom/{maNhom:int}")]
    [Authorize(Policy = Permissions.ProjectsEdit)]
    public async Task<ActionResult<ApiResponse<object>>> RemoveTeamFromProject(int id, int maNhom)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        if (!await CanAccessProjectAsync(actor, id))
        {
            return Forbid();
        }

        var item = await _dbContext.DuAnNhoms
            .FirstOrDefaultAsync(x => x.MaDuAn == id && x.MaNhom == maNhom && (x.TrangThai ?? 1) == 1);

        if (item == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy bản ghi gán nhóm."));
        }

        item.TrangThai = 0;

        await _dbContext.SaveChangesAsync();
        await _auditLogService.LogByUserIdAsync(actor.UserId, $"Hủy gán nhóm {maNhom} khỏi dự án {id}");

        return Ok(ApiResponse<object>.Ok(null, "Hủy gán nhóm thành công."));
    }

    [HttpPost("{id:int}/phongban")]
    [Authorize(Policy = Permissions.ProjectsEdit)]
    public async Task<ActionResult<ApiResponse<object>>> AssignDepartmentToProject(int id, [FromBody] AssignDepartmentRequest request)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        if (!await CanAccessProjectAsync(actor, id))
        {
            return Forbid();
        }

        if (actor.IsManager && actor.MaPhongBan != request.MaPhongBan)
        {
            return Forbid();
        }

        var department = await _dbContext.PhongBans.FirstOrDefaultAsync(x => x.MaPhongBan == request.MaPhongBan);
        if (department == null)
        {
            return BadRequest(ApiResponse<object>.Fail("Không tìm thấy phòng ban."));
        }

        await EnsureDepartmentLinkAsync(id, request.MaPhongBan);
        await _dbContext.SaveChangesAsync();

        await _auditLogService.LogByUserIdAsync(actor.UserId, $"Gán phòng ban {department.TenPhongBan} vào dự án {id}");

        return Ok(ApiResponse<object>.Ok(null, "Gán phòng ban thành công."));
    }

    [HttpGet("{id:int}/phongban")]
    public async Task<ActionResult<ApiResponse<List<DuAnPhongBanDto>>>> GetProjectDepartments(int id)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<List<DuAnPhongBanDto>>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        if (!await CanAccessProjectAsync(actor, id))
        {
            return Forbid();
        }

        var departments = await _dbContext.DuAnPhongBans
            .AsNoTracking()
            .Where(x => x.MaDuAn == id && (x.TrangThai ?? 1) == 1)
            .OrderBy(x => x.PhongBan.TenPhongBan)
            .Select(x => new DuAnPhongBanDto
            {
                MaPhongBan = x.MaPhongBan,
                TenPhongBan = x.PhongBan.TenPhongBan,
                NgayThamGia = x.NgayThamGia
            })
            .ToListAsync();

        return Ok(ApiResponse<List<DuAnPhongBanDto>>.Ok(departments));
    }

    [HttpDelete("{id:int}/phongban/{maPhongBan:int}")]
    [Authorize(Policy = Permissions.ProjectsEdit)]
    public async Task<ActionResult<ApiResponse<object>>> RemoveDepartmentFromProject(int id, int maPhongBan)
    {
        var actor = await GetActorContextAsync();
        if (actor == null)
        {
            return Unauthorized(ApiResponse<object>.Fail("Tài khoản đăng nhập chưa được liên kết nhân viên."));
        }

        if (!await CanAccessProjectAsync(actor, id))
        {
            return Forbid();
        }

        var item = await _dbContext.DuAnPhongBans
            .FirstOrDefaultAsync(x => x.MaDuAn == id && x.MaPhongBan == maPhongBan && (x.TrangThai ?? 1) == 1);

        if (item == null)
        {
            return NotFound(ApiResponse<object>.Fail("Không tìm thấy bản ghi gán phòng ban."));
        }

        item.TrangThai = 0;
        await _dbContext.SaveChangesAsync();

        await _auditLogService.LogByUserIdAsync(actor.UserId, $"Hủy gán phòng ban {maPhongBan} khỏi dự án {id}");

        return Ok(ApiResponse<object>.Ok(null, "Hủy gán phòng ban thành công."));
    }

    private async Task<DuAnDetailDto?> BuildProjectDetailAsync(int maDuAn)
    {
        var duAn = await _dbContext.DuAns
            .AsNoTracking()
            .Where(x => x.MaDuAn == maDuAn && (x.TrangThai ?? 0) != ProjectStatusDeleted)
            .Select(x => new
            {
                x.MaDuAn,
                x.TenDuAn,
                x.MoTa,
                x.NgayBatDau,
                x.NgayKetThuc,
                x.TrangThai,
                TotalTasks = x.CongViecs.Count,
                DoneTasks = x.CongViecs.Count(cv => cv.MaTrangThai == TaskStatusCompleted),
                LateTasks = x.CongViecs.Count(cv => cv.MaTrangThai != TaskStatusCompleted && cv.HanHoanThanh.HasValue && cv.HanHoanThanh.Value.Date < DateTime.Now.Date),
                MemberCount = x.DuAnNhanViens.Count(nv => (nv.TrangThai ?? 1) == 1),
                TeamCount = x.DuAnNhoms.Count(nh => (nh.TrangThai ?? 1) == 1),
                DepartmentCount = x.DuAnPhongBans.Count(pb => (pb.TrangThai ?? 1) == 1),
                KpiCount = x.KpiDuAns.Count(k => (k.TrangThai ?? 1) == 1)
            })
            .FirstOrDefaultAsync();

        if (duAn == null)
        {
            return null;
        }

        var percent = duAn.TotalTasks == 0 ? 0 : Math.Round((double)duAn.DoneTasks * 100 / duAn.TotalTasks, 2);

        return new DuAnDetailDto
        {
            MaDuAn = duAn.MaDuAn,
            TenDuAn = duAn.TenDuAn,
            MoTa = duAn.MoTa,
            NgayBatDau = duAn.NgayBatDau,
            NgayKetThuc = duAn.NgayKetThuc,
            TrangThai = duAn.TrangThai,
            TongCongViec = duAn.TotalTasks,
            CongViecHoanThanh = duAn.DoneTasks,
            CongViecTreHan = duAn.LateTasks,
            SoNhanSu = duAn.MemberCount,
            SoNhom = duAn.TeamCount,
            SoPhongBan = duAn.DepartmentCount,
            SoKpiLienKet = duAn.KpiCount,
            PhanTramHoanThanh = percent
        };
    }

    public class UpsertDuAnRequest
    {
        public string? TenDuAn { get; set; }
        public string? MoTa { get; set; }
        public DateTime? NgayBatDau { get; set; }
        public DateTime? NgayKetThuc { get; set; }
        public int? TrangThai { get; set; }
        public int? MaPhongBan { get; set; }
    }

    public class AssignEmployeeRequest
    {
        public int MaNhanVien { get; set; }
        public string? VaiTro { get; set; }
    }

    public class AssignTeamRequest
    {
        public int MaNhom { get; set; }
        public bool TuDongThemThanhVienNhom { get; set; } = true;
    }

    public class AssignDepartmentRequest
    {
        public int MaPhongBan { get; set; }
    }

    public class DuAnListItemDto
    {
        public int MaDuAn { get; set; }
        public string? TenDuAn { get; set; }
        public string? MoTa { get; set; }
        public DateTime? NgayBatDau { get; set; }
        public DateTime? NgayKetThuc { get; set; }
        public int? TrangThai { get; set; }
        public int TongCongViec { get; set; }
        public int CongViecHoanThanh { get; set; }
        public int CongViecTreHan { get; set; }
        public double PhanTramHoanThanh { get; set; }
    }

    public class DuAnDetailDto
    {
        public int MaDuAn { get; set; }
        public string? TenDuAn { get; set; }
        public string? MoTa { get; set; }
        public DateTime? NgayBatDau { get; set; }
        public DateTime? NgayKetThuc { get; set; }
        public int? TrangThai { get; set; }
        public int TongCongViec { get; set; }
        public int CongViecHoanThanh { get; set; }
        public int CongViecTreHan { get; set; }
        public int SoNhanSu { get; set; }
        public int SoNhom { get; set; }
        public int SoPhongBan { get; set; }
        public int SoKpiLienKet { get; set; }
        public double PhanTramHoanThanh { get; set; }
    }

    public class DuAnNhanVienDto
    {
        public int MaNhanVien { get; set; }
        public string? HoTen { get; set; }
        public string? VaiTro { get; set; }
        public DateTime? NgayThamGia { get; set; }
    }

    public class DuAnNhomDto
    {
        public int MaNhom { get; set; }
        public string? TenNhom { get; set; }
        public DateTime? NgayThamGia { get; set; }
    }

    public class DuAnPhongBanDto
    {
        public int MaPhongBan { get; set; }
        public string? TenPhongBan { get; set; }
        public DateTime? NgayThamGia { get; set; }
    }

    public class ProjectTaskDto
    {
        public int MaCongViec { get; set; }
        public string? TenCongViec { get; set; }
        public DateTime? HanHoanThanh { get; set; }
        public int MaTrangThai { get; set; }
        public List<string?> NguoiThucHien { get; set; } = new();
        public double PhanTramHoanThanh { get; set; }
        public bool LaTreHan { get; set; }
    }

    public class ProjectTrackingDto
    {
        public int TongCongViec { get; set; }
        public int CongViecHoanThanh { get; set; }
        public int CongViecTreHan { get; set; }
        public double PhanTramHoanThanh { get; set; }
        public string PhanLoaiTienDo { get; set; } = "Chậm";
        public ProjectAiWarningDto AiCanhBao { get; set; } = new();
    }

    public class ProjectAiWarningDto
    {
        public double TyLeRuiRoTreHan { get; set; }
        public int SoNhanVienRuiRoCao { get; set; }
        public string MucCanhBao { get; set; } = "Thấp";
        public DateTime? NgayTreDuKien { get; set; }
    }

    public class ProjectCompletionApprovalItemDto
    {
        public int MaDuAn { get; set; }
        public string TenDuAn { get; set; } = string.Empty;
        public string? MoTa { get; set; }
        public DateTime? NgayBatDau { get; set; }
        public DateTime? NgayKetThuc { get; set; }
        public int TongCongViec { get; set; }
        public int CongViecHoanThanh { get; set; }
    }

    public class RejectProjectCompletionRequest
    {
        public string? LyDoTuChoi { get; set; }
    }
}
