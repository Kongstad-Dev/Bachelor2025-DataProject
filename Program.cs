using BlazorTest.Components;
using Microsoft.EntityFrameworkCore;
using MySql.EntityFrameworkCore.Extensions;
using DotNetEnv;
using BlazorTest.Services;

var builder = WebApplication.CreateBuilder(args);

// Load environment variables from .env file
Env.Load();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<DataAnalysisService>();
builder.Services.AddScoped<BankService>();
builder.Services.AddScoped<LaundromatService>();
builder.Services.AddScoped<LaundromatStatsService>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<LaundryStateService>();

builder.Services.AddScoped<CompareAnalysisServices>();

builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

// Register ExternalApiService
builder.Services.AddHttpClient<ExternalApiService>();

// Register background service for daily updates
builder.Services.AddHostedService<DailyUpdateBackgroundService>();

// Configure MySQL
var connectionString = $"Server={Env.GetString("DATABASE_HOST")};Database={Env.GetString("DATABASE_NAME")};User={Env.GetString("DATABASE_USERNAME")};Password={Env.GetString("DATABASE_PASSWORD")};";
builder.Services.AddDbContextFactory<YourDbContext>(options =>
    options.UseMySQL(connectionString));

// Add antiforgery services
builder.Services.AddAntiforgery();
builder.Services.AddMemoryCache();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();