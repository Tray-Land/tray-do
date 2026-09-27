using System.Buffers.Binary;
using TrayDo.Tray;

namespace TrayDo.Tests.Tray;

[TestClass]
public sealed class BadgeIconTests
{
    [TestMethod]
    [DataRow(16)]
    [DataRow(20)]
    [DataRow(32)]
    public void Create_WritesASingle32BitIconOfTheRequestedSize(int size)
    {
        byte[] ico = BadgeIcon.Create(7, size, 0xFF005FB8, 0xFFFFFFFF);

        Assert.AreEqual(1, BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(2)), "type");
        Assert.AreEqual(1, BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(4)), "count");
        Assert.AreEqual(size, ico[6]);
        Assert.AreEqual(32, BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(12)));
        Assert.AreEqual(ico.Length - BadgeIcon.ImageOffset, BinaryPrimitives.ReadInt32LittleEndian(ico.AsSpan(14)));
    }

    [TestMethod]
    public void Create_CornersAreTransparentAndCenterIsOpaque()
    {
        const int size = 16;
        byte[] ico = BadgeIcon.Create(1, size, 0xFF005FB8, 0xFFFFFFFF);

        Assert.AreEqual(0, AlphaAt(ico, size, 0, 0));
        Assert.AreEqual(255, AlphaAt(ico, size, size / 2, size / 2));
    }

    [TestMethod]
    public void Create_FromCoverage_BlendsForegroundOverBackground()
    {
        const int size = 16;
        byte[] coverage = new byte[size * size];
        coverage[(8 * size) + 8] = 255;

        byte[] ico = BadgeIcon.Create(coverage, size, 0xFF000000, 0xFFFFFFFF);

        Assert.AreEqual(0xFFFFFFFFu, PixelAt(ico, size, 8, 8));
        Assert.AreEqual(0xFF000000u, PixelAt(ico, size, 7, 8));
    }

    private static byte AlphaAt(byte[] ico, int size, int x, int y) => (byte)(PixelAt(ico, size, x, y) >> 24);

    // The DIB is stored bottom-up after the 40-byte header.
    private static uint PixelAt(byte[] ico, int size, int x, int y) =>
        BinaryPrimitives.ReadUInt32LittleEndian(ico.AsSpan(BadgeIcon.ImageOffset + 40 + ((((size - 1 - y) * size) + x) * 4)));
}
