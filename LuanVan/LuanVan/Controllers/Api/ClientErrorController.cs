using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace LuanVan.Controllers.Api
{
    [ApiController]
    [Route("client-error-log")]
    public class ClientErrorController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public ClientErrorController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Post([FromBody] JsonElement payload)
        {
            try
            {
                var logsDir = Path.Combine(_env.ContentRootPath, "logs");
                if (!Directory.Exists(logsDir)) Directory.CreateDirectory(logsDir);

                var file = Path.Combine(logsDir, "client-errors.log");
                var entry = new
                {
                    ReceivedAt = DateTime.UtcNow,
                    Url = Request?.Headers["Referer"].ToString() ?? Request?.Path.ToString(),
                    Payload = payload
                };

                var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions { WriteIndented = false });
                await System.IO.File.AppendAllTextAsync(file, json + "\n");
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = ex.Message });
            }
        }
    }
}
