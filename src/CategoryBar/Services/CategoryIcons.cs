using System.Globalization;
using System.Windows.Media.Imaging;

namespace CategoryBar.Services;

/// <summary>
/// Icons of categories on the Windows taskbar: an application's icon or a symbol on a coloured tile, with an
/// optional mark for a running application, written as .ico files.
/// The symbols come from the icon font that ships with Windows, so nothing is bundled with the application.
/// </summary>
internal static class CategoryIcons
{
    internal static readonly string[] Colors = ["#3B82F6", "#8B5CF6", "#EC4899", "#EF4444", "#F59E0B", "#10B981", "#14B8A6", "#64748B"];

    // Code, web, chat, mail, music, video, games, work, settings, folder, star, heart, picture, camera, terminal,
    // cloud, library, repair, shield, shop, edit, calculator, calendar, people, contact, home, phone, download,
    // document, print, map, education, idea, robot, colour, headphones, microphone, pin, lock, bug, computer,
    // devices, emoji, world, tag, airplane, car, health, puzzle, flag.
    private static readonly int[] Codes =
    [
        0xE943, 0xE774, 0xE8BD, 0xE715, 0xE8D6, 0xE714, 0xE7FC, 0xE821, 0xE713, 0xE8B7, 0xE734, 0xEB51, 0xE91B, 0xE722, 0xE756,
        0xE753, 0xE8F1, 0xE90F, 0xEA18, 0xE719, 0xE70F, 0xE8EF, 0xE787, 0xE716, 0xE77B, 0xE80F, 0xE717, 0xE896,
        0xE8A5, 0xE749, 0xE707, 0xE7BE, 0xEA80, 0xE99A, 0xE790, 0xE7F6, 0xE720, 0xE718, 0xE72E, 0xEBE8, 0xE977,
        0xE772, 0xE76E, 0xE909, 0xE8EC, 0xE709, 0xE804, 0xE95E, 0xEA86, 0xE7C1
    ];
    private const int FolderCode = 0xE8B7;

    private static Typeface? typeface;
    private static List<string>? glyphs;

    /// <summary>The symbols the installed icon font really has; empty when Windows has no such font.</summary>
    internal static IReadOnlyList<string> Glyphs
    {
        get
        {
            if (glyphs is not null) return glyphs;
            foreach (var family in new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" })
            {
                var candidate = new Typeface(family);
                if (!candidate.TryGetGlyphTypeface(out var face)) continue;
                var present = Codes.Where(face.CharacterToGlyphMap.ContainsKey).Select(code => ((char)code).ToString()).ToList();
                if (present.Count < Codes.Length / 2) continue;
                typeface = candidate;
                return glyphs = present;
            }
            return glyphs = [];
        }
    }

    internal static FontFamily? Font => Glyphs.Count > 0 ? typeface!.FontFamily : null;
    internal static string? DefaultGlyph => Glyphs.Contains(((char)FolderCode).ToString()) ? ((char)FolderCode).ToString() : Glyphs.FirstOrDefault();

    internal static Color ColorFor(string name)
    {
        var hash = 17;
        foreach (var c in name) hash = unchecked(hash * 31 + c);
        return Theme.Parse(Colors[(int)((uint)hash % Colors.Length)]);
    }

    internal static string Format(string glyph, string color) => $"glyph:{(int)glyph[0]:X4}:{color}";

    /// <summary>Reads "glyph:E943:#3B82F6". False for other kinds of icon and for symbols this Windows does not have.</summary>
    internal static bool TryGlyph(string icon, out string glyph, out Color color)
    {
        glyph = ""; color = default;
        var parts = icon.Split(':');
        if (parts.Length != 3 || parts[0] != "glyph" || !Settings.IsHexColor(parts[2])
            || !int.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code)) return false;
        glyph = ((char)code).ToString(); color = Theme.Parse(parts[2]);
        return Glyphs.Contains(glyph);
    }

    /// <summary>Reads "text:ABC": up to three characters drawn on a tile in the theme's accent colour.</summary>
    internal static bool TryText(string icon, out string text)
    {
        text = icon.StartsWith("text:", StringComparison.Ordinal) ? icon[5..].Trim() : "";
        return text.Length > 0 && new StringInfo(text).LengthInTextElements <= 3;
    }

    /// <summary>
    /// Draws one size of an icon: the application's image when there is one, otherwise the symbol or the
    /// letters on a tile.
    /// </summary>
    internal static BitmapSource Render(int size, ImageSource? application, string? glyph, Color color, bool running, string? text = null, FontFamily? font = null)
    {
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (var context = visual.RenderOpen())
        {
            if (application is not null) context.DrawImage(application, new Rect(0, 0, size, size));
            else
            {
                var inset = size / 16.0;
                context.DrawRoundedRectangle(new SolidColorBrush(color), null, new Rect(inset, inset, size - inset * 2, size - inset * 2), size * .2, size * .2);
                if (text is not null)
                {
                    // Dark letters on a light tile and light ones on a dark tile, whatever accent the theme has.
                    var ink = (color.R * 299 + color.G * 587 + color.B * 114) / 1000 > 150 ? Color.FromRgb(0x16, 0x16, 0x1A) : System.Windows.Media.Colors.White;
                    var letters = new Typeface(font ?? new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                    var scale = new StringInfo(text).LengthInTextElements switch { 1 => .6, 2 => .46, _ => .36 };
                    var drawn = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, letters, size * scale, new SolidColorBrush(ink), 1);
                    var room = (size - inset * 2) * .86;
                    if (drawn.Width > room) drawn.SetFontSize(size * scale * room / drawn.Width);
                    context.DrawText(drawn, new Point((size - drawn.Width) / 2, (size - drawn.Height) / 2));
                }
                else if (glyph is not null && typeface is not null)
                {
                    var symbol = new FormattedText(glyph, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, size * .52, Brushes.White, 1);
                    context.DrawText(symbol, new Point((size - symbol.Width) / 2, (size - symbol.Height) / 2));
                }
            }
            if (running)
            {
                // A green dot with a dark rim stays readable on any icon and on both taskbar colours.
                var radius = size * .19;
                var center = new Point(size - radius, size - radius);
                context.DrawEllipse(new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1F)), null, center, radius, radius);
                context.DrawEllipse(new SolidColorBrush(Color.FromRgb(0x2F, 0xD1, 0x6B)), null, center, radius * .7, radius * .7);
            }
        }
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>A thin vertical line, grey enough to show on a dark and on a light taskbar.</summary>
    internal static BitmapSource RenderDivider(int size)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            var width = Math.Max(1, Math.Round(size / 16.0));
            context.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0xD0, 0x8C, 0x8C, 0x92)), null,
                new Rect(Math.Round((size - width) / 2), Math.Round(size * .17), width, Math.Round(size * .66)), width / 2, width / 2);
        }
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>An .ico file with the sizes the taskbar asks for at 100–200% scaling, plus a large one.</summary>
    internal static byte[] Ico(Func<int, BitmapSource> render)
    {
        int[] sizes = [16, 20, 24, 32, 40, 48, 64, 256];
        var images = sizes.Select(size => Encode(render(size), size)).ToList();
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((short)0); writer.Write((short)1); writer.Write((short)sizes.Length);
        var offset = 6 + 16 * sizes.Length;
        for (var i = 0; i < sizes.Length; i++)
        {
            var edge = (byte)(sizes[i] == 256 ? 0 : sizes[i]);
            writer.Write(edge); writer.Write(edge); writer.Write((byte)0); writer.Write((byte)0);
            writer.Write((short)1); writer.Write((short)32); writer.Write(images[i].Length); writer.Write(offset);
            offset += images[i].Length;
        }
        foreach (var image in images) writer.Write(image);
        return stream.ToArray();
    }

    /// <summary>The large image is a PNG; the small ones are classic bitmaps, which every icon reader understands.</summary>
    private static byte[] Encode(BitmapSource image, int size)
    {
        using var stream = new MemoryStream();
        if (size == 256)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            encoder.Save(stream);
            return stream.ToArray();
        }
        var stride = size * 4;
        var pixels = new byte[stride * size];
        new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0).CopyPixels(pixels, stride, 0);
        var mask = (size + 31) / 32 * 4 * size;
        using var writer = new BinaryWriter(stream);
        writer.Write(40); writer.Write(size); writer.Write(size * 2); writer.Write((short)1); writer.Write((short)32);
        writer.Write(0); writer.Write(pixels.Length + mask); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
        for (var row = size - 1; row >= 0; row--) writer.Write(pixels, row * stride, stride);
        writer.Write(new byte[mask]);
        return stream.ToArray();
    }
}
