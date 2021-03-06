﻿
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

// add (from colorBasics)
using System.ComponentModel; // for CancelEventArgs
using System.Diagnostics;
using System.Globalization;
using System.IO;
// end of add

//using static System.Drawing.Bitmap;

using Microsoft.Kinect;
using Emgu.CV;
using Emgu.CV.Structure;
using SharpGL;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace walkingdog
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : System.Windows.Window, INotifyPropertyChanged
    {
        /// Active Kinect sensor
        private KinectSensor kinectSensor = null;


        ///////////
        // Depth //
        ///////////

        /// Reader for color frames
        private ColorFrameReader colorFrameReader = null;
        private FrameDescription colorFrameDescription = null;

        /// Bitmap to display
        private WriteableBitmap colorBitmap = null;
        private WriteableBitmap colorBitmap_test = null;

        private WriteableBitmap Bitmap1 = null;
        private WriteableBitmap Bitmap2 = null;

        /// Current status text to display
        private string statusText = null;


        ///////////
        // Depth //
        ///////////

        private const int MapDepthToByte = 8000 / 256;
        private DepthFrameReader depthFrameReader = null;
        private FrameDescription depthFrameDescription = null;
        private WriteableBitmap depthBitmap = null;
        private byte[] depthPixels = null;

        private byte[] depthPixels_bin = null;


        ///////////
        // Body //
        ///////////
        private BodyFrameReader bodyFrameReader = null;

        private Boolean _bFloorDetected = false;
        private Body _body = null;
        private Floor _floor = null;


        ///////////////////////
        // CoordinateMapping //
        ///////////////////////

        /// Coordinate mapper to map one type of point to another
        private CoordinateMapper coordinateMapper = null;

        private CameraSpacePoint[] depthMappedToCameraPoints = null;



        // OpenCV //
        //private IplImage _image;
        //PictureBoxIpl _image;
        //Mat src = new Mat("aa", ImreadModes.GrayScale);
        //Mat src2 = new Mat("aa", ImreadModes.GrayScale);
        //Cv2.Canny(src, src2, )
        byte[] colorPixels;

        uint colorPixelsSize;

        // emgucv
        int blobCount = 0;



        // 3D Skeleton
        Boolean bShowSkeleton = false;
        int skeletonCount = 0;
        CameraSpacePoint[] skeletonPosition;
        float minX = 100, minY = 100, minZ = 100, maxX = -100, maxY = -100, maxZ = -100;
        CameraSpacePoint lookPos = new CameraSpacePoint();

        // openGL
        //private SharpGL.SceneControl sceneControl1;


        // MultiFrame
        Boolean bMultiFrame = true;

        Mode _mode = Mode.Depth;

        //KinectSensor _sensor;
        MultiSourceFrameReader _multiSourceFrameReader;
        IList<Body> _bodies;

        bool _displayBody = false;


        public MainWindow()
        {
            // get the kinectSensor object
            this.kinectSensor = KinectSensor.GetDefault();
            
            // open the sensor
            this.kinectSensor.Open();

            this.coordinateMapper = this.kinectSensor.CoordinateMapper;

            this.depthMappedToCameraPoints = new CameraSpacePoint[512 * 424];


            if (!bMultiFrame)
            {
                // open the reader for the color frames
                //this.colorFrameReader = this.kinectSensor.ColorFrameSource.OpenReader();

                // wire handler for frame arrival
                //this.colorFrameReader.FrameArrived += this.Reader_ColorFrameArrived;
               
                // create the colorFrameDescription from the ColorFrameSource using Bgra format
                this.colorFrameDescription = this.kinectSensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);
                //FrameDescription colorFrameDescription = this.kinectSensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Rgba);

                colorPixelsSize = colorFrameDescription.BytesPerPixel * colorFrameDescription.LengthInPixels;
                Console.WriteLine("[INFO] colorPixelsSize : " + colorPixels);
                this.colorPixels = new byte[colorPixelsSize];

                // create the bitmap to display
                this.colorBitmap = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);
                this.colorBitmap_test = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);

                ///////////
                // Depth //
                //this.depthFrameReader = this.kinectSensor.DepthFrameSource.OpenReader();

                // wire handler for frame arrival
                //this.depthFrameReader.FrameArrived += this.Reader_FrameArrived;

                // get FrameDescription from DepthFrameSource
                this.depthFrameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

                // allocate space to put the pixels being received and converted
                this.depthPixels = new byte[this.depthFrameDescription.Width * this.depthFrameDescription.Height];

                // create the bitmap to display
                this.depthBitmap = new WriteableBitmap(this.depthFrameDescription.Width, this.depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Gray8, null);

                // test
                depthPixels_bin = new byte[this.depthFrameDescription.Width * this.depthFrameDescription.Height * 4];
            }
            else
            {
                //_multiSourceFrameReader = this.kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Color | FrameSourceTypes.Depth | FrameSourceTypes.Infrared | FrameSourceTypes.Body);
                _multiSourceFrameReader = this.kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Color | FrameSourceTypes.Depth | FrameSourceTypes.Infrared);
                _multiSourceFrameReader.MultiSourceFrameArrived += Reader_MultiSourceFrameArrived;

                // body
                bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();
                bodyFrameReader.FrameArrived += BodyReader_FrameArrived;

                // for test
                // get FrameDescription from DepthFrameSource
                this.depthFrameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;
                Console.WriteLine("depthFrameDescription : " + depthFrameDescription.ToString());

                // allocate space to put the pixels being received and converted
                this.depthPixels = new byte[this.depthFrameDescription.Width * this.depthFrameDescription.Height];
                Console.WriteLine("depthFrameDescription : " + depthFrameDescription.Width + ", " + depthFrameDescription.Height);

                // create the bitmap to display 
                this.depthBitmap = new WriteableBitmap(this.depthFrameDescription.Width, this.depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Gray8, null);
                Console.WriteLine("depthBitmap : " + depthFrameDescription.ToString());

                // test
                depthPixels_bin = new byte[this.depthFrameDescription.Width * this.depthFrameDescription.Height * 4];
                Console.WriteLine("depthPixels_bin : " + depthFrameDescription.ToString());
            }

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText : Properties.Resources.NoSensorStatusText;

            // use the window object as the view model in this simple example
            this.DataContext = this;

            this.skeletonPosition = new CameraSpacePoint[512*424];
            lookPos.X = 0.1537764f;
            lookPos.Y = -1.759739f;
            lookPos.Z = 7.670001f;

            InitializeComponent();
        }


        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;


        /// Gets the bitmap to display
        public ImageSource ImageSourceDepth
        {
            get
            {
                return this.depthBitmap;
            }
        }

        public ImageSource ImageSource
        {
            get
            {
                return this.colorBitmap;
            }
        }

        public ImageSource ImageSource1
        {
            get
            {
                return this.Bitmap1;
            }
        }
        public ImageSource ImageSource2
        {
            get
            {
                return this.Bitmap2;
            }
        }


        /// Gets or sets the current status text to display
        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    // notify any bound elements that the text has changed
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }


        /// Execute shutdown tasks
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.colorFrameReader != null)
            {
                // ColorFrameReder is IDisposable
                this.colorFrameReader.Dispose();
                this.colorFrameReader = null;
            }

            if (this.depthFrameReader != null)
            {
                // DepthFrameReader is IDisposable
                this.depthFrameReader.Dispose();
                this.depthFrameReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }


        /// Handles the color frame data arriving from the sensor
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_ColorFrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            // ColorFrame is IDisposable
            //using (ColorFrame colorFrame = e.FrameReference.AcquireFrame())
            using (ColorFrame colorFrame = this.colorFrameReader.AcquireLatestFrame())
            {
                if (colorFrame != null)
                {
                    FrameDescription colorFrameDescription = colorFrame.FrameDescription;

                    using (KinectBuffer colorBuffer = colorFrame.LockRawImageBuffer())
                    {
                        this.colorBitmap.Lock();

                        // verify data and write the new color frame data to the display bitmap
                        if ((colorFrameDescription.Width == this.colorBitmap.PixelWidth) && (colorFrameDescription.Height == this.colorBitmap.PixelHeight))
                        {
                            colorFrame.CopyConvertedFrameDataToIntPtr(
                                this.colorBitmap.BackBuffer,
                                (uint)(colorFrameDescription.Width * colorFrameDescription.Height * 4),
                                ColorImageFormat.Bgra);

                            // test
                            ////byte[] colorPixels = new byte[colorFrameDescription.BytesPerPixel * colorFrameDescription.LengthInPixels];
                            //colorFrame.CopyConvertedFrameDataToArray(colorPixels, ColorImageFormat.Bgra);

                            //Mat src = Mat.FromImageData(colorPixels, ImreadModes.AnyColor);
                            //Mat srcMat = new Mat(new OpenCvSharp.Size(colorFrameDescription.Width, colorFrameDescription.Height), MatType.CV_8U);
                            //var dest = new Mat();
                            //var destGray = new Mat();


                            //Cv2.CvtColor(srcMat, destGray, ColorConversionCodes.BGRA2GRAY);

                            ////Mat src1 = new Mat(this.colorBitmap.BackBuffer);
                            //int kernelSize = 51;
                            //Binarizer.Bernsen(dest, destGray, kernelSize, 50, 200);

                            ////this.colorBitmap.BackBuffer = dest.Data;
                            ////System.Runtime.InteropServices.Marshal.Copy(dest.Data, this.colorBitmap.BackBuffer, 0, colorFrameDescription.BytesPerPixel * colorFrameDescription.LengthInPixels);
                            ////System.Runtime.InteropServices.Marshal.Copy(dest.Data, this.colorBitmap.BackBuffer, 0, colorFrameDescription.BytesPerPixel * colorFrameDescription.LengthInPixels);

                            //this.colorBitmap.WritePixels(
                            //    new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                            //    dest.Data,
                            //    this.colorBitmap.PixelWidth * sizeof(int),
                            //    0);
                            // end of test

                            this.colorBitmap.AddDirtyRect(new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight));
                        }

                        this.colorBitmap.Unlock();

                        //Image<Bgr, Byte> openCVImg = new Image<Bgr, byte>(colorBitmap.ToBitmap());
                        //Image<Gray, byte> gray_image = openCVImg.Convert<Gray, byte>();
                        //this.Image_Depth.Source = ImageHelpers.ToBitmapSource(gray_image);

                        //int fps = Utility.CalculateFrameRate();
                        //Console.WriteLine("FPS : " + fps);
                    }
                }
            }
        }

        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // on failure, set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.SensorNotAvailableStatusText;
        }



        /// <summary>
        /// Handles the depth frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_FrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            if (!Utility.ControlFrameRate((int)this.sliderFPS.Value))
            {
                return;
            }

            bool depthFrameProcessed = false;

            //using (DepthFrame depthFrame = e.FrameReference.AcquireFrame())
            using (DepthFrame depthFrame = this.depthFrameReader.AcquireLatestFrame())
            {
                if (depthFrame != null)
                {
                    // test
                    //Tongull_DetectBlobs(depthFrame);

                    // the fastest way to process the body index data is to directly access 
                    // the underlying buffer
                    using (Microsoft.Kinect.KinectBuffer depthBuffer = depthFrame.LockImageBuffer())
                    {
                        // verify data and write the color data to the display bitmap
                        if (((this.depthFrameDescription.Width * this.depthFrameDescription.Height) == (depthBuffer.Size / this.depthFrameDescription.BytesPerPixel)) &&
                            (this.depthFrameDescription.Width == this.depthBitmap.PixelWidth) && (this.depthFrameDescription.Height == this.depthBitmap.PixelHeight))
                        {
                            // Note: In order to see the full range of depth (including the less reliable far field depth)
                            // we are setting maxDepth to the extreme potential depth threshold
                            //ushort maxDepth = ushort.MaxValue;
                            ushort maxDepth = 4000;
                            ushort minDepth = 850;// 3000;

                            // If you wish to filter by reliable depth distance, uncomment the following line:
                            //maxDepth = depthFrame.DepthMaxReliableDistance;

                            //this.ProcessDepthFrameData(depthBuffer.UnderlyingBuffer, depthBuffer.Size, depthFrame.DepthMinReliableDistance, maxDepth);
                            this.ProcessDepthFrameData(depthBuffer.UnderlyingBuffer, depthBuffer.Size, minDepth, maxDepth);
                            depthFrameProcessed = true;
                        }
                    }

                    // test
                    var depthBmp = depthFrame.SliceDepthImage((int)sliderMin.Value, (int)sliderMax.Value);
                    //BitmapSource bitmapSrc = BitmapSource.Create(this.depthFrameDescription.Width, this.depthFrameDescription.Height, 96, 96, PixelFormats.Bgr32, null, this.depthPixels_bin, this.depthBitmap.PixelWidth * 4);
                    Image<Bgr, Byte> openCVImg = new Image<Bgr, byte>(depthBmp.ToBitmap());
                    Image<Gray, byte> gray_image = openCVImg.Convert<Gray, byte>();
                    this.Image_Source.Source = ImageHelpers.ToBitmapSource(gray_image);

                    //this.Image_Depth.Source = ImageHelpers.ToBitmapSource(openCVImg); ;
                }
            }
            if (depthFrameProcessed)
            {
                this.RenderDepthPixels();
            }

            int fps = Utility.CalculateFrameRate();
            //Console.WriteLine("FPS : " + fps);
            txtFPS.Text = fps.ToString();
        }

        //void ConvertBinary(DepthFrame depthFrame)
        //{
        //    if (depthFrame == null)
        //    {
        //        return;
        //    }
        //    //Object recognition
        //    blobCount = 0;
        //    //Slicedepthimage is a Custom class
        //    var depthBmp = depthFrame.SliceDepthImage((int)sliderMin.Value, (int)sliderMax.Value);

        //    Image<Bgr, Byte> openCVImg = new Image<Bgr, byte>(depthBmp.ToBitmap());
        //    Image<Gray, byte> gray_image = openCVImg.Convert<Gray, byte>();

        //    using (MemStorage stor = new MemStorage())
        //    {
        //        //Find contours with no holes try CV_RETR_EXTERNAL to find holes
        //        Contour<System.Drawing.Point> contours = gray_image.FindContours(
        //         Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE,
        //         Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_EXTERNAL,
        //         stor);

        //        for (int i = 0; contours != null; contours = contours.HNext)
        //        {
        //            i++;

        //            if ((contours.Area > Math.Pow(sliderMinSize.Value, 2)) && (contours.Area < Math.Pow(sliderMaxSize.Value, 2)))
        //            {
        //                MCvBox2D box = contours.GetMinAreaRect();
        //                openCVImg.Draw(box, new Bgr(System.Drawing.Color.Red), 2);
        //                blobCount++;
        //            }
        //        }
        //    }

        //    txtBlobCount.Text = blobCount.ToString();
        //}

        void Tongull_DetectBlobs(DepthFrame depthFrame)
        {
            if (depthFrame == null)
            {
                return;
            }

            //Object recognition
            blobCount = 0;
            //Slicedepthimage is a Custom class
            //var depthBmp = depthFrame.SliceDepthImage(3000, 5000);
            BitmapSource depthBmp;
            if (_bFloorDetected)
            {
                //depthBmp = depthFrame.SliceDepthImageWithoutPlane(this._floor, this.coordinateMapper, (float)sliderPlanePos.Value, (int)sliderMin.Value, (int)sliderMax.Value);
                depthBmp = depthFrame.SliceDepthImageWithRectWithoutPlane(_floor, this.coordinateMapper, (float)sliderPlanePos.Value, (int)sliderMin.Value, (int)sliderMax.Value, 
                                                                  (int)positionLeft.Value, (int)positionTop.Value, (int)positionRight.Value, (int)positionBottom.Value);
            }
            else
            {
                depthBmp = depthFrame.SliceDepthImage((int)sliderMin.Value, (int)sliderMax.Value);
            }
            //var depthBmp = depthFrame.SliceDepthImageWithRect((int)sliderMin.Value, (int)sliderMax.Value, 
            //                                                  (int)positionLeft.Value, (int)positionTop.Value, (int)positionRight.Value, (int)positionBottom.Value);

            Image<Bgr, Byte> openCVImg = new Image<Bgr, byte>(depthBmp.ToBitmap());
            Image<Gray, byte> gray_image = openCVImg.Convert<Gray, byte>();

            // reduce image noise
            CvInvoke.cvDilate(gray_image, gray_image, IntPtr.Zero, 2); // 팽창
            CvInvoke.cvErode(gray_image, gray_image, IntPtr.Zero, 2); // 침식

            // 1. GaussianBlur
            // You may need to customize Size and Sigma depends on different input image.
            //CvInvoke.GaussianBlur(srcImg, destImg, new Size(0, 0), 5);
            var blurredImage = gray_image.SmoothGaussian(5, 5, 0, 0);

            // reference
            //Image<Gray, byte> Img_Source_Gray = Img_Org_Gray.Copy();
            //Image<Gray, byte> Img_Egde_Gray = Img_Source_Gray.CopyBlank();
            //Image<Gray, byte> Img_SourceSmoothed_Gray = Img_Source_Gray.CopyBlank();
            //Image<Gray, byte> Img_Otsu_Gray = Img_Org_Gray.CopyBlank();`
            Image<Gray, byte> Img_Source_Gray = blurredImage.Copy();
            //Image<Gray, byte> Img_Dest_Gray = Img_Source_Gray.CopyBlank();


            // 2. use Threshold 
            //CvInvoke.Threshold(srcImg, destImg, 10, 255, Emgu.CV.CvEnum.ThresholdType.Binary);
            //CvInvoke.cvThreshold(Img_Source_Gray.Ptr, Img_Dest_Gray.Ptr, 240, 255, 
            //    Emgu.CV.CvEnum.THRESH.CV_THRESH_OTSU | Emgu.CV.CvEnum.THRESH.CV_THRESH_BINARY);

            //CvInvoke.cvThreshold(Img_Source_Gray.Ptr, Img_Source_Gray.Ptr, 240, 255, Emgu.CV.CvEnum.THRESH.CV_THRESH_OTSU | Emgu.CV.CvEnum.THRESH.CV_THRESH_BINARY);


            // 3. Canny 
            //using (var cannyImage = new UMat()) ;
            //Image<Gray, byte> Img_Source_Gray = blurredImage.Copy();
            using (Image<Gray, Byte> cannyImage = new Image<Gray, byte>(Img_Source_Gray.Size))
            using (MemStorage stor = new MemStorage())
            {
                CvInvoke.cvCanny(Img_Source_Gray, Img_Source_Gray, 50, 150, 3);

                //Find contours with no holes try CV_RETR_EXTERNAL to find holes
                //Contour<System.Drawing.Point> contours = gray_image.FindContours(
                Contour<System.Drawing.Point> contours = Img_Source_Gray.FindContours(
                                                            Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE,
                                                            Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_EXTERNAL,
                                                            //Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_TREE,
                                                            stor);

                int largestContourIndex = -1;
                double largestArea = -1;
                double curArea = -1;
                for (int i = 0; contours != null; contours = contours.HNext)
                {
                    i++;

                    curArea = contours.Area;
                    if ((curArea > Math.Pow(sliderMinSize.Value, 2)) && (curArea < Math.Pow(sliderMaxSize.Value, 2)))
                    //if ((contours.Area > Math.Pow(30, 2)) && (contours.Area < Math.Pow(50, 2)))
                    {
                        MCvBox2D box = contours.GetMinAreaRect();
                        if (largestArea < curArea)
                        {
                            largestArea = curArea;
                            largestContourIndex = i;
                        }
                        //blurredImage.Draw(box, new Bgr(System.Drawing.Color.Red), 2);
                        Img_Source_Gray.Draw(box.MinAreaRect(), new Gray(128), 2);

                        blobCount++;
                    }
                }

                txtInfo.Text = "Contour index (" + largestContourIndex + ")"; 
            }

            #region Skeletonize
            if (true)
            {
                Image<Gray, byte> testImage = gray_image.Copy();
                Image<Gray, byte> skelImage = Skeletonize(testImage);

                //Console.WriteLine("skelImage size : " + skelImage.Size);
                this.Image_2.Source = ImageHelpers.ToBitmapSource(skelImage);

                //// 3D skeleton
                ushort[] _depthData = new ushort[512 * 424];
                depthFrame.CopyFrameDataToArray(_depthData);
                CameraSpacePoint[] depthMappedToCameraPoints = new CameraSpacePoint[512 * 424];
                coordinateMapper.MapDepthFrameToCameraSpace(_depthData, depthMappedToCameraPoints);

                byte data;
                byte l,t,r,b;
                byte h,i, j, k;
                int count = 0;

                CameraSpacePoint curPos;

                if (bShowSkeleton)
                {
                    canvas.Children.Clear();

                    // 2D space point
                    Point point = new Point();
                    ColorSpacePoint colorPoint;
                    double ratio = 0.25f;

                    var valH = skelImage.Height;
                    var valW = skelImage.Width;
                    for (int y = 0; y < valH; y++)
                    {
                        for (int x = 0; x < valW; x++)
                        {
                            data = skelImage.Data[y, x, 0];
                            l = skelImage.Data[y, x > 1 && x < valW ? x - 1 : x, 0];
                            t = skelImage.Data[y > 1 && y < valH ? y - 1 : y, x, 0];
                            r = skelImage.Data[y, x > 0 && x < valW - 1 ? x + 1 : x, 0];
                            b = skelImage.Data[y > 0 && y < valH - 1 ? y + 1 : y, x, 0];

                            h = skelImage.Data[y > 1 && y < valH ? y - 1 : y, x > 1 && x < valW ? x - 1 : x, 0];
                            i = skelImage.Data[y > 1 && y < valH ? y - 1 : y, x > 0 && x < valW - 1 ? x + 1 : x, 0];
                            j = skelImage.Data[y > 0 && y < valH - 1 ? y + 1 : y, x > 1 && x < valW ? x - 1 : x, 0];
                            k = skelImage.Data[y > 0 && y < valH - 1 ? y + 1 : y, x > 0 && x < valW - 1 ? x + 1 : x, 0];

                            var a = 0;
                            if (data != 0)
                            {
                                a = 1;
                                a += 3;
                            }

                            if (data != 0 && (l | t | r | b /*|h|i|j|k*/) != 0)
                            {
                                curPos = (CameraSpacePoint)depthMappedToCameraPoints.GetValue(y * valW + x);
                                skeletonPosition[count].X = curPos.X * 10;
                                skeletonPosition[count].Y = curPos.Y * 10;
                                skeletonPosition[count].Z = curPos.Z * 10;

                                colorPoint = this.kinectSensor.CoordinateMapper.MapCameraPointToColorSpace(curPos);
                                if (!float.IsInfinity(curPos.X) && !float.IsInfinity(curPos.Y))
                                {
                                    point.X = colorPoint.X;
                                    point.Y = colorPoint.Y;
                                }

                                if (!float.IsInfinity(curPos.X))
                                {
                                    if (this.skeletonPosition[count].X < minX) minX = skeletonPosition[count].X;
                                    if (this.skeletonPosition[count].X > maxX) maxX = skeletonPosition[count].X;
                                }
                                if (!float.IsInfinity(curPos.Y))
                                {
                                    if (this.skeletonPosition[count].Y < minY) minY = skeletonPosition[count].Y;
                                    if (this.skeletonPosition[count].Y > maxY) maxY = skeletonPosition[count].Y;
                                }
                                if (!float.IsInfinity(curPos.Z))
                                {
                                    if (this.skeletonPosition[count].Z < minZ) minZ = skeletonPosition[count].Z;
                                    if (this.skeletonPosition[count].Z > maxZ) maxZ = skeletonPosition[count].Z;
                                }

                                System.Windows.Shapes.Ellipse ellipse = new System.Windows.Shapes.Ellipse
                                {
                                    Fill = Brushes.Red,
                                    Width = 5,
                                    Height = 5
                                };

                                // draw Canvas skeleton
                                Canvas.SetLeft(ellipse, (point.X - ellipse.Width / 2) * ratio);
                                Canvas.SetTop(ellipse, (point.Y - ellipse.Height / 2) * ratio + 80);
                                canvas.Children.Add(ellipse);

                                count++;
                            }
                        }
                    }
                } else
                {
                    var valH = gray_image.Height;
                    var valW = gray_image.Width;
                    for (int y = 0; y < valH; y++)
                    {
                        for (int x = 0; x < valW; x++)
                        {
                            data = gray_image.Data[y, x, 0]; 
                            l = gray_image.Data[y, x > 1 && x < valW ? x - 1 : x, 0];
                            t = gray_image.Data[y > 1 && y < valH ? y - 1 : y, x, 0];
                            r = gray_image.Data[y, x > 0 && x < valW - 1 ? x + 1 : x, 0];
                            b = gray_image.Data[y > 0 && y < valH - 1 ? y + 1 : y, x, 0];

                            h = gray_image.Data[y > 1 && y < valH ? y - 1 : y, x > 1 && x < valW ? x - 1 : x, 0];
                            i = gray_image.Data[y > 1 && y < valH ? y - 1 : y, x > 0 && x < valW - 1 ? x + 1 : x, 0];
                            j = gray_image.Data[y > 0 && y < valH - 1 ? y + 1 : y, x > 1 && x < valW ? x - 1 : x, 0];
                            k = gray_image.Data[y > 0 && y < valH - 1 ? y + 1 : y, x > 0 && x < valW - 1 ? x + 1 : x, 0];

                            var a = 0;
                            if (data != 0)
                            {
                                a = 1;
                                a += 3;
                            }

                            if (data != 0 && (l | t | r | b /*|h|i|j|k*/) != 0)
                            {
                                curPos = (CameraSpacePoint)depthMappedToCameraPoints.GetValue(y * valW + x);
                                skeletonPosition[count].X = curPos.X * 10;
                                skeletonPosition[count].Y = curPos.Y * 10;
                                skeletonPosition[count].Z = curPos.Z * 10;

                                if (!float.IsInfinity(curPos.X))
                                {
                                    if (this.skeletonPosition[count].X < minX) minX = skeletonPosition[count].X;
                                    if (this.skeletonPosition[count].X > maxX) maxX = skeletonPosition[count].X;
                                }
                                if (!float.IsInfinity(curPos.Y))
                                {
                                    if (this.skeletonPosition[count].Y < minY) minY = skeletonPosition[count].Y;
                                    if (this.skeletonPosition[count].Y > maxY) maxY = skeletonPosition[count].Y;
                                }
                                if (!float.IsInfinity(curPos.Z))
                                {
                                    if (this.skeletonPosition[count].Z < minZ) minZ = skeletonPosition[count].Z;
                                    if (this.skeletonPosition[count].Z > maxZ) maxZ = skeletonPosition[count].Z;
                                }
                                count++;
                            }
                        }
                    }
                }                

                // set lookPos
                //if (skeletonCount == 0)
                //{
                //    lookPos.X = (minX + maxX) / 2;
                //    lookPos.Y = (minY + maxY) / 2;
                //    lookPos.Z = (minZ + maxZ) / 2;
                //}

                skeletonCount = count;
            }
            #endregion

            // clip rect
            gray_image.Draw(new System.Drawing.Rectangle((int)positionLeft.Value, (int)positionTop.Value, (int)(positionRight.Value - positionLeft.Value), (int)(positionBottom.Value - positionTop.Value)), new Gray(64), 1);

            // depthFrame -> canny
            //BitmapSource bs = (BitmapSource)depthFrame.ToBitmap();
            //Image<Bgr, Byte> depthImageCanny = new Image<Bgr, byte>(bs.ToBitmap());

            ////CvInvoke.cvCanny(depthImageCanny, depthImageCanny, 50, 150, 3);

            //this.Image_Depth.Source = ImageHelpers.ToBitmapSource(depthImageCanny);
            this.Image_Source.Source = ImageHelpers.ToBitmapSource(gray_image);
            this.Image_1.Source = ImageHelpers.ToBitmapSource(Img_Source_Gray);

            #region 침식, 팽창 ref
            if (false)
            {
                Image<Gray, byte> testImage = Img_Source_Gray.Copy();

                //CvInvoke.cvDilate(Img_Source_Gray, testImage, IntPtr.Zero, 4);
                CvInvoke.cvErode(Img_Source_Gray, testImage, IntPtr.Zero, 3); // 침식
                CvInvoke.cvDilate(testImage, testImage, IntPtr.Zero, 3); // 팽창

                this.Image_2.Source = ImageHelpers.ToBitmapSource(testImage);
            }
            #endregion

            txtBlobCount.Text = blobCount.ToString();
            //Console.WriteLine("Blob : " + blobCount);
        }

        void Tongull_DetectBlobs_test(DepthFrame depthFrame)
        {
            if (depthFrame == null)
            {
                return;
            }

            //Object recognition
            blobCount = 0;

            BitmapSource depthBmp = (BitmapSource)depthFrame.ToBitmap();

            Image<Bgr, Byte> openCVImg = new Image<Bgr, byte>(depthBmp.ToBitmap());
            Image<Gray, byte> gray_image = openCVImg.Convert<Gray, byte>();

            // reduce image noise

            // 1. GaussianBlur
            var blurredImage = gray_image.SmoothGaussian(3, 3, 5, 10);

            // 1-1. BilateralFilter 
            //var blurredImage = gray_image.SmoothBilatral(7, 50, 50);

            // reference
            Image<Gray, byte> Img_Source_Gray = blurredImage.Copy();


            // 2. use Threshold 
            //CvInvoke.Threshold(srcImg, destImg, 10, 255, Emgu.CV.CvEnum.ThresholdType.Binary);
            CvInvoke.cvThreshold(Img_Source_Gray.Ptr, Img_Source_Gray.Ptr, imageThreshMin.Value, imageThreshMax.Value, Emgu.CV.CvEnum.THRESH.CV_THRESH_BINARY);

            // 3. Canny 
            //using (var cannyImage = new UMat()) ;
            //Image<Gray, byte> Img_Source_Gray = blurredImage.Copy();
            using (Image<Gray, Byte> cannyImage = new Image<Gray, byte>(Img_Source_Gray.Size))
            using (MemStorage stor = new MemStorage())
            {
                //CvInvoke.cvCanny(Img_Source_Gray, Img_Source_Gray, 50, 150, 3);
                CvInvoke.cvCanny(Img_Source_Gray, Img_Source_Gray, cannyThreshMin.Value, cannyThreshMax.Value, 3);

                //Find contours with no holes try CV_RETR_EXTERNAL to find holes
                //Contour<System.Drawing.Point> contours = gray_image.FindContours(
                Contour<System.Drawing.Point> contours = Img_Source_Gray.FindContours(
                                                            Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_NONE,
                                                            //Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE,
                                                            //Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_TC89_KCOS,

                                                            //Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_EXTERNAL,
                                                            //Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_LIST,
                                                            Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_CCOMP,
                                                            //Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_TREE,
                                                            stor);

                var contoursOrg = contours;

                int largestContourIndex = -1;
                double largestArea = -1;
                double curArea = -1;
                for (int i = 0; contours != null; contours = contours.HNext)
                {
                    i++;

                    curArea = contours.Area;
                    if ((curArea > Math.Pow(sliderMinSize.Value, 2)) && (curArea < Math.Pow(sliderMaxSize.Value, 2)))
                    //if ((contours.Area > Math.Pow(30, 2)) && (contours.Area < Math.Pow(50, 2)))
                    {
                        MCvBox2D box = contours.GetMinAreaRect();
                        if (largestArea < curArea)
                        {
                            largestArea = curArea;
                            largestContourIndex = i;
                        }
                        //blurredImage.Draw(box, new Bgr(System.Drawing.Color.Red), 2);
                        Img_Source_Gray.Draw(box.MinAreaRect(), new Gray(128), 3);

                        blobCount++;
                    }
                }

                txtInfo.Text = "Contour index (" + largestContourIndex + ")";

                // draw Contour
                //CvInvoke.cvDrawContours(Img_Source_Gray, contoursOrg, new MCvScalar(230), new MCvScalar(70), 2, 2, Emgu.CV.CvEnum.LINE_TYPE.CV_AA, new System.Drawing.Point(0, 0));
            }

            // clip rect
            gray_image.Draw(new System.Drawing.Rectangle((int)positionLeft.Value, (int)positionTop.Value, (int)(positionRight.Value - positionLeft.Value), (int)(positionBottom.Value - positionTop.Value)), new Gray(64), 1);

            this.Image_Source.Source = ImageHelpers.ToBitmapSource(gray_image);
            this.Image_1.Source = ImageHelpers.ToBitmapSource(Img_Source_Gray);

            #region 침식, 팽창
            if (true)
            {
                Image<Gray, byte> testImage = Img_Source_Gray.Copy();

                //CvInvoke.cvDilate(Img_Source_Gray, testImage, IntPtr.Zero, 4);
                CvInvoke.cvErode(Img_Source_Gray, testImage, IntPtr.Zero, 3); // 침식
                CvInvoke.cvDilate(testImage, testImage, IntPtr.Zero, 3); // 팽창

                this.Image_2.Source = ImageHelpers.ToBitmapSource(testImage);
            }
            #endregion

            txtBlobCount.Text = blobCount.ToString();
            //Console.WriteLine("Blob : " + blobCount);
        }


        void Tongull_DetectBlobs_Infrared(InfraredFrame infFrame)
        {
            if (infFrame == null)
            {
                return;
            }

            //Object recognition
            blobCount = 0;
            //Slicedepthimage is a Custom class
            BitmapSource infBmp = (BitmapSource)infFrame.ToBitmap();

            Image<Bgr, Byte> openCVImg = new Image<Bgr, byte>(infBmp.ToBitmap());
            Image<Gray, byte> gray_image = openCVImg.Convert<Gray, byte>();

            // reduce image noise

            // You may need to customize Size and Sigma depends on different input image.
            Image<Gray, byte> blurredImage;

            // 1. GaussianBlur
            //blurredImage = gray_image.SmoothGaussian(5, 5, 0, 0);

            // 1-1. BilateralFilter 
            blurredImage = gray_image.SmoothBilatral(10, 50, 50);

            // reference
            Image<Gray, byte> Img_Source_Gray = blurredImage.Copy();
            //Image<Gray, byte> Img_Source_Gray = gray_image.Copy();

            // 2. use Threshold 
            //CvInvoke.cvThreshold(Img_Source_Gray.Ptr, Img_Source_Gray.Ptr, 128, 255, Emgu.CV.CvEnum.THRESH.CV_THRESH_BINARY);
            CvInvoke.cvThreshold(Img_Source_Gray.Ptr, Img_Source_Gray.Ptr, imageThreshMin.Value, imageThreshMax.Value, Emgu.CV.CvEnum.THRESH.CV_THRESH_BINARY);

            // 3. Canny 
            using (Image<Gray, Byte> cannyImage = new Image<Gray, byte>(Img_Source_Gray.Size))
            using (MemStorage stor = new MemStorage())
            {
                //CvInvoke.cvCanny(Img_Source_Gray, Img_Source_Gray, 50, 200, 3);
                CvInvoke.cvCanny(Img_Source_Gray, Img_Source_Gray, cannyThreshMin.Value, cannyThreshMax.Value, 3);
                //Console.WriteLine("Canny Threshold (" + cannyThreshMin.Value + ", " + cannyThreshMax.Value + ")");
                

                //Find contours with no holes try CV_RETR_EXTERNAL to find holes
                //Contour<System.Drawing.Point> contours = gray_image.FindContours(
                Contour<System.Drawing.Point> contours = Img_Source_Gray.FindContours(
                                                            Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE,
                                                            Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_EXTERNAL,
                                                            //Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_TREE,
                                                            stor);

                int largestContourIndex = -1;
                double largestArea = -1;
                double curArea = -1;
                for (int i = 0; contours != null; contours = contours.HNext)
                {
                    i++;

                    curArea = contours.Area;
                    if ((curArea > Math.Pow(sliderMinSize.Value, 2)) && (curArea < Math.Pow(sliderMaxSize.Value, 2)))
                    //if ((contours.Area > Math.Pow(30, 2)) && (contours.Area < Math.Pow(50, 2)))
                    {
                        MCvBox2D box = contours.GetMinAreaRect();
                        if (largestArea < curArea)
                        {
                            largestArea = curArea;
                            largestContourIndex = i;
                        }
                        //blurredImage.Draw(box, new Bgr(System.Drawing.Color.Red), 2);
                        Img_Source_Gray.Draw(box.MinAreaRect(), new Gray(128), 3);

                        blobCount++;
                    }
                }

                txtInfo.Text = "Contour index (" + largestContourIndex + ")";
            }

            // depthFrame -> canny
            //BitmapSource bs = (BitmapSource)depthFrame.ToBitmap();
            //Image<Bgr, Byte> depthImageCanny = new Image<Bgr, byte>(bs.ToBitmap());

            ////CvInvoke.cvCanny(depthImageCanny, depthImageCanny, 50, 150, 3);

            //this.Image_Depth.Source = ImageHelpers.ToBitmapSource(depthImageCanny);
            this.Image_Source.Source = ImageHelpers.ToBitmapSource(gray_image);
            this.Image_1.Source = ImageHelpers.ToBitmapSource(Img_Source_Gray);

            #region 침식, 팽창
            if (true)
            {
                Image<Gray, byte> testImage = Img_Source_Gray.Copy();

                //CvInvoke.cvDilate(Img_Source_Gray, testImage, IntPtr.Zero, 4);
                CvInvoke.cvErode(Img_Source_Gray, testImage, IntPtr.Zero, 3); // 침식
                CvInvoke.cvDilate(testImage, testImage, IntPtr.Zero, 3); // 팽창

                this.Image_2.Source = ImageHelpers.ToBitmapSource(testImage);
            }
            #endregion

            txtBlobCount.Text = blobCount.ToString();
            //Console.WriteLine("Blob : " + blobCount);
        }

        /// <summary>
        /// Renders color pixels into the writeableBitmap.
        /// </summary>
        private void RenderDepthPixels()
        {
            //this.depthBitmap.WritePixels(
            //    new Int32Rect(0, 0, this.depthBitmap.PixelWidth, this.depthBitmap.PixelHeight),
            //    this.depthPixels,
            //    this.depthBitmap.PixelWidth,
            //    0);
        }

        //If you get 'dllimport unknown'-, then add 'using System.Runtime.InteropServices;'
        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);

        public ImageSource ImageSourceForBitmap(System.Drawing.Bitmap bmp)
        {
            var handle = bmp.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally { DeleteObject(handle); }
        }

        /// <summary>
        /// Directly accesses the underlying image buffer of the DepthFrame to 
        /// create a displayable bitmap.
        /// This function requires the /unsafe compiler option as we make use of direct
        /// access to the native memory pointed to by the depthFrameData pointer.
        /// </summary>
        /// <param name="depthFrameData">Pointer to the DepthFrame image data</param>
        /// <param name="depthFrameDataSize">Size of the DepthFrame image data</param>
        /// <param name="minDepth">The minimum reliable depth value for the frame</param>
        /// <param name="maxDepth">The maximum reliable depth value for the frame</param>
        /// 
        private unsafe void ProcessDepthFrameData(IntPtr depthFrameData, uint depthFrameDataSize, ushort minDepth, ushort maxDepth)
        {
            // depth frame data is a 16 bit value
            ushort* frameData = (ushort*)depthFrameData;

            // convert depth to a visual representation
            for (int i = 0; i < (int)(depthFrameDataSize / this.depthFrameDescription.BytesPerPixel); ++i)
            {
                // Get the depth for this pixel
                ushort depth = frameData[i];

                // To convert to a byte, we're mapping the depth value to the byte range.
                // Values outside the reliable depth range are mapped to 0 (black).
                this.depthPixels[i] = (byte)(depth >= minDepth && depth <= maxDepth ? (depth / MapDepthToByte) : 0);
            }
        }

        void Reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            var reference = e.FrameReference.AcquireFrame();

            // Color
            using (var frame = reference.ColorFrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    camera.Source = frame.ToBitmap();
                    //if (_mode == Mode.Color)
                    //{
                    //    camera.Source = frame.ToBitmap();
                    //}
                }
            }

            // Depth
            using (var frame = reference.DepthFrameReference.AcquireFrame())
            {
                // 15FPS control
                if (!Utility.ControlFrameRate((int)this.sliderFPS.Value))
                {
                    return;
                }

                bool depthFrameProcessed = false;

                if (frame != null)
                {
                    if (_mode == Mode.Depth)
                    {
                        //camera.Source = frame.ToBitmap();
                        Image_Depth.Source = frame.ToBitmap();

                        Tongull_DetectBlobs(frame);
                        //Tongull_DetectBlobs_test(frame);

                        using (Microsoft.Kinect.KinectBuffer depthBuffer = frame.LockImageBuffer())
                        {
                            // verify data and write the color data to the display bitmap
                            if (((this.depthFrameDescription.Width * this.depthFrameDescription.Height) == (depthBuffer.Size / this.depthFrameDescription.BytesPerPixel)) &&
                                (this.depthFrameDescription.Width == this.depthBitmap.PixelWidth) && (this.depthFrameDescription.Height == this.depthBitmap.PixelHeight))
                            {
                                // Note: In order to see the full range of depth (including the less reliable far field depth)
                                // we are setting maxDepth to the extreme potential depth threshold
                                ushort maxDepth = 4000;
                                ushort minDepth = 850;// 3000;

                                // If you wish to filter by reliable depth distance, uncomment the following line:
                                //maxDepth = depthFrame.DepthMaxReliableDistance;

                                //this.ProcessDepthFrameData(depthBuffer.UnderlyingBuffer, depthBuffer.Size, depthFrame.DepthMinReliableDistance, maxDepth);
                                this.ProcessDepthFrameData(depthBuffer.UnderlyingBuffer, depthBuffer.Size, minDepth, maxDepth);
                                //this.ProcessDepthFrameData(depthBuffer.UnderlyingBuffer, depthBuffer.Size, (ushort)sliderMinSize.Value, (ushort)sliderMaxSize.Value);

                                //Tongull_DetectBlobs(frame);

                                depthFrameProcessed = true;
                            }
                        }

                        //// test
                        //var depthBmp = frame.SliceDepthImage((int)sliderMin.Value, (int)sliderMax.Value);
                        
                        //Image<Bgr, Byte> openCVImg = new Image<Bgr, byte>(depthBmp.ToBitmap());
                        //Image<Gray, byte> gray_image = openCVImg.Convert<Gray, byte>();
                        //this.Image_Source.Source = ImageHelpers.ToBitmapSource(gray_image);
                    }
                }

                if (depthFrameProcessed)
                {
                    this.RenderDepthPixels();
                }
            }

            // Infrared
            using (var frame = reference.InfraredFrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    if (_mode == Mode.Infrared)
                    {
                        camera.Source = frame.ToBitmap();
                        
                        Tongull_DetectBlobs_Infrared(frame);
                    }
                }
            }

            // Body
            using (var frame = reference.BodyFrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    canvas.Children.Clear();

                    _bodies = new Body[frame.BodyFrameSource.BodyCount];

                    frame.GetAndRefreshBodyData(_bodies);

                    foreach (var body in _bodies)
                    {
                        if (body != null)
                        {
                            if (body.IsTracked)
                            {
                                // Draw skeleton.
                                if (_displayBody)
                                {
                                    canvas.DrawSkeleton(body, this.kinectSensor);
                                }
                            }
                        }
                    }
                }
            }

            int fps = Utility.CalculateFrameRate();
            txtFPS.Text = fps.ToString();
        }


        private void BodyReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            using (BodyFrame frame = e.FrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    _floor = frame.Floor();
                    if (_floor.X != 0 && _floor.Y != 0 && _floor.Z != 0)
                    {
                        _bFloorDetected = true;
                        //Console.WriteLine("Plane : (" + _floor.X + ", " + _floor.Y + ", " + _floor.Z + ")");
                    }

                    #region 참고
                    //_floor = frame.Floor();
                    //_body = frame.Body();

                    //if (_floor != null && _body != null)
                    //{
                    //    CameraSpacePoint wrist3D = _body.Joints[JointType.HandLeft].Position;
                    //    Point wrist2D = wrist3D.ToPoint();

                    //    double distance = _floor.DistanceFrom(wrist3D);
                    //    int floorY = _floor.FloorY((int)wrist2D.X, (ushort)(wrist3D.Z * 1000));

                    //    TblDistance.Text = distance.ToString("N2");

                    //    Canvas.SetLeft(ImgHand, wrist2D.X - ImgHand.Width / 2.0);
                    //    Canvas.SetTop(ImgHand, wrist2D.Y - ImgHand.Height / 2.0);
                    //    Canvas.SetLeft(ImgFloor, wrist2D.X - ImgFloor.Width / 2.0);
                    //    Canvas.SetTop(ImgFloor, floorY - ImgFloor.Height / 2.0);
                    //}
                    #endregion
                }
            }
        }


        private void Button_Frame_Color(object sender, RoutedEventArgs e)
        {
            _mode = Mode.Color;
        }

        private void Button_Frame_Depth(object sender, RoutedEventArgs e)
        {
            //this.depthBitmap = new WriteableBitmap(this.depthFrameDescription.Width, this.depthFrameDescription.Height, 96.0, 96.0, PixelFormats.Gray8, null);

            _mode = Mode.Depth;
        }

        private void Button_Frame_Infrared(object sender, RoutedEventArgs e)
        {
            _mode = Mode.Infrared;
        }

        private void Button_Body(object sender, RoutedEventArgs e)
        {
            _displayBody = !_displayBody;
        }

        private void Button_Color_Enable(object sender, RoutedEventArgs e)
        {
            this.colorFrameReader = this.kinectSensor.ColorFrameSource.OpenReader();
            this.colorFrameReader.FrameArrived += this.Reader_ColorFrameArrived;
        }

        private void Button_Color_Disable(object sender, RoutedEventArgs e)
        {
            if (this.colorFrameReader != null)
            {
                this.colorFrameReader.FrameArrived -= this.Reader_ColorFrameArrived;
                this.colorFrameReader.Dispose();
                this.colorFrameReader = null;
            }
        }

        private void Button_Depth_Enable(object sender, RoutedEventArgs e)
        {
            this.depthFrameReader = this.kinectSensor.DepthFrameSource.OpenReader();
            this.depthFrameReader.FrameArrived += this.Reader_FrameArrived;
        }

        private void Button_Depth_Disable(object sender, RoutedEventArgs e)
        {
            if (this.depthFrameReader != null)
            {
                this.depthFrameReader.FrameArrived -= this.Reader_FrameArrived;
                this.depthFrameReader.Dispose();
                this.depthFrameReader = null;
            }
        }

        private void Button_Skeleton_Enable(object sender, RoutedEventArgs e)
        {
            this.bShowSkeleton = true;
        }

        private void Button_Skeleton_Disable(object sender, RoutedEventArgs e)
        {
            this.bShowSkeleton = false;
        }
        

        // org
        //public static Bitmap Skelatanize(Bitmap image)
        //{
        //    Image<Gray, byte> imgOld = new Image<Gray, byte>(image);
        //    Image<Gray, byte> img2 = (new Image<Gray, byte>(imgOld.Width, imgOld.Height, new Gray(255))).Sub(imgOld);
        //    Image<Gray, byte> eroded = new Image<Gray, byte>(img2.Size);
        //    Image<Gray, byte> temp = new Image<Gray, byte>(img2.Size);
        //    Image<Gray, byte> skel = new Image<Gray, byte>(img2.Size);
        //    skel.SetValue(0);
        //    CvInvoke.Threshold(img2, img2, 127, 256, 0);
        //    var element = CvInvoke.GetStructuringElement(ElementShape.Cross, new Size(3, 3), new Point(-1, -1));
        //    bool done = false;

        //    while (!done)
        //    {
        //        CvInvoke.Erode(img2, eroded, element, new Point(-1, -1), 1, BorderType.Reflect, default(MCvScalar));
        //        CvInvoke.Dilate(eroded, temp, element, new Point(-1, -1), 1, BorderType.Reflect, default(MCvScalar));
        //        CvInvoke.Subtract(img2, temp, temp);
        //        CvInvoke.BitwiseOr(skel, temp, skel);
        //        eroded.CopyTo(img2);
        //        if (CvInvoke.CountNonZero(img2) == 0) done = true;
        //    }
        //    return skel.Bitmap;
        //}

        public static System.Drawing.Bitmap Skeletonize(System.Drawing.Bitmap image)
        {
            Image<Gray, byte> imgOld = new Image<Gray, byte>(image);
            Image<Gray, byte> img2 = (new Image<Gray, byte>(imgOld.Width, imgOld.Height, new Gray(255))).Sub(imgOld);
            Image<Gray, byte> eroded = new Image<Gray, byte>(img2.Size);
            Image<Gray, byte> temp = new Image<Gray, byte>(img2.Size);
            Image<Gray, byte> skel = new Image<Gray, byte>(img2.Size);
            skel.SetValue(0);
            CvInvoke.cvThreshold(img2, img2, 127, 256, 0);
            //var element = CvInvoke.GetStructuringElement(ElementShape.Cross, new Size(3, 3), new Point(-1, -1));
            bool done = false;

            Console.WriteLine("Skeletonize Start !!!!!!!!!!!!!!!");
            int i = 0;
            while (!done)
            {
                CvInvoke.cvErode(img2, eroded, IntPtr.Zero, 1);
                CvInvoke.cvDilate(eroded, temp, IntPtr.Zero, 1);
                CvInvoke.cvSub(img2, temp, temp, IntPtr.Zero);
                CvInvoke.cvOr(skel, temp, skel, IntPtr.Zero);
                eroded.CopyTo(img2);
                if (CvInvoke.cvCountNonZero(img2) == 0) done = true;
                Console.WriteLine("Skeletonize " + i++);
            }
            Console.WriteLine("Skeletonize Start !!!!!!!!!!!!!!!");
            return skel.Bitmap;
        }

        public static Image<Gray, byte> Skeletonize(Image<Gray, byte> image)
        {
            StructuringElementEx element = new StructuringElementEx(3, 3, 1, 1, Emgu.CV.CvEnum.CV_ELEMENT_SHAPE.CV_SHAPE_CROSS);

            //CvInvoke.cvDilate(image, image, element, 1);

            //Image<Gray, byte> imgOld = image;
            //Image<Gray, byte> img2 = (new Image<Gray, byte>(imgOld.Width, imgOld.Height, new Gray(255))).Sub(imgOld);
            Image<Gray, byte> img2 = image;

            Image<Gray, byte> eroded = new Image<Gray, byte>(img2.Size);
            Image<Gray, byte> temp = new Image<Gray, byte>(img2.Size);
            Image<Gray, byte> skel = new Image<Gray, byte>(img2.Size);
            skel.SetValue(0);
            CvInvoke.cvThreshold(img2, img2, 127, 256, 0);

            bool done = false;

            //Console.WriteLine("Skeletonize Start !!!!!!!!!!!!!!!");
            int i = 0;
            while (!done)
            {
                if (true)
                {
                    CvInvoke.cvErode(img2, eroded, element, 1);
                    CvInvoke.cvDilate(eroded, temp, element, 1);
                    CvInvoke.cvSub(img2, temp, temp, IntPtr.Zero);
                    CvInvoke.cvOr(skel, temp, skel, IntPtr.Zero);
                    eroded.CopyTo(img2);
                } else
                {
                    CvInvoke.cvErode(img2, eroded, element, 1);
                    CvInvoke.cvDilate(eroded, temp, element, 1);
                    temp = img2.Sub(temp);
                    skel = skel | temp;
                    img2 = eroded;
                }

                if (CvInvoke.cvCountNonZero(img2) == 0) done = true;
                //Console.WriteLine("Skeletonize " + i++);
            }
            //Console.WriteLine("Skeletonize End !!!!!!!!!!!!!!!");
            return skel;
        }


        #region OpenGL
        private void OpenGLControl_OpenGLDraw(object sender, SharpGL.SceneGraph.OpenGLEventArgs args)
        {
            //  Get the OpenGL object.
            OpenGL gl = openGLControl.OpenGL;

            // Clear The Screen And The Depth Buffer
            gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
            // Reset The Current Modelview Matrix
            gl.LoadIdentity();

            //  Rotate around the Y axis.
            //gl.Rotate(rotation, 0.0f, 1.0f, 0.0f);

            //gl.PushMatrix();
            ////gl.Translate(lookPos.X, lookPos.Y, lookPos.Z);
            //gl.Rotate(rotation, 0.0f, 1.0f, 0.0f);
            ////gl.Translate(1.0f, 0.0f, 0.0f);
            //gl.PopMatrix();

            //  Draw a coloured pyramid.
            if (false)
            {
                gl.Begin(OpenGL.GL_TRIANGLES);
                gl.Color(1.0f, 0.0f, 0.0f);
                gl.Vertex(0.0f, 1.0f, 0.0f);
                gl.Color(0.0f, 1.0f, 0.0f);
                gl.Vertex(-1.0f, -1.0f, 1.0f);
                gl.Color(0.0f, 0.0f, 1.0f);
                gl.Vertex(1.0f, -1.0f, 1.0f);
                gl.Color(1.0f, 0.0f, 0.0f);
                gl.Vertex(0.0f, 1.0f, 0.0f);
                gl.Color(0.0f, 0.0f, 1.0f);
                gl.Vertex(1.0f, -1.0f, 1.0f);
                gl.Color(0.0f, 1.0f, 0.0f);
                gl.Vertex(1.0f, -1.0f, -1.0f);
                gl.Color(1.0f, 0.0f, 0.0f);
                gl.Vertex(0.0f, 1.0f, 0.0f);
                gl.Color(0.0f, 1.0f, 0.0f);
                gl.Vertex(1.0f, -1.0f, -1.0f);
                gl.Color(0.0f, 0.0f, 1.0f);
                gl.Vertex(-1.0f, -1.0f, -1.0f);
                gl.Color(1.0f, 0.0f, 0.0f);
                gl.Vertex(0.0f, 1.0f, 0.0f);
                gl.Color(0.0f, 0.0f, 1.0f);
                gl.Vertex(-1.0f, -1.0f, -1.0f);
                gl.Color(0.0f, 1.0f, 0.0f);
                gl.Vertex(-1.0f, -1.0f, 1.0f);
                gl.End();
            }

            

            gl.PushMatrix();

            gl.Translate(lookPos.X, lookPos.Y, lookPos.Z);
            gl.Rotate(rotation, 0.0f, 1.0f, 0.0f);
            gl.Translate(-lookPos.X, -lookPos.Y, -lookPos.Z);
            //gl.Translate(1.0f, 0.0f, 0.0f);

            // drawskeleton
            gl.PointSize(3.0f);
            gl.Begin(OpenGL.GL_POINTS);
            for (int i = 0; i <= skeletonCount; i++)
            {
                gl.Color(1.0f, 1.0f, 1.0f);
                gl.Vertex(skeletonPosition[i].X, skeletonPosition[i].Y, skeletonPosition[i].Z);
            }
            gl.End();

            //  Draw the grid lines.
            if (true)
            {
                gl.Begin(OpenGL.GL_LINES);
                for (int i = -30; i <= 30; i++)
                {
                    float fcol = ((i % 10) == 0) ? 0.3f : 0.15f;
                    gl.Color(fcol, fcol, fcol);

                    gl.Vertex(i, -5, -30);
                    gl.Vertex(i, -5, 30);
                    gl.Vertex(-30, -5, i);
                    gl.Vertex(30, -5, i);
                }
                gl.End();
            }

            gl.PopMatrix();

            rotation += 1.5f;
            if (rotation > 360.0f)
                rotation -= 360.0f;

            //  Flush OpenGL.
            gl.Flush();
        }

        private void OpenGLControl_OpenGLInitialized(object sender, SharpGL.SceneGraph.OpenGLEventArgs args)
        {
            //  Get the OpenGL object.
            OpenGL gl = openGLControl.OpenGL;
            
            //  Set the clear color.
            gl.ClearColor(0, 0, 0, 0);

            //// Enables Smooth Shading 
            //gl.ShadeModel(OpenGL.GL_SMOOTH);
            //// Depth Buffer Setup 
            //gl.ClearDepth(1.0f);
            //// Enables Depth Testing 
            //gl.Enable(OpenGL.GL_DEPTH_TEST);
            //// The Type Of Depth Test To Do 
            //gl.DepthFunc(OpenGL.GL_LEQUAL);

            //// Really Nice Perspective    Calculations 
            //gl.Hint(OpenGL.GL_PERSPECTIVE_CORRECTION_HINT, OpenGL.GL_NICEST);
        }

        private void OpenGLControl_Resized(object sender, SharpGL.SceneGraph.OpenGLEventArgs args)
        {
            //  Get the OpenGL object.
            OpenGL gl = openGLControl.OpenGL;

            //  Set the projection matrix.
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            //  Load the identity.
            gl.LoadIdentity();

            //  Create a perspective transformation.
            gl.Perspective(45.0f, (double)Width / (double)Height, 0.01, 200.0);

            //  Use the 'look at' helper function to position and aim the camera.
            //gl.LookAt(-5, 5, -5, 0, 0, 8, 0, 1, 0);
            //gl.LookAt(-5, 5, 20, 0, 0, 15, 0, 1, 0);
            //gl.LookAt(-5, 5, -5, lookPos.X, lookPos.Y, lookPos.Z, 0, 1, 0);
            //gl.LookAt(-5, 5, -5, 0.1537764, -1.759739, 7.670001, 0, 1, 0);
            gl.LookAt(0, 0, 15, lookPos.X, lookPos.Y, lookPos.Z, 0, 1, 0);
            Console.WriteLine("lookPos : " + lookPos.X + ", " + lookPos.Y + ", " + lookPos.Z + ")");

            //  Set the modelview matrix.
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            
            // Reset The Modelview Matrix 
            gl.LoadIdentity(); 
        }

        /// The current rotation.
        private float rotation = 0.0f;

        #endregion
        
    } // end of MainWindow

    public class Utility
    {
        #region Basic Frame Counter
        private static int lastTick;
        private static int lastFrameRate;
        private static int frameRate;

        private static int lastTickCon;
        private static int lastFrameRateCon;
        private static int frameRateCon;

        public static int CalculateFrameRate()
        {
            if (System.Environment.TickCount - lastTick >= 1000)
            {
                lastFrameRate = frameRate;
                frameRate = 0;
                lastTick = System.Environment.TickCount;
            }
            frameRate++;
            return lastFrameRate;
        }

        public static bool ControlFrameRate(int fps)
        {
            int val = 1000 / fps;
            if (System.Environment.TickCount - lastTickCon >= val)
            {
                //lastFrameRateCon = frameRateCon;
                //frameRateCon = 0;
                lastTickCon = System.Environment.TickCount;
                return true;
            }
            //frameRateCon++;
            return false;
        }
        #endregion
    }

    public enum Mode
    {
        Color,
        Depth,
        Infrared
    }
}
