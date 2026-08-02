using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;
using Forms = System.Windows.Forms;

namespace MyCO.Manager.Services;

// A small, self-contained renderer keeps the native tray menu behavior while
// giving it the same quiet surface language as the Manager shell.
internal sealed class TrayMenuRenderer : Forms.ToolStripProfessionalRenderer
{
    private const int MenuCornerRadiusDip = 9;
    private const int MenuPaddingDip = 8;
    private const int MenuVerticalPaddingDip = 6;
    private const int ItemHorizontalPaddingDip = 10;
    private const int ItemVerticalPaddingDip = 6;
    private const int ItemHorizontalMarginDip = 2;
    private const int ItemVerticalMarginDip = 1;
    private const int SeparatorInsetDip = 12;
    private const int SeparatorVerticalMarginDip = 3;

    private TrayMenuPalette _palette = TrayMenuPalette.Light;

    public TrayMenuRenderer()
        : base(new TrayMenuColorTable())
    {
    }

    public void ApplyTheme(Forms.ContextMenuStrip menu, bool dark)
    {
        _palette = dark ? TrayMenuPalette.Dark : TrayMenuPalette.Light;
        menu.BackColor = _palette.Surface;
        menu.ForeColor = _palette.Text;
        menu.Invalidate(true);
    }

    public void ApplyLayout(Forms.ContextMenuStrip menu)
    {
        menu.Padding = ScaledPadding(
            menu,
            MenuPaddingDip,
            MenuVerticalPaddingDip,
            MenuPaddingDip,
            MenuVerticalPaddingDip);

        foreach (var item in menu.Items.OfType<Forms.ToolStripItem>())
        {
            if (item is Forms.ToolStripSeparator)
            {
                item.Margin = ScaledPadding(
                    menu,
                    0,
                    SeparatorVerticalMarginDip,
                    0,
                    SeparatorVerticalMarginDip);
                continue;
            }

            item.Margin = ScaledPadding(
                menu,
                ItemHorizontalMarginDip,
                ItemVerticalMarginDip,
                ItemHorizontalMarginDip,
                ItemVerticalMarginDip);
            item.Padding = ScaledPadding(
                menu,
                ItemHorizontalPaddingDip,
                ItemVerticalPaddingDip,
                ItemHorizontalPaddingDip,
                ItemVerticalPaddingDip);
        }
    }

    protected override void OnRenderToolStripBackground(
        Forms.ToolStripRenderEventArgs eventArgs)
    {
        var bounds = new Drawing.Rectangle(
            Drawing.Point.Empty,
            eventArgs.ToolStrip.ClientSize);
        var radius = Scale(eventArgs.ToolStrip, MenuCornerRadiusDip);
        using var path = TrayMenuGeometry.CreateRoundedPath(bounds, radius);
        using var brush = new Drawing.SolidBrush(_palette.Surface);
        eventArgs.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
        eventArgs.Graphics.FillPath(brush, path);
    }

    protected override void OnRenderMenuItemBackground(
        Forms.ToolStripItemRenderEventArgs eventArgs)
    {
        var toolStrip = eventArgs.ToolStrip;
        if (toolStrip is null)
        {
            return;
        }

        if (!eventArgs.Item.Enabled ||
            (!eventArgs.Item.Selected && !eventArgs.Item.Pressed))
        {
            return;
        }

        var bounds = eventArgs.Item.Bounds;
        var horizontalInset = Scale(toolStrip, 4);
        var verticalInset = Scale(toolStrip, 2);
        bounds.Inflate(-horizontalInset, -verticalInset);
        var radius = Scale(toolStrip, 6);
        using var path = TrayMenuGeometry.CreateRoundedPath(bounds, radius);
        using var brush = new Drawing.SolidBrush(_palette.Hover);
        eventArgs.Graphics.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
        eventArgs.Graphics.FillPath(brush, path);
    }

    protected override void OnRenderSeparator(
        Forms.ToolStripSeparatorRenderEventArgs eventArgs)
    {
        var toolStrip = eventArgs.ToolStrip;
        if (toolStrip is null)
        {
            return;
        }

        var bounds = eventArgs.Item.Bounds;
        var inset = Scale(toolStrip, SeparatorInsetDip);
        var y = bounds.Top + bounds.Height / 2;
        using var pen = new Drawing.Pen(_palette.Separator);
        eventArgs.Graphics.DrawLine(
            pen,
            bounds.Left + inset,
            y,
            bounds.Right - inset,
            y);
    }

    protected override void OnRenderToolStripBorder(
        Forms.ToolStripRenderEventArgs eventArgs)
    {
        // The rounded region and solid surface intentionally have no outline.
    }

    protected override void OnRenderItemText(
        Forms.ToolStripItemTextRenderEventArgs eventArgs)
    {
        eventArgs.TextColor = eventArgs.Item.Enabled
            ? _palette.Text
            : _palette.DisabledText;
        base.OnRenderItemText(eventArgs);
    }

    private static Forms.Padding ScaledPadding(
        Forms.ToolStrip toolStrip,
        int left,
        int top,
        int right,
        int bottom) =>
        new(
            Scale(toolStrip, left),
            Scale(toolStrip, top),
            Scale(toolStrip, right),
            Scale(toolStrip, bottom));

    private static int Scale(Forms.ToolStrip toolStrip, int dip) =>
        Math.Max(0, (int)Math.Round(dip * toolStrip.DeviceDpi / 96d));
}

internal sealed class TrayContextMenuStrip : Forms.ContextMenuStrip
{
    private const int MenuCornerRadiusDip = 9;
    private Drawing.Region? _roundedRegion;

    public TrayContextMenuStrip(TrayMenuRenderer renderer)
    {
        Renderer = renderer;
        ShowCheckMargin = false;
        ShowImageMargin = false;
        AutoSize = true;
        DoubleBuffered = true;
    }

    protected override void OnLayout(Forms.LayoutEventArgs eventArgs)
    {
        base.OnLayout(eventArgs);
        UpdateRoundedRegion();
    }

    protected override void OnSizeChanged(EventArgs eventArgs)
    {
        base.OnSizeChanged(eventArgs);
        UpdateRoundedRegion();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Region = null;
            _roundedRegion?.Dispose();
            _roundedRegion = null;
        }

        base.Dispose(disposing);
    }

    private void UpdateRoundedRegion()
    {
        if (ClientSize.Width < 2 || ClientSize.Height < 2)
        {
            return;
        }

        var bounds = new Drawing.Rectangle(
            Drawing.Point.Empty,
            ClientSize);
        var radius = Math.Max(
            1,
            (int)Math.Round(MenuCornerRadiusDip * DeviceDpi / 96d));
        using var path = TrayMenuGeometry.CreateRoundedPath(bounds, radius);
        var next = new Drawing.Region(path);
        var previous = _roundedRegion;
        _roundedRegion = next;
        Region = next;
        previous?.Dispose();
    }
}

internal readonly record struct TrayMenuPalette(
    Drawing.Color Surface,
    Drawing.Color Text,
    Drawing.Color DisabledText,
    Drawing.Color Hover,
    Drawing.Color Separator)
{
    public static TrayMenuPalette Light => new(
        Drawing.ColorTranslator.FromHtml("#FFFFFF"),
        Drawing.ColorTranslator.FromHtml("#202124"),
        Drawing.ColorTranslator.FromHtml("#9AA39E"),
        Drawing.ColorTranslator.FromHtml("#EEF5F1"),
        Drawing.ColorTranslator.FromHtml("#E1E8E4"));

    public static TrayMenuPalette Dark => new(
        Drawing.ColorTranslator.FromHtml("#1B1D21"),
        Drawing.ColorTranslator.FromHtml("#F3F4F6"),
        Drawing.ColorTranslator.FromHtml("#858A8F"),
        Drawing.ColorTranslator.FromHtml("#2B3230"),
        Drawing.ColorTranslator.FromHtml("#363B3E"));
}

internal sealed class TrayMenuColorTable : Forms.ProfessionalColorTable
{
    public override Drawing.Color MenuBorder => Drawing.Color.Transparent;
    public override Drawing.Color ToolStripDropDownBackground => Drawing.Color.Transparent;
    public override Drawing.Color ImageMarginGradientBegin => Drawing.Color.Transparent;
    public override Drawing.Color ImageMarginGradientMiddle => Drawing.Color.Transparent;
    public override Drawing.Color ImageMarginGradientEnd => Drawing.Color.Transparent;
}

internal static class TrayMenuGeometry
{
    public static Drawing2D.GraphicsPath CreateRoundedPath(
        Drawing.Rectangle bounds,
        int radius)
    {
        var path = new Drawing2D.GraphicsPath();
        radius = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2);
        var diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(
            bounds.Right - diameter,
            bounds.Y,
            diameter,
            diameter,
            270,
            90);
        path.AddArc(
            bounds.Right - diameter,
            bounds.Bottom - diameter,
            diameter,
            diameter,
            0,
            90);
        path.AddArc(
            bounds.X,
            bounds.Bottom - diameter,
            diameter,
            diameter,
            90,
            90);
        path.CloseFigure();
        return path;
    }
}
