using AForge.Video.DirectShow;
using CamCapture.core;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Http;
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
    //private bool screenshot = false; 
    private string prefix = null;
    private HTTPServer server;
    private bool preview = false;
    private string captureName;

    private const string SYM_START = "4";
    private const string SYM_STOP  = "<";
    private const string DEF_PREFIX = "";


    public MainWindow()
    {
        InitializeComponent();
        server = new  HTTPServer();
        server.AddRouteAction(HttpMethod.Post, new RouteAction("/capture", OnHttpCapture));
        server.AddRouteAction(HttpMethod.Get, new RouteAction("/capture", OnHttpCapture));
        tbServer_TextChanged(null, null);
        getCameras();

    }

    private bool OnHttpCapture(HttpListenerRequest request, HttpListenerResponse response)
    {
        if ( (request.HttpMethod == HttpMethod.Post.ToString()) && request.HasEntityBody)
        {
            string bodystr = server.getBody(request);
            if (bodystr != null)
            {
                try
                {
                    Dictionary<string, string> body = JsonConvert.DeserializeObject<Dictionary<string, string>>(bodystr);
                    prefix = body.GetValueOrDefault("prefix", DEF_PREFIX);
                } catch { }
            }
        }

        if (request.HttpMethod == HttpMethod.Get.ToString())
        {
            string[] parts = request.Url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries );
            if (parts.Length > 1) prefix = parts[1];
        }

        captureName = createCaptureName(prefix);
        server.sendJson(response, System.IO.Path.GetFileName(captureName));
        return true;
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
        cbResolution.IsEnabled = !isCapturing;
        cbCams.IsEnabled = !isCapturing;
        image.Visibility = isCapturing ? Visibility.Visible : Visibility.Hidden;
        btnPlay.Content = !isCapturing?SYM_START:SYM_STOP;

        if (cam != null)
        {
            cam.NewFrame -= Cam_NewFrame;
            cam.SignalToStop();
            cam = null;
        }
        else
        {
            btnShot.IsEnabled = Directory.Exists(folder);
            FilterInfo vid = cameras[cbCams.SelectedIndex];
            cam = new VideoCaptureDevice(vid.MonikerString); // get First device
            VideoCapabilities res = (cbResolution.SelectedItem as VideoCap).Item;

            CameraConfig.ConfigCamera(cameras[cbCams.SelectedIndex], cam);

            cam.VideoResolution = res;
            cam.NewFrame += Cam_NewFrame;
            cam.Start();
        }
    }


    private void Cam_NewFrame(object sender, AForge.Video.NewFrameEventArgs eventArgs)
    {
        if (!HasCapture && !preview) return;

        Bitmap bmp = new Bitmap(eventArgs.Frame);
        BitmapImage bi = new BitmapImage();
        bi.BeginInit();
            MemoryStream ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Bmp);
            ms.Seek(0, SeekOrigin.Begin);
            bi.StreamSource = ms;
        bi.EndInit();
        bi.Freeze();

        try
        {
            Dispatcher.Invoke(() =>
            {
                if (image == null) return; // which can be in bad timings while closing applications
                image.Visibility = Visibility.Visible; 
                image.Source = bi; 
            });
        } catch
        {
        }

        if (!HasCapture) return;
        bmp.Save(captureName);
        bmp.Dispose();
        captureName = null;
    }

    private string createCaptureName(string prefix)
    {
        if (folder == null) return null;    // we can not capture
        string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        string pre = prefix ?? DEF_PREFIX;
        string path = System.IO.Path.Combine(folder, $"{pre}{timestamp}.png");
        return path;
    }

    private bool HasCapture
    {
        get => captureName != null;
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
        if (captureName != null) return;
        captureName = createCaptureName("manual");
    }

    private void cbCams_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        btnPlay.IsEnabled = cbCams.SelectedIndex >= 0;

        if (cbCams.SelectedIndex < 0) return;
        FilterInfo vid = cameras[cbCams.SelectedIndex];

        // i am not using the member here because i only try to get 
        // camera properties and not aquire the camera
        VideoCaptureDevice cam = new VideoCaptureDevice(vid.MonikerString); // get First device

        VideoCapabilities[] vcap = cam.VideoCapabilities;
        cbResolution.Items.Clear();
        foreach (VideoCapabilities cap in vcap)
        {
            cbResolution.Items.Add( new VideoCap(cap) );
        }
        cbResolution.SelectedIndex = 0;


    

    }

    private void onPreviewChanged(object sender, RoutedEventArgs e)
    {
        preview = cbPreview.IsChecked ?? false;
    }

    private void btnReload_Click(object sender, RoutedEventArgs e)
    {
        if ((cam == null) || (cbCams.SelectedIndex < 0)) return;
        CameraConfig.ConfigCamera(cameras[cbCams.SelectedIndex], cam);
    }

    private void tbFolder_TextChanged(object sender, TextChangedEventArgs e)
    {
        folder = tbFolder.Text;
    }
}