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

namespace CamCapture.core
{
    interface ICameraManager
    {
        List<string> Cameras { get; }
        bool IsActive { get; }
        void Start(int camIndex, IVideoCap? cap);
        void Stop();
        void UpdateConfig();
        List<IVideoCap> getResolutions(int camIndex);
        void SetFrameListener(Action<Bitmap, BitmapImage> handler);
    }
}
