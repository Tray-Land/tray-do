using System.Buffers.Binary;

namespace TrayDo.Tray;

/// <summary>
/// Draws the hit list badge (a rounded square with one or two digits) as a single-image,
/// 32-bit .ico file, using a tiny built-in pixel font so it needs nothing beyond the BCL.
/// </summary>
public static class BadgeIcon
{
    /// <summary>Offset of the image (BITMAPINFOHEADER onward) in the .ico: ICONDIR plus one ICONDIRENTRY.</summary>
    public const int ImageOffset = 6 + 16;

    private const int GlyphWidth = 3;
    private const int GlyphHeight = 5;
    private const int BitmapInfoHeaderSize = 40;

    // 3x5 glyphs, one byte per row, the low three bits left to right.
    private static readonly byte[][] Digits =
    [
        [0b111, 0b101, 0b101, 0b101, 0b111],
        [0b010, 0b110, 0b010, 0b010, 0b111],
        [0b111, 0b001, 0b111, 0b100, 0b111],
        [0b111, 0b001, 0b111, 0b001, 0b111],
        [0b101, 0b101, 0b111, 0b001, 0b001],
        [0b111, 0b100, 0b111, 0b001, 0b111],
        [0b111, 0b100, 0b111, 0b101, 0b111],
        [0b111, 0b001, 0b001, 0b001, 0b001],
        [0b111, 0b101, 0b111, 0b101, 0b111],
        [0b111, 0b101, 0b111, 0b001, 0b111],
    ];

    /// <param name="count">Number to show; drawn as 0-99.</param>
    /// <param name="size">Icon width and height in pixels (1-255).</param>
    /// <param name="background">Badge color as 0xAARRGGBB.</param>
    /// <param name="foreground">Digit color as 0xAARRGGBB.</param>
    public static byte[] Create(int count, int size, uint background, uint foreground)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(size, 255);

        uint[] pixels = new uint[size * size];
        FillRoundedSquare(pixels, size, background);
        DrawText(pixels, size, GlyphsFor(count), foreground);
        return Encode(pixels, size);
    }

    /// <summary>Draws the badge with text rendered elsewhere (e.g. in a real font) instead of the pixel font.</summary>
    /// <param name="textCoverage">Text coverage per pixel, 0-255, row-major top-down; <paramref name="size"/> squared long.</param>
    /// <param name="size">Icon width and height in pixels (1-255).</param>
    /// <param name="background">Badge color as 0xAARRGGBB.</param>
    /// <param name="foreground">Text color as 0xAARRGGBB.</param>
    public static byte[] Create(ReadOnlySpan<byte> textCoverage, int size, uint background, uint foreground)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(size, 255);
        ArgumentOutOfRangeException.ThrowIfNotEqual(textCoverage.Length, size * size, nameof(textCoverage));

        uint[] pixels = new uint[size * size];
        FillRoundedSquare(pixels, size, background);
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Blend(pixels[i], foreground, textCoverage[i]);
        }

        return Encode(pixels, size);
    }

    private static byte[][] GlyphsFor(int count)
    {
        int clamped = Math.Clamp(count, 0, 99);
        return clamped < 10 ? [Digits[clamped]] : [Digits[clamped / 10], Digits[clamped % 10]];
    }

    private static void FillRoundedSquare(uint[] pixels, int size, uint color)
    {
        double radius = size / 5.0;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Distance past the corner arc, sampled at the pixel center, gives cheap anti-aliasing.
                double dx = Math.Max(0, Math.Abs(x + 0.5 - (size / 2.0)) - ((size / 2.0) - radius));
                double dy = Math.Max(0, Math.Abs(y + 0.5 - (size / 2.0)) - ((size / 2.0) - radius));
                double coverage = Math.Clamp(radius - Math.Sqrt((dx * dx) + (dy * dy)) + 0.5, 0, 1);
                pixels[(y * size) + x] = WithAlpha(color, coverage);
            }
        }
    }

    private static void DrawText(uint[] pixels, int size, IReadOnlyList<byte[]> glyphs, uint color)
    {
        int columns = (glyphs.Count * (GlyphWidth + 1)) - 1;
        int inner = size - (2 * Math.Max(1, size / 16));
        int scale = Math.Max(1, Math.Min(inner / columns, inner / GlyphHeight));
        int left = (size - (columns * scale)) / 2;
        int top = (size - (GlyphHeight * scale)) / 2;

        for (int i = 0; i < glyphs.Count; i++)
        {
            int glyphLeft = left + (i * (GlyphWidth + 1) * scale);
            for (int row = 0; row < GlyphHeight; row++)
            {
                for (int column = 0; column < GlyphWidth; column++)
                {
                    if ((glyphs[i][row] & (1 << (GlyphWidth - 1 - column))) != 0)
                    {
                        FillBlock(pixels, size, glyphLeft + (column * scale), top + (row * scale), scale, color);
                    }
                }
            }
        }
    }

    private static void FillBlock(uint[] pixels, int size, int left, int top, int scale, uint color)
    {
        for (int y = Math.Max(0, top); y < Math.Min(size, top + scale); y++)
        {
            for (int x = Math.Max(0, left); x < Math.Min(size, left + scale); x++)
            {
                pixels[(y * size) + x] = color;
            }
        }
    }

    /// <summary>Mixes <paramref name="top"/> over <paramref name="bottom"/> by <paramref name="coverage"/>/255, per channel.</summary>
    private static uint Blend(uint bottom, uint top, byte coverage)
    {
        uint result = 0;
        for (int shift = 0; shift < 32; shift += 8)
        {
            int from = (int)((bottom >> shift) & 0xFF);
            int to = (int)((top >> shift) & 0xFF);
            result |= (uint)(from + (int)Math.Round((to - from) * coverage / 255.0)) << shift;
        }

        return result;
    }

    private static uint WithAlpha(uint color, double coverage) =>
        ((uint)Math.Round((color >> 24) * coverage) << 24) | (color & 0x00FFFFFF);

    /// <summary>ICONDIR + one ICONDIRENTRY + a 32bpp DIB (bottom-up BGRA, then an all-zero AND mask).</summary>
    private static byte[] Encode(uint[] pixels, int size)
    {
        int pixelBytes = size * size * 4;
        int maskBytes = ((size + 31) / 32) * 4 * size;
        int imageBytes = BitmapInfoHeaderSize + pixelBytes + maskBytes;
        byte[] ico = new byte[ImageOffset + imageBytes];
        Span<byte> span = ico;

        // ICONDIR: reserved, type 1 (icon), one image.
        BinaryPrimitives.WriteUInt16LittleEndian(span[2..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(span[4..], 1);

        // ICONDIRENTRY: width, height, no palette, reserved, planes, bit count, size, offset.
        span[6] = (byte)size;
        span[7] = (byte)size;
        BinaryPrimitives.WriteUInt16LittleEndian(span[10..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(span[12..], 32);
        BinaryPrimitives.WriteInt32LittleEndian(span[14..], imageBytes);
        BinaryPrimitives.WriteInt32LittleEndian(span[18..], ImageOffset);

        // BITMAPINFOHEADER: the height covers the color bitmap and the mask.
        Span<byte> header = span[ImageOffset..];
        BinaryPrimitives.WriteInt32LittleEndian(header, BitmapInfoHeaderSize);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], size);
        BinaryPrimitives.WriteInt32LittleEndian(header[8..], size * 2);
        BinaryPrimitives.WriteUInt16LittleEndian(header[12..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(header[14..], 32);
        BinaryPrimitives.WriteInt32LittleEndian(header[20..], pixelBytes + maskBytes);

        Span<byte> bits = header[BitmapInfoHeaderSize..];
        for (int y = 0; y < size; y++)
        {
            Span<byte> row = bits[((size - 1 - y) * size * 4)..];
            for (int x = 0; x < size; x++)
            {
                // 0xAARRGGBB little-endian is exactly the DIB's B, G, R, A byte order.
                BinaryPrimitives.WriteUInt32LittleEndian(row[(x * 4)..], pixels[(y * size) + x]);
            }
        }

        return ico;
    }
}
