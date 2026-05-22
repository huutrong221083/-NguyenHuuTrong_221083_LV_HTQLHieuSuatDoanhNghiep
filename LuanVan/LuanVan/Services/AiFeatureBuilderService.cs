using LuanVan.Data;
using Microsoft.EntityFrameworkCore;

namespace LuanVan.Services;

public interface IAiFeatureBuilderService
{
    Task<AiTaskDelayModelInput?> BuildTaskDelayInputAsync(PredictDelayCommand command, CancellationToken cancellationToken = default);
    Task<AiPerformanceModelInput?> BuildPerformanceInputAsync(ClassifyPerformanceCommand command, CancellationToken cancellationToken = default);
}

public sealed class AiTaskDelayModelInput
{
    public int? MaCongViec { get; set; }
    public int? MaNhanVien { get; set; }
    public double DoKho { get; set; }
    public double DoUuTien { get; set; }
    public int SoNguoiThamGia { get; set; }
    public double TienDoHienTai { get; set; }
    public int SoNgayConLai { get; set; }
    public double EstimatedHours { get; set; }
    public double SpentHours { get; set; }
}

public sealed class AiPerformanceModelInput
{
    public int MaNhanVien { get; set; }
    public int SoCongViecHoanThanh { get; set; }
    public int SoCongViecTreHan { get; set; }
    public double ThoiGianTrungBinh { get; set; }
    public double KpiTrungBinh { get; set; }
}

public sealed class AiFeatureBuilderService : IAiFeatureBuilderService
{
    private readonly AppDbContext _dbContext;

    public AiFeatureBuilderService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private static double NormalizePositive(double? value, double fallback)
        => value.HasValue && value.Value > 0 ? value.Value : fallback;

    public async Task<AiTaskDelayModelInput?> BuildTaskDelayInputAsync(PredictDelayCommand command, CancellationToken cancellationToken = default)
    {
        var normalizedProgress = command.TienDoHienTai ?? command.TienDo;

        if (!command.MaCongViec.HasValue)
        {
            if (!command.DoKho.HasValue || !command.DoUuTien.HasValue || !command.SoNguoiThamGia.HasValue || !normalizedProgress.HasValue || !command.SoNgayConLai.HasValue)
            {
                return null;
            }

            var estimatedHours = command.EstimatedHours ?? Math.Max(1d, command.SoNgayConLai.Value * 8d);
            var spentHours = command.SpentHours ?? Math.Max(0d, estimatedHours * (normalizedProgress.Value / 100d));

            return new AiTaskDelayModelInput
            {
                MaNhanVien = command.MaNhanVien,
                DoKho = command.DoKho.Value,
                DoUuTien = command.DoUuTien.Value,
                SoNguoiThamGia = command.SoNguoiThamGia.Value,
                TienDoHienTai = normalizedProgress.Value,
                SoNgayConLai = command.SoNgayConLai.Value,
                EstimatedHours = estimatedHours,
                SpentHours = spentHours
            };
        }

        var taskInfo = await _dbContext.CongViecs
            .AsNoTracking()
            .Where(x => x.MaCongViec == command.MaCongViec.Value)
            .Select(x => new
            {
                x.MaCongViec,
                x.MaDoKho,
                HeSoDoKho = x.DoKho != null ? x.DoKho.HeSo : (decimal?)null,
                x.MaDoUuTien,
                HeSoDoUuTien = x.DoUuTien != null ? x.DoUuTien.HeSo : (decimal?)null,
                x.HanHoanThanh,
                x.NgayBatDau
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (taskInfo == null)
        {
            return null;
        }

        var participantEmployeeIds = await _dbContext.PhanCongNhanViens
            .AsNoTracking()
            .Where(x => x.MaCongViec == taskInfo.MaCongViec)
            .Select(x => x.MaNhanVien)
            .Distinct()
            .ToListAsync(cancellationToken);

        var latestProgress = await _dbContext.TienDoCongViecs
            .AsNoTracking()
            .Where(x => x.MaCongViec == taskInfo.MaCongViec)
            .OrderByDescending(x => x.NgayCapNhat)
            .Select(x => (double?)x.PhanTramHoanThanh)
            .FirstOrDefaultAsync(cancellationToken);

        var remainingDays = taskInfo.HanHoanThanh.HasValue
            ? (int)Math.Ceiling((taskInfo.HanHoanThanh.Value.Date - DateTime.UtcNow.Date).TotalDays)
            : 0;

        var startDate = taskInfo.NgayBatDau?.Date ?? DateTime.UtcNow.Date.AddDays(-7);
        var deadline = taskInfo.HanHoanThanh?.Date ?? DateTime.UtcNow.Date.AddDays(Math.Max(1, remainingDays));
        var estimatedHoursDb = Math.Max(1d, (deadline - startDate).TotalHours);
        var currentSpentHours = Math.Max(0d, (DateTime.UtcNow.Date - startDate).TotalHours);

        return new AiTaskDelayModelInput
        {
            MaCongViec = taskInfo.MaCongViec,
            MaNhanVien = command.MaNhanVien ?? participantEmployeeIds.FirstOrDefault(),
            DoKho = NormalizePositive(command.DoKho ?? (double?)(taskInfo.HeSoDoKho ?? taskInfo.MaDoKho), 1d),
            DoUuTien = NormalizePositive(command.DoUuTien ?? (double?)(taskInfo.HeSoDoUuTien ?? taskInfo.MaDoUuTien), 1d),
            SoNguoiThamGia = command.SoNguoiThamGia ?? Math.Max(1, participantEmployeeIds.Count),
            TienDoHienTai = command.TienDoHienTai ?? command.TienDo ?? latestProgress ?? 0,
            SoNgayConLai = command.SoNgayConLai ?? remainingDays,
            EstimatedHours = command.EstimatedHours ?? estimatedHoursDb,
            SpentHours = command.SpentHours ?? currentSpentHours
        };
    }

    public async Task<AiPerformanceModelInput?> BuildPerformanceInputAsync(ClassifyPerformanceCommand command, CancellationToken cancellationToken = default)
    {
        if (!command.MaNhanVien.HasValue)
        {
            if (!command.SoCongViecHoanThanh.HasValue || !command.SoCongViecTreHan.HasValue || !command.ThoiGianTrungBinh.HasValue || !command.KpiTrungBinh.HasValue)
            {
                return null;
            }

            return new AiPerformanceModelInput
            {
                MaNhanVien = 0,
                SoCongViecHoanThanh = command.SoCongViecHoanThanh.Value,
                SoCongViecTreHan = command.SoCongViecTreHan.Value,
                ThoiGianTrungBinh = command.ThoiGianTrungBinh.Value,
                KpiTrungBinh = command.KpiTrungBinh.Value
            };
        }

        var maNhanVien = command.MaNhanVien.Value;

        var aiData = await _dbContext.DuLieuAis
            .AsNoTracking()
            .Where(x => x.MaNhanVien == maNhanVien)
            .OrderByDescending(x => x.MaDuLieu)
            .Select(x => new
            {
                x.SoCongViecHoanThanh,
                x.SoCongViecTreHan,
                x.ThoiGianTrungBinh,
                x.KpiTrungBinh
            })
            .FirstOrDefaultAsync(cancellationToken);

        var completedTasks = await _dbContext.PhanCongNhanViens
            .AsNoTracking()
            .Where(x => x.MaNhanVien == maNhanVien)
            .Join(_dbContext.CongViecs.AsNoTracking(), p => p.MaCongViec, c => c.MaCongViec, (p, c) => new { p, c })
            .CountAsync(x => x.c.MaTrangThai == 3, cancellationToken);

        var lateTasks = await _dbContext.PhanCongNhanViens
            .AsNoTracking()
            .Where(x => x.MaNhanVien == maNhanVien)
            .Join(_dbContext.CongViecs.AsNoTracking(), p => p.MaCongViec, c => c.MaCongViec, (p, c) => new { p, c })
            .CountAsync(x => x.c.HanHoanThanh.HasValue && x.c.HanHoanThanh.Value.Date < DateTime.UtcNow.Date && x.c.MaTrangThai != 3, cancellationToken);

        var averageWorkHours = _dbContext.PhanCongNhanViens
            .AsNoTracking()
            .Where(x => x.MaNhanVien == maNhanVien && x.NgayBatDauThucTe.HasValue && x.NgayKetThucThucTe.HasValue)
            .AsEnumerable().Select(x => (x.NgayKetThucThucTe!.Value - x.NgayBatDauThucTe!.Value).TotalHours)
            .DefaultIfEmpty(0)
            .Average();

        var kpiValues = await _dbContext.KetQuaKpis
            .AsNoTracking()
            .Where(x => x.MaNhanVien == maNhanVien && x.DiemSo.HasValue)
            .OrderByDescending(x => x.nam)
            .ThenByDescending(x => x.thang)
            .Select(x => (double)x.DiemSo!.Value)
            .Take(6)
            .ToListAsync(cancellationToken);

        var kpiAverage = kpiValues.Count == 0 ? 0 : kpiValues.Average();

        return new AiPerformanceModelInput
        {
            MaNhanVien = maNhanVien,
            SoCongViecHoanThanh = command.SoCongViecHoanThanh ?? aiData?.SoCongViecHoanThanh ?? completedTasks,
            SoCongViecTreHan = command.SoCongViecTreHan ?? aiData?.SoCongViecTreHan ?? lateTasks,
            ThoiGianTrungBinh = command.ThoiGianTrungBinh ?? (double?)(aiData?.ThoiGianTrungBinh) ?? averageWorkHours,
            KpiTrungBinh = command.KpiTrungBinh ?? (double?)(aiData?.KpiTrungBinh) ?? kpiAverage
        };
    }
}
