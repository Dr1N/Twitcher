using MultiWatcher.Interfaces;
using MultiWatcher.Utils;
using OeBrowser.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;

namespace MultiWatcher.Code
{
    public delegate void WatcherManagerCreated(object sender, EventArgs e);
    public delegate void WatcherManagerFileOperation(object sender, WatcherManagerFileOperationEventArgs e);

    public class WatcherManager : IDisposable, INotifyPropertyChanged, IEnumerable<Watcher>
    {
        #region Constants

        private readonly string tempDirectory = "mwtemp";
        private readonly string sessionFileName = "session.xml";

        #endregion

        #region Fields

        private ObservableCollection<Watcher> watchers = new ObservableCollection<Watcher>();
        private string url;
        private int watchersCount;
        private int usersCount;
        private List<string[]> users = new List<string[]>();
        private bool isWatchersCreated;
        private string channelUrl;
        #endregion

        #region Events

        public event PropertyChangedEventHandler PropertyChanged;
        public event WatcherManagerCreated WatcherManagerCreatedEvent;
        public event WatcherManagerFileOperation WatcherManagerFileOperationEvent;

        #endregion

        #region Properties

        public ISolver CaptchaSolver { get; private set; }

        public ObservableCollection<Watcher> Watchers
        {
            get
            {
                return watchers;
            }
        }

        public Watcher this[string id]
        {
            get
            {
                return watchers.Where(w => w.ID == id).FirstOrDefault();
            }
        }

        public int WatcherCount
        {
            get
            {
                return watchers.Count;
            }
            set
            {
                watchersCount = value;
                OnPropertyChanged("WatcherCount");
            }
        }

        public int UserCount
        {
            get
            {
                return usersCount;
            }
            set
            {
                usersCount = value;
                OnPropertyChanged("UserCount");
            }
        }

        public string Url
        {
            get
            {
                return !String.IsNullOrEmpty(url) ? url : "N/A";
            }
            set
            {
                if (Uri.TryCreate(value, UriKind.Absolute, out Uri uri))
                {
                    url = uri.AbsoluteUri;
                    foreach (Watcher watcher in watchers)
                    {
                        watcher.UrlAsync = url;
                    }
                    OnPropertyChanged("Url");
                }
            }
        }

        public string Channel
        {
            get
            {
                return channelUrl;
            }
            set
            {
                channelUrl = value;
                OnPropertyChanged("Channel");
            }
        }

        public int FirstWatcher { get; set; }

        public int LastWatcher { get; set; }

        #endregion

        #region Life

        public WatcherManager()
        {
            FirstWatcher = 0;
            LastWatcher = 0;
        }

        #region IDisposable Support

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Clear();
                    watchers = null;
                    CaptchaSolver?.Dispose();
                    CaptchaSolver = null;
                    WriteLog(" ***** Destroyed ***** ");
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

        #region Implements

        public IEnumerator<Watcher> GetEnumerator()
        {
            return watchers.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return watchers.GetEnumerator();
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion

        #region Public

        public void Start()
        {
            WriteLog("Start New Watching");

            Clear();

            if (!isWatchersCreated)
            {
                CreateWatchersFromUserList();
            }

            foreach (Watcher watcher in watchers)
            {
                watcher.TargetUrl = Channel;
            }

            WatcherManagerCreatedEvent?.Invoke(this, new EventArgs());

            foreach (Watcher watcher in watchers)
            {
                watcher.TwitchAuthorizationAsync();
            }
        }

        public Task StartAsync()
        {
            return Task.Run(() => Start());
        }

        public bool ReadUsersFromFile(string fileName)
        {
            try
            {
                WriteLog($"Read users from file ({fileName})");
                users.Clear();
                var usersData = File.ReadAllLines(fileName);
                foreach (var item in usersData)
                {
                    string[] data = item.Split(':');
                    if (data.Length == 2)
                    {
                        users.Add(data);
                    }
                }
                UserCount = users.Count;

                return true;
            }
            catch (Exception ex)
            {
                WriteLog($"Read File Error: {ex.Message}");
                return false;
            }
        }

        public void SetCaptchaSolver(ReCaptchaSettings settings)
        {
            if (settings != null)
            {
                CaptchaSolver = CreateReCaptchaSolver(settings);
            }
        }

        public Task<bool> ReadUsersFromFileAsync(string filename)
        {
            return Task.Run(() => { return ReadUsersFromFile(filename); });
        }

        public void SaveFile(string filename)
        {
            bool success = false;
            string errorMessage = "";
            try
            {
                if (File.Exists(filename)) File.Delete(filename);

                WriteLog($"Save File To: {filename}");

                string systemTempDirectory = Path.GetTempPath();
                DirectoryInfo saveDirectory = new DirectoryInfo(Path.Combine(systemTempDirectory, tempDirectory));

                if (saveDirectory.Exists) saveDirectory.Delete(true);

                WriteLog($"Temp Directory: {saveDirectory.FullName}");
                               
                foreach (var watcher in Watchers)
                {
                    string watcherCache = Path.Combine(systemTempDirectory, watcher.WebView.Engine.CachePath);
                    FileHelper.DirectoryCopy(watcherCache, Path.Combine(saveDirectory.FullName, watcher.WebView.Engine.CachePath));
                }

                WriteLog($"Cache Copied");

                SaveSession(saveDirectory.FullName);

                ZipFile.CreateFromDirectory(saveDirectory.FullName, filename, CompressionLevel.Optimal, false);

                WriteLog($"File Created");

                if (saveDirectory.Exists) saveDirectory.Delete(true);

                WriteLog($"Temp Directory Removed. Save Done!");

                success = true;
            }
            catch (Exception ex)
            {
                WriteLog($"Save Error: {ex.Message}");
                success = false;
                errorMessage = ex.Message;
            }
            finally
            {
                WatcherManagerFileOperationEvent?.Invoke(this, new WatcherManagerFileOperationEventArgs(FileOperation.SAVE, success, errorMessage));
            }
        }

        public Task SaveFileAsync(string filename)
        {
            return Task.Run(() => SaveFile(filename));
        }

        public void OpenFile(string filename)
        {
            bool success = false;
            string errorMessage = "";
            try
            {
                WriteLog($"Open File - {filename}");

                Clear();

                if (!File.Exists(filename))
                {
                    string message = $"File {filename} Not Exists";
                    WriteLog(message);
                    errorMessage = message;
                    success = false;
                    return;
                }

                string openDirectory = Path.Combine(Path.GetTempPath(), tempDirectory);

                if (Directory.Exists(openDirectory)) Directory.Delete(openDirectory, true);

                ZipFile.ExtractToDirectory(filename, openDirectory);

                XmlDocument session = new XmlDocument();
                session.Load(Path.Combine(openDirectory, sessionFileName));
                XmlElement root = session.DocumentElement;
                foreach (XmlNode watcher in root.ChildNodes)
                {
                    try
                    {
                        string id = GetAttributeValue<string>(watcher, "id");
                        string url = GetAttributeValue<string>(watcher, "url");
                        bool logged = GetAttributeValue<bool>(watcher, "logged");
                        string login = GetAttributeValue<string>(watcher, "login");
                        string password = GetAttributeValue<string>(watcher, "password");

                        var src = Path.Combine(Path.GetTempPath(), tempDirectory, id);
                        if (Directory.Exists(src))
                        {
                            string dst = Path.Combine(Path.GetTempPath(), id);
                            if (Directory.Exists(dst)) Directory.Delete(dst, true);

                            FileHelper.DirectoryCopy(src, dst);

                            Watcher w = new Watcher(id, url, logged, login, password);
                            
                            Application.Current.Dispatcher.Invoke(() => watchers.Add(w));
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteLog($"Create Watcher Error: {ex.Message}");
                        continue;
                    }
                }
                Directory.Delete(openDirectory, true);

                WriteLog($"File Loaded {filename}");
                success = true;
                WatcherCount = watchers.Count;
            }
            catch (Exception ex)
            {
                string message = $"Open file error {ex.Message}";
                WriteLog(message);
                success = false;
                errorMessage = message;
            }
            finally
            {
                WatcherManagerFileOperationEvent?.Invoke(this, new WatcherManagerFileOperationEventArgs(FileOperation.LOAD, success, errorMessage));
            }
        }

        public Task OpenFileAsync(string filename)
        {
            return Task.Run(() => OpenFile(filename));
        }

        #endregion

        #region Private

        private void SaveSession(string savePath)
        {
            WriteLog($"Begin Save Session to: {savePath}");
            XmlDocument session = new XmlDocument();
            XmlDeclaration declaration = session.CreateXmlDeclaration("1.0", "UTF-8", null);
            XmlElement root = session.CreateElement("watchers");
            session.AppendChild(root);
            session.InsertBefore(declaration, root);

            foreach (var watcher in Watchers)
            {
                XmlNode wtch = session.CreateElement("watcher");

                XmlAttribute attrId = session.CreateAttribute("id");
                attrId.Value = watcher.ID;
                XmlAttribute attrUrl = session.CreateAttribute("url");
                attrUrl.Value = watcher.CurrentUrl;
                XmlAttribute attrLogged = session.CreateAttribute("logged");
                attrLogged.Value = watcher.IsAuthorized.ToString();
                XmlAttribute attrLogin = session.CreateAttribute("login");
                attrLogin.Value = watcher.Login;
                XmlAttribute attrPassword = session.CreateAttribute("password");
                attrPassword.Value = watcher.Password;

                wtch.Attributes.Append(attrId);
                wtch.Attributes.Append(attrUrl);
                wtch.Attributes.Append(attrLogged);
                wtch.Attributes.Append(attrLogin);
                wtch.Attributes.Append(attrPassword);
                root.AppendChild(wtch);
            }

            string path = Path.Combine(savePath, sessionFileName);
            session.Save(path);
            WriteLog($"End Save Session. Result: {path}");
        }

        private void CreateWatchersFromUserList()
        {
            WriteLog($"Creating Watchers: [from {FirstWatcher} to {LastWatcher} user]");

            for (int i = FirstWatcher; i < LastWatcher; i++)
            {
                try
                {
                    Watcher watcher = new Watcher(users[i][0].Trim(), users[i][1].Trim());
                    if (CaptchaSolver != null)
                    {
                        watcher.Captcha = true;
                        watcher.SetCaptchaSolver(CaptchaSolver);
                    }
                    Application.Current.Dispatcher.Invoke(() => { watchers.Add(watcher); });
                }
                catch (Exception ex)
                {
                    WriteLog("Create Watcher error: " + ex.Message);
                    continue;
                }
            }
            WatcherCount = watchers.Count;
            isWatchersCreated = true;

            WriteLog("Watchers Created");
        }

        private ISolver CreateReCaptchaSolver(ReCaptchaSettings settings)
        {
            try
            {
                if (settings.IsValid() == false) return null;
                ISolver solver = new ReCaptchaV2(settings);

                return solver;
            }
            catch (Exception ex)
            {
                App.LogWriter.WriteLog($"Can't Create Captcha Solver {ex.Message}");
                return null;
            }
        }

        private T GetAttributeValue<T>(XmlNode node, string name)
        {
            T result = default(T);
            foreach (XmlAttribute attribute in node.Attributes)
            {
                if (String.Compare(attribute.Name, name, true) == 0)
                {
                    result = (T)Convert.ChangeType(attribute.Value, typeof(T));
                }
            }

            return result;
        }

        private void Clear()
        {
            if (watchers == null || watchers.Count == 0) return;
            
            WriteLog($"Clear Watchers");

            foreach (var watcher in watchers)
            {
                watcher.Dispose();
            }
            Application.Current.Dispatcher.Invoke(() => watchers.Clear());
            isWatchersCreated = false;
        }

        private void WriteLog(string message)
        {
            App.LogWriter.WriteLog($"[MANAGER] - {message}");
        }

        #endregion
    }
}
