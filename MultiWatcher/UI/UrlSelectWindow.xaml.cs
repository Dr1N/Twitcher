using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MultiWatcher
{
    /// <summary>
    /// Interaction logic for UrlSelectWindow.xaml
    /// </summary>
    public sealed partial class UrlSelectWindow : Window
    {
        public UrlSelectWindow()
        {
            InitializeComponent();
            Closed += UrlSelectWindow_Closed;
            Closing += UrlSelectWindow_Closing;
        }

        private void UrlSelectWindow_Closed(object sender, EventArgs e)
        {
            Title = UrlText.Text.Trim();
        }

        private void UrlSelectWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DialogResult == true && IsValidUrl() == false)
            {
                e.Cancel = true;
                UrlText.BorderBrush = Brushes.Red;
            }
        }

        private void UrlText_TextChanged(object sender, TextChangedEventArgs e)
        {
            UrlText.ClearValue(BorderBrushProperty);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private bool IsValidUrl()
        {            
            if (Uri.TryCreate(UrlText.Text.Trim(), UriKind.Absolute, out Uri result))
            {
                if (!result.AbsoluteUri.StartsWith("http://") || !result.AbsoluteUri.StartsWith("https://"))
                {
                    return true;
                }
            }
           
            return false;
        }
    }
}
