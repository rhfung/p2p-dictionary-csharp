
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
using com.rhfung.P2PDictionary;
using com.rhfung.P2PDictionary.Peers;

namespace SharedPhotos
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public P2PDictionary dictionary;
        int counter = 0;

        public MainWindow(int port)
        {
            InitializeComponent();

            dictionary = new P2PDictionary("Peer Dictionary", 
                port,
                "shared_photos",
                P2PDictionaryServerMode.AutoRegister,
                P2PDictionaryClientMode.AutoConnect,
                peerDiscovery: new ZeroconfDiscovery());
            dictionary.AddSubscription("*");
            dictionary.Notification += new EventHandler<NotificationEventArgs>(dictionary_Notification);

            dictionary[dictionary.LocalID + "/" + counter] = ConsoleColor.Blue;
        }

        void dictionary_Notification(object sender, NotificationEventArgs e)
        {
            Dispatcher.Invoke(new Action(delegate()
            {
                listBox1.Items.Add(e.Value.ToString());
            }));
        }

        private void listBox1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            dictionary.Close();
        }

    }
}
