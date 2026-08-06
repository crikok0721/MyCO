using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MyCO.Configuration;

// Shared Manager preview; it binds to the owning MainWindowViewModel.
namespace MyCO.Manager.Controls;

public sealed class PreviewEffectiveAvatarSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var delta = value is double number && double.IsFinite(number) ? number : 0;
        return Math.Clamp(
            AppearanceGeometryResolver.AvatarSizeBaseline + delta,
            24,
            72);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class PreviewUserHorizontalOffsetConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var delta = value is double number && double.IsFinite(number) ? number : 0;
        // Runtime's right anchor grows inward for positive values; WPF's X
        // transform grows rightward, so the preview uses the inverse sign.
        return -delta;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class PreviewEffectiveAvatarVerticalOffsetConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var delta = value is double number && double.IsFinite(number) ? number : 0;
        return Math.Clamp(
            AppearanceGeometryResolver.AssistantAvatarOffsetYBaseline + delta,
            -20,
            40);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class PreviewEffectiveUserAvatarVerticalOffsetConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var delta = value is double number && double.IsFinite(number) ? number : 0;
        return Math.Clamp(
            AppearanceGeometryResolver.UserAvatarOffsetYBaseline + delta,
            -20,
            40);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class PreviewEffectiveRadiusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var delta = value is double number && double.IsFinite(number) ? number : 0;
        return new CornerRadius(Math.Clamp(14 + delta, 0, 36));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class PreviewNicknameBubbleMarginConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var delta = value is double number && double.IsFinite(number) ? number : 0;
        // Runtime keeps a 22px identity slot and grows it only when a positive
        // nickname offset exceeds the normal 18px line plus the 4px separation.
        return new Thickness(0, Math.Max(0, delta - 4), 0, 0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public partial class ChatPreviewControl : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty PreviewBubblePaddingProperty =
        DependencyProperty.Register(
            nameof(PreviewBubblePadding),
            typeof(Thickness),
            typeof(ChatPreviewControl),
            new PropertyMetadata(new Thickness(14, 10, 14, 10)));

    public static readonly DependencyProperty PreviewBubbleMaxWidthProperty =
        DependencyProperty.Register(
            nameof(PreviewBubbleMaxWidth),
            typeof(double),
            typeof(ChatPreviewControl),
            new PropertyMetadata(320d));

    // Source-compatible alias for preview hosts using the former
    // assistant-only property name. Both role surfaces share one cap.
    public static readonly DependencyProperty PreviewAssistantBubbleMaxWidthProperty =
        PreviewBubbleMaxWidthProperty;

    public static readonly DependencyProperty PreviewMessageGapProperty =
        DependencyProperty.Register(
            nameof(PreviewMessageGap),
            typeof(Thickness),
            typeof(ChatPreviewControl),
            new PropertyMetadata(new Thickness(0, 28, 0, 0)));

    public ChatPreviewControl()
    {
        InitializeComponent();

        var paddingBinding = new MultiBinding
        {
            Converter = BubblePaddingConverter.Instance,
            Mode = BindingMode.OneWay
        };
        paddingBinding.Bindings.Add(new System.Windows.Data.Binding("BubblePaddingX"));
        paddingBinding.Bindings.Add(new System.Windows.Data.Binding("BubblePaddingY"));
        SetBinding(PreviewBubblePaddingProperty, paddingBinding);

        var widthBinding = new MultiBinding
        {
            Converter = AssistantBubbleWidthConverter.Instance,
            Mode = BindingMode.OneWay
        };
        widthBinding.Bindings.Add(new System.Windows.Data.Binding(nameof(ActualWidth))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.Self)
        });
        widthBinding.Bindings.Add(
            new System.Windows.Data.Binding("AssistantBubbleMaxWidth"));
        widthBinding.Bindings.Add(
            new System.Windows.Data.Binding("AvatarSize"));
        SetBinding(PreviewBubbleMaxWidthProperty, widthBinding);

        SetBinding(
            PreviewMessageGapProperty,
            new System.Windows.Data.Binding("MessageGap")
            {
                Converter = MessageGapConverter.Instance,
                Mode = BindingMode.OneWay
            });
    }

    public Thickness PreviewBubblePadding
    {
        get => (Thickness)GetValue(PreviewBubblePaddingProperty);
        set => SetValue(PreviewBubblePaddingProperty, value);
    }

    public double PreviewBubbleMaxWidth
    {
        get => (double)GetValue(PreviewBubbleMaxWidthProperty);
        set => SetValue(PreviewBubbleMaxWidthProperty, value);
    }

    public double PreviewAssistantBubbleMaxWidth
    {
        get => PreviewBubbleMaxWidth;
        set => PreviewBubbleMaxWidth = value;
    }

    public Thickness PreviewMessageGap
    {
        get => (Thickness)GetValue(PreviewMessageGapProperty);
        set => SetValue(PreviewMessageGapProperty, value);
    }

    private sealed class BubblePaddingConverter : IMultiValueConverter
    {
        public static BubblePaddingConverter Instance { get; } = new();

        public object Convert(
            object[] values,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            var horizontal = Math.Clamp(14 + ReadDouble(values, 0, 0), 4, 40);
            var vertical = Math.Clamp(10 + ReadDouble(values, 1, 0), 4, 32);
            return new Thickness(horizontal, vertical, horizontal, vertical);
        }

        public object[] ConvertBack(
            object value,
            Type[] targetTypes,
            object parameter,
            CultureInfo culture) =>
            throw new NotSupportedException();

        private static double ReadDouble(object[] values, int index, double fallback) =>
            index < values.Length && values[index] is double value && double.IsFinite(value)
                ? value
                : fallback;
    }

    private sealed class AssistantBubbleWidthConverter : IMultiValueConverter
    {
        public static AssistantBubbleWidthConverter Instance { get; } = new();

        public object Convert(
            object[] values,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            var controlWidth = ReadDouble(values, 0, 600);
            var percentage = Math.Clamp(66 + ReadDouble(values, 1, 0), 45, 80);
            var avatarSize = Math.Clamp(
                AppearanceGeometryResolver.AvatarSizeBaseline +
                ReadDouble(values, 2, 0),
                24,
                72);
            // Match the Runtime content box: outer preview padding (24px on
            // each side), the role avatar and the shared 12px anchor gap.
            var usableWidth = Math.Max(0, controlWidth - 48 - avatarSize - 12);
            return Math.Min(480, Math.Max(0, usableWidth * percentage / 100));
        }

        public object[] ConvertBack(
            object value,
            Type[] targetTypes,
            object parameter,
            CultureInfo culture) =>
            throw new NotSupportedException();

        private static double ReadDouble(object[] values, int index, double fallback) =>
            index < values.Length && values[index] is double value && double.IsFinite(value)
                ? value
                : fallback;
    }

    private sealed class MessageGapConverter : IValueConverter
    {
        public static MessageGapConverter Instance { get; } = new();

        public object Convert(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            var delta = value is double number && double.IsFinite(number)
                ? number
                : 0;
            var gap = Math.Clamp(28 + delta, 4, 80);
            return new Thickness(0, gap, 0, 0);
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
