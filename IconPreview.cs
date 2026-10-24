// Renders the tray icon in every state onto one PNG: an 8x pixel-exact zoom plus the actual size on a
// taskbar-coloured strip. Dev tool only. Usage: IconPreview.exe out.png [size]

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

static class IconPreview
{
    static void Main(string[] args)
    {
        string outPath = args.Length > 0 ? args[0] : "icons.png";
        int size = args.Length > 1 ? int.Parse(args[1]) : 16;
        const int zoom = 8;
        int cell = size * zoom + 24;

        Color white = Color.White, grey = Color.FromArgb(150, 150, 150);
        Color amber = Color.FromArgb(240, 170, 40), red = Color.FromArgb(235, 70, 60), green = Color.FromArgb(60, 200, 90);
        var states = new[] {
            Tuple.Create("100%", "100", white, false), Tuple.Create("70%", "70", white, false),
            Tuple.Create("30%", "30", amber, false), Tuple.Create("10%", "10", red, false),
            Tuple.Create("charging", "40", green, true), Tuple.Create("off", "40", grey, false),
            Tuple.Create("starting", "?", grey, false), Tuple.Create("no dongle", "x", grey, false) };
        string digits = "0123456789";

        Color taskbar = Color.FromArgb(32, 32, 32);
        using (var sheet = new Bitmap(states.Length * cell + 16, 2 * cell + 120))
        using (var g = Graphics.FromImage(sheet))
        using (var font = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var text = new SolidBrush(Color.FromArgb(220, 220, 220)))
        using (var bar = new SolidBrush(taskbar))
        {
            g.Clear(Color.FromArgb(18, 18, 18));
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            for (int c = 0; c < states.Length; c++)
            {
                int x = 8 + c * cell, y = 30;
                g.DrawString(states[c].Item1, font, text, x, 10);
                using (var icon = IconArt.Render(states[c].Item2, states[c].Item3, states[c].Item4, size))
                {
                    g.FillRectangle(bar, x, y, size * zoom, size * zoom);
                    g.DrawImage(icon, new Rectangle(x, y, size * zoom, size * zoom));
                    g.FillRectangle(bar, x, y + size * zoom + 6, size + 24, size + 12);
                    g.DrawImageUnscaled(icon, x + 12, y + size * zoom + 12);
                }
            }
            // every digit pair, actual size, to eyeball the whole font
            int rowY = 30 + cell + 40;
            g.DrawString("all values, actual size:", font, text, 8, rowY - 22);
            g.FillRectangle(bar, 8, rowY, 11 * (size + 8) + 8, size + 12);
            for (int i = 0; i <= 10; i++)
                using (var icon = IconArt.Render((i * 10).ToString(), Color.White, false, size))
                    g.DrawImageUnscaled(icon, 16 + i * (size + 8), rowY + 6);
            int rowY2 = rowY + size + 20;
            g.FillRectangle(bar, 8, rowY2, 10 * (size + 8) + 8, size + 12);
            for (int i = 0; i < 10; i++)
                using (var icon = IconArt.Render(digits[i].ToString() + digits[(i + 3) % 10], Color.White, false, size))
                    g.DrawImageUnscaled(icon, 16 + i * (size + 8), rowY2 + 6);
            sheet.Save(outPath, ImageFormat.Png);
        }
    }
}
