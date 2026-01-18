using Microsoft.AspNetCore.Mvc;
using ShiftScheduler.API.Interfaces;
using ShiftScheduler.API.Models;

namespace ShiftScheduler.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SavedSchedulesController : ControllerBase
{
    private readonly ISavedScheduleRepository _repository;

    public SavedSchedulesController(ISavedScheduleRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<List<SavedSchedule>>> GetAll()
    {
        var schedules = await _repository.GetAllAsync();
        return Ok(schedules);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SavedSchedule>> GetById(string id)
    {
        var schedule = await _repository.GetByIdAsync(id);
        if (schedule == null)
            return NotFound("לוח זמנים לא נמצא");

        return Ok(schedule);
    }

    [HttpGet("share/{shareCode}")]
    public async Task<ActionResult<SavedSchedule>> GetByShareCode(string shareCode)
    {
        var schedule = await _repository.GetByShareCodeAsync(shareCode);
        if (schedule == null)
            return NotFound("לוח זמנים משותף לא נמצא");

        return Ok(schedule);
    }

    [HttpGet("shared")]
    public async Task<ActionResult<List<SavedSchedule>>> GetShared()
    {
        var schedules = await _repository.GetSharedSchedulesAsync();
        return Ok(schedules);
    }

    [HttpPost]
    public async Task<ActionResult<SavedSchedule>> Create([FromBody] CreateScheduleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("שם לוח זמנים הוא חובה");

        if (request.Schedule == null || !request.Schedule.Any())
            return BadRequest("לוח זמנים ריק");

        var savedSchedule = new SavedSchedule
        {
            Name = request.Name,
            Description = request.Description ?? string.Empty,
            CreatedBy = request.CreatedBy ?? "משתמש",
            Schedule = request.Schedule,
            IsShared = request.IsShared,
            ShareCode = request.IsShared ? GenerateShareCode() : string.Empty
        };

        var created = await _repository.CreateAsync(savedSchedule);
        return Ok(created);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<SavedSchedule>> Update(string id, [FromBody] UpdateScheduleRequest request)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            return NotFound("לוח זמנים לא נמצא");

        if (!string.IsNullOrWhiteSpace(request.Name))
            existing.Name = request.Name;

        if (request.Description != null)
            existing.Description = request.Description;

        if (request.Schedule != null && request.Schedule.Any())
            existing.Schedule = request.Schedule;

        existing.LastModified = DateTime.Now;

        var updated = await _repository.UpdateAsync(id, existing);
        return Ok(updated);
    }

    [HttpPut("{id}/share")]
    public async Task<ActionResult<SavedSchedule>> ToggleShare(string id, [FromBody] ShareRequest request)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            return NotFound("לוח זמנים לא נמצא");

        existing.IsShared = request.IsShared;
        existing.ShareCode = request.IsShared ? GenerateShareCode() : string.Empty;

        var updated = await _repository.UpdateAsync(id, existing);
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing == null)
            return NotFound("לוח זמנים לא נמצא");

        await _repository.DeleteAsync(id);
        return Ok();
    }

    private string GenerateShareCode()
    {
        // יצירת קוד שיתוף ייחודי (6 תווים)
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 6)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}

public class CreateScheduleRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CreatedBy { get; set; }
    public List<DaySchedule> Schedule { get; set; } = new();
    public bool IsShared { get; set; } = false;
}

public class UpdateScheduleRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public List<DaySchedule>? Schedule { get; set; }
}

public class ShareRequest
{
    public bool IsShared { get; set; }
}
