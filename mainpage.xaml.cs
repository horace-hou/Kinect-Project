using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI.Xaml;  // for multi sourceframe
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using WindowsPreview.Kinect;
using System.ComponentModel;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace App5
{
    public enum DisplayFrameType
    {
        Infrared,
        Color,
        Depth
    }

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page,INotifyPropertyChanged
    {
        

        private const float InfraredSourceValueMaximum = (float)ushort.MaxValue;

        private const float InfraredOutputValueMinimum = 0.01f;

        private const float InfraredOutputValueMaximum = 1.0f;

        private const float InfraredSceneValueAverage = 0.08f;

        private const float InfraredSceneStandardDeviations = 3.0f;

        private const int BytesPerPixel = 4;

        // consts and other private variables....

        private const DisplayFrameType DEFAULT_DISPLAYFRAMETYPE = DisplayFrameType.Color;

        private KinectSensor kinectSensor = null;

        private string statusText = null;

        private WriteableBitmap bitmap = null;

        private FrameDescription currentFrameDescription;

        private DisplayFrameType currentDisplayFrameType;

        private MultiSourceFrameReader multiSourceFrameReader = null;
       
        public event PropertyChangedEventHandler PropertyChanged;

        public string StatusText
        {
            get { return this.statusText; }
            set
            {
                if(this.statusText != value)
                {
                    this.statusText = value;
                    if(this.PropertyChanged!=null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        public FrameDescription CurrentFrameDescription 
        {
            get { return this.currentFrameDescription; }
            set
            {
                if(this.currentFrameDescription!=value)
                {
                    this.currentFrameDescription = value;
                    if(this.PropertyChanged!=null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("CurrentFrameDescrition"));
                    }
                }
            }
        }

        // Infrared Frame
        private ushort[] infraredFrameData = null;

        private byte[] infraredPixels = null;

        // Depth Frame
        private ushort[] depthFrameData = null;
        private byte[] depthPixels = null;

        public MainPage()
        {
            // one sensor is currently supported
            this.kinectSensor = KinectSensor.GetDefault();

            SetupCurrentDisplay(DEFAULT_DISPLAYFRAMETYPE);

            this.multiSourceFrameReader =
                this.kinectSensor.OpenMultiSourceFrameReader(
                 FrameSourceTypes.Infrared 
                 | FrameSourceTypes.Color
                 | FrameSourceTypes.Depth);

            this.multiSourceFrameReader.MultiSourceFrameArrived +=
                this.Reader_MultiSourceFrameArrived;

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // use the window object as the view model in this simple example
            this.DataContext = this;

            // open the sensor
            this.kinectSensor.Open();

            this.InitializeComponent();
        }


        private void Reader_MultiSourceFrameArrived(MultiSourceFrameReader sender, MultiSourceFrameArrivedEventArgs e)
        {
            MultiSourceFrame multiSouceFrame = e.FrameReference.AcquireFrame();

            // If the Frame has expired by the time we process this event, return.
            if(multiSouceFrame==null)
            {
                return;
            }

            switch (currentDisplayFrameType)
            {
                case DisplayFrameType.Infrared:
                    using (InfraredFrame infraredFrame = multiSouceFrame.InfraredFrameReference.AcquireFrame())
                    {
                        ShowInfraredFrame(infraredFrame);
                    }
                    break;
                case DisplayFrameType.Color:
                    using (ColorFrame colorFrame = multiSouceFrame.ColorFrameReference.AcquireFrame())
                    {
                        ShowColorFrame(colorFrame);
                    }
                    break;
                case DisplayFrameType.Depth:
                    using(DepthFrame depthFrame= multiSouceFrame.DepthFrameReference.AcquireFrame())
                    {
                        ShowDepthFrame(depthFrame);
                    }
                    break;
                default:
                    break;
            }

        }


        private void Sensor_IsAvailableChanged(KinectSensor sender, IsAvailableChangedEventArgs args)
        {
            this.StatusText = this.kinectSensor.IsAvailable ? 
                  "Running" : "Not Available";
        }

        private void ShowInfraredFrame(InfraredFrame infraredFrame)
        {
            bool infraredFrameProcessed = false;

            if(infraredFrame != null)
            {
                FrameDescription infraredFrameDescription = infraredFrame.FrameDescription;

                // verify data and write the new infrared frame data tot he display bitmap

                if(((infraredFrameDescription.Width*infraredFrameDescription.Height)==this.infraredFrameData.Length)
                    &&(infraredFrameDescription.Width==this.bitmap.PixelWidth)
                    &&(infraredFrameDescription.Height==this.bitmap.PixelHeight))
                {
                    // Copy the pixel data from the image to a temporary array 

                    infraredFrame.CopyFrameDataToArray(this.infraredFrameData);

                    infraredFrameProcessed = true;
                }
            }
        
            if (infraredFrameProcessed)
            {
                this.ConvertInfraredDataToPixels();

                this.RenderPixelArray(this.infraredPixels);
            }
        }

        private void ShowColorFrame(ColorFrame colorFrame)
        {
            bool colorFrameProcessed = false;

            if(colorFrame != null)
            {
                FrameDescription colorFrameDescription = colorFrame.FrameDescription;

                // verify data and write the new color frame data to the writable bitmap

                if((colorFrameDescription.Width==this.bitmap.PixelWidth)&&(colorFrameDescription.Height==this.bitmap.PixelHeight))
                {
                    if(colorFrame.RawColorImageFormat==ColorImageFormat.Bgra)
                    {
                        colorFrame.CopyRawFrameDataToBuffer(this.bitmap.PixelBuffer);
                    }
                    else
                    {
                        colorFrame.CopyConvertedFrameDataToBuffer(this.bitmap.PixelBuffer,ColorImageFormat.Bgra);
                    }
                    colorFrameProcessed = true;
                }
            }
            if(colorFrameProcessed)
            {
                this.bitmap.Invalidate();

                FrameDisplayImage.Source = this.bitmap;
            }
        }

        private void ShowDepthFrame(DepthFrame depthFrame)
        {
            bool depthFrameProcessed = false;

            ushort minDepth = 0;
            ushort maxDepth = 0;

            if(depthFrame !=null)
            {
                FrameDescription depthFrameDescription = depthFrame.FrameDescription;

                // verify data and write the new infrared frame data 
                // to the display bitmap
                if(((depthFrameDescription.Width*depthFrameDescription.Height)==this.infraredFrameData.Length)
                    &&(depthFrameDescription.Width == this.bitmap.PixelWidth)
                    &&(depthFrameDescription.Height == this.bitmap.PixelHeight))
                {
                    // Copy the pixel data from the image to a temporary array
                    depthFrame.CopyFrameDataToArray(this.depthFrameData);

                    minDepth = depthFrame.DepthMinReliableDistance;

                    maxDepth = depthFrame.DepthMaxReliableDistance;

                    depthFrameProcessed = true;

                }

            }
            if(depthFrameProcessed)
            {
                this.ConvertDepthDataToPixels(minDepth, maxDepth);
                
                this.RenderPixelArray(this.depthPixels);
            }
        }


        private void ConvertInfraredDataToPixels()
        {
            // Convert the infrared to RGB
            int colorPixelIndex = 0;

            for (int i = 0; i < this.infraredFrameData.Length; ++i)
            {
                // normalize the incoming infrared data (ushort) to 
                // a float ranging from InfraredOutputValueMinimum
                // to InfraredOutputValueMaximum] by

                // 1. dividing the incoming value by the 
                // source maximum value
                float intensityRatio = (float)this.infraredFrameData[i] / InfraredSourceValueMaximum;

                // 2. dividing by the 
                // (average scene value * standard deviations)
                intensityRatio /= InfraredSceneValueAverage * InfraredSceneStandardDeviations;

                // 3. limiting the value to InfraredOutputValueMaximum
                intensityRatio = Math.Min(InfraredOutputValueMaximum,
                    intensityRatio);

                // 4. limiting the lower value InfraredOutputValueMinimum
                intensityRatio = Math.Max(InfraredOutputValueMinimum,
                    intensityRatio);

                // 5. converting the normalized value to a byte and using 
                // the result as the RGB components required by the image
                byte intensity = (byte)(intensityRatio * 255.0f);

                this.infraredPixels[colorPixelIndex++] = intensity; //Blue

                this.infraredPixels[colorPixelIndex++] = intensity; //Green

                this.infraredPixels[colorPixelIndex++] = intensity; //Red

                this.infraredPixels[colorPixelIndex++] = 255;       //Alpha           

            }
        }

        private void ConvertDepthDataToPixels(ushort minDepth, ushort maxDepth)
        {
            int colorPixelIndex = 0;

            // Shape the depth to the range of a byte
            int mapDepthToByte = maxDepth / 256;

            for (int i=0; i<this.depthFrameData.Length;++i)
            {
                // Get the depth for this pixel
                ushort depth = this.depthFrameData[i];

               // To convert to a byte, we're mapping the depth value
               // to the byte range.
               // Values outside the reliable depth range are 
               // mapped to 0 (black).
                byte intensity = (byte)(depth >= minDepth && depth <= maxDepth ?
                    (depth / mapDepthToByte) : 0);
                this.depthPixels[colorPixelIndex++] = intensity;    //b
                this.depthPixels[colorPixelIndex++] = intensity;    //g
                this.depthPixels[colorPixelIndex++] = intensity;    //r
                this.depthPixels[colorPixelIndex++] = 255;  //alpha
            }
        }

        private void RenderPixelArray(byte[] pixels)
        {
            pixels.CopyTo(this.bitmap.PixelBuffer);

            this.bitmap.Invalidate();

            FrameDisplayImage.Source = this.bitmap;     // FrameDisplayInmage is the image in the xmal 
        }

        private void SetupCurrentDisplay(DisplayFrameType newDisplayFrameType)
        {
            currentDisplayFrameType = newDisplayFrameType;
            
            switch (currentDisplayFrameType)
            {
                case DisplayFrameType.Infrared:
                    
                    FrameDescription infraredFrameDescription = this.kinectSensor.InfraredFrameSource.FrameDescription;

                    this.CurrentFrameDescription = infraredFrameDescription;

                    // allocate space to put the pixel being received and converted
                    this.infraredFrameData = new ushort[infraredFrameDescription.Width*infraredFrameDescription.Height];

                    this.infraredPixels = new byte[infraredFrameDescription.Width*infraredFrameDescription.Height*BytesPerPixel];

                    this.bitmap = new WriteableBitmap(infraredFrameDescription.Width, infraredFrameDescription.Height);

                    break;

                case DisplayFrameType.Color:

                    FrameDescription colorFrameDescription = this.kinectSensor.ColorFrameSource.FrameDescription;

                    this.CurrentFrameDescription = colorFrameDescription;

                    // create the bitmap to display
                    this.bitmap = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height);

                    break;

                case DisplayFrameType.Depth:

                    FrameDescription depthFrameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

                    this.CurrentFrameDescription = depthFrameDescription;

                    // allocate space to put the pixels being, received and converted
                    this.depthFrameData = new ushort[depthFrameDescription.Width * depthFrameDescription.Height];

                    this.depthPixels = new byte[depthFrameDescription.Width * depthFrameDescription.Height * BytesPerPixel];

                    this.bitmap = new WriteableBitmap(depthFrameDescription.Width, depthFrameDescription.Height);

                    break;

                default:
                    break;

            } 
        }
        private void InfraredButton_Click(object sender, RoutedEventArgs e)
        {
            SetupCurrentDisplay(DisplayFrameType.Infrared);
        }
        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            SetupCurrentDisplay(DisplayFrameType.Color);
        }
        private void DepthButton_Click(object sender, RoutedEventArgs e)
        {
            SetupCurrentDisplay(DisplayFrameType.Depth);
        }

        ///<notes>
        ///To get pixels in the byte array into something xaml can use. it must
        ///be copied into a WritableBitmap pbject. Onec it's in a WritabeleBitmap,
        ///the frame can be used in xaml simply by linking the source of an Image in 
        ///Xaml to the bitmap calss variable.
        ///</notes>
        
    }

}
