using System;
using System.Threading.Tasks;
using LuanVan.Services;
using LuanVan.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LuanVan.Controllers.Api;

[Route("api/ai/evaluate")]
[ApiController]
[Authorize(Policy = Permissions.AiSuggestResources)]
public class AiEvaluationController : ControllerBase
{
    private readonly IAiEvaluationService _eval;
    private readonly ILogger<AiEvaluationController> _logger;

    public AiEvaluationController(IAiEvaluationService eval, ILogger<AiEvaluationController> logger)
    {
        _eval = eval;
        _logger = logger;
    }

    [HttpPost("run")]
    public async Task<IActionResult> Run([FromBody] EvaluationRequest req)
    {
        if (req == null) return BadRequest();

        try
        {
            var id = await _eval.RunEvaluationAsync(req.MaModel, req.LoaiMoHinh ?? string.Empty, req.TuNgay, req.DenNgay, req.PositiveLabel);
            var modelLabel = string.IsNullOrWhiteSpace(req.LoaiMoHinh) ? "Auto" : req.LoaiMoHinh.Trim();
            var rangeLabel = req.TuNgay.HasValue && req.DenNgay.HasValue
                ? $"{req.TuNgay:dd/MM/yyyy} - {req.DenNgay:dd/MM/yyyy}"
                : "toàn bộ dữ liệu";

            return Ok(new
            {
                success = true,
                runId = id,
                message = $"Đã đánh giá model {modelLabel} trong khoảng ngày {rangeLabel}."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while running AI evaluation.");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    public class EvaluationRequest
    {
        public int MaModel { get; set; }
        public string? LoaiMoHinh { get; set; }
        public DateTime? TuNgay { get; set; }
        public DateTime? DenNgay { get; set; }
        public string? PositiveLabel { get; set; }
    }
}
