using BlazorTest.Components;
using Microsoft.EntityFrameworkCore;
using MySql.EntityFrameworkCore.Extensions;
using DotNetEnv;
using System.Net.Http;
using BlazorTest.Services;

var builder = WebApplication.CreateBuilder(args);

// Load environment variables from .env file
Env.Load();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<DataAnalysisService>();


// Configure MySQL
var connectionString = $"Server={Env.GetString("DATABASE_HOST")};Database={Env.GetString("DATABASE_NAME")};User={Env.GetString("DATABASE_USERNAME")};Password={Env.GetString("DATABASE_PASSWORD")};";
builder.Services.AddDbContextFactory<YourDbContext>(options =>
    options.UseMySQL(connectionString));

builder.Services.AddDbContext<YourDbContext>(options =>
    options.UseMySQL(connectionString));

// Register HttpClient and ExternalApiService
builder.Services.AddHttpClient<ExternalApiService>();

builder.Services.AddScoped<DataAnalysisService>();
// Add controllers
builder.Services.AddControllers();

// Add antiforgery services
builder.Services.AddAntiforgery();

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
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Use MapGroup to separate API controllers from Razor components
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();