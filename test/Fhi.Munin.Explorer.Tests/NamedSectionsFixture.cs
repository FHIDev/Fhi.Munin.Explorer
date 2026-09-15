using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>Two sections an explorer might hand a detail view, shared by the three views' tests.</summary>
internal static class NamedSectionsFixture
{
    internal static readonly DetailNamedSection[] Two =
    [
        new("first", "Første", builder => builder.AddMarkupContent(0, "<p>Den første</p>")),
        new("second", "Andre", builder => builder.AddMarkupContent(0, "<p>Den andre</p>")),
    ];
}
