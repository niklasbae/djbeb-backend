using djbeb;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SpotifyConfig>(
    builder.Configuration.GetSection("Spotify"));

builder.Services.AddSingleton<SpotifyService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials(); // Important for cookies/session
    });
});

var app = builder.Build();
// and after builder.Build()
app.UseCors();

app.UseSession();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// temporarily remove or comment out HTTPS redirection for local testing
// app.UseHttpsRedirection();

app.UseAuthorization();
app.MapControllers();
app.Run();