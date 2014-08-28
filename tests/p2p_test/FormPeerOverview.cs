using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace TestP2PDict
{
    public partial class FormPeerOverview : Form
    {
        public FormPeerOverview()
        {
            InitializeComponent();
        }

        com.rhfung.P2PDictionary.P2PDictionary server;

        public void SetServer(com.rhfung.P2PDictionary.P2PDictionary server)
        {
            this.server = server;

            this.Text = "P2P Client " + server.LocalID + " at" + server.LocalEndPoint.ToString();

            server.Notification += new EventHandler<com.rhfung.P2PDictionary.NotificationEventArgs>(server_Notification);
            server.SubscriptionChanged += new EventHandler<com.rhfung.P2PDictionary.SubscriptionEventArgs>(server_SubscriptionChanged);
        }

        void server_SubscriptionChanged(object sender, com.rhfung.P2PDictionary.SubscriptionEventArgs e)
        {
            if (e.Reason == com.rhfung.P2PDictionary.SubscriptionEventReason.Change)
            {
                AppendText(txtMessages, "Changed subscription to " + e.SubscripitonPattern);
            }
            else if (e.Reason == com.rhfung.P2PDictionary.SubscriptionEventReason.Remove)
            {
                AppendText(txtMessages, "Stopped subscribing to " + e.SubscripitonPattern);
            }
            else
            {
                AppendText(txtMessages, "Subscribed to " + e.SubscripitonPattern);
            }
        }

        void server_Notification(object sender, com.rhfung.P2PDictionary.NotificationEventArgs e)
        {
            if (e.Reason == com.rhfung.P2PDictionary.NotificationReason.Add)
            {
                AppendText(txtData, "Added " + e.Key + " by " + e.Sender);
            }
            else if (e.Reason == com.rhfung.P2PDictionary.NotificationReason.Change)
            {
                AppendText(txtData, "Changed " + e.Key + " by " + e.Sender);
            }
            else
            {
                AppendText(txtData, "Removed " + e.Key + " by " + e.Sender);
            }
        }

        public void AppendText(TextBox text, string append)
        {
            if (this.IsHandleCreated)
            {
                this.Invoke(new MethodInvoker(delegate
                {
                    text.Text = text.Text + "\r\n" + append;
                    text.SelectionStart = text.Text.Length;
                }));
            }
        }
    }
}
