namespace Fhi.Munin.Explorer.Contracts;

/// <summary>Which shape the reader wants their list in.</summary>
public enum ExportFormat
{
    /// <summary>Excel. The API's own default.</summary>
    Xlsx = 0,

    /// <summary>Comma-separated. Becomes a zip when codebooks are asked for as well.</summary>
    Csv = 1
}

/// <summary>
/// A saved list as a file, exactly as the API produced it.
/// </summary>
/// <remarks>
/// The name and the content type are the API's, read off the response rather than composed here:
/// asking for CSV <em>with</em> codebooks answers with a zip of two files, so a caller that built
/// the name itself would offer a <c>.csv</c> that is not one.
/// </remarks>
/// <param name="Bytes">The file. Held whole, because that is what a download is.</param>
/// <param name="ContentType">What the API said it is.</param>
/// <param name="FileName">The name from <c>Content-Disposition</c>, e.g. <c>variabelliste-2026-08-26-141530.xlsx</c>.</param>
public sealed record ExportedList(byte[] Bytes, string ContentType, string FileName);
