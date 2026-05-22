using LuanVan.Data;
using LuanVan.Models;
using Microsoft.EntityFrameworkCore;

namespace LuanVan.Services;

public interface IKpiService
{
    Task<KpiCalculateResult> CalculateAsync(KpiCalculateRequest request);
}

public class KpiService : IKpiService
{
    private readonly AppDbContext _dbContext;

    public KpiService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<KpiCalculateResult> CalculateAsync(KpiCalculateRequest request)
    {
        if (request.thang is < 1 or > 12)
        {
            throw new ArgumentException("thang phải trong khoang 1..12.");
        }

        if (request.nam <= 2000 || request.nam > 3000)
        {
            throw new ArgumentException("nam không hợp lệ.");
        }

        var maKpi = request.MaKpi;
        if (maKpi.HasValue)
        {
            var exists = await _dbContext.DanhMucKpis.AsNoTracking().AnyAsync(x => x.MaKpi == maKpi.Value);
            if (!exists)
            {
                throw new ArgumentException("MAKPI không tồn tại trong DANHMUCKPI.");
            }
        }

        IQueryable<NhanVien> employeeQuery = _dbContext.NhanViens.AsNoTracking().Where(x => x.TrangThai == 1);

        if (request.MaPhongBan.HasValue)
        {
            employeeQuery = employeeQuery.Where(x => x.MaPhongBan == request.MaPhongBan.Value);
        }

        if (request.MaNhanVien.HasValue)
        {
            employeeQuery = employeeQuery.Where(x => x.MaNhanVien == request.MaNhanVien.Value);
        }

        var employeeIds = await employeeQuery.Select(x => x.MaNhanVien).ToListAsync();
        if (employeeIds.Count == 0)
        {
            return new KpiCalculateResult
            {
                thang = request.thang,
                nam = request.nam,
                MaKpi = maKpi ?? 0,
                TongNhanVien = 0,
                TongTaskTrongKy = 0,
                SoBanGhiTaoMoi = 0,
                SoBanGhiCapNhat = 0,
                SoBanGhiTongTaoMoi = 0,
                SoBanGhiTongCapNhat = 0
            };
        }

        var periodStart = new DateTime(request.nam, request.thang, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);

        var assignmentScope = _dbContext.KpiNhanViens
            .AsNoTracking()
            .Where(x =>
                x.IsActive
                && employeeIds.Contains(x.MaNhanVien)
                && (!x.TuNgay.HasValue || x.TuNgay.Value <= periodEnd)
                && (!x.DenNgay.HasValue || x.DenNgay.Value >= periodStart));

        if (maKpi.HasValue)
        {
            assignmentScope = assignmentScope.Where(x => x.MaKpi == maKpi.Value);
        }

        var activeAssignments = await (
            from a in assignmentScope
            join dmk in _dbContext.DanhMucKpis.AsNoTracking() on a.MaKpi equals dmk.MaKpi
            join lk in _dbContext.LoaiKpis.AsNoTracking() on dmk.MaLoaiKpi equals lk.MaLoaiKpi into loaiJoin
            from lk in loaiJoin.DefaultIfEmpty()
            select new EmployeeKpiAssignment
            {
                MaNhanVien = a.MaNhanVien,
                MaKpi = a.MaKpi,
                TrongSoApDung = a.TrongSoApDung,
                HeSoLoaiKpi = lk != null ? lk.HeSo : 1m,
                MaLoaiKpi = dmk.MaLoaiKpi,
                TenLoaiKpi = lk != null ? lk.TenLoaiKpi : null
            })
            .ToListAsync();

        if (activeAssignments.Count == 0)
        {
            return new KpiCalculateResult
            {
                thang = request.thang,
                nam = request.nam,
                MaKpi = maKpi ?? 0,
                TongNhanVien = employeeIds.Count,
                TongTaskTrongKy = 0,
                SoBanGhiTaoMoi = 0,
                SoBanGhiCapNhat = 0,
                SoBanGhiTongTaoMoi = 0,
                SoBanGhiTongCapNhat = 0
            };
        }

        var assignmentRows = await (
            from pc in _dbContext.PhanCongNhanViens.AsNoTracking()
            join cv in _dbContext.CongViecs.AsNoTracking() on pc.MaCongViec equals cv.MaCongViec
            where cv.HanHoanThanh.HasValue
                  && cv.HanHoanThanh.Value.Month == request.thang
                  && cv.HanHoanThanh.Value.Year == request.nam
                  && employeeIds.Contains(pc.MaNhanVien)
            select new AssignmentRow
            {
                MaCongViec = pc.MaCongViec,
                MaNhanVien = pc.MaNhanVien,
                MaDoKho = cv.MaDoKho,
                HeSoDoKho = cv.DoKho != null ? cv.DoKho.HeSo : (decimal?)null,
                MaTrangThai = cv.MaTrangThai,
                HanHoanThanh = cv.HanHoanThanh!.Value,
                DiemCongViec = cv.DiemCongViec
            })
            .ToListAsync();

        var uniqueAssignments = assignmentRows
            .GroupBy(x => new { x.MaCongViec, x.MaNhanVien })
            .Select(g => g.First())
            .ToList();

        var taskIds = uniqueAssignments.Select(x => x.MaCongViec).Distinct().ToList();

        var progressByTask = taskIds.Count() == 0
            ? new Dictionary<int, decimal>()
            : await _dbContext.TienDoCongViecs.AsNoTracking()
                .Where(x => taskIds.Contains(x.MaCongViec))
                .GroupBy(x => x.MaCongViec)
                .Select(g => new { MaCongViec = g.Key, PhanTram = g.Average(v => v.PhanTramHoanThanh ?? 0m) })
                .ToDictionaryAsync(x => x.MaCongViec, x => x.PhanTram);

        var participantCountByTask = uniqueAssignments
            .GroupBy(x => x.MaCongViec)
            .ToDictionary(g => g.Key, g => g.Select(v => v.MaNhanVien).Distinct().Count());

        var kpiShareByTask = new Dictionary<int, double>();
        foreach (var taskGroup in uniqueAssignments.GroupBy(x => x.MaCongViec))
        {
            var sample = taskGroup.First();
            var progress = (double)progressByTask.GetValueOrDefault(sample.MaCongViec, 0m); // no progress => 0
            var taskStatus = sample.MaTrangThai ?? 0;
            var taskDifficulty = (double)(sample.HeSoDoKho ?? sample.MaDoKho ?? 0);
            var isCompleted = taskStatus == 3 || progress >= 100;
            var isLate = taskStatus == 4 || (!isCompleted && sample.HanHoanThanh.Date < DateTime.Today);
            var onTime = isCompleted && !isLate;

            var rawTaskKpi = (progress * 0.6) + (onTime ? 30 : 0) + (taskDifficulty * 5) - (isLate ? 10 : 0);
            var participantCount = Math.Max(1, participantCountByTask.GetValueOrDefault(sample.MaCongViec, 1));
            var scorePerParticipant = rawTaskKpi / participantCount;
            kpiShareByTask[sample.MaCongViec] = scorePerParticipant;
        }

        var kpiByEmployee = employeeIds.ToDictionary(id => id, _ => 0d);
        var employeeStats = BuildEmployeeStats(uniqueAssignments, progressByTask);

        var scoresByEmployee = uniqueAssignments
            .GroupBy(x => x.MaNhanVien)
            .ToDictionary(
                g => g.Key,
                g => g.Select(v => kpiShareByTask.GetValueOrDefault(v.MaCongViec, 0d)).ToList());

        foreach (var employeeId in employeeIds)
        {
            if (!scoresByEmployee.TryGetValue(employeeId, out var scores) || scores.Count() == 0)
            {
                // Rule: no tasks in period => KPI = 0
                kpiByEmployee[employeeId] = 0;
                continue;
            }

            var avg = scores.Average();
            kpiByEmployee[employeeId] = Math.Min(100, Math.Max(0, avg)); // clamp 0..100
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync();
        var assignmentPairs = activeAssignments
            .Select(x => new { x.MaNhanVien, x.MaKpi })
            .Distinct()
            .ToList();

        var activeEmployeeIdSet = assignmentPairs
            .Select(x => x.MaNhanVien)
            .Distinct()
            .ToHashSet();
        var activeKpiIdSet = assignmentPairs
            .Select(x => x.MaKpi)
            .Distinct()
            .ToHashSet();

        var aiStatsByEmployee = await _dbContext.DuDoanAis
            .AsNoTracking()
            .Where(x =>
                x.thang == request.thang
                && x.nam == request.nam
                && activeEmployeeIdSet.Contains(x.MaNhanVien)
                && x.DiemDuDoan.HasValue)
            .GroupBy(x => x.MaNhanVien)
            .Select(g => new
            {
                g.Key,
                Score = g.Average(v => v.DiemDuDoan!.Value),
                Count = g.Count()
            })
            .ToDictionaryAsync(x => x.Key, x => new AiBlendStat((decimal)x.Score, x.Count));

        var existingRows = await _dbContext.KetQuaKpis
            .Where(x =>
                x.thang == request.thang
                && x.nam == request.nam
                && activeEmployeeIdSet.Contains(x.MaNhanVien)
                && activeKpiIdSet.Contains(x.MaKpi))
            .ToListAsync();

        var duplicateRows = existingRows
            .GroupBy(x => new { x.MaNhanVien, x.MaKpi })
            .SelectMany(g => g.OrderByDescending(x => x.MaKetQua).Skip(1))
            .ToList();

        if (duplicateRows.Count > 0)
        {
            _dbContext.KetQuaKpis.RemoveRange(duplicateRows);
            await _dbContext.SaveChangesAsync();
        }

        var existingByPair = existingRows
            .GroupBy(x => new { x.MaNhanVien, x.MaKpi })
            .ToDictionary(g => (g.Key.MaNhanVien, g.Key.MaKpi), g => g.OrderByDescending(x => x.MaKetQua).First());
        var created = 0;
        var updated = 0;
        var createdTotal = 0;
        var updatedTotal = 0;

        var componentScores = new List<(int MaNhanVien, int MaKpi, decimal DiemSo, decimal TrongSoApDung, decimal HeSoLoaiKpi)>();
        foreach (var group in activeAssignments.GroupBy(x => x.MaNhanVien))
        {
            var employeeId = group.Key;
            var baseScore = (decimal)kpiByEmployee.GetValueOrDefault(employeeId, 0d);
            var stats = employeeStats.GetValueOrDefault(employeeId, EmployeeKpiStats.Empty);
            var aiStat = aiStatsByEmployee.GetValueOrDefault(employeeId, AiBlendStat.Empty);
            foreach (var item in group)
            {
                var ruleScore = CalculateRuleScoreByKpiType(item, stats, baseScore);
                var blendedScore = BlendWithAi(ruleScore, aiStat);
                blendedScore = ClampToScoreRange(blendedScore);
                var pairKey = (employeeId, item.MaKpi);
                if (existingByPair.TryGetValue(pairKey, out var existing))
                {
                    existing.DiemSo = blendedScore;
                    updated++;
                }
                else
                {
                    _dbContext.KetQuaKpis.Add(new KetQuaKpi
                    {
                        MaNhanVien = employeeId,
                        MaKpi = item.MaKpi,
                        DiemSo = blendedScore,
                        thang = request.thang,
                        nam = request.nam
                    });
                    created++;
                }

                componentScores.Add((employeeId, item.MaKpi, blendedScore, item.TrongSoApDung, item.HeSoLoaiKpi));
            }
        }

        var existingTotals = await _dbContext.KetQuaKpiTongs
            .Where(x => x.Thang == request.thang && x.Nam == request.nam && activeEmployeeIdSet.Contains(x.MaNhanVien))
            .ToDictionaryAsync(x => x.MaNhanVien, x => x);

        foreach (var group in componentScores.GroupBy(x => x.MaNhanVien))
        {
            var employeeId = group.Key;
            var totalEffectiveWeight = group.Sum(x => x.TrongSoApDung * x.HeSoLoaiKpi);
            var weighted = totalEffectiveWeight > 0m
                ? group.Sum(x => x.DiemSo * x.TrongSoApDung * x.HeSoLoaiKpi) / totalEffectiveWeight
                : group.Average(x => x.DiemSo);
            var totalScore = ClampToScoreRange(weighted);
            var soKpi = group.Count();
            var xepLoai = ClassifyRank(totalScore);
            var now = DateTime.Now;

            if (existingTotals.TryGetValue(employeeId, out var totalRow))
            {
                totalRow.DiemTong = totalScore;
                totalRow.XepLoai = xepLoai;
                totalRow.SoKpiThanhPhan = soKpi;
                totalRow.NgayTinh = now;
                updatedTotal++;
            }
            else
            {
                _dbContext.KetQuaKpiTongs.Add(new KetQuaKpiTong
                {
                    MaNhanVien = employeeId,
                    Thang = request.thang,
                    Nam = request.nam,
                    DiemTong = totalScore,
                    XepLoai = xepLoai,
                    SoKpiThanhPhan = soKpi,
                    NgayTinh = now
                });
                createdTotal++;
            }
        }

        await _dbContext.SaveChangesAsync();
        await tx.CommitAsync();

        return new KpiCalculateResult
        {
            thang = request.thang,
            nam = request.nam,
            MaKpi = maKpi ?? 0,
            TongNhanVien = activeEmployeeIdSet.Count,
            TongTaskTrongKy = taskIds.Count(),
            SoBanGhiTaoMoi = created,
            SoBanGhiCapNhat = updated,
            SoBanGhiTongTaoMoi = createdTotal,
            SoBanGhiTongCapNhat = updatedTotal
        };
    }

    private static decimal ClampToScoreRange(decimal score)
    {
        if (score < 0m) return 0m;
        if (score > 100m) return 100m;
        return Math.Round(score, 2, MidpointRounding.AwayFromZero);
    }

    private static string ClassifyRank(decimal score)
    {
        return score switch
        {
            >= 90m => "Xuất sắc",
            >= 80m => "Tốt",
            >= 65m => "Đạt",
            _ => "Cần cải thiện"
        };
    }

    private sealed class AssignmentRow
    {
        public int MaCongViec { get; set; }
        public int MaNhanVien { get; set; }
        public int? MaDoKho { get; set; }
        public decimal? HeSoDoKho { get; set; }
        public int? MaTrangThai { get; set; }
        public DateTime HanHoanThanh { get; set; }
        public decimal? DiemCongViec { get; set; }
    }

    private sealed class EmployeeKpiAssignment
    {
        public int MaNhanVien { get; set; }
        public int MaKpi { get; set; }
        public decimal TrongSoApDung { get; set; }
        public decimal HeSoLoaiKpi { get; set; } = 1m;
        public int MaLoaiKpi { get; set; }
        public string? TenLoaiKpi { get; set; }
    }

    private readonly record struct EmployeeKpiStats(
        int TotalTasks,
        decimal AvgProgress,
        decimal OnTimeRate,
        decimal LateRate,
        decimal CompletionRate,
        decimal AvgDifficulty,
        decimal AvgTaskScore)
    {
        public static EmployeeKpiStats Empty => new(0, 0, 0, 0, 0, 0, 0);
    }

    private static Dictionary<int, EmployeeKpiStats> BuildEmployeeStats(
        List<AssignmentRow> uniqueAssignments,
        IReadOnlyDictionary<int, decimal> progressByTask)
    {
        var map = new Dictionary<int, EmployeeKpiStats>();
        foreach (var employeeGroup in uniqueAssignments.GroupBy(x => x.MaNhanVien))
        {
            var rows = employeeGroup.ToList();
            var total = rows.Count;
            if (total == 0)
            {
                map[employeeGroup.Key] = EmployeeKpiStats.Empty;
                continue;
            }

            var avgProgress = rows.Average(x => progressByTask.GetValueOrDefault(x.MaCongViec, 0m));
            var onTimeCount = 0;
            var lateCount = 0;
            var completedCount = 0;
            decimal sumDifficulty = 0m;
            decimal sumTaskScore = 0m;
            var scoredTasks = 0;

            foreach (var row in rows)
            {
                var progress = progressByTask.GetValueOrDefault(row.MaCongViec, 0m);
                var isCompleted = (row.MaTrangThai ?? 0) == 3 || progress >= 100m;
                var isLate = (row.MaTrangThai ?? 0) == 4 || (!isCompleted && row.HanHoanThanh.Date < DateTime.Today);
                var isOnTime = isCompleted && !isLate;

                if (isCompleted) completedCount++;
                if (isLate) lateCount++;
                if (isOnTime) onTimeCount++;

                sumDifficulty += row.HeSoDoKho ?? row.MaDoKho ?? 0;
                if (row.DiemCongViec.HasValue)
                {
                    sumTaskScore += row.DiemCongViec.Value;
                    scoredTasks++;
                }
            }

            map[employeeGroup.Key] = new EmployeeKpiStats(
                total,
                avgProgress,
                total == 0 ? 0 : (decimal)onTimeCount / total,
                total == 0 ? 0 : (decimal)lateCount / total,
                total == 0 ? 0 : (decimal)completedCount / total,
                total == 0 ? 0 : sumDifficulty / total,
                scoredTasks == 0 ? 0 : sumTaskScore / scoredTasks
            );
        }

        return map;
    }

    private static decimal CalculateRuleScoreByKpiType(EmployeeKpiAssignment kpi, EmployeeKpiStats stats, decimal fallbackBaseScore)
    {
        var typeKey = NormalizeTypeKey(kpi.TenLoaiKpi, kpi.MaLoaiKpi);
        return typeKey switch
        {
            "deadline" => ClampToScoreRange((stats.OnTimeRate * 100m * 0.7m) + (stats.AvgProgress * 0.3m)),
            "quality" => ClampToScoreRange((stats.AvgTaskScore > 0 ? stats.AvgTaskScore : stats.AvgProgress) * 0.7m + ((1m - stats.LateRate) * 100m * 0.3m)),
            "discipline" => ClampToScoreRange((stats.CompletionRate * 100m * 0.5m) + ((1m - stats.LateRate) * 100m * 0.5m)),
            "efficiency" => ClampToScoreRange((stats.AvgProgress * 0.4m) + (stats.CompletionRate * 100m * 0.4m) + (Math.Min(stats.AvgDifficulty, 10m) * 2m)),
            _ => ClampToScoreRange(fallbackBaseScore)
        };
    }

    private static decimal BlendWithAi(decimal ruleScore, AiBlendStat aiStat)
    {
        // Chỉ blend AI khi đủ dữ liệu dự đoán trong kỳ.
        if (aiStat.Count < 3)
        {
            return ruleScore;
        }

        return (ruleScore * 0.7m) + (aiStat.Score * 0.3m);
    }

    private static string NormalizeTypeKey(string? tenLoaiKpi, int maLoaiKpi)
    {
        var key = (tenLoaiKpi ?? string.Empty).Trim().ToLowerInvariant();
        if (key.Contains("hạn") || key.Contains("deadline") || key.Contains("đúng hạn") || key.Contains("tre han") || key.Contains("trễ hạn"))
            return "deadline";
        if (key.Contains("chất lượng") || key.Contains("chat luong") || key.Contains("bug") || key.Contains("lỗi"))
            return "quality";
        if (key.Contains("kỷ luật") || key.Contains("ky luat") || key.Contains("báo cáo") || key.Contains("bao cao"))
            return "discipline";
        if (key.Contains("hiệu suất") || key.Contains("hieu suat") || key.Contains("năng suất") || key.Contains("nang suat"))
            return "efficiency";

        // fallback bằng mã loại phổ biến nếu dữ liệu tên chưa chuẩn hóa
        return maLoaiKpi switch
        {
            1 => "deadline",
            2 => "quality",
            3 => "discipline",
            4 => "efficiency",
            _ => "generic"
        };
    }

    private readonly record struct AiBlendStat(decimal Score, int Count)
    {
        public static AiBlendStat Empty => new(-1m, 0);
    }
}

public class KpiCalculateRequest
{
    public int thang { get; set; }
    public int nam { get; set; }
    public int? MaKpi { get; set; } = 1;
    public int? MaPhongBan { get; set; }
    public int? MaNhanVien { get; set; }
}

public class KpiCalculateResult
{
    public int thang { get; set; }
    public int nam { get; set; }
    public int MaKpi { get; set; }
    public int TongNhanVien { get; set; }
    public int TongTaskTrongKy { get; set; }
    public int SoBanGhiTaoMoi { get; set; }
    public int SoBanGhiCapNhat { get; set; }
    public int SoBanGhiTongTaoMoi { get; set; }
    public int SoBanGhiTongCapNhat { get; set; }
}



