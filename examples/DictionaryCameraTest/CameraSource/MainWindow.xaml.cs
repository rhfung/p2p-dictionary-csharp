/*
 * This example introduces a ~30 Hz ticker in the dictionary.
 * When the user checks the box, the video feed is also sent to the dictionary.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using AForge.Video;
using AForge.Video.DirectShow;
using System.IO;
using com.rhfung.P2PDictionary;
using com.rhfung.P2PDictionary.Peers;

namespace CameraSource
{
    

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        VideoCaptureDevice webcam;

        com.rhfung.P2PDictionary.P2PDictionary dict;
        MemoryStream lastBuffer = null;
        int timerTicker = 0;
        int frameTicker = 0;
        System.Windows.Threading.DispatcherTimer timer;


        public MainWindow()
        {
            InitializeComponent();
        }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            // find webcams
            FilterInfoCollection devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (devices.Count == 0)
            {
                MessageBox.Show("A webcam is required for this demo project");
                Application.Current.Shutdown();
            }

            // initialize the first webcam found -- not always true
            webcam = new VideoCaptureDevice(devices[0].MonikerString);
            webcam.DesiredFrameRate = 30;
            webcam.NewFrame += new NewFrameEventHandler(webcam_NewFrame);

            // useful feedback
            this.Title = devices[0].Name;

            // set up the dictionary
            dict = new com.rhfung.P2PDictionary.P2PDictionary("Camera source",
                com.rhfung.P2PDictionary.P2PDictionary.GetFreePort(2011),
                "cameratest", peerDiscovery: new ZeroconfDiscovery());

            // start 30 Hz source
            timer = new System.Windows.Threading.DispatcherTimer();
            timer.Tick += new EventHandler(timer_Tick);
            timer.Interval = new TimeSpan(0, 0, 0, 0, 1000 / 30);
            timer.Start();
        }

        void timer_Tick(object sender, EventArgs e)
        {
            dict["time"] = timerTicker++;
        }

        // handle new video frame from webcam
        void webcam_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            Dispatcher.Invoke((Action) delegate()
            {
                BitmapImage image;
                MemoryStream buffer;
                ToWpfBitmap(eventArgs.Frame, out image, out buffer);
                image1.Source = image;

                if (lastBuffer != null)
                {
                    lastBuffer.Dispose();
                }
                lastBuffer = buffer;
                dict["frame"] = buffer.ToArray();
                dict["frame_number"] = frameTicker++;
            });
        }

        private void checkBox1_Checked(object sender, RoutedEventArgs e)
        {
            webcam.Start();
        }

        private void checkBox1_Unchecked(object sender, RoutedEventArgs e)
        {
            webcam.SignalToStop();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (webcam != null)
            {
                webcam.SignalToStop();
            }
        }


        // stupid AForge -- have to convert from WinForms to WPF
        public static BitmapImage ToWpfBitmap(System.Drawing.Bitmap bitmap)
        {
            BitmapImage result;
            MemoryStream stream;
            ToWpfBitmap(bitmap, out result, out stream);
            stream.Dispose();

            return result;
        }


        // stupid AForge -- have to convert from WinForms to WPF
        // this method allows us to keep the MemoryStream for writing directly
        public static void ToWpfBitmap(System.Drawing.Bitmap bitmap, out BitmapImage result, out MemoryStream stream)
        {
            stream = new MemoryStream();

            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);

            stream.Position = 0;
            result = new BitmapImage();

            result.BeginInit();
            // According to MSDN, "The default OnDemand cache option retains access to the stream until the image is needed."
            // Force the bitmap to load right now so we can dispose the stream.
            result.CacheOption = BitmapCacheOption.OnLoad;
            result.StreamSource = stream;
            result.EndInit();
            result.Freeze();

        }

    }
}
