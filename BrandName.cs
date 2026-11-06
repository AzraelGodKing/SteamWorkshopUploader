namespace SteamCmdHelper;

/// <summary>
/// The app's name in the brand bar: "Workshop" in the title colour and font, then "Uploader" in the light brand font
/// and colour. Two labels side by side would leave the padding WinForms puts around label text between them.
/// </summary>
sealed class BrandName : Label
{
    const string First = "Workshop ", Second = "Uploader";
    const TextFormatFlags Flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;

    public BrandName()
    {
        Text = First + Second;
        AccessibleName = Text;
        UseMnemonic = false;
        AutoSize = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
    }

    Size Part(string text, Font font) => TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue), Flags);

    public override Size GetPreferredSize(Size proposedSize)
    {
        var a = Part(First, Theme.TitleFont);
        var b = Part(Second, Theme.BrandFont);
        return new Size(a.Width + b.Width + Padding.Horizontal, Math.Max(a.Height, b.Height) + Padding.Vertical);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using (var back = new SolidBrush(BackColor)) e.Graphics.FillRectangle(back, ClientRectangle);
        var a = Part(First, Theme.TitleFont);
        var b = Part(Second, Theme.BrandFont);
        var height = Math.Max(a.Height, b.Height);
        // Both parts share a baseline: the fonts are the same size, so lining up their bottoms is enough.
        TextRenderer.DrawText(e.Graphics, First, Theme.TitleFont, new Point(Padding.Left, Padding.Top + height - a.Height), ForeColor, Flags);
        TextRenderer.DrawText(e.Graphics, Second, Theme.BrandFont, new Point(Padding.Left + a.Width, Padding.Top + height - b.Height), Theme.Banner.Brand, Flags);
    }
}
