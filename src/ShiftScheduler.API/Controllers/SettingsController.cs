using Microsoft.AspNetCore.Mvc;
using ShiftScheduler.API.Interfaces;
using ShiftScheduler.API.Models;

namespace ShiftScheduler.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;

    public SettingsController(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet]
    public async Task<ActionResult<SchedulerSettings>> Get()
    {
        var settings = await _settingsService.GetSettingsAsync();
        return Ok(settings);
    }

    [HttpPut]
    public async Task<ActionResult<SchedulerSettings>> Update([FromBody] SchedulerSettings settings)
    {
        try
        {
            var updated = await _settingsService.UpdateSettingsAsync(settings);
            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
