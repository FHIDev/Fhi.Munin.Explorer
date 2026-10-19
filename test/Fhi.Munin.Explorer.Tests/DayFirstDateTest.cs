using System.Globalization;
using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

public class DayFirstDateTest
{
    [Theory]
    [InlineData("05.03.2024")]
    [InlineData("5.3.2024")]
    [InlineData("05.3.2024")]
    [InlineData("05032024")]
    [InlineData(" 05.03.2024 ")]
    [InlineData("2024-03-05")]
    public void TryParse_WhenTheServerRunsInAnEnglishUsCulture_ThenTheDayIsStillReadDayFirst(string typed)
    {
        // A parse that asked the current culture would read 05.03.2024 as 3 May on a US server.
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            Assert.True(DayFirstDate.TryParse(typed, out var day));
            Assert.Equal(new DateOnly(2024, 3, 5), day);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("3/5/2024")]
    [InlineData("03/05/2024")]
    [InlineData("03-05-2024")]
    [InlineData("24-03-05")]
    [InlineData("2024/03/05")]
    [InlineData("05.03.24")]
    [InlineData("050324")]
    [InlineData("5032024")]
    [InlineData("31.02.2024")]
    [InlineData("2024")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_WhenTheTextIsNotADayWrittenDayFirstWithAFourDigitYear_ThenItIsRefused(string? typed)
    {
        Assert.False(DayFirstDate.TryParse(typed, out _));
    }

    [Fact]
    public void Format_WhenGivenADay_ThenItIsWrittenWithTwoDigitDayAndMonth()
    {
        Assert.Equal("05.03.2024", DayFirstDate.Format(new DateOnly(2024, 3, 5)));
    }
}
