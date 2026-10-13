using DroidEdge.Core.Clipboard;

namespace DroidEdge.Tests;

public class ClipboardSyncStateTests
{
    [Fact]
    public void Pushes_new_text_once()
    {
        var s = new ClipboardSyncState();
        Assert.True(s.ShouldPush("hello", excluded: false));
        s.MarkSynced("hello");
        Assert.False(s.ShouldPush("hello", excluded: false));
        Assert.True(s.ShouldPush("world", excluded: false));
    }

    [Fact]
    public void Never_pushes_empty_or_excluded_text()
    {
        var s = new ClipboardSyncState();
        Assert.False(s.ShouldPush(null, excluded: false));
        Assert.False(s.ShouldPush("", excluded: false));
        Assert.False(s.ShouldPush("secret", excluded: true));
    }

    [Fact]
    public void Text_received_from_the_phone_is_not_pushed_back()
    {
        var s = new ClipboardSyncState();
        s.MarkSynced("from phone");
        Assert.False(s.ShouldPush("from phone", excluded: false));
    }
}
