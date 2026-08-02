using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MyCO.Manager.Localization;
using MyCO.Manager.Services;
using Input = System.Windows.Input;
using WpfPoint = System.Windows.Point;

namespace MyCO.Manager.Views;

public partial class AvatarCropWindow : Window
{
    private const int OutputSize = 512;
    private readonly BitmapSource _source;
    private double _coverScale;
    private double _zoom = AvatarCropMath.MinimumZoom;
    private double _offsetX;
    private double _offsetY;
    private WpfPoint? _dragStart;
    private (double X, double Y) _dragOrigin;
    private bool _loaded;

    public AvatarCropWindow(byte[] imageBytes)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        InitializeComponent();
        // XAML can raise ValueChanged while later named controls are still null.
        // Subscribe only after the complete visual tree has been initialized.
        ZoomSlider.ValueChanged += ZoomSlider_ValueChanged;
        _zoom = Math.Clamp(
            ZoomSlider.Value,
            AvatarCropMath.MinimumZoom,
            AvatarCropMath.MaximumZoom);
        UpdateZoomLabel();
        _source = DecodeImage(imageBytes);
        SourceImage.Source = _source;
        SourceImage.Width = _source.PixelWidth;
        SourceImage.Height = _source.PixelHeight;
        Loaded += HandleLoaded;
        SizeChanged += (_, _) => UpdateMask();
    }

    public byte[]? CroppedPng { get; private set; }

    private void HandleLoaded(object sender, RoutedEventArgs eventArgs)
    {
        _loaded = true;
        _coverScale = AvatarCropMath.CoverScale(
            _source.PixelWidth,
            _source.PixelHeight,
            CropViewport.ActualWidth);
        UpdateMask();
        ApplyImageTransform();
    }

    private void CropViewport_MouseLeftButtonDown(
        object sender,
        Input.MouseButtonEventArgs eventArgs)
    {
        if (!_loaded)
        {
            return;
        }
        _dragStart = eventArgs.GetPosition(CropViewport);
        _dragOrigin = (_offsetX, _offsetY);
        CropViewport.CaptureMouse();
        CropViewport.Cursor = Input.Cursors.SizeAll;
        eventArgs.Handled = true;
    }

    private void CropViewport_MouseMove(
        object sender,
        Input.MouseEventArgs eventArgs)
    {
        if (_dragStart is not WpfPoint start ||
            eventArgs.LeftButton != Input.MouseButtonState.Pressed)
        {
            return;
        }
        var current = eventArgs.GetPosition(CropViewport);
        SetOffset(
            _dragOrigin.X + current.X - start.X,
            _dragOrigin.Y + current.Y - start.Y);
        eventArgs.Handled = true;
    }

    private void CropViewport_MouseLeftButtonUp(
        object sender,
        Input.MouseButtonEventArgs eventArgs)
    {
        _dragStart = null;
        if (CropViewport.IsMouseCaptured)
        {
            CropViewport.ReleaseMouseCapture();
        }
        CropViewport.Cursor = Input.Cursors.Arrow;
        eventArgs.Handled = true;
    }

    private void CropViewport_MouseWheel(
        object sender,
        Input.MouseWheelEventArgs eventArgs)
    {
        ZoomSlider.Value += eventArgs.Delta > 0 ? 0.1 : -0.1;
        eventArgs.Handled = true;
    }

    private void ZoomSlider_ValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> eventArgs)
    {
        _zoom = Math.Clamp(
            eventArgs.NewValue,
            AvatarCropMath.MinimumZoom,
            AvatarCropMath.MaximumZoom);
        UpdateZoomLabel();
        if (_loaded)
        {
            SetOffset(_offsetX, _offsetY);
        }
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs eventArgs) =>
        ZoomSlider.Value = Math.Max(
            ZoomSlider.Minimum,
            ZoomSlider.Value - 0.1);

    private void ZoomIn_Click(object sender, RoutedEventArgs eventArgs) =>
        ZoomSlider.Value = Math.Min(
            ZoomSlider.Maximum,
            ZoomSlider.Value + 0.1);

    private void UpdateZoomLabel()
    {
        if (ZoomLabel is not null)
        {
            ZoomLabel.Text = $"{Math.Round(_zoom * 100):0}%";
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs eventArgs)
    {
        try
        {
            var rect = AvatarCropMath.CalculateSourceCrop(
                _source.PixelWidth,
                _source.PixelHeight,
                CropViewport.ActualWidth,
                _zoom,
                _offsetX,
                _offsetY);
            CroppedPng = EncodePng(rect);
            DialogResult = true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or
                IOException or NotSupportedException)
        {
            System.Windows.MessageBox.Show(
                this,
                LocalizationService.Get("ErrorCropAvatar"),
                LocalizationService.Get("AvatarCropTitle"),
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs eventArgs)
    {
        CroppedPng = null;
        DialogResult = false;
    }

    private void SetOffset(double x, double y)
    {
        var clamped = AvatarCropMath.ClampOffset(
            _source.PixelWidth,
            _source.PixelHeight,
            CropViewport.ActualWidth,
            _zoom,
            x,
            y);
        _offsetX = clamped.X;
        _offsetY = clamped.Y;
        ApplyImageTransform();
    }

    private void ApplyImageTransform()
    {
        if (!_loaded)
        {
            return;
        }
        ImageScale.ScaleX = _coverScale * _zoom;
        ImageScale.ScaleY = _coverScale * _zoom;
        ImageTranslate.X = _offsetX;
        ImageTranslate.Y = _offsetY;
        Canvas.SetLeft(
            SourceImage,
            (CropViewport.ActualWidth - SourceImage.Width) / 2d);
        Canvas.SetTop(
            SourceImage,
            (CropViewport.ActualHeight - SourceImage.Height) / 2d);
    }

    private void UpdateMask()
    {
        var width = CropViewport.ActualWidth;
        var height = CropViewport.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }
        var size = Math.Min(width, height);
        var left = (width - size) / 2d;
        var top = (height - size) / 2d;
        var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
        group.Children.Add(new RectangleGeometry(new Rect(0, 0, width, height)));
        group.Children.Add(
            new EllipseGeometry(
                new WpfPoint(left + size / 2d, top + size / 2d),
                size / 2d,
                size / 2d));
        CropMask.Data = group;
        CropMask.Width = width;
        CropMask.Height = height;
        CropOutline.Width = size;
        CropOutline.Height = size;
        CropOutline.Margin = new Thickness(left, top, 0, 0);
    }

    private byte[] EncodePng(AvatarCropRect rect)
    {
        var cropped = new CroppedBitmap(
            _source,
            new Int32Rect(rect.X, rect.Y, rect.Width, rect.Height));
        var scaled = new TransformedBitmap(
            cropped,
            new ScaleTransform(
                (double)OutputSize / rect.Width,
                (double)OutputSize / rect.Height));
        scaled.Freeze();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(scaled));
        using var output = new MemoryStream();
        encoder.Save(output);
        return output.ToArray();
    }

    private static BitmapSource DecodeImage(byte[] bytes)
    {
        using var input = new MemoryStream(bytes, writable: false);
        var decoder = BitmapDecoder.Create(
            input,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        if (decoder.Frames.Count == 0)
        {
            throw new ArgumentException("Avatar image contains no frame.");
        }
        var frame = decoder.Frames[0];
        var orientation = ReadOrientation(frame.Metadata);
        if (orientation == 1)
        {
            frame.Freeze();
            return frame;
        }
        var transformed = new TransformedBitmap(frame, OrientationTransform(orientation));
        transformed.Freeze();
        return transformed;
    }

    private static int ReadOrientation(ImageMetadata? metadata)
    {
        if (metadata is not BitmapMetadata bitmapMetadata)
        {
            return 1;
        }
        try
        {
            var value = bitmapMetadata.GetQuery("/app1/ifd/{ushort=274}");
            return value switch
            {
                ushort number => number,
                short number => number,
                byte number => number,
                _ => 1
            };
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            return 1;
        }
    }

    private static Transform OrientationTransform(int orientation) =>
        orientation switch
        {
            2 => new ScaleTransform(-1, 1),
            3 => new RotateTransform(180),
            4 => new ScaleTransform(1, -1),
            5 => new TransformGroup
            {
                Children =
                {
                    new ScaleTransform(-1, 1),
                    new RotateTransform(90)
                }
            },
            6 => new RotateTransform(90),
            7 => new TransformGroup
            {
                Children =
                {
                    new ScaleTransform(-1, 1),
                    new RotateTransform(270)
                }
            },
            8 => new RotateTransform(270),
            _ => Transform.Identity
        };
}
