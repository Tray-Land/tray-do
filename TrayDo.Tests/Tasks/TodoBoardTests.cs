using TrayDo.Tasks;

namespace TrayDo.Tests.Tasks;

[TestClass]
public sealed class TodoBoardTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 9, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Add_PutsNewTasksOnTopInTheOrderGiven()
    {
        TodoBoard board = new();
        board.Add("old", addToHitList: false, Now);

        board.Add(["first", "  ", "second"], addToHitList: false, Now);

        CollectionAssert.AreEqual(new[] { "first", "second", "old" }, Texts(board.AllItems));
    }

    [TestMethod]
    public void Add_ToHitList_AppendsToTheEndOfTheHitList()
    {
        TodoBoard board = new();
        board.Add("a", addToHitList: true, Now);

        board.Add(["b", "c"], addToHitList: true, Now);

        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, Texts(board.HitListItems));
        Assert.AreEqual(3, board.HitListRemaining);
    }

    [TestMethod]
    public void Add_BlankText_AddsNothing()
    {
        TodoBoard board = new();

        Assert.IsNull(board.Add("   ", addToHitList: true, Now));
        Assert.AreEqual(0, board.AllItems.Count);
        Assert.AreEqual(0, board.HitListCount);
    }

    [TestMethod]
    public void SetDone_MovesTaskBelowOpenTasksAndLowersHitListRemaining()
    {
        TodoBoard board = new();
        board.Add(["a", "b", "c"], addToHitList: true, Now);
        Guid a = board.AllItems[0].Id;

        Assert.IsTrue(board.SetDone(a, true, Now));

        CollectionAssert.AreEqual(new[] { "b", "c", "a" }, Texts(board.AllItems));
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, Texts(board.HitListItems), "Done tasks keep their hit list slot.");
        Assert.AreEqual(2, board.HitListRemaining);
    }

    [TestMethod]
    public void SetDone_ListsMostRecentlyCompletedFirst()
    {
        TodoBoard board = new();
        board.Add(["a", "b", "c"], addToHitList: false, Now);

        board.SetDone(board.AllItems[0].Id, true, Now); // "a"
        board.SetDone(board.AllItems[0].Id, true, Now.AddMinutes(1)); // "b"

        CollectionAssert.AreEqual(new[] { "c", "b", "a" }, Texts(board.AllItems));
    }

    [TestMethod]
    public void SetDone_Reopening_ReturnsTaskToTheTop()
    {
        TodoBoard board = new();
        board.Add(["a", "b", "c"], addToHitList: false, Now);
        Guid c = board.AllItems[2].Id;
        board.SetDone(c, true, Now);

        board.SetDone(c, false, Now);

        CollectionAssert.AreEqual(new[] { "c", "a", "b" }, Texts(board.AllItems));
        Assert.IsNull(board.Find(c)!.CompletedAt);
    }

    [TestMethod]
    public void ClearHitList_EmptiesItWithoutTouchingTasks()
    {
        TodoBoard board = new();
        board.Add(["a", "b"], addToHitList: true, Now);
        board.SetDone(board.AllItems[0].Id, true, Now);

        Assert.AreEqual(2, board.ClearHitList());

        Assert.AreEqual(0, board.HitListCount);
        Assert.AreEqual(0, board.HitListRemaining);
        Assert.AreEqual(2, board.AllItems.Count);
        Assert.AreEqual(1, board.CompletedCount);
    }

    [TestMethod]
    public void AddToHitList_IgnoresDuplicatesAndUnknownIds()
    {
        TodoBoard board = new();
        Guid a = board.Add("a", addToHitList: false, Now)!.Id;

        Assert.IsTrue(board.AddToHitList(a));
        Assert.IsFalse(board.AddToHitList(a));
        Assert.IsFalse(board.AddToHitList(Guid.NewGuid()));
        Assert.AreEqual(1, board.HitListCount);
    }

    [TestMethod]
    public void MoveInHitList_ClampsTheTargetIndex()
    {
        TodoBoard board = new();
        board.Add(["a", "b", "c"], addToHitList: true, Now);
        Guid a = board.HitListItems[0].Id;

        Assert.IsTrue(board.MoveInHitList(a, 99));

        CollectionAssert.AreEqual(new[] { "b", "c", "a" }, Texts(board.HitListItems));
    }

    [TestMethod]
    public void SetHitListOrder_KeepsMissingIdsAndDropsStrangers()
    {
        TodoBoard board = new();
        board.Add(["a", "b", "c"], addToHitList: true, Now);
        Guid[] ids = [.. board.HitListItems.Select(i => i.Id)];

        board.SetHitListOrder([ids[2], Guid.NewGuid(), ids[0]]);

        CollectionAssert.AreEqual(new[] { "c", "a", "b" }, Texts(board.HitListItems));
    }

    [TestMethod]
    public void SetOpenOrder_ReordersOpenTasksAndLeavesDoneOnes()
    {
        TodoBoard board = new();
        board.Add(["a", "b", "c", "d"], addToHitList: false, Now);
        Guid[] ids = [.. board.AllItems.Select(i => i.Id)];
        board.SetDone(ids[1], true, Now);

        Assert.IsTrue(board.SetOpenOrder([ids[3], ids[1], ids[0]]));

        CollectionAssert.AreEqual(new[] { "d", "a", "c", "b" }, Texts(board.AllItems));
    }

    [TestMethod]
    public void SetOpenOrder_SameOrder_ReportsNoChange()
    {
        TodoBoard board = new();
        board.Add(["a", "b"], addToHitList: false, Now);

        Assert.IsFalse(board.SetOpenOrder(board.AllItems.Select(i => i.Id)));
    }

    [TestMethod]
    public void MoveOpenTask_ClampsTheTargetIndex()
    {
        TodoBoard board = new();
        board.Add(["a", "b", "c"], addToHitList: false, Now);
        Guid a = board.AllItems[0].Id;

        Assert.IsTrue(board.MoveOpenTask(a, 99));

        CollectionAssert.AreEqual(new[] { "b", "c", "a" }, Texts(board.AllItems));
    }

    [TestMethod]
    public void Remove_TakesTaskOffTheHitListToo()
    {
        TodoBoard board = new();
        Guid a = board.Add("a", addToHitList: true, Now)!.Id;

        Assert.IsTrue(board.Remove(a));

        Assert.AreEqual(0, board.HitListCount);
        Assert.IsNull(board.Find(a));
    }

    [TestMethod]
    public void RemoveCompleted_DeletesOnlyDoneTasks()
    {
        TodoBoard board = new();
        board.Add(["a", "b"], addToHitList: true, Now);
        board.SetDone(board.AllItems[0].Id, true, Now);

        Assert.AreEqual(1, board.RemoveCompleted());

        CollectionAssert.AreEqual(new[] { "b" }, Texts(board.AllItems));
        CollectionAssert.AreEqual(new[] { "b" }, Texts(board.HitListItems));
    }

    [TestMethod]
    public void Rename_TrimsAndRejectsBlank()
    {
        TodoBoard board = new();
        Guid a = board.Add("a", addToHitList: false, Now)!.Id;

        Assert.IsFalse(board.Rename(a, "  "));
        Assert.IsTrue(board.Rename(a, " renamed "));
        Assert.AreEqual("renamed", board.Find(a)!.Text);
    }

    [TestMethod]
    public void Constructor_RepairsBadData()
    {
        TodoItem a = new() { Text = "a" };
        TodoData data = new()
        {
            Items = [a, new TodoItem { Id = a.Id, Text = "dupe" }, new TodoItem { Text = " " }],
            HitList = [a.Id, a.Id, Guid.NewGuid()],
        };

        TodoBoard board = new(data);

        CollectionAssert.AreEqual(new[] { "a" }, Texts(board.AllItems));
        Assert.AreEqual(1, board.HitListCount);
    }

    private static string[] Texts(IEnumerable<TodoItem> items) => [.. items.Select(i => i.Text)];
}
