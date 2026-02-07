using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using ShiftScheduler.API.Interfaces;
using ShiftScheduler.API.Models;
using ShiftScheduler.API.Repositories;

namespace ShiftScheduler.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _config;
    private readonly IUserContextService _userContext;

    public AuthController(IUserRepository userRepository, IConfiguration config, IUserContextService userContext)
    {
        _userRepository = userRepository;
        _config = config;
        _userContext = userContext;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("נא להזין אימייל וסיסמה");

        request.Email = request.Email.Trim().ToLowerInvariant();
        if (request.Password.Length < 6)
            return BadRequest("הסיסמה חייבת להכיל לפחות 6 תווים");

        var existing = await _userRepository.GetByEmailAsync(request.Email);
        if (existing != null)
            return BadRequest("משתמש עם אימייל זה כבר קיים");

        var user = new User
        {
            Id = Guid.NewGuid().ToString("N"),
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow
        };
        await _userRepository.CreateAsync(user);

        var userDataPath = Path.Combine(AppContext.BaseDirectory, "Data", user.Id);
        if (!Directory.Exists(userDataPath))
            Directory.CreateDirectory(userDataPath);

        var token = GenerateJwt(user);
        return Ok(new AuthResponse { Token = token, UserId = user.Id, Email = user.Email });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("נא להזין אימייל וסיסמה");

        var user = await _userRepository.GetByEmailAsync(request.Email.Trim().ToLowerInvariant());
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized("אימייל או סיסמה שגויים");

        var token = GenerateJwt(user);
        return Ok(new AuthResponse { Token = token, UserId = user.Id, Email = user.Email });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<AuthResponse>> Me()
    {
        var userId = _userContext.GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null) return Unauthorized();

        return Ok(new AuthResponse { UserId = user.Id, Email = user.Email });
    }

    private string GenerateJwt(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"] ?? "ShiftScheduler-SecretKey-ChangeInProduction-Min32Chars!"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("sub", user.Id)
        };
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "ShiftScheduler",
            audience: _config["Jwt:Audience"] ?? "ShiftScheduler",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string? Token { get; set; }
    public string? UserId { get; set; }
    public string? Email { get; set; }
}
