using Microsoft.AspNetCore.Mvc;
using ShiftScheduler.API.Interfaces;
using ShiftScheduler.API.Models;
using ClosedXML.Excel;

namespace ShiftScheduler.API.Controllers;

/// <summary>
/// העלאת קובץ אקסל של לוח משמרות וחישוב סטטיסטיקות לכל חייל.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ScheduleExcelController : ControllerBase
{
    private readonly ISchedulerService _schedulerService;
    private readonly ISoldierRepository _soldierRepository;
    private readonly IPositionRepository _positionRepository;

    public ScheduleExcelController(ISchedulerService schedulerService, ISoldierRepository soldierRepository, IPositionRepository positionRepository)
    {
        _schedulerService = schedulerService;
        _soldierRepository = soldierRepository;
        _positionRepository = positionRepository;
    }

    /// <summary>
    /// מעלה קובץ אקסל (שורות: תאריך, שעת התחלה, שעת סיום, עמדה, חייל) ומחזיר סטטיסטיקות לכל אדם + בדיקת אילוצים.
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(5_242_880)] // 5 MB
    public async Task<ActionResult<ScheduleValidationReport>> UploadExcel(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("נא להעלות קובץ אקסל.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls")
            return BadRequest("הקובץ חייב להיות אקסל (.xlsx).");

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        stream.Position = 0;

        try
        {
            var parseResult = ParseExcelToStats(stream);
            var report = parseResult.Report;
            if (parseResult.Slots.Count > 0)
            {
                var soldiers = await _soldierRepository.GetAllAsync();
                var positions = await _positionRepository.GetAllAsync();
                var nameToSoldierId = soldiers.Where(s => !string.IsNullOrEmpty(s.Name)).ToDictionary(s => s.Name.Trim(), s => s.Id, StringComparer.OrdinalIgnoreCase);
                var nameToPositionId = positions.Where(p => !string.IsNullOrEmpty(p.Name)).ToDictionary(p => p.Name.Trim(), p => p.Id, StringComparer.OrdinalIgnoreCase);
                var schedule = parseResult.Slots.Select((slot, idx) => new DaySchedule
                {
                    Date = slot.DateStr,
                    ShiftNumber = idx + 1,
                    Start = slot.Start,
                    End = slot.End,
                    Assignments = slot.Assignments.Select(a => new ShiftAssignment
                    {
                        SoldierId = nameToSoldierId.TryGetValue(a.SoldierName.Trim(), out var sid) ? sid : a.SoldierName,
                        SoldierName = a.SoldierName,
                        PositionId = nameToPositionId.TryGetValue(a.PositionName.Trim(), out var pid) ? pid : a.PositionName,
                        PositionName = a.PositionName
                    }).ToList()
                }).ToList();
                var constraintsResult = await _schedulerService.ValidateScheduleHardConstraintsAsync(schedule);
                report.HardConstraintsValid = constraintsResult.IsValid;
                report.HardConstraintViolations = constraintsResult.Violations ?? new List<string>();
            }
            return Ok(report);
        }
        catch (Exception ex)
        {
            return BadRequest($"שגיאה בקריאת הקובץ: {ex.Message}");
        }
    }

    private static ParseExcelResult ParseExcelToStats(Stream excelStream)
    {
        var report = new ScheduleValidationReport();
        using var workbook = new XLWorkbook(excelStream);
        var sheet = workbook.Worksheet(1);
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow < 2)
            return new ParseExcelResult { Report = report, Slots = new List<ExcelSlot>() };

        var headerRow = sheet.Row(1);
        var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;

        // זיהוי פורמט מטריצה: יום | תאריך | שעה | ש.ג | אחורי | מנהלתי | מבצעי (עמדות כעמודות)
        bool isMatrixFormat = false;
        int colDate = -1, colTime = -1;
        var positionCols = new List<int>();
        for (int c = 1; c <= lastCol; c++)
        {
            var h = GetCellString(headerRow.Cell(c)).Trim();
            if (h.Contains("תאריך") || h.Equals("date", StringComparison.OrdinalIgnoreCase)) colDate = c;
            else if (h.Contains("שעה") || h.Equals("time", StringComparison.OrdinalIgnoreCase)) colTime = c;
        }
        if (colDate > 0 && colTime > 0 && lastCol > colTime)
        {
            isMatrixFormat = true;
            for (int c = colTime + 1; c <= lastCol; c++)
                positionCols.Add(c);
        }

        Dictionary<string, List<(DateTime Start, DateTime End, bool IsStandby)>> soldierShifts;
        List<ExcelSlot> slots;

        if (isMatrixFormat)
        {
            (soldierShifts, slots) = ParseMatrixFormat(sheet, lastRow, headerRow, colDate, colTime, positionCols);
        }
        else
        {
            // פורמט רשימה: תאריך, שעת התחלה, שעת סיום, עמדה, חייל
            int colStart = -1, colEnd = -1, colPosition = -1, colSoldier = -1;
            foreach (var cell in headerRow.CellsUsed())
            {
                var v = GetCellString(cell).Trim().ToLowerInvariant();
                if (v.Contains("תאריך") || v == "date") colDate = colDate <= 0 ? cell.Address.ColumnNumber : colDate;
                else if (v.Contains("שעת התחלה") || v.Contains("start") || v == "שעה") colStart = cell.Address.ColumnNumber;
                else if (v.Contains("שעת סיום") || v.Contains("end")) colEnd = cell.Address.ColumnNumber;
                else if (v.Contains("עמדה") || v.Contains("position") || v.Contains("משמרת")) colPosition = cell.Address.ColumnNumber;
                else if (v.Contains("חייל") || v.Contains("soldier") || v.Contains("שם")) colSoldier = cell.Address.ColumnNumber;
            }
            if (colSoldier <= 0) throw new InvalidOperationException("לא נמצאה עמודת חייל/שם.");
            if (colDate <= 0) throw new InvalidOperationException("לא נמצאה עמודת תאריך.");
            if (colStart <= 0) colStart = colDate + 1;
            if (colEnd <= 0) colEnd = colStart + 1;
            if (colPosition <= 0) colPosition = colEnd + 1;

            soldierShifts = new Dictionary<string, List<(DateTime Start, DateTime End, bool IsStandby)>>();
            var slotDict = new Dictionary<(DateTime date, DateTime start, DateTime end), List<(string PositionName, string SoldierName)>>();
            for (int r = 2; r <= lastRow; r++)
            {
                var row = sheet.Row(r);
                var soldierName = GetCellString(row.Cell(colSoldier));
                if (string.IsNullOrWhiteSpace(soldierName)) continue;
                if (!TryParseDate(row.Cell(colDate), out var date)) continue;
                TryParseTime(row.Cell(colStart), date, out var start);
                TryParseTime(row.Cell(colEnd), date, out var end);
                if (end < start) end = end.AddDays(1);
                var positionName = GetCellString(row.Cell(colPosition));
                var isStandby = positionName.Contains("כוננות", StringComparison.OrdinalIgnoreCase);
                if (!soldierShifts.ContainsKey(soldierName)) soldierShifts[soldierName] = new List<(DateTime, DateTime, bool)>();
                soldierShifts[soldierName].Add((start, end, isStandby));
                var key = (date, start, end);
                if (!slotDict.ContainsKey(key)) slotDict[key] = new List<(string, string)>();
                slotDict[key].Add((positionName, soldierName));
            }
            slots = slotDict.Select(kv => new ExcelSlot(kv.Key.start, kv.Key.end, kv.Key.date.ToString("yyyy-MM-dd"), kv.Value)).ToList();
        }

        // חישוב סטטיסטיקות (כמו ב־GetScheduleValidationReportAsync)
        bool IsNightShift(DateTime start, DateTime end)
        {
            var h = start.Hour;
            return h == 0 || h == 3 || h == 6;
        }

        double? globalMinGap = null;
        double? globalMaxGap = null;

        foreach (var kv in soldierShifts)
        {
            var guardOnly = kv.Value.Where(t => !t.IsStandby).OrderBy(t => t.Start).ToList();
            var totalShifts = kv.Value.Count;
            var guardCount = guardOnly.Count;
            var nightCount = guardOnly.Count(t => IsNightShift(t.Start, t.End));
            double? minGap = null;
            double? maxGap = null;
            double? averageGap = null;

            if (guardOnly.Count >= 2)
            {
                double sumGap = 0;
                for (int i = 1; i < guardOnly.Count; i++)
                {
                    var gap = (guardOnly[i].Start - guardOnly[i - 1].End).TotalHours;
                    sumGap += gap;
                    if (!minGap.HasValue || gap < minGap.Value) minGap = gap;
                    if (!maxGap.HasValue || gap > maxGap.Value) maxGap = gap;
                }
                averageGap = sumGap / (guardOnly.Count - 1);
                if (!globalMinGap.HasValue || minGap < globalMinGap) globalMinGap = minGap;
                if (!globalMaxGap.HasValue || maxGap > globalMaxGap) globalMaxGap = maxGap;
            }

            report.PerSoldierStats.Add(new SoldierScheduleStats
            {
                SoldierId = kv.Key,
                SoldierName = kv.Key,
                TotalShifts = totalShifts,
                GuardCount = guardCount,
                NightGuardCount = nightCount,
                AverageGapHours = averageGap,
                MinGapHours = minGap,
                MaxGapHours = maxGap
            });
        }

        if (report.PerSoldierStats.Any())
        {
            var minGuards = report.PerSoldierStats.Min(s => s.GuardCount);
            var maxGuards = report.PerSoldierStats.Max(s => s.GuardCount);
            report.SoldiersWithFewestGuards = report.PerSoldierStats.Where(s => s.GuardCount == minGuards).Select(s => s.SoldierName).ToList();
            report.SoldiersWithMostGuards = report.PerSoldierStats.Where(s => s.GuardCount == maxGuards).Select(s => s.SoldierName).ToList();

            var minNights = report.PerSoldierStats.Min(s => s.NightGuardCount);
            var maxNights = report.PerSoldierStats.Max(s => s.NightGuardCount);
            report.SoldiersWithFewestNights = report.PerSoldierStats.Where(s => s.NightGuardCount == minNights).Select(s => s.SoldierName).ToList();
            report.SoldiersWithMostNights = report.PerSoldierStats.Where(s => s.NightGuardCount == maxNights).Select(s => s.SoldierName).ToList();

            if (globalMinGap.HasValue)
            {
                report.ShortestGapHours = globalMinGap.Value;
                report.SoldiersWithShortestGap = report.PerSoldierStats.Where(s => s.MinGapHours.HasValue && Math.Abs(s.MinGapHours.Value - globalMinGap.Value) < 0.01).Select(s => s.SoldierName).ToList();
            }
            if (globalMaxGap.HasValue)
            {
                report.LongestGapHours = globalMaxGap.Value;
                report.SoldiersWithLongestGap = report.PerSoldierStats.Where(s => s.MaxGapHours.HasValue && Math.Abs(s.MaxGapHours.Value - globalMaxGap.Value) < 0.01).Select(s => s.SoldierName).ToList();
            }
        }

        return new ParseExcelResult { Report = report, Slots = slots };
    }

    /// <summary>פורמט מטריצה: שורה = תאריך (או ריק), טווח שעה (12:00-15:00), עמודות = עמדות עם שם חייל.</summary>
    private static (Dictionary<string, List<(DateTime Start, DateTime End, bool IsStandby)>> soldierShifts, List<ExcelSlot> slots) ParseMatrixFormat(
        IXLWorksheet sheet, int lastRow, IXLRow headerRow, int colDate, int colTime, List<int> positionCols)
    {
        var soldierShifts = new Dictionary<string, List<(DateTime Start, DateTime End, bool IsStandby)>>(StringComparer.OrdinalIgnoreCase);
        var slots = new List<ExcelSlot>();
        DateTime? lastDate = null;
        var dateFormats = new[] { "dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd", "dd/MM/yyyy" };

        for (int r = 2; r <= lastRow; r++)
        {
            var row = sheet.Row(r);
            var dateStr = GetCellString(row.Cell(colDate)).Trim();
            if (!string.IsNullOrEmpty(dateStr) && TryParseDateString(dateStr, dateFormats, out var parsedDate))
                lastDate = parsedDate;
            if (!lastDate.HasValue) continue;

            var date = lastDate.Value;
            var timeRangeStr = GetCellString(row.Cell(colTime)).Trim();
            if (string.IsNullOrEmpty(timeRangeStr) || !TryParseTimeRange(timeRangeStr, date, out var start, out var end))
                continue;

            var assignments = new List<(string PositionName, string SoldierName)>();
            foreach (var col in positionCols)
            {
                var soldierName = GetCellString(row.Cell(col)).Trim();
                if (string.IsNullOrWhiteSpace(soldierName)) continue;
                var positionName = GetCellString(headerRow.Cell(col)).Trim();
                var isStandby = positionName.Contains("כוננות", StringComparison.OrdinalIgnoreCase);

                if (!soldierShifts.ContainsKey(soldierName))
                    soldierShifts[soldierName] = new List<(DateTime, DateTime, bool)>();
                soldierShifts[soldierName].Add((start, end, isStandby));
                assignments.Add((positionName, soldierName));
            }
            if (assignments.Count > 0)
                slots.Add(new ExcelSlot(start, end, date.ToString("yyyy-MM-dd"), assignments));
        }

        return (soldierShifts, slots);
    }

    private static bool TryParseDateString(string s, string[] formats, out DateTime date)
    {
        date = default;
        return DateTime.TryParseExact(s, formats, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out date);
    }

    private static bool TryParseTimeRange(string range, DateTime date, out DateTime start, out DateTime end)
    {
        start = date;
        end = date;
        var parts = range.Split('-');
        if (parts.Length != 2) return false;
        var startStr = parts[0].Trim();
        var endStr = parts[1].Trim();
        if (!TimeSpan.TryParse(startStr, out var tsStart) || !TimeSpan.TryParse(endStr, out var tsEnd))
        {
            if (DateTime.TryParse(startStr, out var dtStart) && DateTime.TryParse(endStr, out var dtEnd))
            {
                start = date.Add(dtStart.TimeOfDay);
                end = date.Add(dtEnd.TimeOfDay);
                if (end < start) end = end.AddDays(1);
                return true;
            }
            return false;
        }
        start = date.Add(tsStart);
        end = date.Add(tsEnd);
        if (end < start) end = end.AddDays(1);
        return true;
    }

    private static string GetCellString(IXLCell cell)
    {
        try
        {
            return cell.GetString()?.Trim() ?? "";
        }
        catch
        {
            return cell.Value.ToString()?.Trim() ?? "";
        }
    }

    private static bool TryParseDate(IXLCell cell, out DateTime date)
    {
        date = default;
        try
        {
            var dt = cell.GetDateTime();
            date = dt.Date;
            return true;
        }
        catch { }
        var s = GetCellString(cell);
        if (!string.IsNullOrEmpty(s) && DateTime.TryParse(s, out var parsed))
        {
            date = parsed.Date;
            return true;
        }
        try
        {
            var num = cell.GetDouble();
            date = DateTime.FromOADate(num).Date;
            return true;
        }
        catch { }
        return false;
    }

    private static void TryParseTime(IXLCell cell, DateTime date, out DateTime result)
    {
        result = date;
        try
        {
            var dtVal = cell.GetDateTime();
            result = date.Add(dtVal.TimeOfDay);
            return;
        }
        catch { }
        try
        {
            var num = cell.GetDouble();
            if (num >= 0 && num < 24)
            {
                result = date.AddHours(Math.Floor(num)).AddMinutes((num % 1) * 60);
                return;
            }
            result = DateTime.FromOADate(num);
            return;
        }
        catch { }
        var s = GetCellString(cell);
        if (!string.IsNullOrEmpty(s))
        {
            if (TimeSpan.TryParse(s, out var ts))
            {
                result = date.Add(ts);
                return;
            }
            if (DateTime.TryParse(s, out var dtParsed))
            {
                result = date.Add(dtParsed.TimeOfDay);
                return;
            }
            if (double.TryParse(s, System.Globalization.NumberStyles.Any, null, out var h) && h >= 0 && h < 24)
            {
                result = date.AddHours(h);
                return;
            }
        }
    }

    private sealed class ExcelSlot
    {
        public DateTime Start { get; }
        public DateTime End { get; }
        public string DateStr { get; }
        public List<(string PositionName, string SoldierName)> Assignments { get; }

        public ExcelSlot(DateTime start, DateTime end, string dateStr, List<(string PositionName, string SoldierName)> assignments)
        {
            Start = start;
            End = end;
            DateStr = dateStr;
            Assignments = assignments;
        }
    }

    private sealed class ParseExcelResult
    {
        public ScheduleValidationReport Report { get; set; } = new();
        public List<ExcelSlot> Slots { get; set; } = new();
    }
}
