using djbeb;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// ✅ Bind SpotifyConfig from appsettings.json
builder.Services.Configure<SpotifyConfig>(
    builder.Configuration.GetSection("Spotify"));

builder.Services.AddSingleton<SpotifyService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDistributedMemoryCache();

// ✅ Configure Data Protection to persist keys in production
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo("/var/data/DataProtectionKeys"));
}

// ✅ Session configuration
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".DJBeb.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromDays(14);
});

// ✅ Fetch frontend URL from SpotifyConfig dynamically
var frontendUrl = builder.Configuration.GetSection("Spotify")["FrontendUrl"] ?? "http://localhost:5173";

// ✅ Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(frontendUrl)
            .AllowCredentials()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddControllers();

var app = builder.Build();

app.UseCors();
app.UseCookiePolicy();
app.UseSession();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.Run();