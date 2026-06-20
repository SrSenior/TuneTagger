using TuneTagger.Api.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

//Este es un endpoint de ejemplo creado automáticamente
app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

//Endpoint de prueba para verificar que la API está funcionando correctamente
app.MapGet("/api/health", () =>
{
    return Results.Ok(new
    {
        status = "ok",
        app = "TuneTagger.Api"
    });
})
.WithName("GetHealth");

//Endpoint de prueba para simular el análisis de una pista de música y devolver resultados ficticios
app.MapGet("/api/tracks/mock-analysis", () =>
{
    var result = new TrackAnalysisResult(
        OriginalFileName: "Black Clover Opening 10 Full.mp3",
        Title: "Black Catcher",
        Artist: "Vickeblanka",
        Album: "Black Catcher",
        SuggestedFileName: "Vickeblanka - Black Catcher.mp3",
        Confidence: 0.94,
        Status: "mock"
    );

    return Results.Ok(result);
})
.WithName("GetMockTrackAnalysis");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
