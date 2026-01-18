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

        // Calculate optimal number of shifts per day for maximum spacing
        // Number of shifts = number of positions (each position needs a guard)
        // This ensures maximum spacing between shifts for each soldier
        var shiftsPerDay = positions.Count;

        // Generate time slots with optimal spacing
        Console.WriteLine($"מייצר time slots: {shiftsPerDay} משמרות ביום, {settings.ShiftHours} שעות למשמרת");
        var timeSlots = GenerateTimeSlots(config, settings.ShiftHours, shiftsPerDay);
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
                    soldierAssignments
                );

                Soldier? assignedSoldier = null;
                
                if (availableSoldiers.Any())
                {
                    assignedSoldier = SelectBestSoldier(
                        availableSoldiers,
                        position,
                        slot,
                        soldierAssignments,
                        previousSlot,
                        previousGuardSoldiers
                    );
                }
                else
                {
                    // No available soldiers - find the best soldier anyway (ignore constraints)
                    // Filter only by commander requirement and groups
                    var fallbackSoldiers = soldiers.Where(soldier =>
                    {
                        // Check if position requires commander and soldier is not a commander
                        if (position.RequiresCommander && !soldier.IsCommander)
                            return false;


                        // Check if already assigned in this time slot
                        var existingAssignment = soldierAssignments[soldier.Id].FirstOrDefault(
                            a => a.Date.Date == slot.Date.Date && a.ShiftNumber == slot.ShiftNumber);

                        return existingAssignment == null;
                    }).ToList();
                    
                    if (fallbackSoldiers.Any())
                    {
                        assignedSoldier = SelectBestSoldier(
                            fallbackSoldiers,
                            position,
                            slot,
                            soldierAssignments,
                            previousSlot,
                            previousGuardSoldiers
                        );
                    }
                }

                if (assignedSoldier != null)
                {
                    daySchedule.Assignments.Add(new ShiftAssignment
                    {
                        PositionId = position.Id,
                        PositionName = position.Name,
                        SoldierId = assignedSoldier.Id,
                        SoldierName = assignedSoldier.Name
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
            
            // Now assign standby positions (כוננות) - prioritize soldiers who finished guard shifts in previous slot
            // Group them by 4 and assign to standby positions
            var soldiersFromPreviousGuardForStandby = new List<Soldier>();
            if (previousGuardSoldiers != null && previousGuardSoldiers.Any() && standbyPositions.Any())
            {
                var testPosition = standbyPositions.First();
                soldiersFromPreviousGuardForStandby = soldiers
                    .Where(s => previousGuardSoldiers.Contains(s.Id))
                    .Where(s => GetAvailableSoldiers(
                        new List<Soldier> { s },
                        testPosition,
                        slot,
                        soldierAssignments
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
                    soldierAssignments
                );

                Soldier? assignedSoldier = null;
                
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
                            previousSlot,
                            previousGuardSoldiers
                        );
                    }
                }
                else
                {
                    // No available soldiers - find the best soldier anyway (ignore constraints)
                    // Filter only by commander requirement and groups
                    var fallbackSoldiers = soldiers.Where(soldier =>
                    {
                        // Check if position requires commander and soldier is not a commander
                        if (position.RequiresCommander && !soldier.IsCommander)
                            return false;


                        // Check if already assigned in this time slot
                        var existingAssignment = soldierAssignments[soldier.Id].FirstOrDefault(
                            a => a.Date.Date == slot.Date.Date && a.ShiftNumber == slot.ShiftNumber);

                        return existingAssignment == null;
                    }).ToList();
                    
                    if (fallbackSoldiers.Any())
                    {
                        assignedSoldier = SelectBestSoldier(
                            fallbackSoldiers,
                            position,
                            slot,
                            soldierAssignments,
                            previousSlot,
                            previousGuardSoldiers
                        );
                    }
                }

                if (assignedSoldier != null)
                {
                    daySchedule.Assignments.Add(new ShiftAssignment
                    {
                        PositionId = position.Id,
                        PositionName = position.Name,
                        SoldierId = assignedSoldier.Id,
                        SoldierName = assignedSoldier.Name
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

    private List<TimeSlot> GenerateTimeSlots(ScheduleConfig config, double shiftHours, int shiftsPerDay)
    {
        var timeSlots = new List<TimeSlot>();
        var startDate = config.StartDate.Date;
        var endDate = config.EndDate.Date;
        
        // Get start and end hours (default to full day if not specified)
        var hasStartHour = config.StartHour.HasValue;
        var hasEndHour = config.EndHour.HasValue;
        var startHour = config.StartHour ?? 0;
        var endHour = config.EndHour ?? 23;
        
        // Calculate total time range
        var totalStartTime = hasStartHour ? startDate.AddHours(startHour) : startDate;
        var totalEndTime = hasEndHour ? endDate.AddHours(endHour) : endDate.AddDays(1).AddSeconds(-1);
        
        var currentDate = startDate;
        var shiftNumber = 0;

        Console.WriteLine($"טווח זמן: {totalStartTime} עד {totalEndTime}");
        
        while (currentDate <= endDate)
        {
            // Determine the hour range for this day
            var isFirstDay = currentDate.Date == startDate;
            var isLastDay = currentDate.Date == endDate;
            
            var dayStartHour = (isFirstDay && hasStartHour) ? startHour : 0;
            var dayEndHour = (isLastDay && hasEndHour) ? endHour : 23;
            
            Console.WriteLine($"יום {currentDate:yyyy-MM-dd}: שעת התחלה={dayStartHour}, שעת סיום={dayEndHour}, יום ראשון={isFirstDay}, יום אחרון={isLastDay}");
            
            // Calculate available hours for this day
            var dayAvailableHours = dayEndHour >= dayStartHour 
                ? (dayEndHour - dayStartHour + 1) 
                : (24 - dayStartHour + dayEndHour + 1);
            
            // Calculate spacing between shifts for maximum rest time
            // Distribute shifts evenly across available hours
            var hoursBetweenShifts = dayAvailableHours / (double)shiftsPerDay;
            
            int slotsAddedForDay = 0;
            for (int shiftNum = 0; shiftNum < shiftsPerDay; shiftNum++)
            {
                // Start each shift with equal spacing, rounded to nearest hour
                var shiftStartHour = dayStartHour + (int)Math.Round(shiftNum * hoursBetweenShifts);
                if (shiftStartHour >= 24) shiftStartHour -= 24;
                
                var shiftStart = currentDate.AddHours(shiftStartHour);
                var shiftEnd = shiftStart.AddHours(shiftHours);
                
                // Check if shift start is within the time range
                if (shiftStart < totalStartTime)
                {
                    Console.WriteLine($"  משמרת {shiftNum}: דילוג - shiftStart ({shiftStart}) < totalStartTime ({totalStartTime})");
                    continue;
                }
                
                // Check if shift exceeds the end time
                bool shouldSkip = false;
                if (isLastDay && hasEndHour)
                {
                    // For last day with specific end hour, check if shift start exceeds end hour
                    if (shiftStart.Date == currentDate && shiftStart.Hour > endHour)
                    {
                        Console.WriteLine($"  משמרת {shiftNum}: דילוג - shiftStart.Hour ({shiftStart.Hour}) > endHour ({endHour})");
                        shouldSkip = true;
                    }
                    
                    // Check if shift end exceeds end hour (on the same day)
                    if (!shouldSkip && shiftEnd.Date == currentDate && shiftEnd.Hour > endHour)
                    {
                        Console.WriteLine($"  משמרת {shiftNum}: דילוג - shiftEnd.Hour ({shiftEnd.Hour}) > endHour ({endHour})");
                        shouldSkip = true;
                    }
                    
                    // Also check if shift end is on next day and exceeds total end time
                    if (!shouldSkip && shiftEnd.Date > currentDate && shiftEnd > totalEndTime)
                    {
                        Console.WriteLine($"  משמרת {shiftNum}: דילוג - shiftEnd ({shiftEnd}) > totalEndTime ({totalEndTime})");
                        shouldSkip = true;
                    }
                }
                else
                {
                    // For other cases, check if shift end exceeds total end time
                    if (shiftEnd > totalEndTime)
                    {
                        Console.WriteLine($"  משמרת {shiftNum}: דילוג - shiftEnd ({shiftEnd}) > totalEndTime ({totalEndTime})");
                        shouldSkip = true;
                    }
                }
                
                if (shouldSkip)
                    continue;

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

    private List<Soldier> GetAvailableSoldiers(
        List<Soldier> soldiers,
        Position position,
        TimeSlot slot,
        Dictionary<string, List<SoldierAssignment>> soldierAssignments)
    {
        var shiftStartHour = slot.Start.Hour;
        var shiftEndHour = slot.End.Hour;

        return soldiers.Where(soldier =>
        {
            // Check if position requires commander and soldier is not a commander
            if (position.RequiresCommander && !soldier.IsCommander)
                return false;

            // Check constraints
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

                // Forbidden positions constraint
                if (soldier.Constraints.ForbiddenPositions != null &&
                    soldier.Constraints.ForbiddenPositions.Contains(position.Id))
                    return false;
            }

            // Check if already assigned in this time slot
            var existingAssignment = soldierAssignments[soldier.Id].FirstOrDefault(
                a => a.Date.Date == slot.Date.Date && a.ShiftNumber == slot.ShiftNumber);

            if (existingAssignment != null)
                return false;

            // Check gap requirements based on position type
            var assignments = soldierAssignments[soldier.Id];
            if (assignments.Any())
            {
                if (!position.IsStandby)
                {
                    // For guard positions (שמירה) - need significant gap between guard shifts
                    var minGapHours = 8.0; // Minimum 8 hours between guard shifts
                    
                    // Check all guard shifts (not standby) for conflicts
                    foreach (var guardShift in assignments.Where(a => !a.IsStandbyPosition))
                    {
                        // Check if shifts overlap
                        if (slot.Start < guardShift.ShiftEnd && slot.End > guardShift.ShiftStart)
                            return false; // Shifts overlap
                        
                        // Check gap before current shift (guard shift ended, current shift starts)
                        if (guardShift.ShiftEnd <= slot.Start)
                        {
                            var gapBefore = (slot.Start - guardShift.ShiftEnd).TotalHours;
                            if (gapBefore < minGapHours)
                                return false; // Too close to previous guard shift
                        }
                        
                        // Check gap after current shift (current shift ends, guard shift starts)
                        if (slot.End <= guardShift.ShiftStart)
                        {
                            var gapAfter = (guardShift.ShiftStart - slot.End).TotalHours;
                            if (gapAfter < minGapHours)
                                return false; // Too close to next guard shift
                        }
                    }
                }
                else
                {
                    // For standby positions (כוננות) - can be continuous after guard shift, but prefer gap between standby shifts
                    // Allow continuous assignment if soldier just finished a guard shift
                    var justFinishedGuardShift = assignments
                        .Where(a => !a.IsStandbyPosition)
                        .Any(a => Math.Abs((slot.Start - a.ShiftEnd).TotalHours) < 1.0); // Within 1 hour = continuous
                    
                    if (!justFinishedGuardShift)
                    {
                        // If not continuous from guard shift, check gap between standby shifts
                        var minStandbyGapHours = 4.0; // Minimum 4 hours between standby shifts
                        var conflictingStandbyShift = assignments.FirstOrDefault(a => a.IsStandbyPosition &&
                            ((slot.Start - a.ShiftEnd).TotalHours >= 0 && (slot.Start - a.ShiftEnd).TotalHours < minStandbyGapHours) ||
                            ((a.ShiftStart - slot.End).TotalHours >= 0 && (a.ShiftStart - slot.End).TotalHours < minStandbyGapHours));

                        if (conflictingStandbyShift != null)
                            return false; // Too close to another standby shift
                    }
                }
            }

            return true;
        }).ToList();
    }

    private Soldier? SelectBestSoldier(
        List<Soldier> availableSoldiers,
        Position position,
        TimeSlot slot,
        Dictionary<string, List<SoldierAssignment>> soldierAssignments,
        TimeSlot? previousSlot = null,
        List<string>? previousGuardSoldiers = null)
    {
        if (!availableSoldiers.Any())
            return null;

        // Calculate total shifts needed for fairness
        var totalShifts = soldierAssignments.Values.Sum(v => v.Count);
        var averageShiftsPerSoldier = totalShifts / (double)soldierAssignments.Count;

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
                FairnessScore = averageShiftsPerSoldier - soldierAssignments[s.Id].Count // Positive = below average (preferred)
            })
            .OrderByDescending(x => 
            {
                if (!position.IsStandby)
                {
                    // For guard positions (שמירה) - prioritize maximum gap from last guard shift
                    return x.HoursSinceLastGuardShift;
                }
                else
                {
                    // For standby positions (כוננות) - prioritize continuous from guard, then gap between standby
                    if (x.CanBeContinuousFromGuard)
                        return 1000.0; // High priority for continuous from guard
                    return x.HoursSinceLastStandbyShift; // Then prioritize gap between standby shifts
                }
            })
            .ThenByDescending(x => x.FairnessScore) // Always consider fairness
            .ThenBy(x => x.TotalAssignments) // Then by total assignments
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

    private class TimeSlot
    {
        public DateTime Date { get; set; }
        public int ShiftNumber { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
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
