using djbeb;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// ✅ Bind SpotifyConfig from appsettings.json
builder.Services.Configure<SpotifyConfig>(
    builder.Configuration.GetSection("Spotify"));

builder.Services.AddSingleton<SpotifyService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ✅ Add session handling
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ✅ Fetch frontend URL from SpotifyConfig dynamically
var frontendUrl = builder.Configuration.GetSection("Spotify")["FrontendUrl"] ?? "http://localhost:5173";

// ✅ Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(frontendUrl)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// ✅ Enable CORS only once
app.UseCors();

app.UseSession();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 🔹 Temporarily disable HTTPS redirection for local testing
// app.UseHttpsRedirection();

app.UseAuthorization();
app.MapControllers();
app.Run();