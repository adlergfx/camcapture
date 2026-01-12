using AForge.Video.DirectShow;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace CamCapture.core
{
    class CameraManager : ICameraManager
    {
        private List<string> availableCameraNames;
        private FilterInfoCollection ? availableCameras;

        private FilterInfo ? activeCameraInfo;
        private VideoCaptureDevice ? activeCamera;

        private Action<Bitmap, BitmapImage> OnFrame;


        public CameraManager() {
            availableCameraNames = getAvailableCameras();
        }

        public void SetFrameListener(Action<Bitmap, BitmapImage> handler)
        {
            OnFrame = handler;
        }

        public List<string> Cameras
        {
            get => availableCameraNames;
        }

        public bool IsActive
        {
            get => activeCamera != null;
        }

        public void Start(int camIndex, IVideoCap ? cap)
        {
            if (IsActive || cap == null || availableCameras == null || camIndex < 0 || camIndex >= availableCameras.Count) return;
            activeCameraInfo = availableCameras[camIndex];
            activeCamera = new VideoCaptureDevice(activeCameraInfo.MonikerString); // get First device
            VideoCapabilities res = (cap as VideoCap)?.Item;
            activeCamera.VideoResolution = res;
            activeCamera.NewFrame += Cam_NewFrame;
            activeCamera.Start();
            UpdateConfig();
        }

        public void UpdateConfig()
        {
            if (!IsActive) return;
            CameraConfig.ConfigCamera(activeCameraInfo, activeCamera);
        }

        private void Cam_NewFrame(object sender, AForge.Video.NewFrameEventArgs eventArgs)
        {
            Bitmap bmp = new Bitmap(eventArgs.Frame);
            BitmapImage bi = new BitmapImage();
            bi.BeginInit();
            MemoryStream ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Bmp);
            ms.Seek(0, SeekOrigin.Begin);
            bi.StreamSource = ms;
            bi.EndInit();
            bi.Freeze();

            if (OnFrame != null) OnFrame(bmp, bi);
        }

        public void Stop()
        {
            if (!IsActive) return;
            activeCamera.NewFrame -= Cam_NewFrame;
            activeCamera.SignalToStop();
            activeCamera = null;
            activeCameraInfo = null;
        }

        private List<string> getAvailableCameras()
        {
            availableCameras = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            List<string> res = new List<string>();
            foreach (FilterInfo cam in availableCameras)
            {
                res.Add(cam.Name);
            }
            return res;
        }

        public List<IVideoCap> getResolutions(int camIndex)
        {
            List<IVideoCap> res = new List<IVideoCap>();
            if ((camIndex < 0) || (camIndex >= availableCameras.Count)) return res;

            FilterInfo vid = availableCameras[camIndex];
            VideoCaptureDevice cam = new VideoCaptureDevice(vid.MonikerString); // get First device

            VideoCapabilities[] vcap = cam.VideoCapabilities;

            foreach (VideoCapabilities cap in vcap)
            {
                res.Add(new VideoCap(cap));
            }
            return res;
        }


    }
}
