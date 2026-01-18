using Microsoft.AspNetCore.Mvc;
using ShiftScheduler.API.Interfaces;
using ShiftScheduler.API.Models;

namespace ShiftScheduler.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SoldiersController : ControllerBase
{
    private readonly ISoldierRepository _repository;

    public SoldiersController(ISoldierRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<List<Soldier>>> GetAll()
    {
        var soldiers = await _repository.GetAllAsync();
        return Ok(soldiers);
    }

    [HttpPost]
    public async Task<ActionResult<Soldier>> Create([FromBody] Soldier soldier)
    {
        if (string.IsNullOrWhiteSpace(soldier.Name))
            return BadRequest("שם החייל הוא חובה");

        if (string.IsNullOrEmpty(soldier.Id))
            soldier.Id = Guid.NewGuid().ToString();

        if (soldier.Constraints == null)
            soldier.Constraints = new SoldierConstraints();

        var created = await _repository.CreateAsync(soldier);
        await _repository.SaveAsync();
        return Ok(created);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Soldier>> Update(string id, [FromBody] Soldier soldier)
    {
        if (string.IsNullOrWhiteSpace(soldier.Name))
            return BadRequest("שם החייל הוא חובה");

        soldier.Id = id;
        if (soldier.Constraints == null)
            soldier.Constraints = new SoldierConstraints();

        var updated = await _repository.UpdateAsync(id, soldier);
        
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
