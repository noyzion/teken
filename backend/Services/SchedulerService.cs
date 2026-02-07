using ShiftScheduler.API.Interfaces;
using ShiftScheduler.API.Models;

namespace ShiftScheduler.API.Services;

/// <summary>
/// Scheduler service implementation
/// Single Responsibility: Handles schedule generation logic
/// Open/Closed: Can be extended without modification
/// </summary>
public class SchedulerService : ISchedulerService
{
    private readonly IPositionRepository _positionRepository;
    private readonly ISoldierRepository _soldierRepository;
    private readonly ISettingsService _settingsService;

    public SchedulerService(
        IPositionRepository positionRepository,
        ISoldierRepository soldierRepository,
        ISettingsService settingsService)
    {
        _positionRepository = positionRepository;
        _soldierRepository = soldierRepository;
        _settingsService = settingsService;
    }

    public async Task<List<DaySchedule>> GenerateScheduleAsync(ScheduleConfig config)
    {
        var positions = await _positionRepository.GetAllAsync();
        var soldiers = await _soldierRepository.GetAllAsync();
        var settings = await _settingsService.GetSettingsAsync();

        if (!positions.Any())
            throw new InvalidOperationException("אין עמדות מוגדרות");
        
        if (!soldiers.Any())
            throw new InvalidOperationException("אין חיילים מוגדרים");

        var schedule = new List<DaySchedule>();
        var soldierAssignments = new Dictionary<string, List<SoldierAssignment>>();

        // Initialize soldier assignments tracking
        foreach (var soldier in soldiers)
        {
            soldierAssignments[soldier.Id] = new List<SoldierAssignment>();
        }

        // כמה פעמים שובץ כל חייל ב־fallback (relaxGap) – למניעת "קורבן קבוע"
        var relaxCountBySoldier = new Dictionary<string, int>();

        // לכל חייל: מינימום שמירות = מספר הימים הזמינים (לפחות שמירה אחת ביום) – בונוס בציון, לא סינון
        var availableDaysPerSoldier = GetAvailableDaysPerSoldier(soldiers, config.StartDate, config.EndDate);
        var soldiersWithFewerAvailableDays = GetSoldiersWithFewerAvailableDaysFromDict(availableDaysPerSoldier);

        // Generate time slots with continuous coverage (no gaps)
        // Number of shifts per day = ceil(available hours / shift length) so every hour is covered
        Console.WriteLine($"מייצר time slots: {settings.ShiftHours} שעות למשמרת, כיסוי רצוף (ללא פערים)");
        var timeSlots = GenerateTimeSlots(config, settings.ShiftHours);
        Console.WriteLine($"נוצרו {timeSlots.Count} time slots");

        // Assign soldiers to positions for each time slot
        TimeSlot? previousSlot = null;
        List<string>? previousGuardSoldiers = null; // חיילים שיורדים משמירה בתקן הקודם
        
        foreach (var slot in timeSlots)
        {
            var daySchedule = new DaySchedule
            {
                Date = slot.Date.ToString("yyyy-MM-dd"),
                ShiftNumber = slot.ShiftNumber,
                Start = slot.Start,
                End = slot.End,
                Assignments = new List<ShiftAssignment>()
            };

            // בקשת שיבוץ ספציפי: האם יש בקשה לשים חייל מסוים במשמרת הזו?
            string? preferredSoldierId = null;
            if (config.ShiftRequests != null && config.ShiftRequests.Any())
            {
                var slotDateStr = slot.Date.ToString("yyyy-MM-dd");
                var slotStartHour = slot.Start.Hour;
                var match = config.ShiftRequests.FirstOrDefault(r =>
                    r.Date == slotDateStr && (r.StartHour == null || r.StartHour == slotStartHour));
                if (match != null)
                    preferredSoldierId = match.SoldierId;
            }

            // Find soldiers who finished guard shifts in the previous slot
            List<string> currentGuardSoldiers = new List<string>();
            
            // First, assign guard positions (שמירה)
            var guardPositions = positions.Where(p => !p.IsStandby).ToList();
            var standbyPositions = positions.Where(p => p.IsStandby).ToList();
            
            foreach (var position in guardPositions)
            {
                var availableSoldiers = GetAvailableSoldiers(
                    soldiers,
                    position,
                    slot,
                    soldierAssignments,
                    soldiersWithFewerAvailableDays
                );

                Soldier? assignedSoldier = null;
                var cameFromFallback = false;

                if (availableSoldiers.Any())
                {
                    // מינימום שמירה ביום זמין – בונוס בציון בלבד (לא סינון), כדי לא לשבור גאפ אידיאלי
                    assignedSoldier = SelectBestSoldier(
                        availableSoldiers,
                        position,
                        slot,
                        soldierAssignments,
                        preferredSoldierId,
                        previousSlot,
                        previousGuardSoldiers,
                        soldiersWithFewerAvailableDays,
                        availableDaysPerSoldier,
                        relaxCountBySoldier
                    );
                }
                else
                {
                    // Fallback: מרככים רק גאפ (relaxGap), שומרים על ימים/שעות/עמדות אסורות
                    var fallbackSoldiers = GetAvailableSoldiers(soldiers, position, slot, soldierAssignments, soldiersWithFewerAvailableDays, relaxGap: true);
                    if (fallbackSoldiers.Any())
                    {
                        cameFromFallback = true;
                        assignedSoldier = SelectBestSoldier(
                            fallbackSoldiers,
                            position,
                            slot,
                            soldierAssignments,
                            preferredSoldierId,
                            previousSlot,
                            previousGuardSoldiers,
                            soldiersWithFewerAvailableDays,
                            availableDaysPerSoldier,
                            relaxCountBySoldier
                        );
                        if (assignedSoldier != null)
                            relaxCountBySoldier[assignedSoldier.Id] = relaxCountBySoldier.GetValueOrDefault(assignedSoldier.Id, 0) + 1;
                    }
                }

                if (assignedSoldier != null)
                {
                    daySchedule.Assignments.Add(new ShiftAssignment
                    {
                        PositionId = position.Id,
                        PositionName = position.Name,
                        SoldierId = assignedSoldier.Id,
                        SoldierName = assignedSoldier.Name,
                        IsForced = cameFromFallback
                    });

                    soldierAssignments[assignedSoldier.Id].Add(new SoldierAssignment
                    {
                        Date = slot.Date,
                        ShiftNumber = slot.ShiftNumber,
                        PositionId = position.Id,
                        IsStandbyPosition = position.IsStandby,
                        ShiftStart = slot.Start,
                        ShiftEnd = slot.End
                    });
                    
                    // Track guard soldiers for next slot
                    currentGuardSoldiers.Add(assignedSoldier.Id);
                }
            }
            
            // Now assign standby positions (כוננות) - prioritize soldiers who finished guard in previous slot, רק אם לא עמוסים מדי
            var soldiersFromPreviousGuardForStandby = new List<Soldier>();
            if (previousGuardSoldiers != null && previousGuardSoldiers.Any() && standbyPositions.Any())
            {
                var averageAssignments = soldierAssignments.Values.Average(v => v.Count);
                var testPosition = standbyPositions.First();
                soldiersFromPreviousGuardForStandby = soldiers
                    .Where(s => previousGuardSoldiers.Contains(s.Id))
                    .Where(s => soldierAssignments[s.Id].Count <= averageAssignments + 1) // לא לתת כוננות למי שכבר עמוס מאוד
                    .Where(s => GetAvailableSoldiers(
                        new List<Soldier> { s },
                        testPosition,
                        slot,
                        soldierAssignments,
                        soldiersWithFewerAvailableDays
                    ).Any())
                    .ToList();
            }
            
            // Group soldiers from previous guard by 4
            var guardGroups = new List<List<Soldier>>();
            for (int i = 0; i < soldiersFromPreviousGuardForStandby.Count; i += 4)
            {
                guardGroups.Add(soldiersFromPreviousGuardForStandby.Skip(i).Take(4).ToList());
            }
            
            int currentGuardGroupIndex = 0;
            int currentGuardGroupSoldierIndex = 0;
            
            foreach (var position in standbyPositions)
            {
                var availableSoldiers = GetAvailableSoldiers(
                    soldiers,
                    position,
                    slot,
                    soldierAssignments,
                    soldiersWithFewerAvailableDays
                );

                Soldier? assignedSoldier = null;
                var standbyCameFromFallback = false;

                if (availableSoldiers.Any())
                {
                    // First priority: assign soldiers from previous guard shift in groups of 4
                    if (currentGuardGroupIndex < guardGroups.Count && guardGroups.Count > 0)
                    {
                        var currentGroup = guardGroups[currentGuardGroupIndex];
                        if (currentGroup != null && currentGuardGroupSoldierIndex < currentGroup.Count)
                        {
                            var candidateSoldier = currentGroup[currentGuardGroupSoldierIndex];
                            // Check if this soldier is available and not already assigned in this slot
                            if (candidateSoldier != null && 
                                availableSoldiers.Any(s => s.Id == candidateSoldier.Id) &&
                                !daySchedule.Assignments.Any(a => a.SoldierId == candidateSoldier.Id))
                            {
                                assignedSoldier = candidateSoldier;
                                currentGuardGroupSoldierIndex++;
                                
                                // If we've assigned all soldiers in this group, move to next group
                                if (currentGuardGroupSoldierIndex >= currentGroup.Count)
                                {
                                    currentGuardGroupIndex++;
                                    currentGuardGroupSoldierIndex = 0;
                                }
                            }
                            else
                            {
                                // This soldier is not available, skip to next in group
                                currentGuardGroupSoldierIndex++;
                                if (currentGuardGroupSoldierIndex >= currentGroup.Count)
                                {
                                    currentGuardGroupIndex++;
                                    currentGuardGroupSoldierIndex = 0;
                                }
                            }
                        }
                        else if (currentGroup != null && currentGuardGroupSoldierIndex >= currentGroup.Count)
                        {
                            // Move to next group if we've exhausted current group
                            currentGuardGroupIndex++;
                            currentGuardGroupSoldierIndex = 0;
                        }
                    }
                    
                    // If no soldier from previous guard available, use normal selection
                    if (assignedSoldier == null)
                    {
                        assignedSoldier = SelectBestSoldier(
                            availableSoldiers,
                            position,
                            slot,
                            soldierAssignments,
                            preferredSoldierId,
                            previousSlot,
                            previousGuardSoldiers,
                            soldiersWithFewerAvailableDays,
                            availableDaysPerSoldier,
                            relaxCountBySoldier
                        );
                    }
                }
                else
                {
                    var fallbackSoldiers = GetAvailableSoldiers(soldiers, position, slot, soldierAssignments, soldiersWithFewerAvailableDays, relaxGap: true);
                    if (fallbackSoldiers.Any())
                    {
                        standbyCameFromFallback = true;
                        assignedSoldier = SelectBestSoldier(
                            fallbackSoldiers,
                            position,
                            slot,
                            soldierAssignments,
                            preferredSoldierId,
                            previousSlot,
                            previousGuardSoldiers,
                            soldiersWithFewerAvailableDays,
                            availableDaysPerSoldier,
                            relaxCountBySoldier
                        );
                        if (assignedSoldier != null)
                            relaxCountBySoldier[assignedSoldier.Id] = relaxCountBySoldier.GetValueOrDefault(assignedSoldier.Id, 0) + 1;
                    }
                }

                if (assignedSoldier != null)
                {
                    daySchedule.Assignments.Add(new ShiftAssignment
                    {
                        PositionId = position.Id,
                        PositionName = position.Name,
                        SoldierId = assignedSoldier.Id,
                        SoldierName = assignedSoldier.Name,
                        IsForced = standbyCameFromFallback
                    });

                    soldierAssignments[assignedSoldier.Id].Add(new SoldierAssignment
                    {
                        Date = slot.Date,
                        ShiftNumber = slot.ShiftNumber,
                        PositionId = position.Id,
                        IsStandbyPosition = position.IsStandby,
                        ShiftStart = slot.Start,
                        ShiftEnd = slot.End
                    });
                }
            }

            schedule.Add(daySchedule);
            Console.WriteLine($"משמרת {slot.ShiftNumber}: {daySchedule.Date}, {daySchedule.Assignments.Count} שיבוצים");
            
            // Update for next iteration
            previousSlot = slot;
            previousGuardSoldiers = currentGuardSoldiers;
        }

        Console.WriteLine($"סה\"כ נוצרו {schedule.Count} משמרות עם {schedule.Sum(s => s.Assignments.Count)} שיבוצים");
        return schedule;
    }

    private List<TimeSlot> GenerateTimeSlots(ScheduleConfig config, double shiftHours)
    {
        var timeSlots = new List<TimeSlot>();
        var startDate = config.StartDate.Date;
        var endDate = config.EndDate.Date;
        
        // Get start and end hours (default to full day if not specified)
        var hasStartHour = config.StartHour.HasValue;
        var hasEndHour = config.EndHour.HasValue;
        var startHour = config.StartHour ?? 0;
        var endHour = config.EndHour ?? 23;
        var firstShiftStartHour = config.FirstShiftStartHour ?? 0;
        
        // Calculate total time range
        var totalStartTime = hasStartHour ? startDate.AddHours(startHour) : startDate;
        var totalEndTime = hasEndHour ? endDate.AddHours(endHour) : endDate.AddDays(1).AddSeconds(-1);
        
        var currentDate = startDate;
        var shiftNumber = 0;
        var fullDayCoverage = !hasStartHour && !hasEndHour;

        Console.WriteLine($"טווח זמן: {totalStartTime} עד {totalEndTime}, שעת משמרת ראשונה={firstShiftStartHour}");
        
        while (currentDate <= endDate)
        {
            // Determine the hour range for this day
            var isFirstDay = currentDate.Date == startDate;
            var isLastDay = currentDate.Date == endDate;
            
            var dayStartHour = (isFirstDay && hasStartHour) ? startHour : 0;
            var dayEndHour = (isLastDay && hasEndHour) ? endHour : 23;
            
            // Available hours in this day (e.g. 0-23 = 24 hours)
            var dayAvailableHours = dayEndHour >= dayStartHour 
                ? (dayEndHour - dayStartHour + 1) 
                : (24 - dayStartHour + dayEndHour + 1);
            
            // Number of consecutive shifts needed to cover the day (no gaps)
            var shiftsPerDay = fullDayCoverage 
                ? (int)Math.Ceiling(24 / shiftHours) 
                : (int)Math.Ceiling(dayAvailableHours / shiftHours);
            
            var baseStartHour = fullDayCoverage ? firstShiftStartHour : dayStartHour;
            
            Console.WriteLine($"יום {currentDate:yyyy-MM-dd}: בסיס={baseStartHour}, {shiftsPerDay} משמרות רצופות");
            
            int slotsAddedForDay = 0;
            for (int shiftNum = 0; shiftNum < shiftsPerDay; shiftNum++)
            {
                // Consecutive shifts from base: e.g. 6-9, 9-12, 12-15, ... 21-00, 00-3, 3-6 (AddHours handles overflow)
                var shiftStartOffset = baseStartHour + (shiftNum * shiftHours);
                var shiftStart = currentDate.AddHours(shiftStartOffset);
                var shiftEnd = shiftStart.AddHours(shiftHours);
                
                // First day only: skip shifts that end before the range start (e.g. before start hour)
                if (isFirstDay && shiftEnd <= totalStartTime)
                {
                    Console.WriteLine($"  משמרת {shiftNum}: דילוג (יום ראשון) - shiftEnd ({shiftEnd}) <= totalStartTime ({totalStartTime})");
                    continue;
                }
                // Last day only: skip shifts that start after the range end (e.g. after end hour)
                if (isLastDay && shiftStart >= totalEndTime)
                {
                    Console.WriteLine($"  משמרת {shiftNum}: דילוג (יום אחרון) - shiftStart ({shiftStart}) >= totalEndTime ({totalEndTime})");
                    continue;
                }
                // Middle days: always include all shifts (full 24-hour coverage, no gaps)

                timeSlots.Add(new TimeSlot
                {
                    Date = currentDate,
                    ShiftNumber = shiftNumber++,
                    Start = shiftStart,
                    End = shiftEnd
                });
                slotsAddedForDay++;
                Console.WriteLine($"  משמרת {shiftNum}: נוספה - {shiftStart:yyyy-MM-dd HH:mm} עד {shiftEnd:yyyy-MM-dd HH:mm}");
            }
            Console.WriteLine($"  סה\"כ נוספו {slotsAddedForDay} משמרות ליום {currentDate:yyyy-MM-dd}");
            currentDate = currentDate.AddDays(1);
        }

        return timeSlots;
    }

    /// <summary>
    /// לכל חייל: מספר הימים הזמינים (ימים שבהם אין אילוץ יום אסור).
    /// משמש למינימום שמירות: לכל חייל לפחות שמירה אחת ביום זמין.
    /// </summary>
    private static Dictionary<string, int> GetAvailableDaysPerSoldier(List<Soldier> soldiers, DateTime startDate, DateTime endDate)
    {
        var result = new Dictionary<string, int>();
        foreach (var soldier in soldiers)
        {
            int count = 0;
            for (var d = startDate.Date; d <= endDate.Date; d = d.AddDays(1))
            {
                var dayOfWeek = (int)d.DayOfWeek;
                if (soldier.Constraints?.ForbiddenDaysOfWeek == null || !soldier.Constraints.ForbiddenDaysOfWeek.Any())
                    count++;
                else if (!soldier.Constraints.ForbiddenDaysOfWeek.Contains(dayOfWeek))
                    count++;
            }
            result[soldier.Id] = count;
        }
        return result;
    }

    /// <summary>
    /// מזהי חיילים שנמצאים פחות ימים מכולם – נאפשר להם רווח קטן יותר בין שמירות.
    /// </summary>
    private static HashSet<string> GetSoldiersWithFewerAvailableDaysFromDict(Dictionary<string, int> availableDaysPerSoldier)
    {
        var result = new HashSet<string>();
        if (availableDaysPerSoldier.Count == 0) return result;
        var maxDays = availableDaysPerSoldier.Values.Max();
        foreach (var kv in availableDaysPerSoldier)
            if (kv.Value < maxDays)
                result.Add(kv.Key);
        return result;
    }

    /// <summary>
    /// זמינים לשיבוץ: אילוצים קשיחים (סעיף 1) – לעולם לא מפרים.
    /// א. זמינות: ימים/שעות/טווחים אסורים, טווחים חוצי ימים. ב. עמדות: עמדות אסורות, מפקד. ג. לא באותו סלוט פעמיים. חפיפות: אין חפיפה.
    /// רווח מינימלי (סעיף 2.א) – אלא אם relaxGap (fallback, סעיף 5: כמעט אף פעם לא).
    /// </summary>
    private List<Soldier> GetAvailableSoldiers(
        List<Soldier> soldiers,
        Position position,
        TimeSlot slot,
        Dictionary<string, List<SoldierAssignment>> soldierAssignments,
        HashSet<string>? soldiersWithFewerAvailableDays = null,
        bool relaxGap = false)
    {
        var shiftStartHour = slot.Start.Hour;
        var shiftEndHour = slot.End.Hour;

        return soldiers.Where(soldier =>
        {
            // 1.ב דרישות עמדה – מפקד
            if (position.RequiresCommander && !soldier.IsCommander)
                return false;

            // 1.א 1.ב אילוצים – ימים/שעות/עמדות אסורות
            if (soldier.Constraints != null)
            {
                var dayOfWeek = (int)slot.Date.DayOfWeek; // C# DayOfWeek: Sunday=0, Monday=1, ..., Saturday=6
                var dayOfWeekStr = dayOfWeek.ToString();

                // Forbidden days of week constraint - ימים שלמים שהחייל לא נמצא
                if (soldier.Constraints.ForbiddenDaysOfWeek != null &&
                    soldier.Constraints.ForbiddenDaysOfWeek.Any())
                {
                    if (soldier.Constraints.ForbiddenDaysOfWeek.Contains(dayOfWeek))
                        return false;
                }

                // Forbidden hours by day constraint - שעות אסורות לפי יום
                if (soldier.Constraints.ForbiddenHoursByDay != null &&
                    soldier.Constraints.ForbiddenHoursByDay.ContainsKey(dayOfWeekStr))
                {
                    var forbiddenHoursForDay = soldier.Constraints.ForbiddenHoursByDay[dayOfWeekStr];
                    if (forbiddenHoursForDay != null && forbiddenHoursForDay.Any())
                    {
                        // Check if any hour in the shift is forbidden for this day
                        var shiftHours = new List<int>();
                        
                        // Handle shifts that cross midnight
                        if (shiftEndHour < shiftStartHour)
                        {
                            // Shift crosses midnight - check both days
                            for (int h = shiftStartHour; h < 24; h++)
                                shiftHours.Add(h);
                            for (int h = 0; h < shiftEndHour; h++)
                                shiftHours.Add(h);
                            
                            // Also check next day if shift crosses midnight
                            var nextDay = (dayOfWeek + 1) % 7;
                            var nextDayStr = nextDay.ToString();
                            if (soldier.Constraints.ForbiddenHoursByDay.ContainsKey(nextDayStr))
                            {
                                var forbiddenHoursNextDay = soldier.Constraints.ForbiddenHoursByDay[nextDayStr];
                                if (forbiddenHoursNextDay != null && forbiddenHoursNextDay.Any())
                                {
                                    // Check hours after midnight
                                    for (int h = 0; h < shiftEndHour; h++)
                                    {
                                        if (forbiddenHoursNextDay.Contains(h))
                                            return false;
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Normal shift
                            for (int h = shiftStartHour; h < shiftEndHour; h++)
                                shiftHours.Add(h);
                        }
                        
                        // Check if any forbidden hour overlaps with shift hours
                        if (shiftHours.Any(h => forbiddenHoursForDay.Contains(h)))
                            return false;
                    }
                }

                // Forbidden hour ranges by day constraint - טווחי שעות אסורות לפי יום
                if (soldier.Constraints.ForbiddenHourRangesByDay != null)
                {
                    // Check ranges that start on current day
                    if (soldier.Constraints.ForbiddenHourRangesByDay.ContainsKey(dayOfWeekStr))
                    {
                        var forbiddenRangesForDay = soldier.Constraints.ForbiddenHourRangesByDay[dayOfWeekStr];
                        if (forbiddenRangesForDay != null && forbiddenRangesForDay.Any())
                        {
                            foreach (var range in forbiddenRangesForDay)
                            {
                                // Check if shift overlaps with forbidden range
                                if (IsTimeRangeOverlapping(shiftStartHour, shiftEndHour, dayOfWeek, dayOfWeek, range))
                                    return false;
                            }
                        }
                    }
                    
                    // Check ranges that end on current day (ranges that started on previous day)
                    foreach (var kvp in soldier.Constraints.ForbiddenHourRangesByDay)
                    {
                        var rangeStartDay = int.Parse(kvp.Key);
                        var ranges = kvp.Value;
                        if (ranges == null) continue;
                        
                        foreach (var range in ranges)
                        {
                            if (range.EndDay.HasValue && range.EndDay.Value == dayOfWeek)
                            {
                                // This range ends on current day, check if shift overlaps
                                if (IsTimeRangeOverlapping(shiftStartHour, shiftEndHour, dayOfWeek, rangeStartDay, range))
                                    return false;
                            }
                        }
                    }
                }

                // Forbidden positions constraint
                if (soldier.Constraints.ForbiddenPositions != null &&
                    soldier.Constraints.ForbiddenPositions.Contains(position.Id))
                    return false;
            }

            // 1.א חייל לא בשתי עמדות באותו סלוט
            var existingAssignment = soldierAssignments[soldier.Id].FirstOrDefault(
                a => a.Date.Date == slot.Date.Date && a.ShiftNumber == slot.ShiftNumber);

            if (existingAssignment != null)
                return false;

            // 1.ג חפיפות + 2.א 2.ב 2.ג רווח מינימלי/רצוי/מניעת קיצוניות (אלא אם relaxGap – fallback בלבד)
            var assignments = soldierAssignments[soldier.Id];
            if (assignments.Any())
            {
                if (!position.IsStandby)
                {
                    // For guard positions (שמירה) - gap between guard shifts. Hard 12h, normal 15h; הקלה רק בין 15 ל־12 (לא ל־4).
                    var hasManyConstraints = HasManyConstraints(soldier);
                    var hasFewerAvailableDays = soldiersWithFewerAvailableDays != null && soldiersWithFewerAvailableDays.Contains(soldier.Id);
                    var useSmallerGap = hasManyConstraints || hasFewerAvailableDays;
                    var hardMinGapHours = 12.0;   // מינימום מוחלט – אין 9/12
                    var normalMinGapHours = 15.0; // מועדף; עם אילוצים מותר 12–15

                    foreach (var guardShift in assignments.Where(a => !a.IsStandbyPosition))
                    {
                        if (slot.Start < guardShift.ShiftEnd && slot.End > guardShift.ShiftStart)
                            return false; // Shifts overlap

                        if (relaxGap)
                            continue; // Fallback: רק מרככים גאפ, שומרים על כל שאר האילוצים

                        if (guardShift.ShiftEnd <= slot.Start)
                        {
                            var gapBefore = (slot.Start - guardShift.ShiftEnd).TotalHours;
                            if (gapBefore < hardMinGapHours)
                                return false;
                            if (!useSmallerGap && gapBefore < normalMinGapHours)
                                return false;
                        }

                        if (slot.End <= guardShift.ShiftStart)
                        {
                            var gapAfter = (guardShift.ShiftStart - slot.End).TotalHours;
                            if (gapAfter < hardMinGapHours)
                                return false;
                            if (!useSmallerGap && gapAfter < normalMinGapHours)
                                return false;
                        }
                    }
                }
                else
                {
                    if (!relaxGap)
                    {
                        var justFinishedGuardShift = assignments
                            .Where(a => !a.IsStandbyPosition)
                            .Any(a => Math.Abs((slot.Start - a.ShiftEnd).TotalHours) < 1.0);

                        if (!justFinishedGuardShift)
                        {
                            var minStandbyGapHours = 4.0;
                            var conflictingStandbyShift = assignments.FirstOrDefault(a => a.IsStandbyPosition &&
                                (((slot.Start - a.ShiftEnd).TotalHours >= 0 && (slot.Start - a.ShiftEnd).TotalHours < minStandbyGapHours) ||
                                 ((a.ShiftStart - slot.End).TotalHours >= 0 && (a.ShiftStart - slot.End).TotalHours < minStandbyGapHours)));

                            if (conflictingStandbyShift != null)
                                return false;
                        }
                    }
                }
            }

            return true;
        }).ToList();
    }

    /// <summary>
    /// ציון גאפ לשמירה: קנס רציף על גאפ קצר, בונוס ל"נשכח" (מניעת 40+), בונוס למי שכבר נפגע.
    /// </summary>
    private static double ComputeGuardGapScore(double hoursSinceLastGuardShift, double minGapBetweenGuardShifts, double preferredForSlot)
    {
        const double idealGapHours = 18.0;
        const double longGapSoftHours = 24.0;   // מתחת ל־24: בונוס קל; מעל 36: בונוס חזק (מניעת 42h)
        const double longGapStrongHours = 36.0;
        const double alreadyHurtThreshold = 12.0;

        double score = hoursSinceLastGuardShift;

        // קנס רציף על גאפ קצר – מחמיר (9h ייענש חזק מול 14h)
        if (hoursSinceLastGuardShift < idealGapHours)
        {
            double diff = idealGapHours - hoursSinceLastGuardShift;
            double penalty = diff * diff / 9.0; // 9h → penalty 9, 14h → ~1.8
            score -= penalty;
        }

        // בונוס ל"נשכח" – מדורג: מעל 24h בונוס קל, מעל 36h בונוס חזק (למחוק 42h)
        if (hoursSinceLastGuardShift > longGapSoftHours)
            score += (hoursSinceLastGuardShift - longGapSoftHours) * 1.5;
        if (hoursSinceLastGuardShift > longGapStrongHours)
            score += (hoursSinceLastGuardShift - longGapStrongHours) * 2.0;

        // soft-cap: מעל 30h לא להמשיך לתגמל – בלימה (27–30–36 לא ימשיכו לטפס)
        const double softCapHours = 30.0;
        if (hoursSinceLastGuardShift > softCapHours)
            score -= (hoursSinceLastGuardShift - softCapHours) * 1.2;

        // בונוס למי שכבר נפגע – לפזר נזק
        if (minGapBetweenGuardShifts < alreadyHurtThreshold && minGapBetweenGuardShifts < double.MaxValue)
            score += (alreadyHurtThreshold - minGapBetweenGuardShifts) * 2.5;

        score += preferredForSlot;
        return score;
    }

    /// <summary>
    /// סדר עדיפויות (סעיף 5): גאפ אידיאלי → הוגנות (3) → רווחים → חיילים עם אילוצים (6) → נוחות.
    /// 4 בקשות אישיות (PreferredForSlot). 3.ג הימנע משני לילות ברצף. 6 לא קורבן קבוע – העדפה לחיילים עם אילוצים.
    /// </summary>
    private Soldier? SelectBestSoldier(
        List<Soldier> availableSoldiers,
        Position position,
        TimeSlot slot,
        Dictionary<string, List<SoldierAssignment>> soldierAssignments,
        string? preferredSoldierId = null,
        TimeSlot? previousSlot = null,
        List<string>? previousGuardSoldiers = null,
        HashSet<string>? soldiersWithFewerAvailableDays = null,
        Dictionary<string, int>? availableDaysPerSoldier = null,
        Dictionary<string, int>? relaxCountBySoldier = null)
    {
        if (!availableSoldiers.Any())
            return null;

        // 3.א 3.ב פיזור שמירות ולילות – הפרש מקסימלי 1
        // Only allow assigning to soldiers with guard count <= min + 1 (never to someone with min+2 or more when others have min/min+1).
        if (!position.IsStandby)
        {
            var minGuardShifts = soldierAssignments.Values.Min(list =>
                list.Count(a => !a.IsStandbyPosition));
            var maxAllowedGuardShifts = minGuardShifts + 1;
            var candidates = availableSoldiers
                .Where(s => soldierAssignments[s.Id].Count(a => !a.IsStandbyPosition) <= maxAllowedGuardShifts)
                .ToList();
            if (candidates.Any())
                availableSoldiers = candidates;
            else
            {
                // No one with <= min+1 available (all constrained) - pick those with smallest count among available
                var minAmongAvailable = availableSoldiers.Min(s =>
                    soldierAssignments[s.Id].Count(a => !a.IsStandbyPosition));
                availableSoldiers = availableSoldiers
                    .Where(s => soldierAssignments[s.Id].Count(a => !a.IsStandbyPosition) == minAmongAvailable)
                    .ToList();
            }

            // לילות: איזון קפדני – הפרש מקסימלי 1 (לא 1 לילה לאחד ו־3 לאחרים). רק מי שיש לו בדיוק מינימום לילות.
            if (IsNightShift(slot.Start, slot.End))
            {
                var minNightGuardShifts = soldierAssignments.Values.Min(list =>
                    list.Count(a => !a.IsStandbyPosition && IsNightShift(a.ShiftStart, a.ShiftEnd)));
                // רק מי שיש לו בדיוק מינימום – כך אחרי שיבוץ יש לו min+1, וההפרש מכל השאר (min) הוא 1
                var withMinNightsOnly = availableSoldiers
                    .Where(s => soldierAssignments[s.Id].Count(a => !a.IsStandbyPosition && IsNightShift(a.ShiftStart, a.ShiftEnd)) == minNightGuardShifts)
                    .ToList();
                if (withMinNightsOnly.Any())
                {
                    availableSoldiers = withMinNightsOnly;
                }
                else
                {
                    // אין אף זמין עם מינימום (כולם כבר min+1 ומעלה) – לוקחים את מי שהכי פחות לילות among הזמינים
                    var minNightAmongAvailable = availableSoldiers.Min(s =>
                        soldierAssignments[s.Id].Count(a => !a.IsStandbyPosition && IsNightShift(a.ShiftStart, a.ShiftEnd)));
                    availableSoldiers = availableSoldiers
                        .Where(s => soldierAssignments[s.Id].Count(a => !a.IsStandbyPosition && IsNightShift(a.ShiftStart, a.ShiftEnd)) == minNightAmongAvailable)
                        .ToList();
                }
            }
        }

        // Calculate total shifts needed for fairness
        var totalShifts = soldierAssignments.Values.Sum(v => v.Count);
        var averageShiftsPerSoldier = totalShifts / (double)soldierAssignments.Count;

        // Calculate guard and night-guard statistics for fairness
        var totalGuardShifts = soldierAssignments.Values.Sum(v => v.Count(a => !a.IsStandbyPosition));
        var totalNightGuardShifts = soldierAssignments.Values.Sum(v =>
            v.Count(a => !a.IsStandbyPosition && IsNightShift(a.ShiftStart, a.ShiftEnd)));

        var averageGuardShiftsPerSoldier = totalGuardShifts / (double)soldierAssignments.Count;
        var averageNightGuardShiftsPerSoldier = totalNightGuardShifts / (double)soldierAssignments.Count;

        var isNightShift = IsNightShift(slot.Start, slot.End);

        // Select soldier with best balance based on position type
        return availableSoldiers
            .Select(s => new
            {
                Soldier = s,
                HoursSinceLastGuardShift = GetHoursSinceLastGuardShift(s.Id, slot.Start, soldierAssignments),
                HoursSinceLastStandbyShift = GetHoursSinceLastStandbyShift(s.Id, slot.Start, soldierAssignments),
                HoursSinceLastShift = GetHoursSinceLastShift(s.Id, slot.Start, soldierAssignments),
                CanBeContinuousFromGuard = CanBeContinuousFromGuard(s.Id, slot.Start, soldierAssignments),
                TotalAssignments = soldierAssignments[s.Id].Count,
                GuardAssignments = soldierAssignments[s.Id].Count(a => !a.IsStandbyPosition),
                NightGuardAssignments = soldierAssignments[s.Id].Count(a => !a.IsStandbyPosition && IsNightShift(a.ShiftStart, a.ShiftEnd)),
                FairnessScore = averageShiftsPerSoldier - soldierAssignments[s.Id].Count, // Positive = below average (preferred)
                GuardFairnessScore = averageGuardShiftsPerSoldier - soldierAssignments[s.Id].Count(a => !a.IsStandbyPosition),
                NightGuardFairnessScore = averageNightGuardShiftsPerSoldier - soldierAssignments[s.Id].Count(a => !a.IsStandbyPosition && IsNightShift(a.ShiftStart, a.ShiftEnd)),
                MinGapBetweenGuardShifts = GetMinGapBetweenGuardShifts(s.Id, soldierAssignments),
                PreferredForSlot = !string.IsNullOrEmpty(preferredSoldierId) && s.Id == preferredSoldierId ? 100000.0 : 0.0, // בקשת שיבוץ ספציפי
                LastGuardWasNight = WasLastGuardShiftNight(s.Id, slot.Start, soldierAssignments),
                HasConstraints = HasManyConstraints(s) || (soldiersWithFewerAvailableDays != null && soldiersWithFewerAvailableDays.Contains(s.Id)),
                BelowMinBonus = availableDaysPerSoldier != null && soldierAssignments[s.Id].Count(a => !a.IsStandbyPosition) < availableDaysPerSoldier.GetValueOrDefault(s.Id, 0) ? 1 : 0,
                RelaxCount = relaxCountBySoldier?.GetValueOrDefault(s.Id, 0) ?? 0
            })
            .OrderByDescending(x =>
            {
                if (!position.IsStandby)
                {
                    // ציון גאפ: קנס רציף על גאפ קצר, בונוס על "נשכח" (גאפ ארוך), בונוס למי שכבר נפגע (לפזר נזק)
                    return ComputeGuardGapScore(
                        x.HoursSinceLastGuardShift,
                        x.MinGapBetweenGuardShifts,
                        x.PreferredForSlot
                    );
                }
                else
                {
                    // For standby positions (כוננות) - continuous from guard, then gap, + בקשת שיבוץ
                    var baseScore = x.CanBeContinuousFromGuard ? 1000.0 : x.HoursSinceLastStandbyShift;
                    return baseScore + x.PreferredForSlot;
                }
            })
            // הימנע משני לילות ברצף (סעיף 3.ג): להעדיף חייל שהשמירה הקודמת שלו לא הייתה לילה
            .ThenByDescending(x => !position.IsStandby && isNightShift ? (x.LastGuardWasNight ? 0 : 1) : 0)
            // Fairness – שוויון שמירות ולילות:
            .ThenByDescending(x =>
            {
                if (!position.IsStandby)
                {
                    if (isNightShift)
                        return x.NightGuardFairnessScore;
                    return x.GuardFairnessScore;
                }
                return x.FairnessScore;
            })
            // במשמרת יום – להעדיף גם מי שיש לו פחות לילות (שוויון לילות לאורך כל הלוח)
            .ThenByDescending(x => !position.IsStandby && !isNightShift ? x.NightGuardFairnessScore : 0)
            // Equalize gaps: prefer soldiers with larger current min gap
            .ThenByDescending(x => !position.IsStandby ? x.MinGapBetweenGuardShifts : 0)
            .ThenByDescending(x => x.FairnessScore)
            // בונוס: מתחת למינימום שמירות ליום זמין (לא סינון – רק העדפה)
            .ThenByDescending(x => x.BelowMinBonus)
            // העדפה לחיילים עם אילוצים / פחות ימים
            .ThenByDescending(x => x.HasConstraints ? 1 : 0)
            // קנס: מי שכבר נשבר (relaxGap) – מניעת "קורבן קבוע"
            .ThenBy(x => x.RelaxCount)
            .ThenBy(x => x.TotalAssignments)
            .FirstOrDefault()?.Soldier;
    }

    private double GetHoursSinceLastShift(
        string soldierId,
        DateTime currentShiftStart,
        Dictionary<string, List<SoldierAssignment>> soldierAssignments)
    {
        var assignments = soldierAssignments[soldierId];
        if (!assignments.Any())
            return double.MaxValue; // No previous shifts = maximum spacing

        // Find the most recent shift before current shift
        var lastShift = assignments
            .Where(a => a.ShiftEnd < currentShiftStart)
            .OrderByDescending(a => a.ShiftEnd)
            .FirstOrDefault();

        if (lastShift == null)
            return 24.0; // At least one day spacing

        // Calculate actual hours between end of last shift and start of current shift
        var timeSinceLastShift = currentShiftStart - lastShift.ShiftEnd;
        return timeSinceLastShift.TotalHours;
    }

    private double GetHoursSinceLastGuardShift(
        string soldierId,
        DateTime currentShiftStart,
        Dictionary<string, List<SoldierAssignment>> soldierAssignments)
    {
        var assignments = soldierAssignments[soldierId];
        if (!assignments.Any())
            return double.MaxValue;

        // Find the most recent guard shift (not standby) before current shift
        var lastGuardShift = assignments
            .Where(a => !a.IsStandbyPosition && a.ShiftEnd < currentShiftStart)
            .OrderByDescending(a => a.ShiftEnd)
            .FirstOrDefault();

        if (lastGuardShift == null)
            return 24.0;

        var timeSinceLastGuardShift = currentShiftStart - lastGuardShift.ShiftEnd;
        return timeSinceLastGuardShift.TotalHours;
    }

    /// <summary>בודק אם השמירה הקודמת של החייל (שמירה, לא כוננות) הייתה לילה – למניעת שני לילות ברצף.</summary>
    private bool WasLastGuardShiftNight(string soldierId, DateTime currentShiftStart, Dictionary<string, List<SoldierAssignment>> soldierAssignments)
    {
        var assignments = soldierAssignments[soldierId];
        var lastGuardShift = assignments
            .Where(a => !a.IsStandbyPosition && a.ShiftEnd < currentShiftStart)
            .OrderByDescending(a => a.ShiftEnd)
            .FirstOrDefault();
        return lastGuardShift != null && IsNightShift(lastGuardShift.ShiftStart, lastGuardShift.ShiftEnd);
    }

    private double GetHoursSinceLastStandbyShift(
        string soldierId,
        DateTime currentShiftStart,
        Dictionary<string, List<SoldierAssignment>> soldierAssignments)
    {
        var assignments = soldierAssignments[soldierId];
        if (!assignments.Any())
            return double.MaxValue;

        // Find the most recent standby shift before current shift
        var lastStandbyShift = assignments
            .Where(a => a.IsStandbyPosition && a.ShiftEnd < currentShiftStart)
            .OrderByDescending(a => a.ShiftEnd)
            .FirstOrDefault();

        if (lastStandbyShift == null)
            return 24.0;

        var timeSinceLastStandbyShift = currentShiftStart - lastStandbyShift.ShiftEnd;
        return timeSinceLastStandbyShift.TotalHours;
    }

    private bool CanBeContinuousFromGuard(
        string soldierId,
        DateTime currentShiftStart,
        Dictionary<string, List<SoldierAssignment>> soldierAssignments)
    {
        var assignments = soldierAssignments[soldierId];
        if (!assignments.Any())
            return false;

        // Check if soldier just finished a guard shift (within 1 hour)
        return assignments
            .Where(a => !a.IsStandbyPosition)
            .Any(a => Math.Abs((currentShiftStart - a.ShiftEnd).TotalHours) < 1.0);
    }

    /// <summary>
    /// Returns the minimum gap (hours) between consecutive guard shifts for this soldier.
    /// Used to equalize gaps: prefer assigning to soldiers with larger min gap so gaps become more similar.
    /// </summary>
    private double GetMinGapBetweenGuardShifts(string soldierId, Dictionary<string, List<SoldierAssignment>> soldierAssignments)
    {
        var guardShifts = soldierAssignments[soldierId]
            .Where(a => !a.IsStandbyPosition)
            .OrderBy(a => a.ShiftEnd)
            .ToList();
        if (guardShifts.Count < 2)
            return double.MaxValue; // No gap yet – prefer for assignment to balance
        double minGap = double.MaxValue;
        for (int i = 1; i < guardShifts.Count; i++)
        {
            var gap = (guardShifts[i].ShiftStart - guardShifts[i - 1].ShiftEnd).TotalHours;
            if (gap < minGap) minGap = gap;
        }
        return minGap;
    }

    /// <summary>
    /// Determines if a shift should be treated as a night shift.
    /// Night is defined as shifts that start from 21:00 or before 06:00.
    /// </summary>
    /// <summary>שמירות לילה = 00–3, 3–6, 6–9 (שעת התחלה 0, 3 או 6).</summary>
    private bool IsNightShift(DateTime shiftStart, DateTime shiftEnd)
    {
        var startHour = shiftStart.Hour;
        return startHour == 0 || startHour == 3 || startHour == 6;
    }

    /// <summary>
    /// Detects if a soldier has "many" constraints, so we can allow slightly smaller gaps
    /// between guard shifts for them in order to keep overall fairness.
    /// </summary>
    private bool HasManyConstraints(Soldier soldier)
    {
        if (soldier.Constraints == null)
            return false;

        int constraintCount = 0;

        if (soldier.Constraints.ForbiddenDaysOfWeek != null)
            constraintCount += soldier.Constraints.ForbiddenDaysOfWeek.Count;

        if (soldier.Constraints.ForbiddenHourRangesByDay != null)
        {
            foreach (var kvp in soldier.Constraints.ForbiddenHourRangesByDay)
            {
                if (kvp.Value != null)
                    constraintCount += kvp.Value.Count;
            }
        }

        if (soldier.Constraints.ForbiddenPositions != null)
            constraintCount += soldier.Constraints.ForbiddenPositions.Count;

        // 3+ separate constraints are considered "many"
        return constraintCount >= 3;
    }

    private class TimeSlot
    {
        public DateTime Date { get; set; }
        public int ShiftNumber { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }

    /// <summary>
    /// Checks if a shift time range overlaps with a forbidden hour range
    /// </summary>
    private bool IsTimeRangeOverlapping(int shiftStartHour, int shiftEndHour, int shiftDayOfWeek, int rangeStartDay, HourRange forbiddenRange)
    {
        // If range has end day, it crosses days
        if (forbiddenRange.EndDay.HasValue)
        {
            int rangeEndDay = forbiddenRange.EndDay.Value;
            
            // Check if shift is on the start day of the range
            if (shiftDayOfWeek == rangeStartDay)
            {
                // Shift is on range start day
                // Check if shift overlaps with the start part of the range (from StartHour to midnight)
                if (shiftEndHour <= shiftStartHour)
                {
                    // Shift crosses midnight
                    // Check if shift start hour is within range (from StartHour to 24)
                    if (shiftStartHour >= forbiddenRange.StartHour && shiftStartHour < 24)
                        return true;
                    // Check if shift end hour (next day) is within range end
                    if (rangeEndDay == (shiftDayOfWeek + 1) % 7 && shiftEndHour <= forbiddenRange.EndHour)
                        return true;
                }
                else
                {
                    // Normal shift on start day
                    // Check if shift overlaps with range from StartHour to 24
                    if (shiftStartHour < 24 && shiftEndHour > forbiddenRange.StartHour)
                        return true;
                }
            }
            
            // Check if shift is on the end day of the range
            if (shiftDayOfWeek == rangeEndDay)
            {
                // Shift is on range end day - range on this day is [0, EndHour]
                if (shiftEndHour <= shiftStartHour)
                {
                    // Shift crosses midnight (e.g. 21:00-00:00)
                    // Same-day part [shiftStartHour, 24) overlaps [0, EndHour] if shiftStartHour < EndHour
                    if (shiftStartHour < forbiddenRange.EndHour)
                        return true;
                    // After-midnight part [0, shiftEndHour) overlaps [0, EndHour]
                    if (shiftEndHour > 0 && shiftEndHour <= forbiddenRange.EndHour)
                        return true;
                }
                else
                {
                    // Normal shift on end day
                    if (shiftStartHour < forbiddenRange.EndHour && shiftEndHour > 0)
                        return true;
                }
            }
        }
        else if (forbiddenRange.EndHour < forbiddenRange.StartHour)
        {
            // Range crosses midnight within same day (e.g., 20:00-00:00)
            if (shiftDayOfWeek == rangeStartDay)
            {
                if (shiftEndHour <= shiftStartHour)
                {
                    // Both shift and range cross midnight
                    // Check overlap with range from StartHour to 24
                    if (shiftStartHour >= forbiddenRange.StartHour && shiftStartHour < 24)
                        return true;
                    // Check overlap with range from 0 to EndHour
                    if (shiftEndHour > 0 && shiftEndHour <= forbiddenRange.EndHour)
                        return true;
                }
                else
                {
                    // Normal shift, range crosses midnight
                    // Check if shift overlaps with either part of the range
                    if (shiftStartHour < 24 && shiftEndHour > forbiddenRange.StartHour)
                        return true;
                    if (shiftStartHour < forbiddenRange.EndHour && shiftEndHour > 0)
                        return true;
                }
            }
        }
        else
        {
            // Normal range within same day (e.g., 09:00-23:00)
            if (shiftDayOfWeek == rangeStartDay)
            {
                if (shiftEndHour <= shiftStartHour)
                {
                    // Shift crosses midnight (e.g. 21:00-00:00), range doesn't
                    // Same-day part of shift is [shiftStartHour, 24) - check overlap with [StartHour, EndHour]
                    // Overlap if shift starts before range ends: shiftStartHour <= EndHour
                    if (shiftStartHour <= forbiddenRange.EndHour && forbiddenRange.StartHour < 24)
                        return true;
                }
                else
                {
                    // Normal shift, normal range - overlap if [start,end) overlaps [StartHour, EndHour]
                    if (shiftStartHour < forbiddenRange.EndHour && shiftEndHour > forbiddenRange.StartHour)
                        return true;
                }
            }
        }
        
        return false;
    }

    /// <summary>דוח בדיקות סופיות לפני סגירה – מי עם הכי מעט/הרבה שמירות, הרווח הכי קצר/ארוך (סעיף 7).</summary>
    public async Task<ScheduleValidationReport> GetScheduleValidationReportAsync(List<DaySchedule> schedule)
    {
        var report = new ScheduleValidationReport();
        if (schedule == null || !schedule.Any())
            return report;

        var positions = await _positionRepository.GetAllAsync();
        var posIsStandby = positions.ToDictionary(p => p.Id, p => p.IsStandby);

        // soldierId -> list of (Start, End, IsStandby)
        var soldierShifts = new Dictionary<string, List<(DateTime Start, DateTime End, bool IsStandby)>>();
        foreach (var day in schedule)
        {
            foreach (var a in day.Assignments)
            {
                if (string.IsNullOrEmpty(a.SoldierId)) continue;
                if (a.IsForced)
                    report.ForcedAssignments.Add(new ForcedAssignmentInfo { Date = day.Date, ShiftNumber = day.ShiftNumber, PositionName = a.PositionName, SoldierName = a.SoldierName });
                var isStandby = posIsStandby.TryGetValue(a.PositionId, out var sb) && sb;
                if (!soldierShifts.ContainsKey(a.SoldierId))
                    soldierShifts[a.SoldierId] = new List<(DateTime, DateTime, bool)>();
                soldierShifts[a.SoldierId].Add((day.Start, day.End, isStandby));
            }
        }

        var statsList = new List<SoldierScheduleStats>();
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

            var name = schedule.SelectMany(d => d.Assignments).FirstOrDefault(a => a.SoldierId == kv.Key)?.SoldierName ?? kv.Key;
            statsList.Add(new SoldierScheduleStats
            {
                SoldierId = kv.Key,
                SoldierName = name,
                TotalShifts = totalShifts,
                GuardCount = guardCount,
                NightGuardCount = nightCount,
                AverageGapHours = averageGap,
                MinGapHours = minGap,
                MaxGapHours = maxGap
            });
        }

        report.PerSoldierStats = statsList;
        if (!statsList.Any()) return report;

        var minGuards = statsList.Min(s => s.GuardCount);
        var maxGuards = statsList.Max(s => s.GuardCount);
        report.SoldiersWithFewestGuards = statsList.Where(s => s.GuardCount == minGuards).Select(s => s.SoldierName).ToList();
        report.SoldiersWithMostGuards = statsList.Where(s => s.GuardCount == maxGuards).Select(s => s.SoldierName).ToList();

        var minNights = statsList.Min(s => s.NightGuardCount);
        var maxNights = statsList.Max(s => s.NightGuardCount);
        report.SoldiersWithFewestNights = statsList.Where(s => s.NightGuardCount == minNights).Select(s => s.SoldierName).ToList();
        report.SoldiersWithMostNights = statsList.Where(s => s.NightGuardCount == maxNights).Select(s => s.SoldierName).ToList();

        if (globalMinGap.HasValue)
        {
            report.ShortestGapHours = globalMinGap.Value;
            report.SoldiersWithShortestGap = statsList.Where(s => s.MinGapHours.HasValue && Math.Abs(s.MinGapHours.Value - globalMinGap.Value) < 0.01).Select(s => s.SoldierName).ToList();
        }
        if (globalMaxGap.HasValue)
        {
            report.LongestGapHours = globalMaxGap.Value;
            report.SoldiersWithLongestGap = statsList.Where(s => s.MaxGapHours.HasValue && Math.Abs(s.MaxGapHours.Value - globalMaxGap.Value) < 0.01).Select(s => s.SoldierName).ToList();
        }

        return report;
    }

    /// <summary>בדיקת אילוצים קשיחים (סעיף 1) – אם יש הפרות, הרשימה לא תקינה.</summary>
    public async Task<ScheduleHardConstraintsValidationResult> ValidateScheduleHardConstraintsAsync(List<DaySchedule> schedule)
    {
        var result = new ScheduleHardConstraintsValidationResult { IsValid = true };
        if (schedule == null || !schedule.Any())
            return result;

        var soldiers = (await _soldierRepository.GetAllAsync()).ToDictionary(s => s.Id);
        var positions = (await _positionRepository.GetAllAsync()).ToDictionary(p => p.Id);

        const double hardMinGapHours = 12.0;

        // א. זמינות + ב. עמדות + ג. חפיפות: בונים שיבוצים לכל חייל
        var soldierShifts = new Dictionary<string, List<(DateTime Start, DateTime End, string PositionId, bool IsStandby)>>();
        foreach (var day in schedule)
        {
            var seenInSlot = new HashSet<string>();
            foreach (var a in day.Assignments)
            {
                if (string.IsNullOrEmpty(a.SoldierId)) continue;

                // חייל לא בשתי עמדות באותו סלוט (סעיף 1.א)
                if (!seenInSlot.Add(a.SoldierId))
                {
                    result.Violations.Add($"{day.Date} משמרת {day.ShiftNumber}: החייל {a.SoldierName} משובץ ביותר מעמדה אחת באותו סלוט.");
                    result.IsValid = false;
                }

                if (!soldiers.TryGetValue(a.SoldierId, out var soldierObj))
                {
                    result.Violations.Add($"{day.Date} משמרת {day.ShiftNumber}: חייל לא קיים ({a.SoldierId}).");
                    result.IsValid = false;
                    continue;
                }
                if (!positions.TryGetValue(a.PositionId, out var position))
                {
                    result.Violations.Add($"{day.Date} משמרת {day.ShiftNumber}: עמדה לא קיימת ({a.PositionId}).");
                    result.IsValid = false;
                    continue;
                }

                // דרישות עמדה – מפקד (סעיף 1.ב)
                if (position.RequiresCommander && !soldierObj.IsCommander)
                {
                    result.Violations.Add($"{day.Date} משמרת {day.ShiftNumber}: העמדה דורשת מפקד, {a.SoldierName} אינו מפקד.");
                    result.IsValid = false;
                }

                // עמדות אסורות לחייל (סעיף 1.ב)
                if (soldierObj.Constraints?.ForbiddenPositions != null && soldierObj.Constraints.ForbiddenPositions.Contains(a.PositionId))
                {
                    result.Violations.Add($"{day.Date} משמרת {day.ShiftNumber}: {a.SoldierName} – עמדה אסורה ({position.Name}).");
                    result.IsValid = false;
                }

                var dayOfWeek = (int)day.Start.DayOfWeek;
                var dayOfWeekStr = dayOfWeek.ToString();
                var shiftStartHour = day.Start.Hour;
                var shiftEndHour = day.End.Hour;

                // ימים שלמים אסורים (סעיף 1.א)
                if (soldierObj.Constraints?.ForbiddenDaysOfWeek != null && soldierObj.Constraints.ForbiddenDaysOfWeek.Contains(dayOfWeek))
                {
                    result.Violations.Add($"{day.Date} משמרת {day.ShiftNumber}: {a.SoldierName} – יום אסור.");
                    result.IsValid = false;
                }

                // שעות אסורות / טווחים אסורים (סעיף 1.א, כולל טווחים חוצי ימים)
                if (soldierObj.Constraints != null)
                {
                    if (soldierObj.Constraints.ForbiddenHoursByDay != null && soldierObj.Constraints.ForbiddenHoursByDay.ContainsKey(dayOfWeekStr))
                    {
                        var forbiddenHours = soldierObj.Constraints.ForbiddenHoursByDay[dayOfWeekStr];
                        if (forbiddenHours != null && forbiddenHours.Any())
                        {
                            var shiftHours = new List<int>();
                            if (shiftEndHour <= shiftStartHour)
                            {
                                for (int h = shiftStartHour; h < 24; h++) shiftHours.Add(h);
                                for (int h = 0; h < shiftEndHour; h++) shiftHours.Add(h);
                            }
                            else
                                for (int h = shiftStartHour; h < shiftEndHour; h++) shiftHours.Add(h);
                            if (shiftHours.Any(h => forbiddenHours.Contains(h)))
                            {
                                result.Violations.Add($"{day.Date} משמרת {day.ShiftNumber}: {a.SoldierName} – שעה אסורה.");
                                result.IsValid = false;
                            }
                        }
                    }
                    if (soldierObj.Constraints.ForbiddenHourRangesByDay != null)
                    {
                        if (soldierObj.Constraints.ForbiddenHourRangesByDay.ContainsKey(dayOfWeekStr))
                        {
                            var ranges = soldierObj.Constraints.ForbiddenHourRangesByDay[dayOfWeekStr];
                            if (ranges != null)
                                foreach (var range in ranges)
                                {
                                    if (IsTimeRangeOverlapping(shiftStartHour, shiftEndHour, dayOfWeek, dayOfWeek, range))
                                    {
                                        result.Violations.Add($"{day.Date} משמרת {day.ShiftNumber}: {a.SoldierName} – טווח שעות אסור.");
                                        result.IsValid = false;
                                        break;
                                    }
                                }
                        }
                        foreach (var kvp in soldierObj.Constraints.ForbiddenHourRangesByDay)
                        {
                            var rangeStartDay = int.Parse(kvp.Key);
                            var ranges = kvp.Value;
                            if (ranges == null) continue;
                            foreach (var range in ranges)
                            {
                                if (range.EndDay.HasValue && range.EndDay.Value == dayOfWeek &&
                                    IsTimeRangeOverlapping(shiftStartHour, shiftEndHour, dayOfWeek, rangeStartDay, range))
                                {
                                    result.Violations.Add($"{day.Date} משמרת {day.ShiftNumber}: {a.SoldierName} – טווח שעות אסור (חוצה ימים).");
                                    result.IsValid = false;
                                    break;
                                }
                            }
                        }
                    }
                }

                var isStandby = position.IsStandby;
                if (!soldierShifts.ContainsKey(a.SoldierId))
                    soldierShifts[a.SoldierId] = new List<(DateTime, DateTime, string, bool)>();
                soldierShifts[a.SoldierId].Add((day.Start, day.End, a.PositionId, isStandby));
            }
        }

        // ג. חפיפות ורווח מינימלי (סעיף 1.ג + 2.א): אין חפיפה בין שמירות, רווח מינימלי 12 שעות
        foreach (var kv in soldierShifts)
        {
            var soldierName = schedule.SelectMany(d => d.Assignments).FirstOrDefault(a => a.SoldierId == kv.Key)?.SoldierName ?? kv.Key;
            var guardOnly = kv.Value.Where(t => !t.IsStandby).OrderBy(t => t.Start).ToList();
            for (int i = 1; i < guardOnly.Count; i++)
            {
                var prev = guardOnly[i - 1];
                var curr = guardOnly[i];
                if (curr.Start < prev.End)
                {
                    result.Violations.Add($"{soldierName}: חפיפה בין שמירות ({prev.End:g} – {curr.Start:g}).");
                    result.IsValid = false;
                }
                var gap = (curr.Start - prev.End).TotalHours;
                if (gap < hardMinGapHours)
                {
                    result.Violations.Add($"{soldierName}: רווח {gap:F1} שעות בין שמירות (מינימום {hardMinGapHours}).");
                    result.IsValid = false;
                }
            }
        }

        return result;
    }

    private class SoldierAssignment
    {
        public DateTime Date { get; set; }
        public int ShiftNumber { get; set; }
        public string PositionId { get; set; } = string.Empty;
        public bool IsStandbyPosition { get; set; } // האם זו עמדת כוננות או שמירה
        public DateTime ShiftStart { get; set; }
        public DateTime ShiftEnd { get; set; }
    }
}
