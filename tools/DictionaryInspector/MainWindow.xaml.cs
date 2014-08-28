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
using System.Windows.Threading;
using com.rhfung.P2PDictionary;

namespace DictionaryInspector
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        P2PDictionary mDictionary;
        P2PNetworkMetadata mMetadata;
        DispatcherTimer mTimer;

        public MainWindow(string ns, int portHint)
        {
            InitializeComponent();

            mDictionary = new P2PDictionary("Dictionary Inspector", P2PDictionary.GetFreePort(portHint),
                ns, P2PDictionaryServerMode.AutoRegister, P2PDictionaryClientMode.AutoConnect);
            mDictionary.DebugBuffer = System.Console.Out;
            mDictionary.AddSubscription("*");

            mMetadata = new P2PNetworkMetadata(mDictionary);

            mTimer = new DispatcherTimer();
            mTimer.Interval = new TimeSpan(0, 0, 0, 1);
            mTimer.Tick += new EventHandler(mTimer_Tick);
            mTimer.Start();

            this.Title = "Dictionary Inspector (ns: " + ns + ") port " + ((System.Net.IPEndPoint)mDictionary.LocalEndPoint).Port;

            dataGrid.ItemsSource = GetDictionary(mDictionary);

            
            
        }

        List< Tuple<string, string, string>> GetDictionary(P2PDictionary dict)
        {
            List<Tuple<string, string, string>> l = new List<Tuple<string, string, string>>();
            foreach (string key in dict.Keys)
            {
                if (dict[key] != null)
                {
                    l.Add(new Tuple<string, string, string>(key, dict[key].ToString(), dict[key].GetType().ToString()));
                }
                else

                {
                    l.Add(new Tuple<string, string, string>(key, "null", "null"));
                }
            }
            return l;
        }

        void mTimer_Tick(object sender, EventArgs e)
        {
            // regrab all keys from the dictionary
            dataGrid.ItemsSource = GetDictionary(mDictionary);

            lblStatus.Content = mMetadata.RemotePeersCount + " peers connected";
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            mTimer.Stop();
        }

        bool toggle = true;

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            if (!toggle)
            {
                gridRow2.Height = new GridLength(0);
            }
            else
            {
                gridRow2.Height = new GridLength(1, GridUnitType.Star);
                PopulateList();
            }
            toggle = !toggle;
        }

        void PopulateList()
        {
            List<System.Net.IPEndPoint> endPoints = mMetadata.GetRemotePeerEndpoints();
            StringBuilder builder = new StringBuilder();

            foreach (System.Net.IPEndPoint ep in endPoints)
            {
                builder.AppendLine(ep.Address.ToString() + ":" + ep.Port);
            }

            txtNetwork.Text = builder.ToString();
        }
    }
}
