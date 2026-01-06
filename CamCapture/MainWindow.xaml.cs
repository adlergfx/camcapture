using AForge.Video.DirectShow;
using Microsoft.Win32;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CamCapture;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private FilterInfoCollection cameras;
    private VideoCaptureDevice cam;
    private String folder;
    private bool screenshot = false; 
    private string prefix = null;
    private Server server;

    private const string SYM_START = "4";
    private const string SYM_STOP  = "<";


    public MainWindow()
    {
        InitializeComponent();
        server = new  HTTPServer();
        server.OnRequest += Server_OnRequest;
        tbServer_TextChanged(null, null);
        getCameras();
    }

    private string Server_OnRequest(Dictionary<string, string> req)
    {
        prefix = req.GetValueOrDefault("prefix", null);
        screenshot = req.ContainsKey("screenshot");

        return null;
    }

    private void getCameras()
    {
        cameras = new FilterInfoCollection(FilterCategory.VideoInputDevice);
        cbCams.Items.Clear();
        foreach (FilterInfo cam in cameras)
        {
            cbCams.Items.Add(cam.Name);
        }
    }

    private void btnPlay_Click(object sender, RoutedEventArgs e)
    {
        // if null we will switch to capturing
        bool isCapturing = cam == null;

        btnShot.IsEnabled = isCapturing;
        tbFolder.IsEnabled = !isCapturing;
        btnBrowse.IsEnabled = !isCapturing;

        if (cam != null)
        {
            cam.NewFrame -= Cam_NewFrame;
            cam.SignalToStop();
            cam = null;
            btnPlay.Content = SYM_START;
            image.Visibility = Visibility.Hidden;
        }
        else
        {
            folder = tbFolder.Text;
            btnShot.IsEnabled = Directory.Exists(folder);
            FilterInfo vid = cameras[cbCams.SelectedIndex];
            cam = new VideoCaptureDevice(vid.MonikerString); // get First device

            VideoCapabilities biggest = cam.VideoCapabilities.OrderBy(vc => -vc.FrameSize.Width).ThenBy(vc => -vc.FrameSize.Height).First();

            cam.VideoResolution = biggest;
            cam.NewFrame += Cam_NewFrame;
            cam.Start();
            btnPlay.Content = SYM_STOP;
            image.Visibility = Visibility.Visible;
        }
    }

    private void Cam_NewFrame(object sender, AForge.Video.NewFrameEventArgs eventArgs)
    {
        if (!screenshot) return;

        Bitmap bmp = new Bitmap(eventArgs.Frame);
        BitmapImage bi = new BitmapImage();
        bi.BeginInit();
            MemoryStream ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Bmp);
            ms.Seek(0, SeekOrigin.Begin);
            bi.StreamSource = ms;
        bi.EndInit();
        bi.Freeze();

        Dispatcher.Invoke(() =>
        {
            if (image == null) return; // which can be in bad timings while closing applications
            image.Visibility = Visibility.Visible; 
            image.Source = bi; 
        });

        screenshot = false;
        string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        string pre = prefix ?? "capture";
        string path = System.IO.Path.Combine(folder, $"{pre}{timestamp}.png");
        bmp.Save(path);
        bmp.Dispose();
    }

    private void onBrowseDirectory(object sender, RoutedEventArgs e)
    {
        OpenFolderDialog dlg = new OpenFolderDialog();
        bool? res = dlg.ShowDialog();
        if (res.HasValue && res.Value)
        {
            tbFolder.Text = dlg.FolderName;
        }
    }



    private void onServerStart(object sender, RoutedEventArgs e)
    {
        if (server.IsConnected)
        {
            server.Stop();
        }
        else
        {
            int port = int.Parse(tbServer.Text);
            server.Start(port);
        }
        tbServer.IsEnabled = !server.IsConnected;
        btnServer.Content = server.IsConnected?SYM_STOP:SYM_START;
    }

    private void tbServer_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (btnServer == null) return;
        try
        {
            int port = int.Parse(tbServer.Text);
            btnServer.IsEnabled = port > 1024 && port <= 65535;
        }
        catch { 
            btnServer.IsEnabled = false;
        }
    }

    private void btnShot_Click(object sender, RoutedEventArgs e)
    {
        if (screenshot) return;
        screenshot = true;
    }

    private void cbCams_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        btnPlay.IsEnabled = cbCams.SelectedIndex >= 0;
    }

}