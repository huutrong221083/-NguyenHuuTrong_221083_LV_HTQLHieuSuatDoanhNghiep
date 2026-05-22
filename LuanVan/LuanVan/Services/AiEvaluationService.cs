using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LuanVan.Data;
using LuanVan.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LuanVan.Services;

public class AiEvaluationService : IAiEvaluationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AiEvaluationService> _logger;
    private const string DefaultEvaluationModelName = "AI Evaluation Pipeline";

    public AiEvaluationService(AppDbContext db, ILogger<AiEvaluationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> RunEvaluationAsync(int maModel, string loaiMoHinh, DateTime? tuNgay, DateTime? denNgay, string? positiveLabel = null, CancellationToken cancellationToken = default)
    {
        var resolvedModelId = await ResolveModelIdAsync(maModel, cancellationToken);

        // Prepare date filters
        DateTime from = tuNgay ?? DateTime.MinValue;
        DateTime to = denNgay ?? DateTime.MaxValue;

        // Regression metrics using AiDanhGiaChiTiet when numeric predicted & actual are present
        var regQuery = _db.AiDanhGiaChiTiets
            .Where(x => x.GiatriDuDoanSo != null && x.GiatriThucTeSo != null);

        if (resolvedModelId > 0)
        {
            // try to filter by MaDanhGia -> join AiDanhGiaRun to filter by MaModel
            regQuery = regQuery.Join(_db.AiDanhGiaRuns, c => c.MaDanhGia, r => r.MaDanhGia, (c, r) => new { c, r })
            .Where(t => t.r.MaModel == resolvedModelId && (t.r.NgayDanhGia >= from && t.r.NgayDanhGia <= to))
                .Select(t => t.c);
        }

        var regList = await regQuery.ToListAsync(cancellationToken);

        decimal? mae = null, rmse = null, r2 = null;
        int tot = regList.Count;
        int totDung = 0, totSai = 0;

        if (tot > 0)
        {
            var diffs = regList.Select(x => (double)(x.GiatriDuDoanSo!.Value - x.GiatriThucTeSo!.Value)).ToArray();
            var abs = diffs.Select(d => Math.Abs(d)).ToArray();
            mae = (decimal)abs.Average();
            rmse = (decimal)Math.Sqrt(diffs.Select(d => d * d).Average());

            // R^2
            var y = regList.Select(x => (double)x.GiatriThucTeSo!.Value).ToArray();
            var mean = y.Average();
            var ssRes = y.Zip(diffs, (yi, diff) => Math.Pow(diff, 2)).Sum();
            var ssTot = y.Select(yi => Math.Pow(yi - mean, 2)).Sum();
            if (ssTot > 0) r2 = 1m - (decimal)(ssRes / ssTot);

            totDung = regList.Count(x => (x.DungSo ?? false) || (x.DungNhan ?? false));
            totSai = tot - totDung;
        }

        // Classification metrics from AiDanhGiaChiTiet
        var clsQuery = _db.AiDanhGiaChiTiets
            .Where(x => x.NhanDuDoan != null && x.NhanThucTe != null);

        if (resolvedModelId > 0)
        {
            clsQuery = clsQuery.Join(_db.AiDanhGiaRuns, c => c.MaDanhGia, r => r.MaDanhGia, (c, r) => new { c, r })
            .Where(t => t.r.MaModel == resolvedModelId && (t.r.NgayDanhGia >= from && t.r.NgayDanhGia <= to))
                .Select(t => t.c);
        }

        var clsList = await clsQuery.ToListAsync(cancellationToken);

        decimal? accuracy = null, precision = null, recall = null, f1 = null;
        if (clsList.Count > 0)
        {
            var total = clsList.Count;
            var correct = clsList.Count(x => string.Equals(x.NhanDuDoan, x.NhanThucTe, StringComparison.OrdinalIgnoreCase));
            accuracy = (decimal)correct / total;

            var pos = positiveLabel ?? "Yeu";
            var tp = clsList.Count(x => string.Equals(x.NhanDuDoan, pos, StringComparison.OrdinalIgnoreCase) && string.Equals(x.NhanThucTe, pos, StringComparison.OrdinalIgnoreCase));
            var fp = clsList.Count(x => string.Equals(x.NhanDuDoan, pos, StringComparison.OrdinalIgnoreCase) && !string.Equals(x.NhanThucTe, pos, StringComparison.OrdinalIgnoreCase));
            var fn = clsList.Count(x => !string.Equals(x.NhanDuDoan, pos, StringComparison.OrdinalIgnoreCase) && string.Equals(x.NhanThucTe, pos, StringComparison.OrdinalIgnoreCase));

            precision = tp + fp > 0 ? (decimal)tp / (tp + fp) : null;
            recall = tp + fn > 0 ? (decimal)tp / (tp + fn) : null;
            if (precision.HasValue && recall.HasValue && (precision + recall) > 0)
            {
                f1 = 2 * (precision.Value * recall.Value) / (precision.Value + recall.Value);
            }

            // update totals if regression not present
            if (tot == 0)
            {
                tot = total;
                totDung = correct;
                totSai = total - correct;
            }
        }

        // Business / HITL metrics using AiFeedback and AiBusinessKpiRun
        var fbQuery = _db.AiFeedbacks.AsQueryable();
        if (tuNgay.HasValue)
            fbQuery = fbQuery.Where(x => x.NgayPhanHoi >= tuNgay.Value);
        if (denNgay.HasValue)
            fbQuery = fbQuery.Where(x => x.NgayPhanHoi <= denNgay.Value);

        var feedbacks = await fbQuery.ToListAsync(cancellationToken);
        decimal? utilityScore = null;
        if (feedbacks.Count > 0)
        {
            var avgMuc = feedbacks.Where(x => x.MucHuuIch.HasValue).Select(x => x.MucHuuIch!.Value).DefaultIfEmpty(0).Average();
            utilityScore = (decimal)avgMuc;
        }

        // HITL actions from AI_NHATKY_CAN_THIEP (fallback to feedback count when table has no rows yet)
        var totalPredictions = await _db.DuDoanAis.CountAsync(cancellationToken);
        var hitlActions = await _db.AiNhatKyCanThieps.CountAsync(cancellationToken);
        if (hitlActions == 0)
        {
            hitlActions = feedbacks.Count;
        }

        decimal? interventionRate = null, userAcceptanceRate = null;
        if (totalPredictions > 0)
            interventionRate = (decimal)hitlActions / totalPredictions;

        // acceptance: assume DungSai == false means accepted (applied)
        if (feedbacks.Count > 0)
        {
            var accepted = feedbacks.Count(x => x.DungSai.HasValue && x.DungSai == false);
            userAcceptanceRate = (decimal)accepted / feedbacks.Count;
        }

        // Persist AiDanhGiaRun record
        var run = new AiDanhGiaRun
        {
            MaModel = resolvedModelId,
            LoaiMoHinh = loaiMoHinh ?? string.Empty,
            TuNgay = tuNgay,
            DenNgay = denNgay,
            NgayDanhGia = DateTime.UtcNow,
            TongBanGhi = tot,
            TongDung = totDung,
            TongSai = totSai,
            Mae = mae,
            Rmse = rmse,
            Accuracy = accuracy,
            PrecisionScore = precision,
            RecallScore = recall,
            F1Score = f1,
            GhiChu = "Auto-generated evaluation run"
        };

        _db.AiDanhGiaRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);

        // Persist business run (best-effort for environments that have not updated this table schema yet)
        var bRun = new AiBusinessKpiRun
        {
            MaModel = resolvedModelId,
            LoaiMoHinh = loaiMoHinh ?? string.Empty,
            TuNgay = tuNgay,
            DenNgay = denNgay,
            NgayTao = DateTime.UtcNow,
            TongDuDoan = totalPredictions,
            TongTacDong = hitlActions,
            InterventionRate = interventionRate,
            UserAcceptanceRate = userAcceptanceRate,
            UtilityScore = utilityScore,
            GhiChu = "Auto-generated business KPI run"
        };

        try
        {
            _db.AiBusinessKpiRuns.Add(bRun);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Skip writing AI_BUSINESS_KPI_RUN due to schema mismatch or missing columns.");
            _db.Entry(bRun).State = EntityState.Detached;
        }

        return run.MaDanhGia;
    }

    private async Task<int> ResolveModelIdAsync(int maModel, CancellationToken cancellationToken)
    {
        if (maModel > 0)
        {
            var exists = await _db.MoHinhAis.AnyAsync(x => x.MaModel == maModel, cancellationToken);
            if (exists)
            {
                return maModel;
            }
        }

        var fallback = await _db.MoHinhAis
            .OrderBy(x => x.MaModel)
            .Select(x => new { x.MaModel })
            .FirstOrDefaultAsync(cancellationToken);

        if (fallback != null)
        {
            return fallback.MaModel;
        }

        var model = new MoHinhAi
        {
            TenModel = DefaultEvaluationModelName,
            Version = "v1",
            NgayTrain = DateTime.UtcNow
        };

        _db.MoHinhAis.Add(model);
        await _db.SaveChangesAsync(cancellationToken);
        return model.MaModel;
    }
}
