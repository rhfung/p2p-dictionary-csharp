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

namespace SimpleChat
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

            dictionary = new P2PDictionary("Peer Dictionary", P2PDictionary.GetFreePort(port), "SimpleChat", P2PDictionaryServerMode.AutoRegister, P2PDictionaryClientMode.AutoConnect);
            dictionary.AddSubscription("*");
            dictionary.Notification += new EventHandler<NotificationEventArgs>(dictionary_Notification);

            this.Title = "Simple Chat (" + (dictionary.LocalEndPoint as System.Net.IPEndPoint).Port + ")";
        }

        void dictionary_Notification(object sender, NotificationEventArgs e)
        {
            Dispatcher.Invoke(new Action(delegate() {
                listBox1.Items.Add(e.Sender + " " + e.Value.ToString());
            }));
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            
            if (e.Key == Key.Return || e.Key == Key.Enter)
            {
                dictionary[dictionary.LocalID + "/" + counter] = textBox1.Text;
                textBox1.Text = "";
                counter++;
            }
        }

        private void textBox1_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            dictionary.Close();
        }
    }
}
