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
    }

    public Thickness PreviewBubblePadding
    {
        get => (Thickness)GetValue(PreviewBubblePaddingProperty);
        set => SetValue(PreviewBubblePaddingProperty, value);
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
}
