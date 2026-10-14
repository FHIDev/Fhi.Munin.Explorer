namespace Fhi.Munin.Explorer.Blazor;

// The one switch for both download menus once the file is handed to the browser: close and give focus back to the
// menu's button (Robin's choice, Fhi.Metadata-ht2gj item f, as helsedata's boards draw it), or stay open with «Nedlastingen er klar.».
internal static class DownloadPanel
{
    internal static readonly bool ClosesOnDelivery = true;
}
