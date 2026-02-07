using Microsoft.AspNetCore.Mvc;
using ShiftScheduler.API.Interfaces;
using ShiftScheduler.API.Models;

namespace ShiftScheduler.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SoldierGroupsController : ControllerBase
{
    private readonly ISoldierGroupRepository _repository;

    public SoldierGroupsController(ISoldierGroupRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<List<SoldierGroup>>> GetAll()
    {
        var groups = await _repository.GetAllAsync();
        return Ok(groups);
    }

    [HttpPost]
    public async Task<ActionResult<SoldierGroup>> Create([FromBody] SoldierGroup group)
    {
        if (string.IsNullOrWhiteSpace(group.Name))
            return BadRequest("שם הקבוצה הוא חובה");

        if (group.SoldierIds == null || group.SoldierIds.Count < 2)
            return BadRequest("קבוצה חייבת להכיל לפחות 2 חיילים");

        if (string.IsNullOrEmpty(group.Id))
            group.Id = Guid.NewGuid().ToString();

        var created = await _repository.CreateAsync(group);
        await _repository.SaveAsync();
        return Ok(created);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<SoldierGroup>> Update(string id, [FromBody] SoldierGroup group)
    {
        if (string.IsNullOrWhiteSpace(group.Name))
            return BadRequest("שם הקבוצה הוא חובה");

        if (group.SoldierIds == null || group.SoldierIds.Count < 2)
            return BadRequest("קבוצה חייבת להכיל לפחות 2 חיילים");

        group.Id = id;
        var updated = await _repository.UpdateAsync(id, group);
        
        if (updated == null)
            return NotFound();

        await _repository.SaveAsync();
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id)
    {
        var deleted = await _repository.DeleteAsync(id);
        
        if (!deleted)
            return NotFound();

        await _repository.SaveAsync();
        return NoContent();
    }
}
