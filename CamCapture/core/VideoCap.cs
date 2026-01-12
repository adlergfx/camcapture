using AForge.Video.DirectShow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace CamCapture.core
{
    internal class VideoCap: IVideoCap
    {
        private VideoCapabilities cap;
        public VideoCap(VideoCapabilities cap)
        {
            this.cap = cap;
        }

        public override string ToString() 
        {
            return $"{cap.FrameSize.Width}x{cap.FrameSize.Height}@{cap.AverageFrameRate}";
        }

        public VideoCapabilities Item
        {
            get => cap;
        }
    }
}
