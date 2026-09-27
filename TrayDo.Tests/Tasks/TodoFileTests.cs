using TrayDo.Tasks;

namespace TrayDo.Tests.Tasks;

[TestClass]
public sealed class TodoFileTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void SaveThenLoad_RoundTripsTasksAndHitListOrder()
    {
        string path = Path.Combine(TestContext.TestRunDirectory!, Guid.NewGuid().ToString("N"), "tasks.json");
        TodoBoard board = new();
        board.Add(["a", "b", "c"], addToHitList: false, DateTimeOffset.Now);
        board.AddToHitList(board.AllItems[2].Id);
        board.AddToHitList(board.AllItems[0].Id);
        board.SetDone(board.AllItems[1].Id, true, DateTimeOffset.Now);

        TodoFile.Save(path, board.Data);
        TodoBoard loaded = new(TodoFile.Load(path));

        CollectionAssert.AreEqual(new[] { "c", "a" }, loaded.HitListItems.Select(i => i.Text).ToArray());
        Assert.IsTrue(loaded.AllItems.Single(i => i.Text == "b").IsDone);
        Assert.IsFalse(File.Exists(path + ".tmp"));
    }

    [TestMethod]
    public void Load_MissingFile_IsEmpty()
    {
        TodoData data = TodoFile.Load(Path.Combine(TestContext.TestRunDirectory!, "nope", "tasks.json"));

        Assert.AreEqual(0, data.Items.Count);
    }

    [TestMethod]
    public void Load_CorruptFile_IsEmptyAndKeepsABackup()
    {
        string dir = Path.Combine(TestContext.TestRunDirectory!, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "tasks.json");
        File.WriteAllText(path, "{ not json");

        TodoData data = TodoFile.Load(path);

        Assert.AreEqual(0, data.Items.Count);
        Assert.AreEqual(1, Directory.GetFiles(dir, "tasks.json.*.bad").Length);
    }
}
