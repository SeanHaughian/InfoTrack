using InfoTrack.Solicitors.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<
    ISolicitorHtmlParser,
    SolicitorHtmlParser>();

builder.Services.AddHttpClient<
    ISolicitorScraper,
    SolicitorScraper>(client =>
    {
        client.BaseAddress =
            new Uri("https://www.solicitors.com/");

        client.Timeout = TimeSpan.FromSeconds(20);

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "InfoTrack-Development-Task/1.0");
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// In-memory cache used to avoid re-scraping for pagination
builder.Services.AddMemoryCache();

// Warm cache on startup by scraping default locations once
builder.Services.AddHostedService<CacheWarmupService>();

// Simple in-memory store for persisting location state between sessions (process lifetime)
builder.Services.AddSingleton<ILocationStore, InMemoryLocationStore>();

// In-memory store for saved search results
builder.Services.AddSingleton<ISavedSearchStore, InMemorySavedSearchStore>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");

app.UseHttpsRedirection();

app.MapControllers();

app.Run();