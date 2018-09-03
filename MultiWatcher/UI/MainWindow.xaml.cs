using Microsoft.Win32;
using MultiWatcher.Code;
using MultiWatcher.Utils;
using OeBrowser.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace MultiWatcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public sealed partial class MainWindow : Window, IDisposable
    {

        #region Fields
        private WatcherManager manager;
        private CancellationTokenSource tokenSource;
        private int previewFrameTime = 1;
        private bool isPreview;
        private ILogger textLogger;

        #endregion

        public static double DPI { get; private set; }

        #region Life

        public MainWindow()
        {
            InitializeComponent();

            textLogger = new TextLogger(WatchLog);
            App.LogWriter.AddLogger(textLogger);

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Preview.Source = new BitmapImage(new Uri(@"Images\preview.png", UriKind.Relative));

            //Create
            manager = new WatcherManager();
            WatcherList.ItemsSource = manager.Watchers;

            //Bindings
            Binding watcherCountBinding = new Binding("WatcherCount")
            {
                Source = manager
            };
            StatusWatchersCount.SetBinding(TextBlock.TextProperty, watcherCountBinding);

            Binding urlBinding = new Binding("Channel")
            {
                Source = manager,
            };
            StatusMainUrl.SetBinding(TextBlock.TextProperty, urlBinding);

            Binding userCountBinding = new Binding("UserCount")
            {
                Source = manager,
            };
            StatusUserCount.SetBinding(TextBlock.TextProperty, userCountBinding);

            //Threads
            tokenSource = new CancellationTokenSource();

            //DPI
            GetDpi();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                Dispose();
            }
            catch (Exception ex)
            {
                App.LogWriter.WriteLog($"Closing Main Window Error: {ex.Message}");
            }
        }

        #region IDisposable Support

        private bool disposedValue = false;

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    App.LogWriter.RemoveLogger(textLogger);
                    manager.Dispose();
                    tokenSource.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion

        #endregion

        #region Callbacks

        private void Manager_WatcherManagerCreatedEvent(WatcherManager sender)
        {
            Dispatcher.Invoke(() => {
                SaveButton.IsEnabled = true;
                EnablePreview.IsEnabled = true;
            });
        }

        private async void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                Filter = "Text files (*.txt)|*.txt",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Multiselect = false,
            };

            App.LogWriter.WriteLog("Open Select User File Dialog");

            if (openFileDialog.ShowDialog() == true)
            {
                string filename = openFileDialog.FileName;
                var success = await manager.ReadUsersFromFileAsync(filename);
                InitNewSessionGui(filename, success);
                EnablePreview.IsChecked = false;
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            //Validate

            if (!IsValidUrl())
            {
                MessageBox.Show("Incorrect url. Try Again", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                TargetUrl.Focus();
                TargetUrl.SelectAll();
                return;
            }

            if (StartPosition.Value > EndPosition.Value)
            {
                MessageBox.Show("Incorrect users range. Try Again", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (manager.UserCount == 0)
            {
                MessageBox.Show("No users in file. Try Again", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            //Initialize

            StartButton.IsEnabled = false;
            TargetUrl.IsReadOnly = true;
            StartPosition.IsEnabled = false;
            EndPosition.IsEnabled = false;

            manager.FirstWatcher = (StartPosition.Value != null) ? (int)(StartPosition.Value - 1) : 0;
            manager.LastWatcher = EndPosition.Value ?? manager.UserCount;
            manager.Channel = TargetUrl.Text.Trim();

            if (CaptchaCheckBox.IsChecked == true)
            {
                ReCaptchaSettings settings = new ReCaptchaSettings
                {
                    ApiKey = RuCaptchaApiKey.Text.Trim(),
                    FirstDelay = FirstCaptchaWait.Value.HasValue ? (int)FirstCaptchaWait.Value : -1,
                    SecondDelay = FirstCaptchaWait.Value.HasValue ? (int)SecongCaptchaWait.Value : -1,
                    Attempts = CaptchaRequest.Value.HasValue ? (int)CaptchaRequest.Value : -1
                };
                manager.SetCaptchaSolver(settings);
            }

            //Start

            Overlay overLay = new Overlay() { Owner = this, MyMessage = "Starting..."};
            bool isOverlayClosed = false;
            manager.WatcherManagerCreatedEvent += (s, arg) => {
                overLay.Dispatcher.Invoke(() => overLay.Close());
                isOverlayClosed = true;
                EnablePreview.Dispatcher.Invoke(() => EnablePreview.IsEnabled = true);
                SaveButton.Dispatcher.Invoke(() => SaveButton.IsEnabled = true);
                mainTabControl.Dispatcher.Invoke(() => OptionsTabItem.IsEnabled = false);
            };
            manager.StartAsync();
            if (!isOverlayClosed) overLay.ShowDialog();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog()
            {
                Filter = "Twitch Watcher Session(*.mws)|*.mws",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                AddExtension = true,
                DefaultExt = "mws",
            };

            App.LogWriter.WriteLog("Open Save File Dialog");

            if (dialog.ShowDialog() == true)
            {
                Overlay overLay = new Overlay() { Owner = this, MyMessage = "Save Session..." };
                bool isOverlayClosed = false;
                manager.WatcherManagerFileOperationEvent += (s, a) => {
                    overLay.Dispatcher.Invoke(() => overLay.Close());
                    isOverlayClosed = true;
                    if (a.Success == false)
                    {
                        string msg = !String.IsNullOrEmpty(a.ErrorMessage) ? a.ErrorMessage : "Save File Error";
                        MessageBox.Show(msg, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                };
                manager.SaveFileAsync(dialog.FileName);
                if (!isOverlayClosed) overLay.ShowDialog();
            }
        }

        private void LoadFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog()
            {
                Filter = "Twitch Watcher Session(*.mws)|*.mws",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Multiselect = false,
            };

            App.LogWriter.WriteLog("Open Load File Dialog");

            if (dialog.ShowDialog() == true)
            {
                Overlay overLay = new Overlay() { Owner = this, MyMessage = "Open Session..." };
                bool isOverlayClosed = false;
                manager.WatcherManagerFileOperationEvent += (s, a) => {
                    overLay.Dispatcher.Invoke(() => overLay.Close());
                    isOverlayClosed = true;
                    if (a.Success == false)
                    {
                        string msg = !String.IsNullOrEmpty(a.ErrorMessage) ? a.ErrorMessage : "Load File Error";
                        MessageBox.Show(msg, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else
                    {
                        InitLoadSessionGui(dialog.FileName, true);
                    }
                };
                manager.OpenFileAsync(dialog.FileName);
                if (!isOverlayClosed) overLay.ShowDialog();
            }
        }

        private void EnablePreview_Checked(object sender, RoutedEventArgs e)
        {
            if (isPreview == true) return;
            
            StartPreview();
        }

        private void EnablePreview_Unchecked(object sender, RoutedEventArgs e)
        {
            if (isPreview == true && tokenSource?.IsCancellationRequested == false)
            {
                tokenSource.Cancel();
            }
        }

        private void RadioStretch_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radio && Preview != null)
            {
                if (radio.Name == "RadioFill")
                {
                    Preview.Stretch = System.Windows.Media.Stretch.Fill;
                }
                else if (radio.Name == "RadioNone")
                {
                    Preview.Stretch = System.Windows.Media.Stretch.None;
                }
                else if (radio.Name == "RadioUniform")
                {
                    Preview.Stretch = System.Windows.Media.Stretch.Uniform;
                }
                else if (radio.Name == "RadioUniformToFill")
                {
                    Preview.Stretch = System.Windows.Media.Stretch.UniformToFill;
                }

                App.LogWriter.WriteLog($"Change Preview Stretch Mode: {Preview.Stretch}");
            }
        }

        private void WatcherList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Watcher watcher = WatcherList.SelectedItem as Watcher;

            if (watcher == null) return;

            BrowserWindow browserWindow = new BrowserWindow(watcher);
            browserWindow.Closed += BrowserWindow_Closed;
            App.LogWriter.WriteLog($"Browser Show For {watcher.ShortID}");
            browserWindow.ShowDialog();
        }

        private void BrowserWindow_Closed(object sender, EventArgs e)
        {
            App.LogWriter.WriteLog("Browser Closed");
            if (sender is BrowserWindow browserWindow)
            {
                browserWindow.Closed -= BrowserWindow_Closed;
                string id = browserWindow.Watcher.ID;
                Watcher watcher = manager[id];
                string selectedUrl = browserWindow.CurrentUrl.Replace("http://", "").Replace("https://", "");
                string currentUrl = watcher.CurrentUrl.Replace("http://", "").Replace("https://", "");
                if (watcher != null && String.Compare(currentUrl, selectedUrl) != 0)
                {
                    App.LogWriter.WriteLog($"Change watcher({watcher.ShortID}) URL to {browserWindow.CurrentUrl}");
                    watcher.UrlAsync = browserWindow.CurrentUrl;
                }
            }
        }

        #region Context Menu

        private void MenuUrl_Click(object sender, RoutedEventArgs e)
        {
            Watcher watcher = WatcherList.SelectedItem as Watcher;

            if (watcher == null) return;

            App.LogWriter.WriteLog("Show Url Window");

            UrlSelectWindow urlWindow = new UrlSelectWindow()
            {
                Owner = this,
                Title = watcher.CurrentUrl,
            };

            if (urlWindow.ShowDialog() == true)
            {
                App.LogWriter.WriteLog($"Change watcher ({watcher.ShortID}) url to ({urlWindow.Title})");
                watcher.UrlAsync = urlWindow.Title;
            }
        }

        private void MenuRemove_Click(object sender, RoutedEventArgs e)
        {
            Watcher watcher = WatcherList.SelectedItem as Watcher;

            if (watcher == null) return;

            manager.Watchers.Remove(watcher);
            watcher.Dispose();
        }

        private void MenuAllUrl_Click(object sender, RoutedEventArgs e)
        {
            if (manager.WatcherCount == 0) return;

            App.LogWriter.WriteLog("Show Url Window");

            UrlSelectWindow urlWindow = new UrlSelectWindow()
            {
                Owner = this,
                Title = String.Empty,
            };

            if (urlWindow.ShowDialog() == true)
            {
                App.LogWriter.WriteLog($"Change all watcher url to ({urlWindow.Title})");
                manager.Url = urlWindow.Title;
            }
        }

        private void Authorization_Click(object sender, RoutedEventArgs e)
        {
            Watcher watcher = WatcherList.SelectedItem as Watcher;

            if (watcher == null || watcher.IsAuthorized == true) return;

            App.LogWriter.WriteLog($"Authorization From Context Menu (watcher: {watcher.ShortID})");

            watcher.TwitchAuthorizationAsync();
        }

        #endregion

        #endregion

        #region Private Methods
        
        private void InitNewSessionGui(string filename, bool success)
        {
            //File Panel
            FileBorder.Visibility = Visibility.Visible;
            SelectedFile.Text = "File: " + filename;
            SelectedFile.ToolTip = filename;

            //Positions
            if (success)
            {
                NewSessionPanel.Visibility = Visibility.Visible;
                StartPosition.Minimum = 1;
                StartPosition.Maximum = manager.UserCount;
                StartPosition.Value = 1;

                EndPosition.Minimum = 1;
                EndPosition.Maximum = manager.UserCount;
                EndPosition.Value = manager.UserCount;

                OptionsTabItem.IsEnabled = true;
            }

            //Status
            StatusUsers.Visibility = Visibility.Visible;
            StatusWatchers.Visibility = Visibility.Visible;
            StatusTarget.Visibility = Visibility.Visible;

            StartButton.IsEnabled = success;
        }

        private void InitLoadSessionGui(string filename, bool success)
        {
            Dispatcher.Invoke(() => {
                FileBorder.Visibility = Visibility.Visible;
                SelectedFile.Text = "Session: " + filename;
                SelectedFile.ToolTip = filename;

                NewSessionPanel.Visibility = Visibility.Collapsed;

                StatusWatchers.Visibility = Visibility.Visible;
                StatusUsers.Visibility = Visibility.Collapsed;
                StatusTarget.Visibility = Visibility.Collapsed;

                EnablePreview.IsEnabled = true;
                SaveButton.IsEnabled = true;
                OptionsTabItem.IsEnabled = false;
            });
        }

        private bool IsValidUrl()
        {
            string url = TargetUrl.Text.Trim();
            if (!String.IsNullOrEmpty(url))
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                {
                    if (uri.AbsoluteUri.StartsWith("http://") || uri.AbsoluteUri.StartsWith("https://"))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private Task StartPreview()
        {
            App.LogWriter.WriteLog("Start Preview");
            CancellationToken token = tokenSource.Token;
            isPreview = true;
            return Task.Run(async () => {
                while (token.IsCancellationRequested == false)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(previewFrameTime));

                        Watcher watcher = null;
                        WatcherList.Dispatcher.Invoke(() => { watcher = WatcherList.SelectedItem as Watcher; });

                        if (watcher == null) continue;

                        BitmapSource bi = watcher.WebBitmapImage;
                        if (bi != null)
                        {
                            bi.Freeze();
                            Preview.Dispatcher.Invoke(() => { Preview.Source = bi; });
                            bi = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        App.LogWriter.WriteLog($"Preview Error: {ex.Message}");
                        continue;
                    }
                }
                isPreview = false;
                tokenSource = new CancellationTokenSource();
                RadioUniform.Dispatcher.Invoke(() => { RadioUniform.IsChecked = true; });
                Preview.Dispatcher.Invoke(() => { Preview.Source = null; });
                App.LogWriter.WriteLog("End Preview");
            }, token);
        }

        private double GetDpi()
        {
            PresentationSource source = PresentationSource.FromVisual(this);

            double dpiX = 0, dpiY = 0;
            if (source != null)
            {
                dpiX = 96.0 * source.CompositionTarget.TransformToDevice.M11;
                dpiY = 96.0 * source.CompositionTarget.TransformToDevice.M22;
            }

            DPI = dpiX;

            return dpiX;
        }

        #endregion
    }
}
