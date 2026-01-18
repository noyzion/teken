using Microsoft.AspNetCore.Mvc;
using ShiftScheduler.API.Interfaces;
using ShiftScheduler.API.Models;

namespace ShiftScheduler.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScheduleController : ControllerBase
{
    private readonly ISchedulerService _schedulerService;
    private static List<DaySchedule> _currentSchedule = new();

    public ScheduleController(ISchedulerService schedulerService)
    {
        _schedulerService = schedulerService;
    }

    [HttpGet]
    public ActionResult<List<DaySchedule>> Get()
    {
        return Ok(_currentSchedule);
    }

    [HttpPost("generate")]
    public async Task<ActionResult<List<DaySchedule>>> Generate([FromBody] ScheduleConfig config)
    {
        Console.WriteLine("=== יצירת לוח זמנים - Controller ===");
        Console.WriteLine($"תאריך התחלה: {config.StartDate}, שעת התחלה: {config.StartHour}");
        Console.WriteLine($"תאריך סיום: {config.EndDate}, שעת סיום: {config.EndHour}");
        
        if (config.StartDate >= config.EndDate)
            return BadRequest("תאריך התחלה חייב להיות לפני תאריך סיום");
        
        if (config.StartHour.HasValue && (config.StartHour < 0 || config.StartHour > 23))
            return BadRequest("שעת התחלה חייבת להיות בין 0 ל-23");
        
        if (config.EndHour.HasValue && (config.EndHour < 0 || config.EndHour > 23))
            return BadRequest("שעת סיום חייבת להיות בין 0 ל-23");

        try
        {
            Console.WriteLine("קורא ל-GenerateScheduleAsync...");
            var schedule = await _schedulerService.GenerateScheduleAsync(config);
            Console.WriteLine($"לוח זמנים נוצר: {schedule.Count} משמרות");
            if (schedule.Count > 0)
            {
                Console.WriteLine($"משמרת ראשונה: {schedule[0].Date}, {schedule[0].Assignments.Count} שיבוצים");
            }
            _currentSchedule = schedule;
            return Ok(schedule);
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"InvalidOperationException: {ex.Message}");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
            Console.WriteLine($"StackTrace: {ex.StackTrace}");
            return StatusCode(500, $"שגיאה ביצירת לוח זמנים: {ex.Message}");
        }
    }

    [HttpPut]
    public ActionResult<List<DaySchedule>> Update([FromBody] List<DaySchedule> updatedSchedule)
    {
        _currentSchedule = updatedSchedule;
        return Ok(_currentSchedule);
    }

    [HttpPost("swap")]
    public ActionResult<List<DaySchedule>> SwapSoldiers([FromBody] SwapRequest request)
    {
        if (string.IsNullOrEmpty(request.Soldier1Id) || string.IsNullOrEmpty(request.Soldier2Id))
            return BadRequest("נדרשות 2 ID של חיילים");

        // Swap all assignments of soldier1 with soldier2
        foreach (var daySchedule in _currentSchedule)
        {
            foreach (var assignment in daySchedule.Assignments)
            {
                if (assignment.SoldierId == request.Soldier1Id)
                {
                    assignment.SoldierId = request.Soldier2Id;
                    assignment.SoldierName = request.Soldier2Name;
                }
                else if (assignment.SoldierId == request.Soldier2Id)
                {
                    assignment.SoldierId = request.Soldier1Id;
                    assignment.SoldierName = request.Soldier1Name;
                }
            }
        }

        return Ok(_currentSchedule);
    }

    [HttpPost("replace")]
    public ActionResult<List<DaySchedule>> ReplaceAssignment([FromBody] ReplaceRequest request)
    {
        if (string.IsNullOrEmpty(request.Date) || string.IsNullOrEmpty(request.PositionId) || 
            string.IsNullOrEmpty(request.NewSoldierId))
            return BadRequest("נדרשים כל הפרמטרים");

        // Find the specific assignment and replace
        var daySchedule = _currentSchedule.FirstOrDefault(s => s.Date == request.Date && s.ShiftNumber == request.ShiftNumber);
        if (daySchedule == null)
            return NotFound("משמרת לא נמצאה");

        // If OldSoldierId is provided, find that specific assignment, otherwise find any assignment for this position
        var assignment = string.IsNullOrEmpty(request.OldSoldierId) 
            ? daySchedule.Assignments.FirstOrDefault(a => a.PositionId == request.PositionId)
            : daySchedule.Assignments.FirstOrDefault(a => a.PositionId == request.PositionId && a.SoldierId == request.OldSoldierId);
        
        if (assignment == null)
        {
            // If no assignment exists, create a new one
            assignment = new ShiftAssignment
            {
                PositionId = request.PositionId,
                PositionName = request.PositionName ?? string.Empty
            };
            daySchedule.Assignments.Add(assignment);
        }

        assignment.SoldierId = request.NewSoldierId;
        assignment.SoldierName = request.NewSoldierName;

        return Ok(_currentSchedule);
    }
}

public class SwapRequest
{
    public string Soldier1Id { get; set; } = string.Empty;
    public string Soldier1Name { get; set; } = string.Empty;
    public string Soldier2Id { get; set; } = string.Empty;
    public string Soldier2Name { get; set; } = string.Empty;
}

public class ReplaceRequest
{
    public string Date { get; set; } = string.Empty;
    public int ShiftNumber { get; set; }
    public string PositionId { get; set; } = string.Empty;
    public string? PositionName { get; set; }
    public string OldSoldierId { get; set; } = string.Empty;
    public string NewSoldierId { get; set; } = string.Empty;
    public string NewSoldierName { get; set; } = string.Empty;
}
