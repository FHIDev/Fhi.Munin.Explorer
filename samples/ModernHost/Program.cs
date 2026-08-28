using Fhi.Munin.Explorer.Client;
using ModernHost.Components;

// Deliberately a MODERN Blazor Web App host: AddRazorComponents().AddInteractiveServerComponents()
// + MapRazorComponents<App>(), with a router, a root App component and @rendermode applied at the
// mount site. This is the everyday development host for the RCL.
//
// samples/LegacyHost covers the other shape (AddServerSideBlazor + MapBlazorHub, components
// mounted in MVC views via the <component> tag helper, no router) — that is how helsedata.no's
// Optimizely CMS hosts Blazor today. A component that only ever ran here can still break there,
// and the reverse is just as true, which is why both hosts exist.

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMuninExplorer(
    builder.Configuration,
    // Development-only convenience. Outside Development the base URL must be configured,
    // and startup fails loudly if it is not.
    developmentFallback: builder.Environment.IsDevelopment() ? "https://runa.munin.skytest.fhi.no" : null);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    // Only outside Development. This sample gets run over plain http — either the `http`
    // launch profile or an explicit `dotnet run --urls http://...` — and redirecting that to
    // an https port nothing is listening on turns a working host into a connection error.
    app.UseHttpsRedirection();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
