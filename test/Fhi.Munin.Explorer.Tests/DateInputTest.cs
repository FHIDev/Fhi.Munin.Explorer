using System.Globalization;
using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

public class DateInputTest
{
    [Theory]
    [InlineData("no", "05.03.2020")]
    [InlineData("no", "5.3.2020")]
    [InlineData("no", "2020-03-05")]
    [InlineData("no", " 05.03.2020 ")]
    [InlineData("en", "2020-03-05")]
    [InlineData("en", "05.03.2020")]
    public void TryParse_WhenTheServerRunsInAnEnglishUsCulture_ThenTheDayIsStillTheReadersDay(
        string language, string typed)
    {
        // en-US reads 05.03.2020 month first where it reads it at all. Parsing that asked the
        // current culture would make this 3 May on a US server and 5 March on a Norwegian one.
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            Assert.True(DateInput.TryParse(typed, language, out var day));
            Assert.Equal(new DateOnly(2020, 3, 5), day);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("31.02.2020")]
    [InlineData("2020")]
    [InlineData("abc")]
    [InlineData("03/05/2020")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_WhenTheTextIsNotADayInAnAcceptedFormat_ThenItIsRefused(string? typed)
    {
        Assert.False(DateInput.TryParse(typed, "no", out _));
        Assert.False(DateInput.TryParse(typed, "en", out _));
    }

    [Theory]
    [InlineData("no", "05.03.2020")]
    [InlineData("en", "2020-03-05")]
    [InlineData("en-GB", "2020-03-05")]
    public void Format_WhenGivenADay_ThenItIsWrittenInTheReadersFormat(string language, string expected)
    {
        Assert.Equal(expected, DateInput.Format(new DateOnly(2020, 3, 5), language));
    }
}
