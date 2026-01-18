using ShiftScheduler.API.Interfaces;
using ShiftScheduler.API.Repositories;
using ShiftScheduler.API.Services;
using Microsoft.Extensions.FileProviders;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Dependency Injection - Dependency Inversion Principle
builder.Services.AddScoped<IPositionRepository, PositionRepository>();
builder.Services.AddScoped<ISoldierRepository, SoldierRepository>();
builder.Services.AddScoped<ISavedScheduleRepository, SavedScheduleRepository>();
builder.Services.AddScoped<ISchedulerService, SchedulerService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

// Serve static files from frontend directory (at project root)
var frontendPath = Path.Combine(builder.Environment.ContentRootPath, "..", "..", "frontend");
if (Directory.Exists(frontendPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(frontendPath),
        RequestPath = ""
    });
}
else
{
    // Fallback to default wwwroot if frontend doesn't exist
    app.UseStaticFiles();
}

app.UseAuthorization();
app.MapControllers();

// Default route to serve index.html from frontend
var indexPath = Path.Combine(frontendPath, "index.html");
if (File.Exists(indexPath))
{
    app.MapFallbackToFile("index.html", new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(frontendPath)
    });
}
else
{
    app.MapFallbackToFile("index.html");
}

app.Run();
