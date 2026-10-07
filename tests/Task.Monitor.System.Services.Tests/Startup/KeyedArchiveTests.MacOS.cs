#if __APPLE__
using Task.Monitor.Interop.Mach;
using Task.Monitor.System.Services.Startup;

namespace Task.Monitor.System.Services.Tests.Startup;

public sealed class KeyedArchiveTests
{
    // A hand built archive: $objects[1] is the root, $objects[2] its class.
    private static Dictionary<string, object?> Archive(params object?[] objects) => new() {
        ["$archiver"] = "NSKeyedArchiver",
        ["$top"]      = new Dictionary<string, object?> { ["root"] = new PropertyListUid(1) },
        ["$objects"]  = objects.ToList()
    };

    private static Dictionary<string, object?> Class(string name) =>
        new() { ["$classname"] = name, ["$classes"] = new List<object?> { name, "NSObject" } };

    private static Dictionary<string, object?> Object(long classUid, params (string Key, object? Value)[] fields)
    {
        Dictionary<string, object?> result = fields.ToDictionary(field => field.Key, field => field.Value);
        result["$class"] = new PropertyListUid(classUid);
        return result;
    }

    [Fact]
    public void Decodes_The_Foundation_Archive_Fixture()
    {
        Dictionary<string, object?>? top = KeyedArchive.Decode(PropertyList.ParseKeyedArchive(BackgroundItemsFixture.Bytes));

        Assert.NotNull(top);
        Dictionary<string, object?> store = Assert.IsType<Dictionary<string, object?>>(top["store"]);
        Assert.Equal("Storage", store[KeyedArchive.ClassKey]);

        Dictionary<string, object?> itemsByUser = Assert.IsType<Dictionary<string, object?>>(store["itemsByUserIdentifier"]);
        List<object?> userItems = Assert.IsType<List<object?>>(itemsByUser[BackgroundItemsFixture.UserUuid]);
        Dictionary<string, object?> example = Assert.IsType<Dictionary<string, object?>>(userItems[0]);

        Assert.Equal("ItemRecord", example[KeyedArchive.ClassKey]);
        Assert.Equal("Example", example["name"]);
        Assert.Equal(2L, example["type"]);
        Assert.Equal("file:///Applications/Example%20App.app/", example["url"]);
        Assert.Equal("8A3D9EFD-3AAA-42FE-930A-17E385C436C5", example["uuid"]);
    }

    [Fact]
    public void Resolves_A_Relative_Url_Against_Its_Base()
    {
        Dictionary<string, object?>? top = KeyedArchive.Decode(PropertyList.ParseKeyedArchive(BackgroundItemsFixture.Bytes));

        List<object?> userItems = (List<object?>)((Dictionary<string, object?>)((Dictionary<string, object?>)top!["store"]!)
            ["itemsByUserIdentifier"]!)[BackgroundItemsFixture.UserUuid]!;

        Dictionary<string, object?> based = Assert.IsType<Dictionary<string, object?>>(userItems[2]);
        Assert.Equal(
            "file:///Applications/Example%20App.app/Contents/Library/LaunchAgents/com.example.agent.plist",
            based["url"]);
    }

    [Fact]
    public void Decodes_Null_References_As_Null()
    {
        Dictionary<string, object?>? top = KeyedArchive.Decode(Archive(
            "$null",
            Object(2, ("value", new PropertyListUid(0))),
            Class("Thing")));

        Dictionary<string, object?> root = Assert.IsType<Dictionary<string, object?>>(top!["root"]);
        Assert.True(root.ContainsKey("value"));
        Assert.Null(root["value"]);
    }

    [Fact]
    public void Breaks_A_Reference_Cycle()
    {
        Dictionary<string, object?>? top = KeyedArchive.Decode(Archive(
            "$null",
            Object(2, ("self", new PropertyListUid(1))),
            Class("Thing")));

        Dictionary<string, object?> root = Assert.IsType<Dictionary<string, object?>>(top!["root"]);
        Assert.Null(root["self"]);
    }

    [Fact]
    public void Decodes_A_Dangling_Reference_As_Null()
    {
        Dictionary<string, object?>? top = KeyedArchive.Decode(Archive(
            "$null",
            Object(2, ("missing", new PropertyListUid(99))),
            Class("Thing")));

        Assert.Null(Assert.IsType<Dictionary<string, object?>>(top!["root"])["missing"]);
    }

    [Fact]
    public void Decodes_Foundation_Collections()
    {
        Dictionary<string, object?>? top = KeyedArchive.Decode(Archive(
            "$null",
            Object(2, ("NS.keys", new List<object?> { new PropertyListUid(3) }), ("NS.objects", new List<object?> { new PropertyListUid(4) })),
            Class("NSDictionary"),
            "key",
            Object(5, ("NS.objects", new List<object?> { "a", new PropertyListUid(6) })),
            Class("NSArray"),
            Object(7, ("NS.string", "text")),
            Class("NSMutableString")));

        Dictionary<string, object?> dictionary = Assert.IsType<Dictionary<string, object?>>(top!["root"]);
        Assert.Equal(["a", "text"], Assert.IsType<List<object?>>(dictionary["key"]));
    }

    [Fact]
    public void Decodes_An_NSDate_From_The_2001_Reference_Date()
    {
        Dictionary<string, object?>? top = KeyedArchive.Decode(Archive(
            "$null",
            Object(2, ("NS.time", 86400.0)),
            Class("NSDate")));

        Assert.Equal(new DateTime(2001, 1, 2, 0, 0, 0, DateTimeKind.Utc), top!["root"]);
    }

    [Fact]
    public void Returns_Null_For_Something_That_Is_Not_A_Keyed_Archive()
    {
        Assert.Null(KeyedArchive.Decode(null));
        Assert.Null(KeyedArchive.Decode(new Dictionary<string, object?> { ["Label"] = "com.example.agent" }));
        Assert.Null(KeyedArchive.Decode(new Dictionary<string, object?> {
            ["$archiver"] = "SomethingElse",
            ["$objects"]  = new List<object?>(),
            ["$top"]      = new Dictionary<string, object?>()
        }));
    }
}
#endif
