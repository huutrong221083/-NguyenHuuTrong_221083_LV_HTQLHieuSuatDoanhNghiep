namespace LuanVan.Contracts;

public static class Permissions
{
    public const string PermissionClaimType = "Permission";

    public const string SettingsView = "Settings.View";
    public const string SettingsEdit = "Settings.Edit";
    public const string SettingsCreate = "Settings.Create";
    public const string SettingsDelete = "Settings.Delete";

    public const string ProjectsView = "Projects.View";
    public const string ProjectsCreate = "Projects.Create";
    public const string ProjectsEdit = "Projects.Edit";
    public const string ProjectsDelete = "Projects.Delete";

    public const string TasksView = "Tasks.View";
    public const string TasksCreate = "Tasks.Create";
    public const string TasksEdit = "Tasks.Edit";
    public const string TasksDelete = "Tasks.Delete";
    public const string TasksAssign = "Tasks.Assign";
    public const string TasksHistory = "Tasks.History";
    public const string TasksAttach = "Tasks.Attach";
    public const string TasksApprove = "Tasks.Approve";

    public const string EmployeesView = "Employees.View";
    public const string EmployeesCreate = "Employees.Create";
    public const string EmployeesEdit = "Employees.Edit";
    public const string EmployeesDelete = "Employees.Delete";
    public const string EmployeesSkills = "Employees.Skills";
    public const string EmployeesPerformance = "Employees.Performance";
    public const string EmployeesWorkload = "Employees.Workload";

    public const string KpiView = "Kpi.View";
    public const string MyKpiView = "Kpi.MyView";
    public const string KpiManage = "Kpi.Manage";
    public const string KpiEvaluate = "Kpi.Evaluate";
    public const string KpiTeamView = "Kpi.TeamView";
    public const string KpiRanking = "Kpi.Ranking";

    public const string AiViewAlerts = "Ai.ViewAlerts";
    public const string AiViewForecast = "Ai.ViewForecast";
    public const string AiViewPerformance = "Ai.ViewPerformance";
    public const string AiSuggestResources = "Ai.SuggestResources";

    public const string DocumentsManage = "Documents.Manage";
    public const string NotificationsReceive = "Notifications.Receive";
    public const string ProfileView = "Profile.View";
    public const string ProfileEdit = "Profile.Edit";
    public const string ReportsView = "Reports.View";
    public const string ReportsCreate = "Reports.Create";
    public const string ReportsSubmit = "Reports.Submit";
    public const string ReportsReview = "Reports.Review";
    public const string ReportsManage = "Reports.Manage";
    public const string ReportsExport = "Reports.Export";
    public const string ReportsRequest = "Reports.Request";

    public static readonly string[] AllowedClaims =
    [
        SettingsView,
        SettingsEdit,
        SettingsCreate,
        SettingsDelete,
        ProjectsView,
        ProjectsCreate,
        ProjectsEdit,
        ProjectsDelete,
        TasksView,
        TasksCreate,
        TasksEdit,
        TasksDelete,
        TasksAssign,
        TasksHistory,
        TasksAttach,
        TasksApprove,
        EmployeesView,
        EmployeesCreate,
        EmployeesEdit,
        EmployeesDelete,
        EmployeesSkills,
        EmployeesPerformance,
        EmployeesWorkload,
        KpiView,
        MyKpiView,
        KpiManage,
        KpiEvaluate,
        KpiTeamView,
        KpiRanking,
        AiViewAlerts,
        AiViewForecast,
        AiViewPerformance,
        AiSuggestResources,
        DocumentsManage,
        NotificationsReceive,
        ProfileView,
        ProfileEdit,
        ReportsView,
        ReportsCreate,
        ReportsSubmit,
        ReportsReview,
        ReportsManage,
        ReportsExport,
        ReportsRequest
    ];

    public static readonly IReadOnlyDictionary<string, string[]> DefaultRoleClaims =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [Roles.Admin] = AllowedClaims,
            [Roles.Manager] =
            [
                ProjectsView,
                ProjectsCreate,
                TasksView,
                TasksCreate,
                TasksEdit,
                TasksAssign,
                TasksHistory,
                TasksAttach,
                TasksApprove,
                EmployeesView,
                EmployeesSkills,
                EmployeesPerformance,
                EmployeesWorkload,
                KpiView,
                KpiManage,
                KpiEvaluate,
                KpiTeamView,
                KpiRanking,
                AiViewAlerts,
                AiViewForecast,
                AiViewPerformance,
                AiSuggestResources,
                DocumentsManage,
                NotificationsReceive,
                ProfileView,
                ProfileEdit,
                ReportsView,
                ReportsRequest,
                ReportsReview,
                ReportsExport,
                ReportsCreate,
                ReportsSubmit
            ],
            [Roles.Employee] =
            [
                ProjectsView,
                TasksView,
                TasksEdit,
                TasksHistory,
                TasksAttach,
                EmployeesView,
                MyKpiView,
                AiViewAlerts,
                AiViewForecast,
                AiViewPerformance,
                NotificationsReceive,
                ProfileView,
                ProfileEdit,
                ReportsView,
                ReportsCreate,
                ReportsSubmit,
                ReportsExport
            ]
        };
}
