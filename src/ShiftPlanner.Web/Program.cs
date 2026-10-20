using Microsoft.EntityFrameworkCore;

using ShiftPlanner.Data;
using ShiftPlanner.Web.Components;
using ShiftPlanner.Web.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton(TimeProvider.System);
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
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// 讓 WebApplicationFactory<Program> 可以在整合測試中使用
public partial class Program;
