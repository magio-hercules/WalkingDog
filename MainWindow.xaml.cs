
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



        // openGL
        //private SharpGL.SceneControl sceneControl1;


        // MultiFrame
        Boolean bMultiFrame = true;

        Mode _mode = Mode.Color;

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
                _multiSourceFrameReader = this.kinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Color | FrameSourceTypes.Depth | FrameSourceTypes.Infrared | FrameSourceTypes.Body);
                _multiSourceFrameReader.MultiSourceFrameArrived += Reader_MultiSourceFrameArrived;

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
            if (!Utility.ControlFrameRate(15))
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

                //this.depthBitmap.WritePixels(
                //   new Int32Rect(0, 0, this.depthBitmap.PixelWidth, this.depthBitmap.PixelHeight),
                //   this.depthPixels,
                //   this.depthBitmap.PixelWidth,
                //   0);
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
            //var depthBmp = depthFrame.SliceDepthImage((int)sliderMin.Value, (int)sliderMax.Value);
            var depthBmp = depthFrame.SliceDepthImageWithRect((int)sliderMin.Value, (int)sliderMax.Value, 
                                                              (int)positionLeft.Value, (int)positionTop.Value, (int)positionRight.Value, (int)positionBottom.Value);

            Image<Bgr, Byte> openCVImg = new Image<Bgr, byte>(depthBmp.ToBitmap());
            Image<Gray, byte> gray_image = openCVImg.Convert<Gray, byte>();

            // reduce image noise

            // 1. GaussianBlur
            // You may need to customize Size and Sigma depends on different input image.
            //CvInvoke.GaussianBlur(srcImg, destImg, new Size(0, 0), 5);
            var blurredImage = gray_image.SmoothGaussian(5, 5, 0, 0);

            // reference
            //Image<Gray, byte> Img_Source_Gray = Img_Org_Gray.Copy();
            //Image<Gray, byte> Img_Egde_Gray = Img_Source_Gray.CopyBlank();
            //Image<Gray, byte> Img_SourceSmoothed_Gray = Img_Source_Gray.CopyBlank();
            //Image<Gray, byte> Img_Otsu_Gray = Img_Org_Gray.CopyBlank();
            Image<Gray, byte> Img_Source_Gray = blurredImage.Copy();
            Image<Gray, byte> Img_Dest_Gray = Img_Source_Gray.CopyBlank();


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
            this.depthBitmap.WritePixels(
                new Int32Rect(0, 0, this.depthBitmap.PixelWidth, this.depthBitmap.PixelHeight),
                this.depthPixels,
                this.depthBitmap.PixelWidth,
                0);

            ////BitmapSource bs = (BitmapSource)depthFrame.ToBitmap();
            ////Image<Bgr, Byte> depthImageCanny = new Image<Bgr, byte>(bs.ToBitmap());

            //////CvInvoke.cvCanny(depthImageCanny, depthImageCanny, 50, 150, 3);

            ////this.Image_Depth.Source = ImageHelpers.ToBitmapSource(depthBitmap.ToBitmap());

            //System.Drawing.Bitmap bitmap = depthBitmap.ToBitmap();
            //BitmapSource bitmapSrc = BitmapSource.Create(this.depthFrameDescription.Width, this.depthFrameDescription.Height, 96, 96, PixelFormats.Bgr32, null, this.depthPixels_bin, this.depthBitmap.PixelWidth * 4);
            ////this.Image_Depth.Source = ImageSourceForBitmap(bitmap);
            //this.Image_Depth.Source = bitmapSrc;
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
                    if (_mode == Mode.Color)
                    {
                        camera.Source = frame.ToBitmap();
                    }
                }
            }

            // Depth
            using (var frame = reference.DepthFrameReference.AcquireFrame())
            {
                // 15FPS control
                if (!Utility.ControlFrameRate(15))
                {
                    return;
                }

                bool depthFrameProcessed = false;

                if (frame != null)
                {
                    if (_mode == Mode.Depth)
                    {
                        camera.Source = frame.ToBitmap();

                        Tongull_DetectBlobs(frame);

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


        #region OpenGL
        private void OpenGLControl_OpenGLDraw(object sender, SharpGL.SceneGraph.OpenGLEventArgs args)
        {
            //  Get the OpenGL object.
            OpenGL gl = openGLControl.OpenGL;

            //  Clear the color and depth buffer.
            gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);

            //  Load the identity matrix.
            gl.LoadIdentity();

            //  Rotate around the Y axis.
            gl.Rotate(rotation, 0.0f, 1.0f, 0.0f);

            //  Draw a coloured pyramid.
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

            //  Draw the grid lines.
            gl.Begin(OpenGL.GL_LINES);
            for (int i = -10; i <= 10; i++)
            {
                float fcol = ((i % 10) == 0) ? 0.3f : 0.15f;
                gl.Color(fcol, fcol, fcol);
                //gl.Vertex(i, -10, 0);
                //gl.Vertex(i, 10, 0);
                //gl.Vertex(-10, i, 0);
                //gl.Vertex(10, i, 0);
                gl.Vertex(i, -1, -10);
                gl.Vertex(i, -1, 10);
                gl.Vertex(-10, -1, i);
                gl.Vertex(10, -1, i);
            }
            gl.End();

            //  Nudge the rotation.
            rotation += 3.0f;
        }

        private void OpenGLControl_OpenGLInitialized(object sender, SharpGL.SceneGraph.OpenGLEventArgs args)
        {
            //  Get the OpenGL object.
            OpenGL gl = openGLControl.OpenGL;

            //  Set the clear color.
            gl.ClearColor(0, 0, 0, 0);
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
            gl.Perspective(45.0f, (double)Width / (double)Height, 0.01, 100.0);

            //  Use the 'look at' helper function to position and aim the camera.
            //gl.LookAt(-5, 5, -5, 0, 0, 0, 0, 1, 0);
            gl.LookAt(-5, 5, -5, 0, 0, 0, 0, 1, 0);

            //  Set the modelview matrix.
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
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
