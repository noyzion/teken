using Microsoft.AspNetCore.Mvc;
using ShiftScheduler.API.Interfaces;
using ShiftScheduler.API.Models;

namespace ShiftScheduler.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PositionsController : ControllerBase
{
    private readonly IPositionRepository _repository;

    public PositionsController(IPositionRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<ActionResult<List<Position>>> GetAll()
    {
        var positions = await _repository.GetAllAsync();
        return Ok(positions);
    }

    [HttpPost]
    public async Task<ActionResult<Position>> Create([FromBody] Position position)
    {
        if (string.IsNullOrWhiteSpace(position.Name))
            return BadRequest("שם העמדה הוא חובה");

        if (string.IsNullOrEmpty(position.Id))
            position.Id = Guid.NewGuid().ToString();

        var created = await _repository.CreateAsync(position);
        await _repository.SaveAsync();
        return Ok(created);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Position>> Update(string id, [FromBody] Position position)
    {
        if (string.IsNullOrWhiteSpace(position.Name))
            return BadRequest("שם העמדה הוא חובה");

        position.Id = id;
        var updated = await _repository.UpdateAsync(id, position);
        
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
