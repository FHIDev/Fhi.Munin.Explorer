namespace Fhi.Munin.Explorer.Blazor;

/// <summary>Sends the reader from the list tab to the search, where variables are saved to a list.</summary>
internal sealed class ShowSearchTab(Func<Task> show, Func<Guid, Task> showVariabelgruppe)
{
    public Task ShowAsync() => show();

    // A variabelgruppe has no view of its own, so the list sends the reader to its variables instead.
    public Task ShowVariabelgruppeAsync(Guid id) => showVariabelgruppe(id);
}
