using Fhi.Munin.Explorer.Client;

// The hostile sample host: the same legacy Blazor Server shape as samples/LegacyHost —
// AddServerSideBlazor() + MapBlazorHub(), components mounted through the <component> tag helper
// in an MVC view — but wearing helsedata's own chrome and their own stylesheet.
//
// It exists because the other two samples render the component on a bare page, and a bare page
// cannot show the two things that broke it in the real host: an author stylesheet whose element
// rules beat the browser's defaults, and a header positioned over the top of document flow.
// See Views/Shared/_Layout.cshtml for what each of those is and where it comes from.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddServerSideBlazor();

// No token provider, unlike LegacyHost. This host has no sign-in at all: the view mounts with
// IsAuthenticated="true" so the tabs and the reader's list panel exist to be measured, which is
// the whole reason the page is worth scanning. Authentication is LegacyHost's subject.
builder.Services.AddMuninExplorer(
    builder.Configuration,
    developmentFallback: builder.Environment.IsDevelopment() ? "https://runa.munin.skytest.fhi.no" : null);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Serves _content/Fhi.Helsedata.Stiler/css/main.css out of the package's static web assets,
// which is the same path and the same file helsedata's own layout links.
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapBlazorHub();

app.Run();
