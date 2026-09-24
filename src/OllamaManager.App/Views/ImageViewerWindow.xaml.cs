using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using OllamaManager.App.ViewModels;

namespace OllamaManager.App.Views;

public partial class ImageViewerWindow : Window
{
    public ImageViewerWindow(AttachedImage image)
    {
        InitializeComponent();
        DataContext = this;
        try
        {
            var src = LoadFullSize(image.Path);
            ImageSource = src;
            FileName = Path.GetFileName(image.Path);
            if (src.PixelWidth > 0 && src.PixelHeight > 0)
            {
                Dimensions = $"{src.PixelWidth} × {src.PixelHeight}";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法加载图片：{ex.Message}", "图片预览", MessageBoxButton.OK, MessageBoxImage.Warning);
            Close();
        }
    }

    public BitmapImage ImageSource { get; private set; } = new();
    public string FileName { get; private set; } = string.Empty;
    public string Dimensions { get; private set; } = string.Empty;

    private static BitmapImage LoadFullSize(string path)
    {
        var bmp = new BitmapImage();
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        bmp.BeginInit();
        // Decode at native resolution — the user wants to see the full image, not a thumbnail.
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.StreamSource = fs;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    private void OnCloseClick(object sender, MouseButtonEventArgs e)
    {
        Close();
    }

    private void OnImageClick(object sender, MouseButtonEventArgs e)
    {
        // Clicking the image also closes — matches the common image-viewer UX.
        Close();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape || e.Key == Key.Enter || e.Key == Key.Space)
        {
            Close();
            e.Handled = true;
        }
    }
}