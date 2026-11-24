using Fhi.Munin.Explorer.Blazor;

namespace Fhi.Munin.Explorer.Tests;

public class DescriptionLeadDeepTextHeldOutTest
{
    // Render draws too-deep text as plain lines; a half cut from it could parse and draw a list.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Split_WhenTheWholeTextIsNestedTooDeep_ThenNothingIsFolded(bool formatted)
    {
        var text = "- a\n- b\n\n" + string.Concat(Enumerable.Repeat("Dette er en setning. ", 30)).Trim()
            + "\n\n" + string.Concat(Enumerable.Repeat("> ", 128)) + "a";

        Assert.Equal((text, null), DescriptionLead.Split(text, formatted));
    }
}
