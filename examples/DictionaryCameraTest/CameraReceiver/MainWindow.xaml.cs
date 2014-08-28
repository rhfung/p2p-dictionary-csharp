/*
 * The camera receiver merely shows any received tick count and video frame
 * from the CameraSource program.
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
using System.IO;

namespace CameraReceiver
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        com.rhfung.P2PDictionary.P2PDictionary dict;

        public MainWindow()
        {
            InitializeComponent();

            dict = new com.rhfung.P2PDictionary.P2PDictionary("Camera receiver", com.rhfung.P2PDictionary.P2PDictionary.GetFreePort( 2012), "cameratest");
            dict.AddSubscription("frame");
            dict.AddSubscription("time");
            dict.Notification += new EventHandler<com.rhfung.P2PDictionary.NotificationEventArgs>(dict_Notification);
        }

        void dict_Notification(object sender, com.rhfung.P2PDictionary.NotificationEventArgs e)
        {
            

            if (e.Key == "time")
            {
                Dispatcher.Invoke((Action)delegate()
                {
                    this.Title = ((int)dict["time"]).ToString();
                });
            }
            else if (e.Key == "frame")
            {
                MemoryStream stream = dict["frame"] as MemoryStream;
                stream.Seek(0, SeekOrigin.Begin);
                Dispatcher.Invoke((Action)delegate()
                {

                    BitmapImage result = new BitmapImage();
                    try
                    {
                        result.BeginInit();
                        // According to MSDN, "The default OnDemand cache option retains access to the stream until the image is needed."
                        // Force the bitmap to load right now so we can dispose the stream.
                        result.CacheOption = BitmapCacheOption.OnDemand;
                        result.StreamSource = stream;
                        result.EndInit();
                        result.Freeze();

                        image1.Source = result;
                        this.Background = new SolidColorBrush(Colors.Black);
                    }
                    catch
                    {
                        image1.Source = null;
                        this.Background = new SolidColorBrush(Colors.Green);
                    }

                    
                });
            }
        }
    }
}
