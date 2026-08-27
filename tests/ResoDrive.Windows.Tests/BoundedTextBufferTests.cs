using ResoDrive.Windows;

namespace ResoDrive.Windows.Tests;

public sealed class BoundedTextBufferTests
{
    [Fact]
    public void Append_RetainsNewestCharactersAcrossWraparound()
    {
        var buffer = new BoundedTextBuffer(8);

        buffer.Append("abcde");
        buffer.Append("fghij");

        Assert.Equal("cdefghij", buffer.ToString());
    }

    [Fact]
    public void Append_ValueLargerThanCapacityRetainsItsTail()
    {
        var buffer = new BoundedTextBuffer(5);

        buffer.Append("0123456789");

        Assert.Equal("56789", buffer.ToString());
    }
}
