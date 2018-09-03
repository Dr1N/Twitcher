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
using System.Windows.Shapes;

namespace MultiWatcher
{
    /// <summary>
    /// Interaction logic for Ovelay.xaml
    /// </summary>
    public sealed partial class Overlay : Window
    {
        public string MyMessage { get; set; }

        public Overlay()
        {
            InitializeComponent();
            Loaded += Ovelay_Loaded;
        }

        private void Ovelay_Loaded(object sender, RoutedEventArgs e)
        {
            MessageTextBox.Text = String.IsNullOrEmpty(MyMessage) ? "Please Wait..." : MyMessage;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            Close();
        }
    }
}
