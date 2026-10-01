namespace Fhi.Munin.Explorer.Blazor;

/// <summary>Sends the reader from the list tab to the search, where variables are saved to a list.</summary>
internal sealed class ShowSearchTab(Func<Task> show)
{
    public Task ShowAsync() => show();
}
