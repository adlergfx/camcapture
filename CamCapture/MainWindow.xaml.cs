//using AForge.Video.DirectShow;
using CamCapture.core;
using Microsoft.Win32;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace CamCapture;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler ? PropertyChanged;
    private ICameraManager cameraManager;
    private String folder = ""; 
    private HTTPServer server;
    private REST rest;
    private bool preview = false;
    private string? captureName;

    private const string SYM_START = "4";
    private const string SYM_STOP  = "<";
    private const string DEF_PREFIX = "";
    private const string DATE_FORMAT = "yyyyMMddHHmmss";



    public MainWindow()
    {
        Log.Create("camcapturelog.txt");
        Log.Get().OnLogEvent += MainWindow_OnLogEvent;

        DataContext = this;
        cameraManager = new DSCameraManager();
        cameraManager.SetFrameListener(CameraManager_OnFrame);

        InitializeComponent();

        string[] args = Environment.GetCommandLineArgs();
        string staticPath = "public_html";
        if (args != null && args.Length > 1)
        {
            if (Directory.Exists(staticPath)) staticPath = args[1];
        }

        server = new  HTTPServer(staticPath);
        rest = new REST(server);
        rest.CaptureFunc = onCapture;
        rest.QueryFunc = onList;
        rest.ImageFunc = onImage;
        tbServer_TextChanged(null, null);
        getCameras();

        Log.Info("initialized");
    }

    private void MainWindow_OnLogEvent(Log.LogLevel level, string message)
    {
        if (level == Log.LogLevel.Error)
            sbStatusMessage.Content = message;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private string ? onImage(string filename)
    {
        string path = System.IO.Path.Combine(folder, filename);
        if (!File.Exists(path)) return null;
        return path;
    }

    public bool IsFolderEditable
    {
        get => !IsCameraEnabled && !(server?.IsConnected ?? false);
    }

    private bool HasCapture
    {
        get => captureName != null;
    }

    /*
    public bool IsCameraDisabled
    {
        get => !IsCameraEnabled;
    }*/

    public bool IsCameraEnabled
    {
        get => cameraManager.IsActive;
    }

    public bool CanTakeImage
    {
        get => IsCameraEnabled && Directory.Exists(folder);
    }


    public string Folder
    {
        get => folder;
        set {
            folder = value;
            OnPropertyChanged(nameof(CanTakeImage));
            OnPropertyChanged(nameof(Folder));
        }
    }

    public bool Preview
    {
        get => preview;
        set => preview = value;
    }

    private string ? fitDateString(string ? d)
    {
        if (string.IsNullOrEmpty(d)) return d;
        if (d.Length > DATE_FORMAT.Length) return d.Substring(0, DATE_FORMAT.Length);
        if (d.Length < DATE_FORMAT.Length)
            return d + Enumerable.Range(0, DATE_FORMAT.Length - d.Length).Select(i => "0").Aggregate((a, b) => a + b);
        return d;
    }


    private string[] onList(string ? prefix, string ? from, string ? to)
    {
        string[] files = [];
        
        try
        {
            files = Directory.GetFiles(Folder);
        } catch ( Exception e )
        {
            Log.Error("Capture directory invalid");
            return files;
        }
        

        from = fitDateString(from);
        to = fitDateString(to);

        List<string> pngs = files.Where((f)=>f.ToLower().EndsWith(".png")).Select((s)=>System.IO.Path.GetFileName(s)).ToList();    // only images

        if (string.IsNullOrEmpty(prefix) && string.IsNullOrEmpty(from) && string.IsNullOrEmpty(to)) return pngs.ToArray();

        List<string> result = new List<string>();

        foreach (string file in pngs)
        {
            FileInfo fi = new FileInfo(file);
            string name = System.IO.Path.GetFileNameWithoutExtension(fi.Name);    // stip postfix
            string pre  = name.Substring(0, name.Length-DATE_FORMAT.Length);
            string date = name.Substring(pre.Length);

            bool exclude = false;
            exclude = exclude || (!string.IsNullOrEmpty(prefix) && (prefix != pre));
            exclude = exclude || (!string.IsNullOrEmpty(from) && (string.Compare(date, from) < 0));
            exclude = exclude || (!string.IsNullOrEmpty(to) && (string.Compare(date, to) > 0));

            if (!exclude) result.Add(file);
        }

        return result.ToArray();
    }

    private string ? onCapture(string ? prefix)
    {
        if (!CanTakeImage) return null;

        captureName = createCaptureName(prefix);
        return System.IO.Path.GetFileName(captureName);
    }


    private void getCameras()
    {
        cbCams.Items.Clear();
        foreach (string cam in cameraManager.Cameras)
        {
            cbCams.Items.Add(cam);
        }
    }

#pragma warning disable 8602  // Dereference NULL
    private void StartCamera()
    {
        if (IsCameraEnabled && cbCams.SelectedIndex >= 0) return;
        cameraManager.Start(cbCams.SelectedIndex, cbResolution.SelectedItem as IVideoCap);
        OnPropertyChanged(nameof(IsFolderEditable));
        OnPropertyChanged(nameof(IsCameraEnabled));
        OnPropertyChanged(nameof(CanTakeImage));
        image.Visibility = Visibility.Visible;
        btnPlay.Content = SYM_STOP;

    }

    private void StopCamera()
    {
        cameraManager.Stop();
        OnPropertyChanged(nameof(IsFolderEditable));
        OnPropertyChanged(nameof(IsCameraEnabled));
        OnPropertyChanged(nameof(CanTakeImage));
        image.Visibility = Visibility.Hidden;
        btnPlay.Content = SYM_START;
    }
#pragma warning restore

    private void btnPlay_Click(object sender, RoutedEventArgs e)
    {
        if (IsCameraEnabled)
            StopCamera();
        else
            StartCamera();
    }

    private void CameraManager_OnFrame(Bitmap bmp, BitmapImage bi)
    {
        if (!HasCapture && !Preview) return;

        Dispatcher.Invoke(() =>
        {
            if (image == null) return; // which can be in bad timings while closing applications
            image.Visibility = Visibility.Visible;
            image.Source = bi;
        });

        if (!HasCapture) return;   // HasCapture is same
#pragma warning disable 8604
        bmp.Save(captureName);
#pragma warning restore
        bmp.Dispose();
        captureName = null;
    }


    /// <summary>
    /// Will create a capture name. Capturing is only possible if a valid folder exists
    /// </summary>
    /// <param name="prefix">optional file prefix</param>
    /// <returns>absolut path where file should be stored</returns>
    private string ? createCaptureName(string? prefix)
    {
        if (folder == null) return null;    // we can not capture
        string timestamp = DateTime.Now.ToString(DATE_FORMAT);
        string pre = prefix ?? DEF_PREFIX;
        string path = System.IO.Path.Combine(folder, $"{pre}{timestamp}.png");
        return path;
    }


    private void onBrowseDirectory(object sender, RoutedEventArgs e)
    {
        OpenFolderDialog dlg = new OpenFolderDialog();
        bool? res = dlg.ShowDialog();
        if (res.HasValue && res.Value)
        {
            Folder = dlg.FolderName;
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
        OnPropertyChanged(nameof(IsFolderEditable));
    }

    private void tbServer_TextChanged(object? sender, TextChangedEventArgs? e)
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

        List<IVideoCap> caps = cameraManager.getResolutions(cbCams.SelectedIndex);
        cbResolution.Items.Clear();
        foreach (IVideoCap cap in caps)
        {
            cbResolution.Items.Add( cap );
        }
        cbResolution.SelectedIndex = 0;

    }

    private void btnReload_Click(object sender, RoutedEventArgs e)
    {
        cameraManager.UpdateConfig();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        StopCamera();
        server.Stop();
    }
}