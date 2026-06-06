using LuanVan.Contracts;
using LuanVan.Data;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;
using System.Text;
using System.IO;
using ClosedXML.Excel;

namespace LuanVan.Controllers.Api;

[ApiController]
[Authorize]
[Route("dashboard")]
public class DashboardController : ControllerBase
{
    private const int ProjectStatusInProgress = 1;
    private const int TaskStatusNotStarted = 1;
    private const int TaskStatusDoing = 2;
    private const int TaskStatusCompleted = 3;
    private const int TaskStatusOverdue = 4;

    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private string? _resolvedConnectionString;

    public DashboardController(AppDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    private string? ResolveConnectionString()
    {
        if (!string.IsNullOrWhiteSpace(_resolvedConnectionString))
        {
            return _resolvedConnectionString;
        }

        var configured = _configuration.GetConnectionString("DefaultConnection")
            ?? _configuration["ConnectionStrings:DefaultConnection"]
            ?? _configuration["ConnectionStrings__DefaultConnection"];

        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }

        try
        {
            var builder = new SqlConnectionStringBuilder(configured);
            if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
            {
                builder.InitialCatalog = "LV2026";
            }

            _resolvedConnectionString = builder.ConnectionString;
        }
        catch
        {
            _resolvedConnectionString = configured;
        }

        return _resolvedConnectionString;
    }

    private void EnsureDbConnectionStringInitialized()
    {
        var resolved = ResolveConnectionString();
        if (string.IsNullOrWhiteSpace(resolved))
        {
            return;
        }

        var connection = _dbContext.Database.GetDbConnection();
        if (!string.IsNullOrWhiteSpace(connection.ConnectionString))
        {
            return;
        }

        _dbContext.Database.SetConnectionString(resolved);
    }

    [HttpGet]
    [Authorize(Policy = "DashboardView")]
    public async Task<ActionResult<ApiResponse<DashboardResponseDto>>> GetDashboard(
        [FromQuery] int? thang,
        [FromQuery] int? nam,
        [FromQuery] int? maKpi,
        [FromQuery] int? maPhongBan,
        [FromQuery] int? maNhom,
        [FromQuery] int? maDuAn,
        [FromQuery] int? top = 5)
    {
        try
        {
            EnsureDbConnectionStringInitialized();

            var selectedMonth = thang is >= 1 and <= 12 ? thang.Value : (int?)null;
            var allMonths = !selectedMonth.HasValue;
            var month = selectedMonth ?? DateTime.Now.Month;
            var year = nam > 0 ? nam.Value : DateTime.Now.Year;
            var topCount = Math.Clamp(top ?? 5, 1, 20);
            var today = DateTime.Now.Date;
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var actor = !string.IsNullOrWhiteSpace(currentUserId)
            ? await _dbContext.NhanViens.AsNoTracking()
                .Where(x => x.AspNetUserId == currentUserId)
                .Select(x => new { x.MaNhanVien, x.HoTen, x.MaPhongBan })
                .FirstOrDefaultAsync()
            : null;

        if (actor == null)
        {
            return Unauthorized(ApiResponse<DashboardResponseDto>.Fail("Không xác định được thông tin nhân viên hiện tại."));
        }

        var isAdmin = User.IsInRole(Roles.Admin);
        var isManagerRole = User.IsInRole(Roles.Manager);
        var isEmployeeDashboard = User.IsInRole(Roles.Employee) && !isAdmin && !isManagerRole;

        var managedDepartmentIds = await _dbContext.PhongBans.AsNoTracking()
            .Where(x => x.MaTruongPhong == actor.MaNhanVien)
            .Select(x => x.MaPhongBan)
            .ToListAsync();
        var ledTeamIds = await _dbContext.Nhoms.AsNoTracking()
            .Where(x => x.TruongNhom == actor.MaNhanVien)
            .Select(x => x.MaNhom)
            .ToListAsync();
        var memberTeamIds = await _dbContext.ThanhVienNhoms.AsNoTracking()
            .Where(x => x.MaNhanVien == actor.MaNhanVien)
            .Select(x => x.MaNhom)
            .ToListAsync();
        var visibleTeamIds = ledTeamIds.Concat(memberTeamIds).Distinct().ToList();

        var managerType = managedDepartmentIds.Count > 0
            ? "department_manager"
            : (visibleTeamIds.Count > 0 ? "team_lead" : (isEmployeeDashboard ? "employee" : "manager"));

        var selectedDepartmentId = maPhongBan is > 0 ? maPhongBan.Value : (int?)null;
        var selectedTeamId = maNhom is > 0 ? maNhom.Value : (int?)null;
        var selectedProjectId = maDuAn is > 0 ? maDuAn.Value : (int?)null;

        if (!isAdmin)
        {
            if (isManagerRole)
            {
                if (selectedDepartmentId.HasValue && !managedDepartmentIds.Contains(selectedDepartmentId.Value))
                {
                    return Forbid();
                }

                if (selectedTeamId.HasValue && !visibleTeamIds.Contains(selectedTeamId.Value))
                {
                    return Forbid();
                }
            }
            else if (isEmployeeDashboard)
            {
                selectedDepartmentId = actor.MaPhongBan;
            }
        }

        var employeesQuery = _dbContext.NhanViens.AsNoTracking().Where(x => x.TrangThai == 1);
        var managerScopedEmployeeIds = new List<int>();
        if (!isAdmin && isManagerRole)
        {
            var departmentEmployeeIds = managedDepartmentIds.Count == 0
                ? new List<int>()
                : await _dbContext.NhanViens.AsNoTracking()
                    .Where(x => x.TrangThai == 1 && x.MaPhongBan.HasValue && managedDepartmentIds.Contains(x.MaPhongBan.Value))
                    .Select(x => x.MaNhanVien)
                    .ToListAsync();

            var visibleTeamMemberIds = visibleTeamIds.Count == 0
                ? new List<int>()
                : await _dbContext.ThanhVienNhoms.AsNoTracking()
                    .Where(x => visibleTeamIds.Contains(x.MaNhom))
                    .Select(x => x.MaNhanVien)
                    .Distinct()
                    .ToListAsync();

            managerScopedEmployeeIds = departmentEmployeeIds
                .Concat(visibleTeamMemberIds)
                .Append(actor.MaNhanVien)
                .Distinct()
                .ToList();

            employeesQuery = employeesQuery.Where(x => managerScopedEmployeeIds.Contains(x.MaNhanVien));
        }

        if (selectedDepartmentId.HasValue)
        {
            employeesQuery = employeesQuery.Where(x => x.MaPhongBan == selectedDepartmentId.Value);
        }

        var teamMemberIds = new List<int>();
        if (selectedTeamId.HasValue)
        {
            teamMemberIds = await _dbContext.ThanhVienNhoms.AsNoTracking()
                .Where(x => x.MaNhom == selectedTeamId.Value)
                .Select(x => x.MaNhanVien)
                .Distinct()
                .ToListAsync();
            if (teamMemberIds.Count > 0)
            {
                employeesQuery = employeesQuery.Where(x => teamMemberIds.Contains(x.MaNhanVien));
            }
        }

        if (isEmployeeDashboard)
        {
            employeesQuery = employeesQuery.Where(x => x.MaNhanVien == actor.MaNhanVien);
        }

        var employees = await employeesQuery
            .Select(x => new { x.MaNhanVien, x.HoTen, x.MaPhongBan })
            .ToListAsync();
        var employeeIds = employees.Select(x => x.MaNhanVien).ToList();

        var projectDepartmentIds = selectedDepartmentId.HasValue
            ? new List<int> { selectedDepartmentId.Value }
            : (!isAdmin && isManagerRole ? managedDepartmentIds : new List<int>());
        var projectTeamIds = selectedTeamId.HasValue
            ? new List<int> { selectedTeamId.Value }
            : (!isAdmin && isManagerRole ? visibleTeamIds : new List<int>());

        var departmentProjectIds = projectDepartmentIds.Count > 0
            ? await _dbContext.DuAnPhongBans.AsNoTracking().Where(x => projectDepartmentIds.Contains(x.MaPhongBan) && (x.TrangThai ?? 1) == 1).Select(x => x.MaDuAn).Distinct().ToListAsync()
            : new List<int>();
        var teamProjectIds = projectTeamIds.Count > 0
            ? await _dbContext.DuAnNhoms.AsNoTracking().Where(x => projectTeamIds.Contains(x.MaNhom) && (x.TrangThai ?? 1) == 1).Select(x => x.MaDuAn).Distinct().ToListAsync()
            : new List<int>();
        var employeeProjectIds = employeeIds.Count > 0
            ? await _dbContext.DuAnNhanViens.AsNoTracking().Where(x => employeeIds.Contains(x.MaNhanVien) && (x.TrangThai ?? 1) == 1).Select(x => x.MaDuAn).Distinct().ToListAsync()
            : new List<int>();

        var scopedProjectIds = departmentProjectIds.Concat(teamProjectIds).Concat(employeeProjectIds).Distinct().ToList();
        if (selectedProjectId.HasValue)
        {
            if (!isAdmin && isManagerRole && !scopedProjectIds.Contains(selectedProjectId.Value))
            {
                return Forbid();
            }

            scopedProjectIds = scopedProjectIds.Where(x => x == selectedProjectId.Value).ToList();
        }

        var taskByEmployeeIds = employeeIds.Count > 0
            ? await _dbContext.PhanCongNhanViens.AsNoTracking()
                .Where(x => employeeIds.Contains(x.MaNhanVien))
                .Select(x => x.MaCongViec)
                .Distinct()
                .ToListAsync()
            : new List<int>();
        var taskByTeamIds = projectTeamIds.Count > 0
            ? await _dbContext.PhanCongNhoms.AsNoTracking().Where(x => projectTeamIds.Contains(x.MaNhom) && (x.TrangThai ?? 1) == 1).Select(x => x.MaCongViec).Distinct().ToListAsync()
            : new List<int>();
        var taskByDepartmentIds = projectDepartmentIds.Count > 0
            ? await _dbContext.PhanCongPhongBans.AsNoTracking().Where(x => projectDepartmentIds.Contains(x.MaPhongBan) && (x.TrangThai ?? 1) == 1).Select(x => x.MaCongViec).Distinct().ToListAsync()
            : new List<int>();
        var taskByProjectIds = scopedProjectIds.Count > 0
            ? await _dbContext.CongViecs.AsNoTracking().Where(x => scopedProjectIds.Contains(x.MaDuAn) && (x.DaXoa ?? false) == false).Select(x => x.MaCongViec).Distinct().ToListAsync()
            : new List<int>();

        var scopedTaskIds = taskByEmployeeIds.Concat(taskByTeamIds).Concat(taskByDepartmentIds).Concat(taskByProjectIds).Distinct().ToList();
        var taskRows = scopedTaskIds.Count == 0
            ? new List<TaskRow>()
            : await _dbContext.CongViecs.AsNoTracking()
                .Where(x => scopedTaskIds.Contains(x.MaCongViec) && (x.DaXoa ?? false) == false)
                .Select(x => new TaskRow
                {
                    MaCongViec = x.MaCongViec,
                    TenCongViec = x.TenCongViec ?? $"Task {x.MaCongViec}",
                    MaDuAn = x.MaDuAn,
                    HanHoanThanh = x.HanHoanThanh,
                    MaTrangThai = x.MaTrangThai ?? 0,
                    PhanTramHoanThanh = x.PhanTramHoanThanh ?? 0m
                })
                .ToListAsync();

        if (selectedProjectId.HasValue)
        {
            taskRows = taskRows.Where(x => x.MaDuAn == selectedProjectId.Value).ToList();
        }

        var scopedTaskIdsFinal = taskRows.Select(x => x.MaCongViec).ToList();
        var projectIdsFromTask = taskRows.Select(x => x.MaDuAn).Distinct().ToList();

        var projectNames = projectIdsFromTask.Count == 0
            ? new Dictionary<int, string>()
            : await _dbContext.DuAns.AsNoTracking()
                .Where(x => projectIdsFromTask.Contains(x.MaDuAn))
                .ToDictionaryAsync(x => x.MaDuAn, x => x.TenDuAn ?? $"Dự án {x.MaDuAn}");

        var kpiTongRows = await _dbContext.KetQuaKpiTongs.AsNoTracking()
            .Where(x => (allMonths || x.Thang == month)
                        && x.Nam == year
                        && employeeIds.Contains(x.MaNhanVien))
            .Select(x => new { x.MaNhanVien, Score = x.DiemTong })
            .ToListAsync();

        var kpiByNhanVien = kpiTongRows
            .GroupBy(x => x.MaNhanVien)
            .ToDictionary(g => g.Key, g => g.Average(x => x.Score));

        // Fallback: nếu chưa có bản tổng hợp tháng, dùng trung bình từ KETQUAKPI.
        if (kpiByNhanVien.Count == 0)
        {
            var kpiFallbackQuery = _dbContext.KetQuaKpis.AsNoTracking()
                .Where(x => (allMonths || x.thang == month)
                            && x.nam == year
                            && x.DiemSo.HasValue
                            && employeeIds.Contains(x.MaNhanVien));
            if (maKpi.HasValue)
            {
                kpiFallbackQuery = kpiFallbackQuery.Where(x => x.MaKpi == maKpi.Value);
            }

            kpiByNhanVien = await kpiFallbackQuery
                .GroupBy(x => x.MaNhanVien)
                .Select(g => new { MaNhanVien = g.Key, Score = g.Average(v => v.DiemSo ?? 0m) })
                .ToDictionaryAsync(x => x.MaNhanVien, x => x.Score);
        }

        var kpiCurrent = kpiByNhanVien.Count == 0 ? 0m : kpiByNhanVien.Values.Average();
        var hasCurrentKpiData = kpiByNhanVien.Count > 0;

        var prevMonth = allMonths ? 0 : (month == 1 ? 12 : month - 1);
        var prevYear = allMonths ? year - 1 : (month == 1 ? year - 1 : year);
        var prevKpiTongRows = await _dbContext.KetQuaKpiTongs.AsNoTracking()
            .Where(x => (allMonths || x.Thang == prevMonth)
                        && x.Nam == prevYear
                        && employeeIds.Contains(x.MaNhanVien))
            .Select(x => x.DiemTong)
            .ToListAsync();

        decimal kpiPrev;
        var hasPrevKpiData = false;
        if (prevKpiTongRows.Count > 0)
        {
            kpiPrev = prevKpiTongRows.Average();
            hasPrevKpiData = true;
        }
        else
        {
            var prevFallbackQuery = _dbContext.KetQuaKpis.AsNoTracking()
                .Where(x => (allMonths || x.thang == prevMonth)
                            && x.nam == prevYear
                            && x.DiemSo.HasValue
                            && employeeIds.Contains(x.MaNhanVien));
            if (maKpi.HasValue)
            {
                prevFallbackQuery = prevFallbackQuery.Where(x => x.MaKpi == maKpi.Value);
            }

            var prevKpiRows = await prevFallbackQuery
                .Select(x => x.DiemSo ?? 0m)
                .ToListAsync();
            kpiPrev = prevKpiRows.Count == 0 ? 0m : prevKpiRows.Average();
            hasPrevKpiData = prevKpiRows.Count > 0;
        }
        var kpiDelta = kpiPrev == 0 ? (kpiCurrent > 0 ? 100 : 0) : Math.Round((kpiCurrent - kpiPrev) * 100m / kpiPrev, 2);

        var byTaskAssignees = scopedTaskIdsFinal.Count == 0
            ? new Dictionary<int, List<string>>()
            : await _dbContext.PhanCongNhanViens.AsNoTracking()
                .Where(x => scopedTaskIdsFinal.Contains(x.MaCongViec))
                .Join(_dbContext.NhanViens.AsNoTracking(), x => x.MaNhanVien, x => x.MaNhanVien, (a, e) => new { a.MaCongViec, e.HoTen })
                .GroupBy(x => x.MaCongViec)
                .ToDictionaryAsync(
                    g => g.Key,
                    g => g.Select(x => x.HoTen ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList());

        var taskByEmployee = scopedTaskIdsFinal.Count == 0
            ? new Dictionary<int, List<int>>()
            : await _dbContext.PhanCongNhanViens.AsNoTracking()
                .Where(x => scopedTaskIdsFinal.Contains(x.MaCongViec) && employeeIds.Contains(x.MaNhanVien))
                .GroupBy(x => x.MaNhanVien)
                .ToDictionaryAsync(g => g.Key, g => g.Select(x => x.MaCongViec).Distinct().ToList());

        bool IsCompleted(TaskRow x) => x.MaTrangThai == TaskStatusCompleted;
        bool IsOverdue(TaskRow x) => x.MaTrangThai == TaskStatusOverdue || (!IsCompleted(x) && x.HanHoanThanh.HasValue && x.HanHoanThanh.Value.Date < today);
        bool IsInProgress(TaskRow x) => !IsCompleted(x) && !IsOverdue(x);

        var soTaskHoanThanh = taskRows.Count(IsCompleted);
        var soTaskTreHan = taskRows.Count(IsOverdue);
        var soTaskDangLam = taskRows.Count(IsInProgress);

        if (isEmployeeDashboard)
        {
            var directSummary = await GetEmployeeTaskSummaryAsync(actor.MaNhanVien);
            if (directSummary.HasValue)
            {
                soTaskDangLam = directSummary.Value.SoCongViecDangLam;
                soTaskTreHan = directSummary.Value.SoCongViecTreHan;
                soTaskHoanThanh = directSummary.Value.SoCongViecHoanThanh;
            }
            // Try to fetch richer KPI and task metrics for the employee (weekly/monthly KPIs and completion rates)
            var empKpiFull = allMonths ? null : await GetEmployeeKpiSummaryAsync(actor.MaNhanVien, month, year);
            if (empKpiFull.HasValue)
            {
                // Override KPI and task summary values with the SQL-derived metrics when available
                var v = empKpiFull.Value;
                try
                {
                    kpiCurrent = v.DiemKPIHienTai;
                }
                catch { }

                // Prefer the detailed counts from the SQL result
                soTaskHoanThanh = v.SoCongViecHoanThanh;
                soTaskTreHan = v.SoCongViecTreHan;
                // Derive in-progress from total minus completed and overdue if reasonable
                soTaskDangLam = Math.Max(0, v.TongCongViec - v.SoCongViecHoanThanh - v.SoCongViecTreHan);
            }
        }
        var soTaskChoDuyet = scopedTaskIdsFinal.Count == 0
            ? 0
            : await _dbContext.TienDoCongViecs.AsNoTracking()
                .Where(x => scopedTaskIdsFinal.Contains(x.MaCongViec) && x.TrangThaiPheDuyet == "Chờ duyệt")
                .Select(x => x.MaCongViec).Distinct().CountAsync();

        var distribution = new DashboardTaskStatusDistributionDto
        {
            ChuaBatDau = taskRows.Count(x => x.MaTrangThai == TaskStatusNotStarted),
            DangThucHien = soTaskDangLam,
            ChoDuyet = soTaskChoDuyet,
            HoanThanh = soTaskHoanThanh,
            QuaHan = soTaskTreHan,
            TongTask = taskRows.Count,
            DataSource = "real"
        };

        var urgentTasks = taskRows
            .Select(x =>
            {
                var overdue = x.MaTrangThai == TaskStatusOverdue || (x.MaTrangThai != TaskStatusCompleted && x.HanHoanThanh.HasValue && x.HanHoanThanh.Value.Date < today);
                var hoursLeft = x.HanHoanThanh.HasValue ? (x.HanHoanThanh.Value - DateTime.Now).TotalHours : double.MaxValue;
                var daysLeft = x.HanHoanThanh.HasValue ? (x.HanHoanThanh.Value.Date - today).Days : int.MaxValue;
                var rank = overdue ? 0 : hoursLeft <= 24 ? 1 : daysLeft <= 3 ? 2 : (x.MaTrangThai == TaskStatusDoing ? 3 : 4);
                var reason = overdue ? "Quá hạn" : hoursLeft <= 24 ? "Deadline < 24h" : daysLeft <= 3 ? "Deadline < 3 ngày" : (x.MaTrangThai == TaskStatusDoing ? "Đang thực hiện" : "Chờ xử lý");
                return new { Task = x, Rank = rank, Reason = reason };
            })
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Task.HanHoanThanh ?? DateTime.MaxValue)
            .ThenBy(x => x.Task.MaCongViec)
            .Take(15)
            .Select(x => new DashboardUrgentTaskDto
            {
                MaCongViec = x.Task.MaCongViec,
                TenCongViec = x.Task.TenCongViec,
                NguoiPhuTrach = byTaskAssignees.TryGetValue(x.Task.MaCongViec, out var names) ? string.Join(", ", names) : "(Chưa gán)",
                DuAn = projectNames.TryGetValue(x.Task.MaDuAn, out var duAnName) ? duAnName : $"Dự án {x.Task.MaDuAn}",
                Deadline = x.Task.HanHoanThanh,
                TienDo = x.Task.PhanTramHoanThanh,
                TrangThai = ToTaskStatusLabel(x.Task.MaTrangThai, x.Task.HanHoanThanh, today),
                PriorityReason = x.Reason,
                DataSource = "real"
            })
            .ToList();

        var workloads = employees.Select(x =>
        {
            var taskIds = taskByEmployee.TryGetValue(x.MaNhanVien, out var t) ? t : new List<int>();
            var active = taskRows.Count(r => taskIds.Contains(r.MaCongViec) && r.MaTrangThai != TaskStatusCompleted);
            return new DashboardWorkloadDto
            {
                MaNhanVien = x.MaNhanVien,
                HoTen = x.HoTen ?? $"NV {x.MaNhanVien}",
                TaskDangLam = active,
                MucTai = active >= 12 ? "Quá tải" : (active >= 6 ? "Bình thường" : "Có thể nhận thêm việc"),
                DataSource = "real"
            };
        }).OrderByDescending(x => x.TaskDangLam).ThenBy(x => x.MaNhanVien).ToList();

        var attentionEmployees = employees.Select(x =>
        {
            var taskIds = taskByEmployee.TryGetValue(x.MaNhanVien, out var t) ? t : new List<int>();
            var empTasks = taskRows.Where(r => taskIds.Contains(r.MaCongViec)).ToList();
            var late = empTasks.Count(r => r.MaTrangThai == TaskStatusOverdue || (r.MaTrangThai != TaskStatusCompleted && r.HanHoanThanh.HasValue && r.HanHoanThanh.Value.Date < today));
            var active = empTasks.Count(r => r.MaTrangThai != TaskStatusCompleted);
            var kpi = kpiByNhanVien.TryGetValue(x.MaNhanVien, out var value) ? value : 0m;
            var badge = (kpi < 65m || late >= 3 || active >= 12) ? "Nguy cơ cao" : ((kpi < 75m || late >= 1 || active >= 8) ? "Cần hỗ trợ" : "Ổn định");
            return new DashboardAttentionEmployeeDto
            {
                MaNhanVien = x.MaNhanVien,
                HoTen = x.HoTen ?? $"NV {x.MaNhanVien}",
                Kpi = kpi,
                TaskTre = late,
                TaskDangLam = active,
                MucTai = active >= 12 ? "Quá tải" : (active >= 6 ? "Bình thường" : "Có thể nhận thêm"),
                CanhBao = badge,
                DataSource = "derived"
            };
        })
        .Where(x => x.CanhBao != "Ổn định")
        .OrderByDescending(x => x.CanhBao == "Nguy cơ cao")
        .ThenBy(x => x.Kpi)
        .Take(10)
        .ToList();

        var soDuAnDangChay = scopedProjectIds.Count == 0
            ? 0
            : await _dbContext.DuAns.AsNoTracking().CountAsync(x => scopedProjectIds.Contains(x.MaDuAn) && (x.TrangThai ?? 0) == ProjectStatusInProgress);

        var taskSapDenHan = taskRows.Count(x => x.MaTrangThai != TaskStatusCompleted && x.HanHoanThanh.HasValue && x.HanHoanThanh.Value.Date >= today && x.HanHoanThanh.Value.Date <= today.AddDays(3));
        var lowKpiCount = employees.Count(x => kpiByNhanVien.TryGetValue(x.MaNhanVien, out var kpi) && kpi < 70m);
        var overloadedCount = workloads.Count(x => x.MucTai == "Quá tải");

        var insufficientRiskData = taskRows.Count == 0 || employees.Count == 0;
        var overdueRate = taskRows.Count == 0 ? 0m : (decimal)soTaskTreHan / taskRows.Count;
        var upcomingRate = taskRows.Count == 0 ? 0m : (decimal)taskSapDenHan / taskRows.Count;
        var lowKpiRate = employees.Count == 0 ? 0m : (decimal)lowKpiCount / employees.Count;
        var overloadRate = employees.Count == 0 ? 0m : (decimal)overloadedCount / employees.Count;
        var riskScore = Math.Round((overdueRate * 35m + upcomingRate * 25m + lowKpiRate * 25m + overloadRate * 15m) * 100m, 2);
        riskScore = Math.Clamp(riskScore, 0m, 100m);
        var riskLevel = riskScore < 40m ? "Thấp" : (riskScore < 70m ? "Trung bình" : "Cao");

        var aiPredictions = employeeIds.Count == 0
            ? new List<(int MaNhanVien, decimal XacSuatTreHan, string? DeXuat, string? GoiY, DateTime? ThoiGian)>()
            : await _dbContext.DuDoanAis.AsNoTracking()
                .Where(x =>
                    employeeIds.Contains(x.MaNhanVien)
                    && (allMonths || x.thang == month)
                    && x.nam == year
                    && x.XacSuatTreHan.HasValue)
                .Select(x => new
                {
                    x.MaNhanVien,
                    XacSuatTreHan = x.XacSuatTreHan!.Value,
                    x.DeXuatCaiThien,
                    x.GoiYNguonLuc,
                    x.ThoiGianDuDoan
                })
                .ToListAsync()
                .ContinueWith(t => t.Result
                    .Select(x => (x.MaNhanVien, x.XacSuatTreHan, x.DeXuatCaiThien, x.GoiYNguonLuc, x.ThoiGianDuDoan))
                    .ToList());

        var hasAiRisk = aiPredictions.Count >= 3;
        var aiRiskScore = hasAiRisk
            ? Math.Round(aiPredictions.Average(x => x.XacSuatTreHan <= 1m ? x.XacSuatTreHan * 100m : x.XacSuatTreHan), 2)
            : 0m;
        aiRiskScore = Math.Clamp(aiRiskScore, 0m, 100m);
        var aiRiskLevel = aiRiskScore < 40m ? "Thấp" : (aiRiskScore < 70m ? "Trung bình" : "Cao");

        var aiFeedbackRows = hasAiRisk
            ? await _dbContext.AiFeedbacks.AsNoTracking()
                .Where(x => x.NgayPhanHoi.HasValue
                    && x.NgayPhanHoi.Value >= DateTime.Now.AddDays(-30)
                    && x.MaNhanVien.HasValue
                    && employeeIds.Contains(x.MaNhanVien.Value))
                .OrderByDescending(x => x.NgayPhanHoi)
                .Take(50)
                .ToListAsync()
            : new List<LuanVan.Models.AiFeedback>();

        var aiInterventionCount30d = 0;
        if (hasAiRisk)
        {
            try
            {
                aiInterventionCount30d = await _dbContext.AiNhatKyCanThieps.AsNoTracking()
                    .CountAsync(x => x.NgayCanThiep.HasValue
                        && x.NgayCanThiep.Value >= DateTime.UtcNow.AddDays(-30)
                        && x.MaNhanVien.HasValue
                        && employeeIds.Contains(x.MaNhanVien.Value));
            }
            catch (SqlException ex) when (IsMissingAiInterventionTable(ex))
            {
                // Backward-compatible fallback when AI_NHATKY_CAN_THIEP migration has not been applied yet.
                aiInterventionCount30d = 0;
            }
        }

        var scopedUserIds = employeeIds.Count == 0
            ? new List<string>()
            : await _dbContext.NhanViens.AsNoTracking()
                .Where(x => employeeIds.Contains(x.MaNhanVien) && x.AspNetUserId != null)
                .Select(x => x.AspNetUserId!)
                .Distinct()
                .ToListAsync();

        var pendingApprovals = new DashboardPendingApprovalsDto
        {
            TienDoChoDuyet = scopedTaskIdsFinal.Count == 0 ? 0 : await _dbContext.TienDoCongViecs.AsNoTracking().CountAsync(x => scopedTaskIdsFinal.Contains(x.MaCongViec) && x.TrangThaiPheDuyet == "Chờ duyệt"),
            KpiChoDuyet = employeeIds.Count == 0 ? 0 : await _dbContext.DeXuatKpis.AsNoTracking().CountAsync(x => x.TrangThai == "ChoDuyet" && employeeIds.Contains(x.NguoiDeXuat)),
            BaoCaoChoXem = await _dbContext.YeuCauBaoCaos.AsNoTracking()
                .CountAsync(x => !x.IsDeleted
                    && (x.TrangThai == "MoiTao" || x.TrangThai == "DangXuLy")
                    && (
                        (x.NguoiNhanYeuCau == currentUserId)
                        || (scopedUserIds.Count > 0 && x.NguoiYeuCau != null && scopedUserIds.Contains(x.NguoiYeuCau))
                    )),
            DeXuatHeThongChoXuLy = 0,
            DataSource = "real"
        };

        var projectMeta = scopedProjectIds.Count == 0
            ? new Dictionary<int, (string Name, DateTime? Deadline)>()
            : await _dbContext.DuAns.AsNoTracking()
                .Where(x => scopedProjectIds.Contains(x.MaDuAn))
                .ToDictionaryAsync(
                    x => x.MaDuAn,
                    x => (x.TenDuAn ?? $"Dự án {x.MaDuAn}", x.NgayKetThuc));

        var projectHealth = taskRows
            .GroupBy(x => x.MaDuAn)
            .Select(g =>
            {
                var total = g.Count();
                var done = g.Count(x => x.MaTrangThai == TaskStatusCompleted);
                var late = g.Count(x => x.MaTrangThai == TaskStatusOverdue || (x.MaTrangThai != TaskStatusCompleted && x.HanHoanThanh.HasValue && x.HanHoanThanh.Value.Date < today));
                var progress = total == 0 ? 0m : Math.Round((decimal)done * 100m / total, 2);
                var memberIds = taskByEmployee.Where(kv => kv.Value.Any(id => g.Any(t => t.MaCongViec == id))).Select(kv => kv.Key).Distinct().ToList();
                var kpiVals = memberIds.Where(kpiByNhanVien.ContainsKey).Select(id => kpiByNhanVien[id]).ToList();
                var kpi = kpiVals.Count == 0 ? 0m : Math.Round(kpiVals.Average(), 2);
                var risk = late >= 4 || progress < 50m ? "Cao" : (late >= 1 || progress < 75m ? "Trung bình" : "Thấp");
                var meta = projectMeta.TryGetValue(g.Key, out var m) ? m : ($"Dự án {g.Key}", (DateTime?)null);
                return new DashboardProjectHealthDto
                {
                    MaDuAn = g.Key,
                    TenDuAn = meta.Item1,
                    TienDo = progress,
                    Kpi = kpi,
                    Deadline = meta.Item2,
                    TaskTre = late,
                    Risk = risk,
                    DataSource = "derived"
                };
            })
            .OrderByDescending(x => x.Risk == "Cao")
            .ThenBy(x => x.Deadline ?? DateTime.MaxValue)
            .ToList();

        var kpiTheoPhongBan = await _dbContext.PhongBans.AsNoTracking()
            .Where(x => selectedDepartmentId.HasValue
                ? x.MaPhongBan == selectedDepartmentId.Value
                : (isAdmin || managedDepartmentIds.Contains(x.MaPhongBan)))
            .Select(x => new { x.MaPhongBan, x.TenPhongBan })
            .ToListAsync();
        var kpiByDept = kpiTheoPhongBan.Select(pb =>
        {
            var deptIds = employees.Where(e => e.MaPhongBan == pb.MaPhongBan).Select(e => e.MaNhanVien).ToList();
            var vals = deptIds.Where(kpiByNhanVien.ContainsKey).Select(id => kpiByNhanVien[id]).ToList();
            return new DashboardPhongBanKpiDto
            {
                MaPhongBan = pb.MaPhongBan,
                TenPhongBan = pb.TenPhongBan,
                Kpi = vals.Count == 0 ? 0m : vals.Average()
            };
        }).OrderByDescending(x => x.Kpi).ToList();

        var topNhanVien = employees
            .Select(x => new DashboardNhanVienKpiDto { MaNhanVien = x.MaNhanVien, HoTen = x.HoTen, Kpi = kpiByNhanVien.TryGetValue(x.MaNhanVien, out var kpi) ? kpi : 0m })
            .OrderByDescending(x => x.Kpi).ThenBy(x => x.MaNhanVien).Take(topCount).ToList();

        var departments = await _dbContext.PhongBans.AsNoTracking()
            .Where(x => isAdmin || managedDepartmentIds.Contains(x.MaPhongBan))
            .Select(x => new DashboardFilterItemDto { Id = x.MaPhongBan, Label = x.TenPhongBan ?? $"Phòng {x.MaPhongBan}" })
            .ToListAsync();
        var teams = await _dbContext.Nhoms.AsNoTracking()
            .Where(x => isAdmin || visibleTeamIds.Contains(x.MaNhom))
            .Select(x => new DashboardFilterItemDto { Id = x.MaNhom, Label = x.TenNhom ?? $"Nhóm {x.MaNhom}" })
            .ToListAsync();
        var projects = scopedProjectIds.Count == 0
            ? new List<DashboardFilterItemDto>()
            : await _dbContext.DuAns.AsNoTracking()
                .Where(x => scopedProjectIds.Contains(x.MaDuAn))
                .Select(x => new DashboardFilterItemDto { Id = x.MaDuAn, Label = x.TenDuAn ?? $"Dự án {x.MaDuAn}" })
                .ToListAsync();

        var taskTreHan = taskRows
            .Where(x => x.MaTrangThai == TaskStatusOverdue || (x.MaTrangThai != TaskStatusCompleted && x.HanHoanThanh.HasValue && x.HanHoanThanh.Value.Date < today))
            .OrderBy(x => x.HanHoanThanh)
            .Take(10)
            .Select(x => new DashboardTaskTreHanDto
            {
                MaCongViec = x.MaCongViec,
                TenTask = x.TenCongViec,
                NhanViens = byTaskAssignees.TryGetValue(x.MaCongViec, out var names) ? names : new List<string>(),
                Deadline = x.HanHoanThanh,
                SoNgayTre = x.HanHoanThanh.HasValue ? Math.Max(0, (today - x.HanHoanThanh.Value.Date).Days) : 0
            }).ToList();

        var timeline = new List<DashboardTimelineItemDto>
        {
            new() { Actor = "Hệ thống", Action = allMonths ? $"Cập nhật dashboard toàn bộ tháng năm {year}" : $"Cập nhật dashboard kỳ {month}/{year}", Time = DateTime.Now, DataSource = "derived" },
            new() { Actor = "Hệ thống", Action = $"{soTaskTreHan} công việc quá hạn trong phạm vi quản lý", Time = DateTime.Now, DataSource = "derived" },
            new() { Actor = "Hệ thống", Action = $"KPI trung bình hiện tại: {kpiCurrent:F1}%", Time = DateTime.Now, DataSource = "derived" }
        };

        var summaryCards = new List<DashboardSummaryCardDto>
        {
            new() { Key = "staff", Label = "Nhân sự quản lý", Value = employees.Count.ToString(), DataSource = "real" },
            new() { Key = "projects", Label = "Dự án đang chạy", Value = soDuAnDangChay.ToString(), DataSource = "real" },
            new() { Key = "active_tasks", Label = "Công việc đang làm", Value = soTaskDangLam.ToString(), DataSource = "real" },
            new() { Key = "late_tasks", Label = "Công việc trễ hạn", Value = soTaskTreHan.ToString(), DataSource = "real" },
            new() { Key = "avg_kpi", Label = "KPI trung bình", Value = $"{kpiCurrent:F1}%", DataSource = hasCurrentKpiData ? "real" : "insufficient" },
            new() { Key = "risk", Label = hasAiRisk ? "AI Risk" : "Mức rủi ro", Value = insufficientRiskData ? "Chưa đủ dữ liệu" : $"{(hasAiRisk ? aiRiskScore : riskScore):F1} ({(hasAiRisk ? aiRiskLevel : riskLevel)})", DataSource = insufficientRiskData ? "insufficient" : (hasAiRisk ? "real" : "fallback") }
        };

        var response = new DashboardResponseDto
        {
            IsPersonalDashboard = isEmployeeDashboard,
            NhanVienContext = new DashboardNhanVienContextDto
            {
                MaNhanVien = actor.MaNhanVien,
                HoTen = actor.HoTen,
                MaPhongBan = actor.MaPhongBan,
                KpiCaNhan = kpiByNhanVien.TryGetValue(actor.MaNhanVien, out var selfKpi) ? selfKpi : kpiCurrent
            },
            ScopeContext = new DashboardScopeContextDto
            {
                RoleKey = isAdmin ? "admin" : (isEmployeeDashboard ? "employee" : "manager"),
                ManagerType = managerType,
                MaNhanVien = actor.MaNhanVien,
                MaPhongBan = selectedDepartmentId,
                MaNhom = selectedTeamId,
                MaDuAn = selectedProjectId,
                ScopeName = managerType == "team_lead" ? "Dashboard Trưởng nhóm" : "Dashboard Trưởng phòng",
                DataSource = "real"
            },
            Filters = new DashboardFilterOptionsDto
            {
                Month = allMonths ? 0 : month,
                Year = year,
                Departments = departments,
                Teams = teams,
                Projects = projects,
                SelectedDepartmentId = selectedDepartmentId,
                SelectedTeamId = selectedTeamId,
                SelectedProjectId = selectedProjectId,
                DataSource = "real"
            },
            TongQuan = new DashboardTongQuanDto
            {
                SoNhanVien = employees.Count,
                SoDuAnDangChay = soDuAnDangChay,
                SoTaskDangLam = soTaskDangLam,
                SoTaskHoanThanh = soTaskHoanThanh,
                SoTaskTreHan = soTaskTreHan,
                KpiTrungBinh = kpiCurrent
            },
            KpiTheoPhongBan = kpiByDept,
            TopNhanVien = topNhanVien,
            TaskTreHan = taskTreHan,
            SummaryCards = summaryCards,
            TaskStatusDistribution = distribution,
            Risk = new DashboardRiskDto
            {
                Score = insufficientRiskData ? 0m : (hasAiRisk ? aiRiskScore : riskScore),
                Level = insufficientRiskData ? "Không xác định" : (hasAiRisk ? aiRiskLevel : riskLevel),
                Label = hasAiRisk ? "AI Risk" : "Mức rủi ro",
                Source = insufficientRiskData ? "insufficient" : (hasAiRisk ? "ai" : "fallback"),
                Message = insufficientRiskData
                    ? "Chưa đủ dữ liệu để tính rủi ro"
                    : (hasAiRisk
                        ? "AI Risk được tính từ xác suất trễ hạn của mô hình dự báo trong kỳ hiện tại"
                        : "Ước tính theo task trễ, task sắp đến hạn, KPI thấp và workload quá tải"),
                DataSource = insufficientRiskData ? "insufficient" : (hasAiRisk ? "real" : "fallback")
            },
            KpiTrend = new DashboardKpiTrendDto
            {
                PreviousValue = kpiPrev,
                CurrentValue = kpiCurrent,
                DeltaPercent = kpiDelta,
                Labels = allMonths
                    ? new List<string> { $"Năm {prevYear}", $"Năm {year}" }
                    : new List<string> { $"Tháng {prevMonth}/{prevYear}", $"Tháng {month}/{year}" },
                Values = new List<decimal> { kpiPrev, kpiCurrent },
                DataSource = (hasCurrentKpiData || hasPrevKpiData) ? "real" : "insufficient"
            },
            UrgentTasks = urgentTasks,
            AttentionEmployees = attentionEmployees,
            TeamWorkload = workloads.Take(10).ToList(),
            PendingApprovals = pendingApprovals,
            Insights = BuildSystemInsights(soTaskTreHan, taskSapDenHan, attentionEmployees, pendingApprovals, projectHealth, hasAiRisk, aiPredictions, aiFeedbackRows, aiInterventionCount30d),
            InsightSource = hasAiRisk ? "ai" : "fallback",
            ActivityTimeline = timeline,
            ProjectHealth = projectHealth
        };

            return Ok(ApiResponse<DashboardResponseDto>.Ok(response, "Success"));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<DashboardResponseDto>.Fail($"Lỗi tải dashboard: {ex.Message}"));
        }
    }

    [HttpGet("export")]
    [Authorize(Policy = Permissions.KpiView)]
    public async Task<IActionResult> ExportDashboardCsv(
        [FromQuery] int? thang,
        [FromQuery] int? nam,
        [FromQuery] int? maKpi,
        [FromQuery] int? maPhongBan,
        [FromQuery] int? maNhom,
        [FromQuery] int? maDuAn)
    {
        EnsureDbConnectionStringInitialized();

        var result = await GetDashboard(thang, nam, maKpi, maPhongBan, maNhom, maDuAn, 100);
        if (result.Result is not OkObjectResult ok || ok.Value is not ApiResponse<DashboardResponseDto> payload || payload.Data == null)
        {
            return BadRequest("Không xuất được dữ liệu dashboard.");
        }

        var data = payload.Data;

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Dashboard Tổng Quan");

        // Title
        ws.Range("A1:E1").Merge();
        ws.Cell(1, 1).Value = "BÁO CÁO DASHBOARD QUẢN TRỊ";
        ws.Cell(2, 1).Value = $"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}";
        ws.Cell(2, 3).Value = $"Người xuất: {User?.Identity?.Name ?? "System"}";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 18;
        ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range("A1:E2").Style.Fill.BackgroundColor = XLColor.FromHtml("#0b5394");
        ws.Range("A1:E2").Style.Font.FontColor = XLColor.White;

        var row = 4;

        // Summary section header
        ws.Cell(row, 1).Value = "Tổng quan hệ thống";
        ws.Range(row, 1, row, 2).Merge();
        ws.Cell(row, 1).Style.Font.Bold = true;
        row++;

        ws.Cell(row, 1).Value = "Chỉ số";
        ws.Cell(row, 2).Value = "Giá trị";
        ws.Range(row, 1, row, 2).Style.Font.Bold = true;
        row++;

        foreach (var card in data.SummaryCards)
        {
            ws.Cell(row, 1).Value = card.Label;
            ws.Cell(row, 2).Value = card.Value;
            row++;
        }

        row += 1;

        // Pending approvals
        ws.Cell(row, 1).Value = "Yêu cầu chờ xử lý";
        ws.Range(row, 1, row, 2).Merge();
        ws.Cell(row, 1).Style.Font.Bold = true;
        row++;
        ws.Cell(row, 1).Value = "Hạng mục";
        ws.Cell(row, 2).Value = "Số lượng";
        ws.Range(row, 1, row, 2).Style.Font.Bold = true;
        row++;
        foreach (var p in data.PendingApprovals.ToPairs())
        {
            ws.Cell(row, 1).Value = p.Key;
            ws.Cell(row, 2).Value = p.Value;
            row++;
        }

        row += 1;

        // Project health
        ws.Cell(row, 1).Value = "Dự án rủi ro cao";
        ws.Range(row, 1, row, 5).Merge();
        ws.Cell(row, 1).Style.Font.Bold = true;
        row++;
        ws.Cell(row, 1).Value = "Dự án";
        ws.Cell(row, 2).Value = "Tiến độ";
        ws.Cell(row, 3).Value = "KPI";
        ws.Cell(row, 4).Value = "Task trễ";
        ws.Cell(row, 5).Value = "Risk";
        ws.Range(row, 1, row, 5).Style.Font.Bold = true;
        row++;
        foreach (var x in data.ProjectHealth)
        {
            ws.Cell(row, 1).Value = x.TenDuAn;
            ws.Cell(row, 2).Value = x.TienDo + "%";
            ws.Cell(row, 3).Value = x.Kpi;
            ws.Cell(row, 4).Value = x.TaskTre;
            ws.Cell(row, 5).Value = x.Risk;
            // color risk
            if (string.Equals(x.Risk, "Cao", StringComparison.OrdinalIgnoreCase))
            {
                ws.Range(row, 1, row, 5).Style.Font.FontColor = XLColor.Red;
            }
            row++;
        }

        row += 1;

        // Urgent tasks
        ws.Cell(row, 1).Value = "Công việc khẩn cấp";
        ws.Range(row, 1, row, 4).Merge();
        ws.Cell(row, 1).Style.Font.Bold = true;
        row++;
        ws.Cell(row, 1).Value = "Công việc";
        ws.Cell(row, 2).Value = "Deadline";
        ws.Cell(row, 3).Value = "Trạng thái";
        ws.Cell(row, 4).Value = "Lý do";
        ws.Range(row, 1, row, 4).Style.Font.Bold = true;
        row++;
        foreach (var t in data.UrgentTasks.Take(100))
        {
            ws.Cell(row, 1).Value = t.TenCongViec;
            ws.Cell(row, 2).Value = t.Deadline?.ToString("yyyy-MM-dd") ?? "";
            ws.Cell(row, 3).Value = t.TrangThai;
            ws.Cell(row, 4).Value = t.PriorityReason;
            if (string.Equals(t.TrangThai, "Quá hạn", StringComparison.OrdinalIgnoreCase))
            {
                ws.Range(row, 1, row, 4).Style.Font.FontColor = XLColor.Red;
            }
            row++;
        }

        // Styling and autofit
        var used = ws.RangeUsed();
        if (used != null)
        {
            used.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            used.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        var fileName = $"dashboard_export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private static List<DashboardInsightDto> BuildSystemInsights(
        int lateTasks,
        int nearDueTasks,
        List<DashboardAttentionEmployeeDto> attentionEmployees,
        DashboardPendingApprovalsDto pendingApprovals,
        List<DashboardProjectHealthDto> projectHealth,
        bool hasAiRisk,
        List<(int MaNhanVien, decimal XacSuatTreHan, string? DeXuat, string? GoiY, DateTime? ThoiGian)> aiPredictions,
        List<LuanVan.Models.AiFeedback> aiFeedbackRows,
        int aiInterventionCount30d)
    {
        var result = new List<DashboardInsightDto>();
        if (nearDueTasks > 0)
        {
            result.Add(new DashboardInsightDto
            {
                Type = "warning",
                Title = "Cảnh báo tiến độ",
                Content = $"Có {nearDueTasks} công việc sắp đến hạn trong 3 ngày tới.",
                DataSource = "derived"
            });
        }

        if (lateTasks > 0)
        {
            result.Add(new DashboardInsightDto
            {
                Type = "danger",
                Title = "Cảnh báo vận hành",
                Content = $"Có {lateTasks} công việc đang trễ hạn, cần ưu tiên xử lý ngay.",
                DataSource = "derived"
            });
        }

        if (attentionEmployees.Count > 0)
        {
            result.Add(new DashboardInsightDto
            {
                Type = "warning",
                Title = "Cảnh báo nhân sự",
                Content = $"{attentionEmployees.Count} nhân viên cần chú ý về KPI/tải công việc.",
                DataSource = "derived"
            });
        }

        var highRiskProjects = projectHealth.Count(x => x.Risk == "Cao");
        if (highRiskProjects > 0)
        {
            result.Add(new DashboardInsightDto
            {
                Type = "danger",
                Title = "Cảnh báo dự án",
                Content = $"{highRiskProjects} dự án đang ở mức rủi ro cao, cần rà soát nguồn lực.",
                DataSource = "derived"
            });
        }

        if (pendingApprovals.TienDoChoDuyet > 0 || pendingApprovals.KpiChoDuyet > 0)
        {
            result.Add(new DashboardInsightDto
            {
                Type = "info",
                Title = "Đề xuất hệ thống",
                Content = "Ưu tiên xử lý các mục chờ duyệt tiến độ/KPI để giảm tắc nghẽn.",
                DataSource = "fallback"
            });
        }

        if (result.Count == 0)
        {
            result.Add(new DashboardInsightDto
            {
                Type = "ok",
                Title = "Gợi ý hệ thống",
                Content = "Chưa có cảnh báo lớn trong phạm vi hiện tại.",
                DataSource = "fallback"
            });
        }

        if (hasAiRisk)
        {
            var highRiskCount = aiPredictions.Count(x => (x.XacSuatTreHan <= 1m ? x.XacSuatTreHan * 100m : x.XacSuatTreHan) >= 70m);
            result.Add(new DashboardInsightDto
            {
                Type = highRiskCount > 0 ? "danger" : "info",
                Title = "Phân tích AI",
                Content = highRiskCount > 0
                    ? $"AI phát hiện {highRiskCount} nhân sự có xác suất trễ hạn cao trong kỳ."
                    : "AI chưa phát hiện cụm rủi ro trễ hạn cao trong kỳ.",
                DataSource = "real"
            });

            var aiSuggestion = aiPredictions
                .Select(x => !string.IsNullOrWhiteSpace(x.GoiY) ? x.GoiY : x.DeXuat)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            if (!string.IsNullOrWhiteSpace(aiSuggestion))
            {
                result.Add(new DashboardInsightDto
                {
                    Type = "info",
                    Title = "Khuyến nghị AI",
                    Content = LocalizeAiSuggestion(aiSuggestion!),
                    DataSource = "real"
                });
            }

            if (aiFeedbackRows.Count > 0)
            {
                var usefulRate = aiFeedbackRows.Count(x => (x.MucHuuIch ?? 0) >= 4);
                result.Add(new DashboardInsightDto
                {
                    Type = "info",
                    Title = "AI Feedback Loop",
                    Content = $"Có {aiFeedbackRows.Count} phản hồi AI và {aiInterventionCount30d} can thiệp HITL trong 30 ngày; {usefulRate} phản hồi đánh giá hữu ích cao.",
                    DataSource = "real"
                });
            }
        }

        return result;
    }

    private static bool IsMissingAiInterventionTable(SqlException ex)
    {
        const int invalidObjectNameSqlServerError = 208;
        return ex.Number == invalidObjectNameSqlServerError
               && ex.Message.Contains("AI_NHATKY_CAN_THIEP", StringComparison.OrdinalIgnoreCase);
    }

    private static string Escape(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Contains(',') || text.Contains('"') || text.Contains('\n'))
        {
            return $"\"{text.Replace("\"", "\"\"")}\"";
        }
        return text;
    }

    private static string ToTaskStatusLabel(int status, DateTime? deadline, DateTime today)
    {
        if (status == TaskStatusCompleted) return "Hoàn thành";
        var isLate = status == TaskStatusOverdue || (status != TaskStatusCompleted && deadline.HasValue && deadline.Value.Date < today);
        if (isLate) return "Quá hạn";
        if (status == TaskStatusDoing) return "Đang thực hiện";
        if (status == TaskStatusNotStarted) return "Chưa bắt đầu";
        return "Chờ duyệt";
    }

    private static string LocalizeAiSuggestion(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return text.Trim() switch
        {
            "Saved by AI pipeline" => "Được lưu bởi quy trình AI",
            _ => text
        };
    }

    private sealed class TaskRow
    {
        public int MaCongViec { get; set; }
        public string TenCongViec { get; set; } = string.Empty;
        public int MaDuAn { get; set; }
        public DateTime? HanHoanThanh { get; set; }
        public int MaTrangThai { get; set; }
        public decimal PhanTramHoanThanh { get; set; }
    }

    private async Task<(int SoCongViecDangLam, int SoCongViecTreHan, int SoCongViecHoanThanh)?> GetEmployeeTaskSummaryAsync(int maNhanVien)
    {
        try
        {
            var connectionString = ResolveConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return null;
            }

            const string sql = @"
SELECT
    SUM(CASE
        WHEN tt.TENTRANGTHAI IN (N'Đang thực hiện', N'Chờ phê duyệt', N'Chưa bắt đầu', N'Tạm dừng')
             AND NOT (
                cv.HANHOANTHANH < GETDATE()
                AND tt.TENTRANGTHAI <> N'Hoàn thành'
             )
        THEN 1 ELSE 0
    END) AS SoCongViecDangLam,
    SUM(CASE
        WHEN tt.TENTRANGTHAI = N'Trễ hạn'
             OR (
                cv.HANHOANTHANH < GETDATE()
                AND tt.TENTRANGTHAI <> N'Hoàn thành'
             )
        THEN 1 ELSE 0
    END) AS SoCongViecTreHan,
    SUM(CASE
        WHEN tt.TENTRANGTHAI = N'Hoàn thành'
        THEN 1 ELSE 0
    END) AS SoCongViecHoanThanh
FROM PHANCONGNHANVIEN pc
JOIN CONGVIEC cv ON pc.MACONGVIEC = cv.MACONGVIEC
LEFT JOIN TRANGTHAICONGVIEC tt ON cv.MATRANGTHAI = tt.MATRANGTHAI
WHERE pc.MANHANVIEN = @maNhanVien
  AND ISNULL(cv.DAXOA, 0) = 0;";

                        await using var conn = new SqlConnection(connectionString);
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            var p = cmd.CreateParameter();
            p.ParameterName = "@maNhanVien";
            p.Value = maNhanVien;
            cmd.Parameters.Add(p);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return (0, 0, 0);
            }

            var soDangLam = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0));
            var soTreHan = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1));
            var soHoanThanh = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2));
            return (soDangLam, soTreHan, soHoanThanh);
        }
        catch
        {
            return null;
        }
    }

    private async Task<(
        decimal DiemKPIHienTai,
        decimal TyLeHoanThanhMucTieu,
        decimal TyLeCongViecDungHan,
        int SoHoanThanhTrongTuan,
        int SoHoanThanhTrongThang,
        decimal ChenhLechKPIThangTruoc,
        string XuHuongHieuSuat,
        int TongCongViec,
        int SoCongViecHoanThanh,
        int SoCongViecDungHan,
        int SoCongViecTreHan
    )?> GetEmployeeKpiSummaryAsync(int maNhanVien, int thang, int nam)
    {
        try
        {
            var connectionString = ResolveConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString)) return null;

            const string sql = @"
;WITH CongViecNhanVien AS (
    SELECT 
        nv.MANHANVIEN,
        nv.HOTEN,
        cv.MACONGVIEC,
        cv.TENCONGVIEC,
        tt.TENTRANGTHAI,
        cv.HANHOANTHANH,
        pc.NGAYGIAO,
        pc.NGAYKETTHUCTHUCTE,
        pc.PHANTRAMHOANTHANH
    FROM PHANCONGNHANVIEN pc
    JOIN NHANVIEN nv ON pc.MANHANVIEN = nv.MANHANVIEN
    JOIN CONGVIEC cv ON pc.MACONGVIEC = cv.MACONGVIEC
    LEFT JOIN TRANGTHAICONGVIEC tt ON cv.MATRANGTHAI = tt.MATRANGTHAI
    WHERE nv.MANHANVIEN = @MaNhanVien
      AND ISNULL(cv.DAXOA, 0) = 0
),
ThongKeCongViec AS (
    SELECT
        MANHANVIEN,
        COUNT(*) AS TongCongViec,
        SUM(CASE WHEN TENTRANGTHAI = N'Hoàn thành' THEN 1 ELSE 0 END) AS SoCongViecHoanThanh,
        SUM(CASE WHEN TENTRANGTHAI = N'Hoàn thành' AND NGAYKETTHUCTHUCTE IS NOT NULL AND HANHOANTHANH IS NOT NULL AND NGAYKETTHUCTHUCTE <= HANHOANTHANH THEN 1 ELSE 0 END) AS SoCongViecDungHan,
        SUM(CASE WHEN TENTRANGTHAI = N'Trễ hạn' OR (HANHOANTHANH < GETDATE() AND TENTRANGTHAI <> N'Hoàn thành') THEN 1 ELSE 0 END) AS SoCongViecTreHan,
        SUM(CASE WHEN TENTRANGTHAI = N'Hoàn thành' AND NGAYKETTHUCTHUCTE >= DATEADD(DAY, -7, GETDATE()) THEN 1 ELSE 0 END) AS SoHoanThanhTrongTuan,
        SUM(CASE WHEN TENTRANGTHAI = N'Hoàn thành' AND MONTH(NGAYKETTHUCTHUCTE) = @Thang AND YEAR(NGAYKETTHUCTHUCTE) = @Nam THEN 1 ELSE 0 END) AS SoHoanThanhTrongThang
    FROM CongViecNhanVien
    GROUP BY MANHANVIEN
),
KpiHienTai AS (
    SELECT kqt.MANHANVIEN, kqt.DIEMTONG
    FROM KETQUAKPI_TONG kqt
    WHERE kqt.MANHANVIEN = @MaNhanVien AND kqt.THANG = @Thang AND kqt.NAM = @Nam
),
KpiThangTruoc AS (
    SELECT TOP 1 kqt.MANHANVIEN, kqt.DIEMTONG AS DiemThangTruoc
    FROM KETQUAKPI_TONG kqt
    WHERE kqt.MANHANVIEN = @MaNhanVien
      AND (kqt.NAM < @Nam OR (kqt.NAM = @Nam AND kqt.THANG < @Thang))
    ORDER BY kqt.NAM DESC, kqt.THANG DESC
)
SELECT 
    ISNULL(kpi.DIEMTONG, 0) AS DiemKPIHienTai,
    CASE WHEN ISNULL(tk.TongCongViec,0)=0 THEN 0 ELSE CAST(tk.SoCongViecHoanThanh * 100.0 / tk.TongCongViec AS DECIMAL(5,2)) END AS TyLeHoanThanhMucTieu,
    CASE WHEN ISNULL(tk.SoCongViecHoanThanh,0)=0 THEN 0 ELSE CAST(tk.SoCongViecDungHan * 100.0 / tk.SoCongViecHoanThanh AS DECIMAL(5,2)) END AS TyLeCongViecDungHan,
    ISNULL(tk.SoHoanThanhTrongTuan, 0) AS SoHoanThanhTrongTuan,
    ISNULL(tk.SoHoanThanhTrongThang, 0) AS SoHoanThanhTrongThang,
    ISNULL(kpi.DIEMTONG,0) - ISNULL(ktt.DiemThangTruoc,0) AS ChenhLechKPIThangTruoc,
    CASE WHEN ktt.DiemThangTruoc IS NULL THEN N'Chưa có dữ liệu tháng trước' WHEN kpi.DIEMTONG > ktt.DiemThangTruoc THEN N'Tăng' WHEN kpi.DIEMTONG < ktt.DiemThangTruoc THEN N'Giảm' ELSE N'Không đổi' END AS XuHuongHieuSuat,
    ISNULL(tk.TongCongViec,0) AS TongCongViec,
    ISNULL(tk.SoCongViecHoanThanh,0) AS SoCongViecHoanThanh,
    ISNULL(tk.SoCongViecDungHan,0) AS SoCongViecDungHan,
    ISNULL(tk.SoCongViecTreHan,0) AS SoCongViecTreHan
FROM NHANVIEN nv
LEFT JOIN KpiHienTai kpi ON nv.MANHANVIEN = kpi.MANHANVIEN
LEFT JOIN KpiThangTruoc ktt ON nv.MANHANVIEN = ktt.MANHANVIEN
LEFT JOIN ThongKeCongViec tk ON nv.MANHANVIEN = tk.MANHANVIEN
WHERE nv.MANHANVIEN = @MaNhanVien;";

            await using var conn = new SqlConnection(connectionString);
            if (conn.State != ConnectionState.Open) await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            var p1 = cmd.CreateParameter(); p1.ParameterName = "@MaNhanVien"; p1.Value = maNhanVien; cmd.Parameters.Add(p1);
            var p2 = cmd.CreateParameter(); p2.ParameterName = "@Thang"; p2.Value = thang; cmd.Parameters.Add(p2);
            var p3 = cmd.CreateParameter(); p3.ParameterName = "@Nam"; p3.Value = nam; cmd.Parameters.Add(p3);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            var diem = reader.IsDBNull(0) ? 0m : Convert.ToDecimal(reader.GetValue(0));
            var t1 = reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1));
            var t2 = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2));
            var soWeek = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3));
            var soMonth = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4));
            var chenhlech = reader.IsDBNull(5) ? 0m : Convert.ToDecimal(reader.GetValue(5));
            var xuHuong = reader.IsDBNull(6) ? string.Empty : reader.GetString(6);
            var tong = reader.IsDBNull(7) ? 0 : Convert.ToInt32(reader.GetValue(7));
            var hoan = reader.IsDBNull(8) ? 0 : Convert.ToInt32(reader.GetValue(8));
            var dungHan = reader.IsDBNull(9) ? 0 : Convert.ToInt32(reader.GetValue(9));
            var treHan = reader.IsDBNull(10) ? 0 : Convert.ToInt32(reader.GetValue(10));

            return (diem, t1, t2, soWeek, soMonth, chenhlech, xuHuong, tong, hoan, dungHan, treHan);
        }
        catch
        {
            return null;
        }
    }
}

internal static class DashboardPendingApprovalsExtensions
{
    public static IEnumerable<KeyValuePair<string, string>> ToPairs(this DashboardPendingApprovalsDto x)
    {
        return new[]
        {
            new KeyValuePair<string, string>("Chờ duyệt tiến độ", x.TienDoChoDuyet.ToString()),
            new KeyValuePair<string, string>("Chờ duyệt KPI", x.KpiChoDuyet.ToString()),
            new KeyValuePair<string, string>("Báo cáo chờ xem", x.BaoCaoChoXem.ToString()),
            new KeyValuePair<string, string>("Đề xuất hệ thống chờ xử lý", x.DeXuatHeThongChoXuLy.ToString())
        };
    }
}
