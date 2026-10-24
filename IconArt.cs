// Tray icon artwork for Pulse 3D Battery: a hand-drawn pixel font so the number stays sharp at 16x16.
// Every pixel is fully on or off (no anti-aliasing); at higher DPI the glyphs scale by whole pixels.

using System;
using System.Collections.Generic;
using System.Drawing;

public static class IconArt
{
    const int GlyphH = 12;

    // 6x12 glyphs with 2-pixel strokes, used for one- and two-character labels
    static readonly Dictionary<char, string[]> Wide = new Dictionary<char, string[]> {
        { '0', new[] { ".####.", "######", "##..##", "##..##", "##..##", "##..##", "##..##", "##..##", "##..##", "##..##", "######", ".####." } },
        { '1', new[] { "...##.", "..###.", ".####.", "...##.", "...##.", "...##.", "...##.", "...##.", "...##.", "...##.", "...##.", "...##." } },
        { '2', new[] { ".####.", "######", "##..##", "....##", "....##", "...###", "..###.", ".###..", "###...", "##....", "######", "######" } },
        { '3', new[] { ".####.", "######", "##..##", "....##", "....##", "..###.", "..####", "....##", "....##", "##..##", "######", ".####." } },
        { '4', new[] { "##..##", "##..##", "##..##", "##..##", "##..##", "######", "######", "....##", "....##", "....##", "....##", "....##" } },
        { '5', new[] { "######", "######", "##....", "##....", "##....", "#####.", "######", "....##", "....##", "##..##", "######", ".####." } },
        { '6', new[] { ".####.", "######", "##..##", "##....", "##....", "#####.", "######", "##..##", "##..##", "##..##", "######", ".####." } },
        { '7', new[] { "######", "######", "....##", "....##", "...##.", "...##.", "..##..", "..##..", "..##..", "..##..", "..##..", "..##.." } },
        { '8', new[] { ".####.", "######", "##..##", "##..##", "##..##", ".####.", "######", "##..##", "##..##", "##..##", "######", ".####." } },
        { '9', new[] { ".####.", "######", "##..##", "##..##", "##..##", "######", ".#####", "....##", "....##", "##..##", "######", ".####." } },
        { '?', new[] { ".####.", "######", "##..##", "....##", "...###", "..###.", "..##..", "..##..", "......", "......", "..##..", "..##.." } },
        { 'x', new[] { "......", "......", "......", "......", "##..##", ".####.", "..##..", ".####.", "##..##", "......", "......", "......" } },
    };

    // narrower glyphs so "100" fits in 16 pixels (3 + 1 + 5 + 1 + 5 = 15)
    static readonly Dictionary<char, string[]> Narrow = new Dictionary<char, string[]> {
        { '1', new[] { ".##", "###", ".##", ".##", ".##", ".##", ".##", ".##", ".##", ".##", ".##", ".##" } },
        { '0', new[] { ".###.", "#####", "##.##", "##.##", "##.##", "##.##", "##.##", "##.##", "##.##", "##.##", "#####", ".###." } },
    };

    /// <summary>Renders the label (digits, "?" or "x") in the given colour, with an optional charging bar.</summary>
    public static Bitmap Render(string text, Color color, bool chargeBar, int size)
    {
        var glyphs = new List<string[]>();
        var font = text.Length >= 3 ? Narrow : Wide;
        foreach (char ch in text)
        {
            string[] g;
            if (font.TryGetValue(ch, out g) || Wide.TryGetValue(ch, out g)) glyphs.Add(g);
        }

        int gap = 1, width = -gap;
        foreach (var g in glyphs) width += g[0].Length + gap;
        if (glyphs.Count == 0) width = 0;

        // whole-pixel scale so higher DPI stays sharp; 16 px design grid centred in the icon
        int scale = Math.Max(1, size / 16);
        int ox = (size - 16 * scale) / 2 + (16 - width) / 2 * scale;
        int oy = (size - 16 * scale) / 2 + (chargeBar ? 1 : 2) * scale;

        var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var gfx = Graphics.FromImage(bmp))
        using (var brush = new SolidBrush(color))
        {
            int x = ox;
            foreach (var g in glyphs)
            {
                for (int row = 0; row < GlyphH; row++)
                    for (int col = 0; col < g[row].Length; col++)
                        if (g[row][col] == '#')
                            gfx.FillRectangle(brush, x + col * scale, oy + row * scale, scale, scale);
                x += (g[0].Length + gap) * scale;
            }
            if (chargeBar)
            {
                int barY = (size - 16 * scale) / 2 + 14 * scale;
                gfx.FillRectangle(brush, (size - 16 * scale) / 2 + 1 * scale, barY, 14 * scale, 2 * scale);
            }
        }
        return bmp;
    }
}
