using DirectShowLib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Media.Imaging;

namespace CamCapture.core
{
    internal class DSCameraManager : ICameraManager, ISampleGrabberCB
    {
        private List<string> availableCameraNames;
        private DsDevice[]? availableCameras;
        private DsDevice? activeCameraDevice;

        private IFilterGraph2? graphBuilder;
        private ICaptureGraphBuilder2? captureGraphBuilder;
        private IBaseFilter? videoSource;
        private IBaseFilter? sampleGrabberFilter;
        private ISampleGrabber? sampleGrabber;
        private IMediaControl? mediaControl;
        private IAMStreamConfig? streamConfig;

        private int videoWidth;
        private int videoHeight;
        private int stride;

        
        private Action<Bitmap, BitmapImage> OnFrame;

        public DSCameraManager()
        {
            availableCameraNames = GetAvailableCameras();
        }

        public void SetFrameListener(Action<Bitmap, BitmapImage> onframe)
        {
            OnFrame = onframe;
        }

        public List<string> Cameras
        {
            get => availableCameraNames;
        }

        public bool IsActive
        {
            get => mediaControl != null;
        }

        public void Start(int camIndex, IVideoCap? cap)
        {
            if (IsActive || cap == null || availableCameras == null || camIndex < 0 || camIndex >= availableCameras.Length)
                return;

            try
            {
                activeCameraDevice = availableCameras[camIndex];
                DSVideoCap? dsCap = cap as DSVideoCap;
                if (dsCap == null) return;

                // Create the filter graph
                graphBuilder = (IFilterGraph2)new FilterGraph();
                captureGraphBuilder = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
                captureGraphBuilder.SetFiltergraph(graphBuilder);

                // Add video source
                videoSource = CreateFilterByDevice(activeCameraDevice);
                if (videoSource == null) return;

                int hr = graphBuilder.AddFilter(videoSource, "Video Source");
                DsError.ThrowExceptionForHR(hr);

                // Configure stream
                hr = captureGraphBuilder.FindInterface(
                    PinCategory.Capture,
                    MediaType.Video,
                    videoSource,
                    typeof(IAMStreamConfig).GUID,
                    out object streamConfigObj);

                if (hr == 0)
                {
                    streamConfig = streamConfigObj as IAMStreamConfig;
                    if (streamConfig != null)
                    {
                        SetVideoFormat(streamConfig, dsCap);
                    }
                }

                // Create and configure sample grabber
                sampleGrabberFilter = (IBaseFilter)new SampleGrabber();
                sampleGrabber = (ISampleGrabber)sampleGrabberFilter;

                AMMediaType mt = new AMMediaType
                {
                    majorType = MediaType.Video,
                    subType = MediaSubType.RGB24,
                    formatType = FormatType.VideoInfo
                };

                hr = sampleGrabber.SetMediaType(mt);
                DsError.ThrowExceptionForHR(hr);

                hr = graphBuilder.AddFilter(sampleGrabberFilter, "Sample Grabber");
                DsError.ThrowExceptionForHR(hr);

                hr = sampleGrabber.SetBufferSamples(false);
                DsError.ThrowExceptionForHR(hr);

                hr = sampleGrabber.SetOneShot(false);
                DsError.ThrowExceptionForHR(hr);

                hr = sampleGrabber.SetCallback(this, 1);
                DsError.ThrowExceptionForHR(hr);

                // Connect filters
                hr = captureGraphBuilder.RenderStream(
                    PinCategory.Capture,
                    MediaType.Video,
                    videoSource,
                    null,
                    sampleGrabberFilter);
                DsError.ThrowExceptionForHR(hr);

                // Get video dimensions
                AMMediaType connectedMediaType = new AMMediaType();
                hr = sampleGrabber.GetConnectedMediaType(connectedMediaType);
                DsError.ThrowExceptionForHR(hr);

                if (connectedMediaType.formatType == FormatType.VideoInfo &&
                    connectedMediaType.formatPtr != IntPtr.Zero)
                {
                    VideoInfoHeader videoInfo = (VideoInfoHeader)Marshal.PtrToStructure(
                        connectedMediaType.formatPtr,
                        typeof(VideoInfoHeader))!;

                    videoWidth = videoInfo.BmiHeader.Width;
                    videoHeight = videoInfo.BmiHeader.Height;
                    stride = videoWidth * 3;
                }
                DsUtils.FreeAMMediaType(connectedMediaType);

                // Start capture
                mediaControl = (IMediaControl)graphBuilder;
                hr = mediaControl.Run();
                DsError.ThrowExceptionForHR(hr);

                UpdateConfig();
            }
            catch (Exception)
            {
                Stop();
                throw;
            }
        }

        private void SetVideoFormat(IAMStreamConfig streamConfig, DSVideoCap cap)
        {
            int count, size;
            int hr = streamConfig.GetNumberOfCapabilities(out count, out size);
            DsError.ThrowExceptionForHR(hr);

            IntPtr pSC = Marshal.AllocHGlobal(size);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    AMMediaType mt;
                    hr = streamConfig.GetStreamCaps(i, out mt, pSC);
                    if (hr == 0)
                    {
                        if (mt.formatType == FormatType.VideoInfo &&
                            mt.formatPtr != IntPtr.Zero)
                        {
                            VideoInfoHeader videoInfo = (VideoInfoHeader)Marshal.PtrToStructure(
                                mt.formatPtr,
                                typeof(VideoInfoHeader))!;

                            if (videoInfo.BmiHeader.Width == cap.Width &&
                                videoInfo.BmiHeader.Height == cap.Height)
                            {
                                long frameInterval = (long)(10000000.0 / cap.FrameRate);
                                videoInfo.AvgTimePerFrame = frameInterval;

                                Marshal.StructureToPtr(videoInfo, mt.formatPtr, false);

                                hr = streamConfig.SetFormat(mt);
                                DsUtils.FreeAMMediaType(mt);

                                if (hr == 0)
                                    break;
                            }
                        }
                        DsUtils.FreeAMMediaType(mt);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pSC);
            }
        }

        public void UpdateConfig()
        {
            if (!IsActive || activeCameraDevice == null || videoSource == null) return;
            DSCameraConfig.ConfigCamera(activeCameraDevice, videoSource);
        }

        public void Stop()
        {
            // Stop media control first
            if (mediaControl != null)
            {
                try
                {
                    mediaControl.Stop();
                }
                catch { }
            }

            // Release COM objects in reverse order of creation
            // Don't try to remove filters from graph - just release everything

            if (mediaControl != null)
            {
                Marshal.ReleaseComObject(mediaControl);
                mediaControl = null;
            }

            if (sampleGrabber != null)
            {
                Marshal.ReleaseComObject(sampleGrabber);
                sampleGrabber = null;
            }

            if (sampleGrabberFilter != null)
            {
                Marshal.ReleaseComObject(sampleGrabberFilter);
                sampleGrabberFilter = null;
            }

            if (streamConfig != null)
            {
                Marshal.ReleaseComObject(streamConfig);
                streamConfig = null;
            }

            if (videoSource != null)
            {
                Marshal.ReleaseComObject(videoSource);
                videoSource = null;
            }

            if (captureGraphBuilder != null)
            {
                Marshal.ReleaseComObject(captureGraphBuilder);
                captureGraphBuilder = null;
            }

            if (graphBuilder != null)
            {
                Marshal.ReleaseComObject(graphBuilder);
                graphBuilder = null;
            }

            activeCameraDevice = null;
        }

        private List<string> GetAvailableCameras()
        {
            availableCameras = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
            List<string> res = new List<string>();
            foreach (DsDevice device in availableCameras)
            {
                res.Add(device.Name);
            }
            return res;
        }

        public List<IVideoCap> getResolutions(int camIndex)
        {
            List<IVideoCap> res = new List<IVideoCap>();
            if (availableCameras == null || camIndex < 0 || camIndex >= availableCameras.Length)
                return res;

            DsDevice device = availableCameras[camIndex];
            IBaseFilter? sourceFilter = null;
            IAMStreamConfig? streamConfig = null;

            try
            {
                sourceFilter = CreateFilterByDevice(device);
                if (sourceFilter == null) return res;

                IFilterGraph2 graph = (IFilterGraph2)new FilterGraph();
                ICaptureGraphBuilder2 captureGraph = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
                captureGraph.SetFiltergraph(graph);

                int hr = graph.AddFilter(sourceFilter, "Source");
                DsError.ThrowExceptionForHR(hr);

                hr = captureGraph.FindInterface(
                    PinCategory.Capture,
                    MediaType.Video,
                    sourceFilter,
                    typeof(IAMStreamConfig).GUID,
                    out object streamConfigObj);

                if (hr != 0) return res;

                streamConfig = streamConfigObj as IAMStreamConfig;
                if (streamConfig == null) return res;

                int count, size;
                hr = streamConfig.GetNumberOfCapabilities(out count, out size);
                DsError.ThrowExceptionForHR(hr);

                IntPtr pSC = Marshal.AllocHGlobal(size);
                try
                {
                    for (int i = 0; i < count; i++)
                    {
                        AMMediaType mt;
                        hr = streamConfig.GetStreamCaps(i, out mt, pSC);
                        if (hr == 0)
                        {
                            if (mt.formatType == FormatType.VideoInfo &&
                                mt.formatPtr != IntPtr.Zero)
                            {
                                VideoInfoHeader videoInfo = (VideoInfoHeader)Marshal.PtrToStructure(
                                    mt.formatPtr,
                                    typeof(VideoInfoHeader))!;

                                int width = videoInfo.BmiHeader.Width;
                                int height = videoInfo.BmiHeader.Height;
                                double frameRate = 10000000.0 / videoInfo.AvgTimePerFrame;
                                int bitCount = videoInfo.BmiHeader.BitCount;

                                res.Add(new DSVideoCap(width, height, frameRate, mt, bitCount));
                            }
                            else
                            {
                                DsUtils.FreeAMMediaType(mt);
                            }
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(pSC);
                }

                Marshal.ReleaseComObject(captureGraph);
                Marshal.ReleaseComObject(graph);
            }
            catch (Exception)
            {
                // Return empty list on error
            }
            finally
            {
                if (streamConfig != null)
                    Marshal.ReleaseComObject(streamConfig);
                if (sourceFilter != null)
                    Marshal.ReleaseComObject(sourceFilter);
            }

            return res;
        }

        private IBaseFilter? CreateFilterByDevice(DsDevice device)
        {
            Guid iid = typeof(IBaseFilter).GUID;
            device.Mon.BindToObject(null, null, ref iid, out object source);
            return source as IBaseFilter;
        }

        // ISampleGrabberCB implementation
        public int SampleCB(double sampleTime, IMediaSample pSample)
        {
            return 0;
        }

        public int BufferCB(double sampleTime, IntPtr pBuffer, int bufferLen)
        {
            if (videoWidth == 0 || videoHeight == 0)
                return 0;

            try
            {
                // Create bitmap from buffer
                Bitmap bmp = new Bitmap(videoWidth, videoHeight, PixelFormat.Format24bppRgb);
                BitmapData bmpData = bmp.LockBits(
                    new Rectangle(0, 0, videoWidth, videoHeight),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format24bppRgb);

                // DirectShow returns bottom-up DIB, need to flip
                IntPtr src = pBuffer + (videoHeight - 1) * stride;
                IntPtr dst = bmpData.Scan0;
                for (int i = 0; i < videoHeight; i++)
                {
                    CopyMemory(dst, src, stride);
                    src -= stride;
                    dst += stride;
                }

                bmp.UnlockBits(bmpData);

                // Convert to BitmapImage
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
            catch (Exception e)
            {
                // Ignore frame processing errors
            }

            return 0;
        }

        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory", SetLastError = false)]
        private static extern void CopyMemory(IntPtr dest, IntPtr src, int count);
    }
}
