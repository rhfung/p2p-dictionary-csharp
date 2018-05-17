using System.Windows;

namespace DictionaryInspector
{
    /// <summary>
    /// Interaction logic for ConnectWindow.xaml
    /// </summary>
    public partial class ConnectWindow : Window
    {
        public ConnectWindow()
        {
            InitializeComponent();

            LoadList();
        }

        void LoadList()
        {
            if (DictionaryInspector.Properties.Settings.Default.selectedNamespaces != null)
            {
                foreach (string ns in DictionaryInspector.Properties.Settings.Default.selectedNamespaces)
                {
                    cboNamespace.Items.Add(ns);
                }
            }
            // select mru
            if (cboNamespace.Items.Count > 0)
            {
                cboNamespace.SelectedIndex = 0;
            }
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            if ("".Equals(cboNamespace.Text))
            {
                MessageBox.Show("Enter a namespace first");
                return;
            }

            // save namespaces
            try
            {
                System.Collections.Specialized.StringCollection strColl = new System.Collections.Specialized.StringCollection();
                
                foreach (string s in cboNamespace.Items.SourceCollection )
                {
                    if (s.Length > 0)
                    {
                        strColl.Add(s);
                    }
                }
                if (!strColl.Contains(cboNamespace.Text))
                {
                    strColl.Insert(0,cboNamespace.Text);
                }
                DictionaryInspector.Properties.Settings.Default.selectedNamespaces = strColl;
                DictionaryInspector.Properties.Settings.Default.Save();
            }
            catch
            {
            }

            // open window
            MainWindow window = new MainWindow(cboNamespace.Text, int.Parse(txtPort.Text));
            window.Show();
            this.Close();
        }

        private void clearSelected_Click(object sender, RoutedEventArgs e)
        {
            if (cboNamespace.SelectedIndex != -1)
            {
                cboNamespace.Items.Remove(cboNamespace.SelectedItem);
            }
        }

        private void resetList_Click(object sender, RoutedEventArgs e)
        {
            cboNamespace.Items.Clear();
        }
    }
}
