using TrayDo.Tasks;

namespace TrayDo.Tests.Tasks;

[TestClass]
public sealed class TaskListTextTests
{
    [TestMethod]
    public void ToMarkdown_WritesOneCheckboxLinePerTask()
    {
        TodoItem[] items = [new() { Text = "a" }, new() { Text = "b" }];

        Assert.AreEqual("- [ ] a\n- [ ] b", TaskListText.ToMarkdown(items));
    }

    [TestMethod]
    public void ToMarkdown_NoTasks_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, TaskListText.ToMarkdown([]));
    }

    [TestMethod]
    public void ToMarkdown_LineBreakInsideTask_BecomesSpace()
    {
        TodoItem[] items = [new() { Text = "a\r\nb" }];

        Assert.AreEqual("- [ ] a b", TaskListText.ToMarkdown(items));
    }
}
