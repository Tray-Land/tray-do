using TrayDo.Tasks;

namespace TrayDo.Tests.Tasks;

[TestClass]
public sealed class PastedTasksTests
{
    [TestMethod]
    public void Split_OneTaskPerNonBlankLine_AnyLineEnding()
    {
        CollectionAssert.AreEqual(
            new[] { "Email Sam", "Book flights", "Renew passport" },
            PastedTasks.Split("Email Sam\r\n\r\n  Book flights  \nRenew passport\r").ToArray());
    }

    [TestMethod]
    [DataRow("- Buy milk", "Buy milk")]
    [DataRow("* Buy milk", "Buy milk")]
    [DataRow("• Buy milk", "Buy milk")]
    [DataRow("1. Buy milk", "Buy milk")]
    [DataRow("12) Buy milk", "Buy milk")]
    [DataRow("[ ] Buy milk", "Buy milk")]
    [DataRow("- [x] Buy milk", "Buy milk")]
    [DataRow("☐ Buy milk", "Buy milk")]
    public void CleanLine_StripsListMarkers(string line, string expected) =>
        Assert.AreEqual(expected, PastedTasks.CleanLine(line));

    [TestMethod]
    [DataRow("2026 budget review")]
    [DataRow("-5 degrees tonight")]
    [DataRow("3.5 hours of timesheets")]
    [DataRow("-")]
    public void CleanLine_LeavesTextThatOnlyLooksLikeAMarker(string line) =>
        Assert.AreEqual(line, PastedTasks.CleanLine(line));

    [TestMethod]
    public void IsMultiLine_IgnoresTrailingNewlinesAndBlankLines()
    {
        Assert.IsFalse(PastedTasks.IsMultiLine("Just one\r\n\r\n"));
        Assert.IsTrue(PastedTasks.IsMultiLine("One\nTwo"));
        Assert.IsFalse(PastedTasks.IsMultiLine(null));
    }
}
