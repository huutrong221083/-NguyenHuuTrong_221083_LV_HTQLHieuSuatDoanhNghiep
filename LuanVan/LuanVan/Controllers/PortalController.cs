using LuanVan.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace LuanVan.Controllers;

[Authorize]
public class PortalController : Controller
{
    private string ResolveRoleKeyFromUser()
    {
        if (User.IsInRole(Roles.Admin))
        {
            return "admin";
        }

        if (User.IsInRole(Roles.Manager))
        {
            return "manager";
        }

        if (User.IsInRole(Roles.Employee))
        {
            return "employee";
        }

        return "employee";
    }

    private bool HasPermission(string permission)
    {
        return User.IsInRole(Roles.Admin) || User.HasClaim(Permissions.PermissionClaimType, permission);
    }

    private static string ToLegacyRole(string roleKey)
    {
        return roleKey switch
        {
            "admin" => Roles.Admin,
            "manager" => Roles.Manager,
            _ => Roles.Employee
        };
    }

    private bool CanAccessEmployees()
    {
        return HasPermission(Permissions.EmployeesView);
    }

    private bool CanAccessDepartments()
    {
        return HasPermission(Permissions.EmployeesView);
    }

    private bool CanAccessProjects()
    {
        return HasPermission(Permissions.ProjectsView);
    }

    private bool CanAccessTasks()
    {
        return HasPermission(Permissions.TasksView);
    }

    private bool CanAccessProgressApproval()
    {
        return HasPermission(Permissions.TasksApprove);
    }

    private bool CanAccessKpi()
    {
        return !User.IsInRole(Roles.Employee) && HasPermission(Permissions.KpiView);
    }

    private bool CanAccessMyKpi()
    {
        return User.IsInRole(Roles.Employee) && HasPermission(Permissions.MyKpiView);
    }

    private bool CanAccessEvaluation()
    {
        return HasPermission(Permissions.KpiEvaluate);
    }

    private bool CanAccessAiInsights()
    {
        return HasPermission(Permissions.AiViewAlerts)
            || HasPermission(Permissions.AiViewForecast)
            || HasPermission(Permissions.AiViewPerformance)
            || HasPermission(Permissions.AiSuggestResources);
    }

    private bool CanAccessSettings()
    {
        return HasPermission(Permissions.SettingsView);
    }
    private bool CanAccessReports()
    {
        return HasPermission(Permissions.ReportsView);
    }

    private bool CanAccessAccountManagement()
    {
        return HasPermission(Permissions.SettingsEdit);
    }

    private void SetRoleContext()
    {
        var roleKey = ResolveRoleKeyFromUser();
        ViewData["RoleKey"] = roleKey;
        ViewData["Role"] = ToLegacyRole(roleKey);
        ViewData["CanViewEmployees"] = CanAccessEmployees();
        ViewData["CanViewDepartments"] = CanAccessDepartments();
        ViewData["CanViewProjects"] = CanAccessProjects();
        ViewData["CanViewTasks"] = CanAccessTasks();
        ViewData["CanViewKpi"] = CanAccessKpi();
        ViewData["CanViewMyKpi"] = CanAccessMyKpi();
        ViewData["CanViewEvaluation"] = CanAccessEvaluation();
        ViewData["CanViewAi"] = CanAccessAiInsights();
        ViewData["CanViewSettings"] = CanAccessSettings();
        ViewData["CanEditSettings"] = HasPermission(Permissions.SettingsEdit);
        ViewData["CanManageAccounts"] = CanAccessAccountManagement();
        ViewData["CanManageProject"] = HasPermission(Permissions.ProjectsCreate)
            || HasPermission(Permissions.ProjectsEdit)
            || HasPermission(Permissions.ProjectsDelete);
        ViewData["CanManageTask"] = HasPermission(Permissions.TasksCreate)
            || HasPermission(Permissions.TasksEdit)
            || HasPermission(Permissions.TasksAssign)
            || HasPermission(Permissions.TasksDelete);
        ViewData["CanEditTask"] = HasPermission(Permissions.TasksEdit)
            || HasPermission(Permissions.TasksAttach)
            || HasPermission(Permissions.TasksHistory);
    }

    private IActionResult RedirectToRoleDashboard()
    {
        return RedirectToAction(nameof(Dashboard));
    }

    public IActionResult Dashboard()
    {
        ViewData["Title"] = "Dashboard";
        SetRoleContext();
        return View();
    }

    public IActionResult Employees()
    {
        if (!CanAccessEmployees())
        {
            return RedirectToRoleDashboard();
        }

        ViewData["Title"] = "Quản lý nhân viên";
        SetRoleContext();
        return View();
    }

    public IActionResult Departments()
    {
        if (!CanAccessDepartments())
        {
            return RedirectToRoleDashboard();
        }

        ViewData["Title"] = "Quản lý phòng ban";
        SetRoleContext();
        return View();
    }

    public IActionResult Projects()
    {
        if (!CanAccessProjects())
        {
            return RedirectToRoleDashboard();
        }

        ViewData["Title"] = "Quản lý dự án";
        SetRoleContext();
        return View();
    }

    public IActionResult Tasks()
    {
        if (!CanAccessTasks())
        {
            return RedirectToRoleDashboard();
        }

        ViewData["Title"] = "Quản lý công việc";
        SetRoleContext();
        return View();
    }

    public IActionResult ProgressApproval()
    {
        if (!CanAccessProgressApproval())
        {
            return RedirectToRoleDashboard();
        }

        ViewData["Title"] = "Duyệt tiến độ";
        SetRoleContext();
        return View();
    }

    public IActionResult Kpi()
    {
        if (!CanAccessKpi())
        {
            return RedirectToRoleDashboard();
        }

        ViewData["Title"] = "Quản lý KPI";
        SetRoleContext();
        return View();
    }

    public IActionResult Evaluation()
    {
        if (!CanAccessEvaluation())
        {
            return RedirectToRoleDashboard();
        }

        ViewData["Title"] = "Đánh giá hiệu suất";
        SetRoleContext();
        return View();
    }

    public IActionResult AiInsights()
    {
        if (!CanAccessAiInsights())
        {
            return RedirectToRoleDashboard();
        }

        ViewData["Title"] = "AI phân tích";
        SetRoleContext();
        return View();
    }

    public IActionResult AiFeatureStore()
    {
        if (!CanAccessAiInsights()) return RedirectToRoleDashboard();
        ViewData["Title"] = "Dữ liệu huấn luyện AI";
        SetRoleContext();
        return View();
    }

    public IActionResult AiFeatureStoreLegacy()
    {
        if (!CanAccessAiInsights()) return RedirectToRoleDashboard();
        ViewData["Title"] = "AI - Feature Store (Legacy)";
        SetRoleContext();
        return View("AiFeatureStoreLegacy");
    }

    public IActionResult AiModels()
    {
        if (!CanAccessAiInsights()) return RedirectToRoleDashboard();
        ViewData["Title"] = "AI - Models";
        SetRoleContext();
        return View();
    }

    public IActionResult AiForecast()
    {
        if (!CanAccessAiInsights()) return RedirectToRoleDashboard();
        ViewData["Title"] = "AI - Forecast";
        SetRoleContext();
        return View();
    }

    public IActionResult AiPerformance()
    {
        if (!CanAccessAiInsights()) return RedirectToRoleDashboard();
        ViewData["Title"] = "AI - Performance";
        SetRoleContext();
        return View();
    }

    public IActionResult Profile()
    {
        ViewData["Title"] = "Hồ sơ cá nhân";
        SetRoleContext();
        return View();
    }

    public IActionResult Notifications()
    {
        ViewData["Title"] = "Thông báo";
        SetRoleContext();
        return View();
    }

    public IActionResult MyKpi()
    {
        if (!CanAccessMyKpi())
        {
            return RedirectToRoleDashboard();
        }

        ViewData["Title"] = "KPI của tôi";
        SetRoleContext();
        return View();
    }

    public IActionResult CreateReport()
    {
        if (!CanAccessReports())
        {
            return RedirectToRoleDashboard();
        }

        ViewData["Title"] = "Tạo báo cáo";
        SetRoleContext();
        return View();
    }

    public IActionResult ReportManagement()
    {
        if (!CanAccessReports())
        {
            return RedirectToRoleDashboard();
        }

        ViewData["Title"] = "Quản lý báo cáo";
        SetRoleContext();
        return View();
    }

    public IActionResult Settings()
    {
        if (!CanAccessSettings())
        {
            return RedirectToRoleDashboard();
        }

        ViewData["Title"] = "Cài đặt hệ thống";
        SetRoleContext();
        return View();
    }

    public IActionResult AccountManagement()
    {
        if (!User.IsInRole(Roles.Admin))
        {
            return RedirectToRoleDashboard();
        }

        ViewData["Title"] = "Quản lý tài khoản";
        SetRoleContext();
        return View();
    }
}

