using System.Globalization;
using System.Windows;
using System.Windows.Data;

// Shared Manager preview; it binds to the owning MainWindowViewModel.
namespace MyCO.Manager.Controls;

public partial class ChatPreviewControl : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty PreviewBubblePaddingProperty =
        DependencyProperty.Register(
            nameof(PreviewBubblePadding),
            typeof(Thickness),
            typeof(ChatPreviewControl),
            new PropertyMetadata(new Thickness(14, 10, 14, 10)));

    public static readonly DependencyProperty PreviewAssistantBubbleMaxWidthProperty =
        DependencyProperty.Register(
            nameof(PreviewAssistantBubbleMaxWidth),
            typeof(double),
            typeof(ChatPreviewControl),
            new PropertyMetadata(320d));

    public static readonly DependencyProperty PreviewMessageGapProperty =
        DependencyProperty.Register(
            nameof(PreviewMessageGap),
            typeof(Thickness),
            typeof(ChatPreviewControl),
            new PropertyMetadata(new Thickness(64, 28, 0, 0)));

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
        SetBinding(PreviewAssistantBubbleMaxWidthProperty, widthBinding);

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

    public double PreviewAssistantBubbleMaxWidth
    {
        get => (double)GetValue(PreviewAssistantBubbleMaxWidthProperty);
        set => SetValue(PreviewAssistantBubbleMaxWidthProperty, value);
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
            var horizontal = ReadDouble(values, 0, 14);
            var vertical = ReadDouble(values, 1, 10);
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
                ? Math.Max(0, value)
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
            var percentage = Math.Clamp(ReadDouble(values, 1, 66), 45, 80);
            var usableWidth = Math.Max(180, controlWidth - 120);
            return Math.Min(480, Math.Max(160, usableWidth * percentage / 100));
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
            var gap = value is double number && double.IsFinite(number)
                ? Math.Max(0, number)
                : 28;
            return new Thickness(64, gap, 0, 0);
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
