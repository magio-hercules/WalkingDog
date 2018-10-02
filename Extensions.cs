using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Runtime.InteropServices;

namespace walkingdog
{
    public static class Extensions
    {
        private static Body[] _bodyData = new Body[6];
        private static ushort[] _depthData = new ushort[512 * 424];
        private static CameraSpacePoint[] _cameraData = new CameraSpacePoint[512 * 424];
        private static byte[] _colorData = new byte[512 * 424 * 4];
        private static WriteableBitmap _bitmap = new WriteableBitmap(512, 424, 96.0, 96.0, PixelFormats.Bgr32, null);
        private static CoordinateMapper _mapper = KinectSensor.GetDefault().CoordinateMapper;

        /// <summary>
        /// Returns the first tracked body.
        /// </summary>
        /// <param name="frame">The Body frame.</param>
        /// <returns>The first tracked body.</returns>
        public static Body Body(this BodyFrame frame)
        {
            if (frame == null) return null;

            frame.GetAndRefreshBodyData(_bodyData);

            return _bodyData.Where(b => b != null && b.IsTracked).FirstOrDefault();
        }

        /// <summary>
        /// Creates a new instance of the Floor class.
        /// </summary>
        /// <param name="frame">The Body frame to use.</param>
        /// <returns>A new Floor object.</returns>
        public static Floor Floor(this BodyFrame frame)
        {
            if (frame == null) return null;

            return new Floor(frame.FloorClipPlane);
        }

        /// <summary>
        /// Converts the specified 3D Camera point into its equivalent 2D Depth point.
        /// </summary>
        /// <param name="point3D">The point in the 3D Camera space.</param>
        /// <returns>The equivalent point in the 2D Depth space.</returns>
        public static Point ToPoint(this CameraSpacePoint point3D)
        {
            Point point = new Point();
            DepthSpacePoint point2D = _mapper.MapCameraPointToDepthSpace(point3D);

            if (!float.IsInfinity(point2D.X) && !float.IsInfinity(point2D.Y))
            {
                point.X = point2D.X;
                point.Y = point2D.Y;
            }

            return point;
        }

        /// <summary>
        /// Returns the floor point that is right below the given point.
        /// </summary>
        /// <param name="floor">The floor obect to use.</param>
        /// <param name="x">The X value of the point in the 2D space.</param>
        /// <param name="z">The Z value of the point.</param>
        /// <returns>The equivalent Y value of the corresponding floor point.</returns>
        public static int FloorY(this Floor floor, int x, ushort z)
        {
            _mapper.MapDepthFrameToCameraSpace(_depthData, _cameraData);

            for (int index = 0; index < _depthData.Length; index++)
            {
                ushort currentZ = _depthData[index];
                int currentX = index % 512;

                if (currentX >= x - 10 && currentX <= x + 10 && currentZ >= z - 10 && currentZ <= z + 10)
                {
                    CameraSpacePoint point3D = _cameraData[index];

                    if (floor.DistanceFrom(point3D) < 0.01)
                    {
                        return index / 512;
                    }
                }
            }

            return 424;
        }

        /// <summary>
        /// Creates a bitmap representation of the Depth frame with or without highlighting the floor.
        /// </summary>
        /// <param name="frame">The Depth frame to visualize.</param>
        /// <param name="floor">The Floor to draw.</param>
        /// <returns>A bitmap representation of the Depth frame with the floor.</returns>
        public static WriteableBitmap Bitmap(this DepthFrame frame, Floor floor = null)
        {
            if (frame == null) return null;

            frame.CopyFrameDataToArray(_depthData);
            _mapper.MapDepthFrameToCameraSpace(_depthData, _cameraData);

            int colorIndex = 0;

            for (int index = 0; index < _depthData.Length; index++)
            {
                ushort depth = _depthData[index];
                byte color = (byte)(depth * 255 / 8000);
                CameraSpacePoint point = _cameraData[index];

                if (floor != null && floor.DistanceFrom(point) < 0.01)
                {
                    _colorData[colorIndex++] = Colors.Green.B;
                    _colorData[colorIndex++] = Colors.Green.G;
                    _colorData[colorIndex++] = Colors.Green.R;
                    _colorData[colorIndex++] = Colors.Green.A;
                }
                else
                {
                    _colorData[colorIndex++] = color;
                    _colorData[colorIndex++] = color;
                    _colorData[colorIndex++] = color;
                    _colorData[colorIndex++] = 255;
                }
            }

            _bitmap.Lock();

            Marshal.Copy(_colorData, 0, _bitmap.BackBuffer, _colorData.Length);

            _bitmap.AddDirtyRect(new Int32Rect(0, 0, _bitmap.PixelWidth, _bitmap.PixelHeight));
            _bitmap.Unlock();

            return _bitmap;
        }


        #region Camera

        public static ImageSource ToBitmap(this ColorFrame frame)
        {
            int width = frame.FrameDescription.Width;
            int height = frame.FrameDescription.Height;
            PixelFormat format = PixelFormats.Bgr32;

            byte[] pixels = new byte[width * height * ((format.BitsPerPixel + 7) / 8)];

            if (frame.RawColorImageFormat == ColorImageFormat.Bgra)
            {
                frame.CopyRawFrameDataToArray(pixels);
            }
            else
            {
                frame.CopyConvertedFrameDataToArray(pixels, ColorImageFormat.Bgra);
            }

            int stride = width * format.BitsPerPixel / 8;

            return BitmapSource.Create(width, height, 96, 96, format, null, pixels, stride);
        }

        public static ImageSource ToBitmap(this DepthFrame frame)
        {
            int width = frame.FrameDescription.Width;
            int height = frame.FrameDescription.Height;
            PixelFormat format = PixelFormats.Bgr32;

            ushort minDepth = frame.DepthMinReliableDistance;
            ushort maxDepth = frame.DepthMaxReliableDistance;

            ushort[] pixelData = new ushort[width * height];
            byte[] pixels = new byte[width * height * (format.BitsPerPixel + 7) / 8];

            frame.CopyFrameDataToArray(pixelData);

            int colorIndex = 0;
            for (int depthIndex = 0; depthIndex < pixelData.Length; ++depthIndex)
            {
                ushort depth = pixelData[depthIndex];

                byte intensity = (byte)(depth >= minDepth && depth <= maxDepth ? depth : 0);

                pixels[colorIndex++] = intensity; // Blue
                pixels[colorIndex++] = intensity; // Green
                pixels[colorIndex++] = intensity; // Red

                ++colorIndex;
            }

            int stride = width * format.BitsPerPixel / 8;

            return BitmapSource.Create(width, height, 96, 96, format, null, pixels, stride);
        }

        public static ImageSource ToBitmap(this InfraredFrame frame)
        {
            int width = frame.FrameDescription.Width;
            int height = frame.FrameDescription.Height;
            PixelFormat format = PixelFormats.Bgr32;

            ushort[] frameData = new ushort[width * height];
            byte[] pixels = new byte[width * height * (format.BitsPerPixel + 7) / 8];

            frame.CopyFrameDataToArray(frameData);

            int colorIndex = 0;
            for (int infraredIndex = 0; infraredIndex < frameData.Length; infraredIndex++)
            {
                ushort ir = frameData[infraredIndex];

                byte intensity = (byte)(ir >> 7);

                pixels[colorIndex++] = (byte)(intensity / 1); // Blue
                pixels[colorIndex++] = (byte)(intensity / 1); // Green   
                pixels[colorIndex++] = (byte)(intensity / 0.4); // Red

                colorIndex++;
            }

            int stride = width * format.BitsPerPixel / 8;

            return BitmapSource.Create(width, height, 96, 96, format, null, pixels, stride);
        }

        #endregion

        #region Body

        public static Joint ScaleTo(this Joint joint, double width, double height, float skeletonMaxX, float skeletonMaxY)
        {
            joint.Position = new CameraSpacePoint
            {
                X = Scale(width, skeletonMaxX, joint.Position.X),
                Y = Scale(height, skeletonMaxY, -joint.Position.Y),
                Z = joint.Position.Z
            };

            return joint;
        }

        public static Joint ScaleTo(this Joint joint, double width, double height)
        {
            return ScaleTo(joint, width, height, 1.0f, 1.0f);
        }

        private static float Scale(double maxPixel, double maxSkeleton, float position)
        {
            float value = (float)((((maxPixel / maxSkeleton) / 2) * position) + (maxPixel / 2));

            if (value > maxPixel)
            {
                return (float)maxPixel;
            }

            if (value < 0)
            {
                return 0;
            }

            return value;
        }

        #endregion

        #region Drawing

        public static void DrawSkeleton(this Canvas canvas, Body body, KinectSensor sensor)
        {
            if (body == null) return;

            //canvas.Children.Clear();

            foreach (Joint joint in body.Joints.Values)
            {
                canvas.DrawPoint(joint, sensor);
            }

            //canvas.DrawLine(body.Joints[JointType.Head], body.Joints[JointType.Neck]);
            //canvas.DrawLine(body.Joints[JointType.Neck], body.Joints[JointType.SpineShoulder]);
            //canvas.DrawLine(body.Joints[JointType.SpineShoulder], body.Joints[JointType.ShoulderLeft]);
            //canvas.DrawLine(body.Joints[JointType.SpineShoulder], body.Joints[JointType.ShoulderRight]);
            //canvas.DrawLine(body.Joints[JointType.SpineShoulder], body.Joints[JointType.SpineMid]);
            //canvas.DrawLine(body.Joints[JointType.ShoulderLeft], body.Joints[JointType.ElbowLeft]);
            //canvas.DrawLine(body.Joints[JointType.ShoulderRight], body.Joints[JointType.ElbowRight]);
            //canvas.DrawLine(body.Joints[JointType.ElbowLeft], body.Joints[JointType.WristLeft]);
            //canvas.DrawLine(body.Joints[JointType.ElbowRight], body.Joints[JointType.WristRight]);
            //canvas.DrawLine(body.Joints[JointType.WristLeft], body.Joints[JointType.HandLeft]);
            //canvas.DrawLine(body.Joints[JointType.WristRight], body.Joints[JointType.HandRight]);
            //canvas.DrawLine(body.Joints[JointType.HandLeft], body.Joints[JointType.HandTipLeft]);
            //canvas.DrawLine(body.Joints[JointType.HandRight], body.Joints[JointType.HandTipRight]);
            //canvas.DrawLine(body.Joints[JointType.HandTipLeft], body.Joints[JointType.ThumbLeft]);
            //canvas.DrawLine(body.Joints[JointType.HandTipRight], body.Joints[JointType.ThumbRight]);
            //canvas.DrawLine(body.Joints[JointType.SpineMid], body.Joints[JointType.SpineBase]);
            //canvas.DrawLine(body.Joints[JointType.SpineBase], body.Joints[JointType.HipLeft]);
            //canvas.DrawLine(body.Joints[JointType.SpineBase], body.Joints[JointType.HipRight]);
            //canvas.DrawLine(body.Joints[JointType.HipLeft], body.Joints[JointType.KneeLeft]);
            //canvas.DrawLine(body.Joints[JointType.HipRight], body.Joints[JointType.KneeRight]);
            //canvas.DrawLine(body.Joints[JointType.KneeLeft], body.Joints[JointType.AnkleLeft]);
            //canvas.DrawLine(body.Joints[JointType.KneeRight], body.Joints[JointType.AnkleRight]);
            //canvas.DrawLine(body.Joints[JointType.AnkleLeft], body.Joints[JointType.FootLeft]);
            //canvas.DrawLine(body.Joints[JointType.AnkleRight], body.Joints[JointType.FootRight]);
            canvas.DrawLine(body.Joints[JointType.Head], body.Joints[JointType.Neck], sensor);
            canvas.DrawLine(body.Joints[JointType.Neck], body.Joints[JointType.SpineShoulder], sensor);
            canvas.DrawLine(body.Joints[JointType.SpineShoulder], body.Joints[JointType.ShoulderLeft], sensor);
            canvas.DrawLine(body.Joints[JointType.SpineShoulder], body.Joints[JointType.ShoulderRight], sensor);
            canvas.DrawLine(body.Joints[JointType.SpineShoulder], body.Joints[JointType.SpineMid], sensor);
            canvas.DrawLine(body.Joints[JointType.ShoulderLeft], body.Joints[JointType.ElbowLeft], sensor);
            canvas.DrawLine(body.Joints[JointType.ShoulderRight], body.Joints[JointType.ElbowRight], sensor);
            canvas.DrawLine(body.Joints[JointType.ElbowLeft], body.Joints[JointType.WristLeft], sensor);
            canvas.DrawLine(body.Joints[JointType.ElbowRight], body.Joints[JointType.WristRight], sensor);
            canvas.DrawLine(body.Joints[JointType.WristLeft], body.Joints[JointType.HandLeft], sensor);
            canvas.DrawLine(body.Joints[JointType.WristRight], body.Joints[JointType.HandRight], sensor);
            canvas.DrawLine(body.Joints[JointType.HandLeft], body.Joints[JointType.HandTipLeft], sensor);
            canvas.DrawLine(body.Joints[JointType.HandRight], body.Joints[JointType.HandTipRight], sensor);
            canvas.DrawLine(body.Joints[JointType.HandTipLeft], body.Joints[JointType.ThumbLeft], sensor);
            canvas.DrawLine(body.Joints[JointType.HandTipRight], body.Joints[JointType.ThumbRight], sensor);
            canvas.DrawLine(body.Joints[JointType.SpineMid], body.Joints[JointType.SpineBase], sensor);
            canvas.DrawLine(body.Joints[JointType.SpineBase], body.Joints[JointType.HipLeft], sensor);
            canvas.DrawLine(body.Joints[JointType.SpineBase], body.Joints[JointType.HipRight], sensor);
            canvas.DrawLine(body.Joints[JointType.HipLeft], body.Joints[JointType.KneeLeft], sensor);
            canvas.DrawLine(body.Joints[JointType.HipRight], body.Joints[JointType.KneeRight], sensor);
            canvas.DrawLine(body.Joints[JointType.KneeLeft], body.Joints[JointType.AnkleLeft], sensor);
            canvas.DrawLine(body.Joints[JointType.KneeRight], body.Joints[JointType.AnkleRight], sensor);
            canvas.DrawLine(body.Joints[JointType.AnkleLeft], body.Joints[JointType.FootLeft], sensor);
            canvas.DrawLine(body.Joints[JointType.AnkleRight], body.Joints[JointType.FootRight], sensor);
        }

        public static void DrawPoint(this Canvas canvas, Joint joint, KinectSensor sensor)
        {
            if (joint.TrackingState == TrackingState.NotTracked)
                return;

            Point point = new Point();
            Boolean bTest = true;
            if (!bTest)
            {
                joint = joint.ScaleTo(canvas.ActualWidth, canvas.ActualHeight);
            }
            else
            {
                // 3D space point
                CameraSpacePoint jointPosition = joint.Position;

                // 2D space point
                //point = new Point();

                ColorSpacePoint colorPoint = sensor.CoordinateMapper.MapCameraPointToColorSpace(jointPosition);

                point.X = float.IsInfinity(colorPoint.X) ? 0 : colorPoint.X;
                point.Y = float.IsInfinity(colorPoint.Y) ? 0 : colorPoint.Y;
            }

            int size = 20;
            if (joint.JointType ==JointType.Head)
            {
                size = 100;
            }

            Ellipse ellipse = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(Colors.LightBlue)
            };

            if (!bTest)
            {
                Canvas.SetLeft(ellipse, joint.Position.X - ellipse.Width / 2);
                Canvas.SetTop(ellipse, joint.Position.Y - ellipse.Height / 2);
            } else
            {
                System.Windows.Controls.Canvas.SetLeft(ellipse, point.X - ellipse.Width / 2);
                System.Windows.Controls.Canvas.SetTop(ellipse, point.Y - ellipse.Height / 2);
            }

            canvas.Children.Add(ellipse);
        }

        public static void DrawLine(this Canvas canvas, Joint first, Joint second, KinectSensor sensor)
        {
            if (first.TrackingState == TrackingState.NotTracked || second.TrackingState == TrackingState.NotTracked) return;

            Point pointF = new Point();
            Point pointS = new Point();

            var bTest = true;
            if (!bTest)
            {
                first = first.ScaleTo(canvas.ActualWidth, canvas.ActualHeight);
                second = second.ScaleTo(canvas.ActualWidth, canvas.ActualHeight);
            } else
            {
                // 3D space point
                CameraSpacePoint jointPosition = first.Position;
                ColorSpacePoint colorPoint = sensor.CoordinateMapper.MapCameraPointToColorSpace(jointPosition);

                pointF.X = float.IsInfinity(colorPoint.X) ? 0 : colorPoint.X;
                pointF.Y = float.IsInfinity(colorPoint.Y) ? 0 : colorPoint.Y;

                // Second
                jointPosition = second.Position;
                colorPoint = sensor.CoordinateMapper.MapCameraPointToColorSpace(jointPosition);

                pointS.X = float.IsInfinity(colorPoint.X) ? 0 : colorPoint.X;
                pointS.Y = float.IsInfinity(colorPoint.Y) ? 0 : colorPoint.Y;
            }

            Line line;
            if (!bTest)
            {
                line = new Line
                {
                    X1 = first.Position.X,
                    Y1 = first.Position.Y,
                    X2 = second.Position.X,
                    Y2 = second.Position.Y,
                    StrokeThickness = 8,
                    Stroke = new SolidColorBrush(Colors.LightBlue)
                };
            }
            else
            {
                line = new Line
                {
                    X1 = pointF.X,
                    Y1 = pointF.Y,
                    X2 = pointS.X,
                    Y2 = pointS.Y,
                    StrokeThickness = 8,
                    Stroke = new SolidColorBrush(Colors.LightBlue)
                };
            }

            canvas.Children.Add(line);
        }

        #endregion
    }
}
