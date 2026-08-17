using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using LegacyHost.Authentication;
using Microsoft.AspNetCore.Components.Server.Circuits;

// Deliberately a LEGACY Blazor Server host: AddServerSideBlazor() + MapBlazorHub(),
// with components mounted inside MVC views via the <component> tag helper. That is how
// helsedata.no's Optimizely CMS hosts Blazor today, and it is the configuration our RCL
// has to survive — no router, no @rendermode, no HeadOutlet.
//
// samples/ModernHost covers the other shape (MapRazorComponents<App>). A component that
// only ever ran there can still break here.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddServerSideBlazor();

// Calling Munin as the signed-in user. Registered BEFORE AddMuninExplorer, which is not a
// style choice: AddMuninExplorer uses TryAdd, so whoever registers first wins. Register after
// it and the anonymous default is already in place — the explorer keeps working, silently
// without a token, which is the failure that looks like a Munin bug rather than a host one.
//
// See Authentication/CircuitServicesAccessor.cs for why this cannot be IHttpContextAccessor.
builder.Services.AddSingleton<CircuitServicesAccessor>();
builder.Services.AddScoped<CircuitHandler>(sp => new ServicesAccessorCircuitHandler(
    sp, sp.GetRequiredService<CircuitServicesAccessor>()));
builder.Services.AddScoped<BrukerToken>();
builder.Services.AddSingleton<IMuninExplorerTokenProvider, CircuitTokenProvider>();

builder.Services.AddMuninExplorer(
    builder.Configuration,
    // Development-only convenience. Outside Development the base URL must be configured,
    // and startup fails loudly if it is not.
    utviklingsFallback: builder.Environment.IsDevelopment() ? "https://munin.skytest.fhi.no" : null);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// The circuit endpoint the legacy host needs; MapRazorComponents<App>() is deliberately absent.
app.MapBlazorHub();

app.Run();
