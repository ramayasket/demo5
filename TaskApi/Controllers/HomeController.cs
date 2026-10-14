using Microsoft.AspNetCore.Mvc;

namespace TaskApi.Controllers
{
    [ApiController]
    [ApiExplorerSettings(IgnoreApi = true)]
    public sealed class HomeController : ControllerBase
    {
        [HttpGet("/")]
        public IActionResult Get()
            =>
                Redirect("/swagger");
    }
}
