using MelrandiaManagement.Components;
using MelrandiaManagement.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Public pages use static server rendering. Interactive administration features
// will be enabled only for components that require them in a later phase.
builder.Services.AddRazorComponents();
builder.Services.AddSingleton<PublicProjectCatalog>();
builder.Services.AddSingleton<PublicArticleCatalog>();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
// These stable endpoints are also used by the Ubuntu deployment supervisor.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready");
app.MapRazorComponents<App>();

app.Run();
