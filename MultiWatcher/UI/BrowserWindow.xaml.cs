using EO.WebBrowser;
using EO.WebEngine;
using MultiWatcher.Code;
using System;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;

namespace MultiWatcher
{
    /// <summary>
    /// Interaction logic for BrowserWindow.xaml
    /// </summary>
    public sealed partial class BrowserWindow : Window
    {
        private Watcher watcher;
        private WebView webView;

        public Watcher Watcher => watcher;
        public string CurrentUrl { get; set; }

        public BrowserWindow(Watcher w)
        {
            InitializeComponent();
            watcher = w;
            Loaded += BrowserWindow_Loaded;
            Closed += BrowserWindow_Closed;
        }

        private void BrowserWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Engine engine = watcher.WebView.Engine;
            string url = watcher.CurrentUrl;
            Url.Text = url;
            webView = new WebView()
            {
                Engine = engine,
            };
            WebControl.WebView = webView;
            webView.LoadCompleted += WebView_LoadCompleted;
            webView.LoadFailed += WebView_LoadFailed;
            webView.LoadUrl(url);
        }

        private void BrowserWindow_Closed(object sender, EventArgs e)
        {
            CurrentUrl = webView.Url;
            webView.LoadCompleted -= WebView_LoadCompleted;
            webView.LoadFailed -= WebView_LoadFailed;
            WebControl.WebView = null;
            webView = null;
        }

        private void WebView_LoadCompleted(object sender, LoadCompletedEventArgs e)
        {
            Go.IsEnabled = true;
        }

        private void WebView_LoadFailed(object sender, LoadFailedEventArgs e)
        {
            Go.IsEnabled = true;
        }

        private void Go_Click(object sender, RoutedEventArgs e)
        {
            Url.ClearValue(BorderBrushProperty);
            if (Uri.TryCreate(Url.Text.Trim(), UriKind.Absolute, out Uri uri))
            {
                string absUrl = uri.AbsoluteUri;
                if (absUrl.StartsWith("http://") || absUrl.StartsWith("https://"))
                {
                    Go.IsEnabled = false;
                    webView.LoadUrl(absUrl);
                }
            }
            else
            {
                Url.BorderBrush = Brushes.Red;
            }
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            webView.LoadUrl(webView.Url);
        }

        private void DevTools_Click(object sender, RoutedEventArgs e)
        {
            Window bw = new DevWindow();
            bw.Show();
            IntPtr windowHandle = new WindowInteropHelper(bw).Handle;
            webView.ShowDevTools(windowHandle);
        }

        private void Test_Click(object sender, RoutedEventArgs e)
        {
        }
    }
}
