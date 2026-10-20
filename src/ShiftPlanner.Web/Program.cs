using System.Text.Json.Serialization;

using Microsoft.EntityFrameworkCore;

using ShiftPlanner.Core.Validation;
using ShiftPlanner.Data;
using ShiftPlanner.Scheduling;
using ShiftPlanner.Web.Api;
using ShiftPlanner.Web.Application;
using ShiftPlanner.Web.Components;
using ShiftPlanner.Web.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ScheduleValidator>();
builder.Services.AddSingleton<ShiftScheduleGenerator>();
builder.Services.AddScoped<PlanningDataLoader>();
builder.Services.AddSingleton<Rescheduler>();
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddScoped<RescheduleService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddSingleton<ScopedRunner>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddDbContext<ShiftPlannerDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("ShiftPlanner")
        ?? throw new InvalidOperationException("缺少連線字串 ConnectionStrings:ShiftPlanner")));

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapScheduleApi();
app.MapRescheduleApi();
app.MapReportApi();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// 讓 WebApplicationFactory<Program> 可以在整合測試中使用
public partial class Program;
