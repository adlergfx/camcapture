using DirectShowLib;
using System;

namespace CamCapture.core
{
    internal class DSVideoCap : IVideoCap
    {
        private int width;
        private int height;
        private double frameRate;
        private AMMediaType mediaType;
        private int bitCount;

        public DSVideoCap(int width, int height, double frameRate, AMMediaType mediaType, int bitCount)
        {
            this.width = width;
            this.height = height;
            this.frameRate = frameRate;
            this.mediaType = mediaType;
            this.bitCount = bitCount;
        }

        public int Width
        {
            get => width;
        }

        public int Height
        {
            get => height;
        }

        public double FrameRate
        {
            get => frameRate;
        }

        public int BitCount
        {
            get => bitCount;
        }

        public AMMediaType MediaType
        {
            get => mediaType;
        }

        public override string ToString()
        {
            return $"{width}x{height}@{frameRate:F0}";
        }
    }
}
