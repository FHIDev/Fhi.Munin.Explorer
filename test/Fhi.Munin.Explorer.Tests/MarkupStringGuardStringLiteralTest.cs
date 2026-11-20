namespace Fhi.Munin.Explorer.Tests;

public class MarkupStringGuardStringLiteralTest
{
    [Theory]
    [InlineData("<a href=\"https://x\">@((MarkupString)y)</a>")]
    [InlineData("var u = \"https://x\"; var m = (MarkupString)y;")]
    [InlineData("var s = \"/*\";\nvar m = (MarkupString)y; /* x */")]
    public void MarkupSinks_WhenASinkFollowsAStringHoldingACommentOpener_ThenItIsFound(string source) =>
        Assert.NotEmpty(MarkupStringGuardTest.MarkupSinks(source));
}
