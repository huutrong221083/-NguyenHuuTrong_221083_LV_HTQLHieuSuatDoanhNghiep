using System;

namespace LuanVan.Services;

public interface IAiEvaluationService
{
    Task<int> RunEvaluationAsync(int maModel, string loaiMoHinh, DateTime? tuNgay, DateTime? denNgay, string? positiveLabel = null, CancellationToken cancellationToken = default);
}
