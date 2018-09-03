using EO.WebBrowser;
using EO.WebEngine;
using MultiWatcher.Interfaces;
using MultiWatcher.Utils;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Imaging;

namespace MultiWatcher.Code
{
    public delegate void WatcherStateChanged(object sender, WatcherStateChangedEventArgs e);

    public enum WatcherState
    {
        CREATED,
        LOADING,
        LOADED,
        AUTHORIZING,
        AUTHORIZED,
        LOGINFORM,
        LOGINFORMFILLED,
        SINGNING,
        AUTHORIZINGFAILED,
        WAITING,
        FAILED,
        WATCHING,
        CAPTCHA,
        CAPTCHAERROR,
    }

    public class Watcher : IDisposable, INotifyPropertyChanged
    {
        #region Constants

        private static readonly string TwitchUrl = "https://www.twitch.tv/";
        private static Size defaultSize = new Size(1920, 1080);
        private readonly string userAgent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/62.0.3202.94 Safari/537.36";
        private readonly string loginButtonSelector = "button[data-a-target=login-button]";
        private readonly string loginDivSelector = "div[data-a-target=passport-modal]";
        private readonly string userNameSelector = "div[data-a-target=user-display-name]";
        private readonly string matureSelector = "button#mature-link";
        private readonly string loginInputSelector = "input[autocomplete=username]";
        private readonly string passwordInputSelector = "input[autocomplete=current-password]";
        private readonly string submitInputSelector = "button[data-a-target=passport-login-button]";
        private readonly string signinError = "div.subwindow_notice";
        private readonly string continueButtonSelector = @"body > div.ReactModalPortal > div > div > div > div.tw-c-background.tw-flex.tw-flex-column.tw-pd-x-2.tw-pd-y-3 > div.tw-mg-t-2 > div > button";

        private readonly string reCaptchaFrameSelector = "#recaptcha-element-container > div > div > iframe";
        private readonly string reCaptchaResponse = "textarea#g-recaptcha-response";

        /*
        private readonly string bigPlaySelector = "button.player-button-play";
        private readonly string playSelector = "button.qa-pause-play-button";
        private readonly string volumeControl = "button.qa-control-volume";
        private readonly string muteSpan = "span.mute-button";
        */

        #endregion

        #region Events

        public event PropertyChangedEventHandler PropertyChanged;
        public event WatcherStateChanged StateChanged;

        #endregion

        #region Fields
        
        private ThreadRunner threadRunner;
        private Engine engine;
        private WebView webView;
        private WatcherState state;
        private string id;
        private string urlAsync;
        private string targetUrl;
        private string currentUrl;
        private string login;
        private string password;
        private bool solveCaptcha = true;
        private bool authorized = false;
        private WeakReference<ISolver> cSolver;
        private SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

        private int frameX;
        private int frameY;
        private int loginX;
        private int loginY;
        private int passwordX;
        private int passwordY;
        private int buttonX;
        private int buttonY;
        private bool initialized;

        #endregion

        #region Properties

        public ISolver CaptchaSolver
        {
            get
            {
                ISolver solver;
                if (cSolver.TryGetTarget(out solver))
                {
                    return solver;
                }
                return null;
            }
        }

        public WatcherState State
        {
            get
            {
                return state;
            }
            set
            {
                state = value;
                OnPropertyChanged("State");
                StateChanged?.Invoke(this, new WatcherStateChangedEventArgs(state));
            }
        }

        public string ShortID
        {
            get
            {
                return id.Substring(id.Length - 5);
            }
        }

        public string ID
        {
            get
            {
                return id;
            }
        }

        public string UrlAsync
        {
            get
            {
                return !String.IsNullOrEmpty(urlAsync) ? urlAsync : "N/A";
            }
            set
            {
                urlAsync = value;
                State = WatcherState.LOADING;
                threadRunner.Post(() => webView.LoadUrl(value));
                UpdateCurrentUrl(value);
                OnPropertyChanged("UrlAsync");
            }
        }

        public string CurrentUrl
        {
            get
            {
                return !String.IsNullOrEmpty(currentUrl) ? currentUrl : "N/A";
            }
            set
            {
                currentUrl = value;
                State = WatcherState.LOADING;
                threadRunner.Send(() => webView.LoadUrlAndWait(value));
                OnPropertyChanged("CurrentUrl");
            }
        }

        public string TargetUrl
        {
            get
            {
                return targetUrl;
            }
            set
            {
                targetUrl = value;
                OnPropertyChanged("TargetUrl");
            }
        }

        public ThreadRunner ThreadRunner
        {
            get
            {
                return threadRunner;
            }
        }

        public WebView WebView
        {
            get
            {
                return webView;
            }
        }

        public Image WebImage
        {
            get
            {
                return webView.Capture();
            }
        }

        public BitmapSource WebBitmapSource
        {
            get
            {
                Image image = WebImage;
                if (image != null)
                {
                    BitmapSource bs = BitmapConverter.ConvertBitmapToBitmapSource(image);

                    return bs;
                }
                
                return null;
            }
        }

        public BitmapImage WebBitmapImage
        {
            get
            {
                Image image = WebImage;
                if (image != null)
                {
                    if (initialized)
                    {
                        AddFormMarkers(image);
                    }
                    BitmapImage bi = BitmapConverter.ConvertBitmapToBitmapImage(image);

                    return bi;
                }

                return null;
            }
        }

        public string Login
        {
            get
            {
                return login;
            }
            set
            {
                login = value;
                OnPropertyChanged("Login");
            }
        }

        public string Password
        {
            get
            {
                return password;
            }
        }

        public bool Captcha
        {
            get
            {
                return solveCaptcha;
            }
            set
            {
                solveCaptcha = value;
                OnPropertyChanged("Captcha");
            }
        }

        public bool IsAuthorized
        {
            get
            {
                return authorized;
            }
            set
            {
                authorized = value;
                OnPropertyChanged("IsAuthorized");
            }
        }

        #endregion

        #region Life

        public Watcher(string login, string password)
        {
            if (String.IsNullOrEmpty(login))
            {
                throw new ArgumentNullException("login");
            }
            if (String.IsNullOrEmpty(password))
            {
                throw new ArgumentNullException("password");
            }
  
            this.login = login;
            this.password = password;
            id = Guid.NewGuid().ToString();

            Init();

            State = WatcherState.CREATED;
        }

        public Watcher(string id, string url, bool logged, string login, string password)
        {
            this.id = id;
            this.password = password;
            Login = login;
            IsAuthorized = logged;
           
            Init(url);
        }

        private void Init(string url = null)
        {
            engine = Engine.Create(id);
            engine.Options.AllowProprietaryMediaFormats();
            engine.Options.CachePath = $"{id}";

            threadRunner = new ThreadRunner(ID, engine);

            BrowserOptions options = new BrowserOptions()
            {
                AllowJavaScript = true,
                AllowPlugins = true,
                AllowJavaScriptAccessClipboard = true,
                AllowJavaScriptCloseWindow = true,
                AllowJavaScriptDOMPaste = true,
                AllowZooming = true,
            };

            webView = threadRunner.CreateWebView(defaultSize.Width, defaultSize.Height, options);
            webView.CustomUserAgent = userAgent;

            webView.NeedClientCertificate += WebView_NeedClientCertificate;
            webView.CertificateError += WebView_CertificateError;
            webView.LoadCompleted += WebView_LoadCompleted;
            webView.LoadFailed += WebView_LoadFailed;

            if (!String.IsNullOrEmpty(url))
            {
                TargetUrl = url;
                UrlAsync = url;
                UpdateCurrentUrl(url);
            }

            WriteLog($"Initialized: ID: {id} Cache: {engine.CachePath} Window Size: {defaultSize}");
        }

        #region IDisposable Support

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    webView.NeedClientCertificate -= WebView_NeedClientCertificate;
                    webView.CertificateError -= WebView_CertificateError;
                    webView.LoadCompleted -= WebView_LoadCompleted;
                    webView.LoadFailed -= WebView_LoadFailed;

                    threadRunner.Dispose();
                    threadRunner = null;

                    webView.Dispose();
                    webView = null;

                    engine.Stop(true);
                    engine = null;

                    _semaphoreSlim.Dispose();
                    _semaphoreSlim = null;

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

        #region Public

        public async void TwitchAuthorization()
        {
            try
            {
                State = WatcherState.AUTHORIZING;
                WriteLog("Twitch Autorization Start");
                CurrentUrl = TwitchUrl;
                await Authorization();
            }
            catch (Exception ex)
            {
                State = WatcherState.AUTHORIZINGFAILED;
                WriteLog($"TwitchAuthorization Error: {ex.Message}");
                IsAuthorized = false;
            }
        }

        public Task TwitchAuthorizationAsync()
        {
            return Task.Run(async () => {
                try
                {
                    State = WatcherState.AUTHORIZING;
                    WriteLog("Twitch Autorization Start");
                    CurrentUrl = TwitchUrl;
                    await Authorization();
                }
                catch (Exception ex)
                {
                    State = WatcherState.AUTHORIZINGFAILED;
                    WriteLog($"TwitchAuthorization Error: {ex.Message}");
                    IsAuthorized = false;
                }
            });
        }

        public void SetCaptchaSolver(ISolver solver)
        {
            cSolver = new WeakReference<ISolver>(solver);
        }

        #endregion

        #region Protected

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion

        #region Private

        private async Task Authorization()
        {
            bool isAuthorized = await IsSigned();
            if (isAuthorized)
            {
                State = WatcherState.AUTHORIZED;
                IsAuthorized = true;
                return;
            }

            await TwitchSignIn();
        }

        private async Task TwitchSignIn()
        {
            WriteLog("Try Autorization...");
            bool success = false;

            bool hasLoginButton = await WaitSelector(loginButtonSelector, 10);
            if (hasLoginButton == false)
            {
                State = WatcherState.AUTHORIZINGFAILED;
                WriteLog($"Not Found Login Button");
                return;
            }

            string loginClickScript = $"document.querySelector('{loginButtonSelector}').click()";
            success = EvaluateScript(loginClickScript);
            if (!success)
            {
                State = WatcherState.AUTHORIZINGFAILED;
                WriteLog($"Can't Click Login Button");
                return;
            }

            await Task.Delay(1000);

            bool isLoginFrame = await WaitSelector(loginDivSelector, 20);
            if (isLoginFrame == false)
            {
                State = WatcherState.AUTHORIZINGFAILED;
                WriteLog("Not Found Login Frame");
                return;
            }

            await Task.Delay(1000);

            await _semaphoreSlim.WaitAsync();
            try
            {
                await FillLoginEmulate();
            }
            finally
            {
                _semaphoreSlim.Release();
            }

            #region Commented

            //TODO JS Version
            /*
            success = await FillLoginForm();
            if (!success)
            {
                State = WatcherState.AUTHORIZINGFAILED;
                WriteLog("Can't Filled Login Form");
                return;
            }
            */

            #endregion

            State = WatcherState.LOGINFORMFILLED;

            await Task.Delay(500);

            success = SubmitForm();
            if (!success)
            {
                State = WatcherState.AUTHORIZINGFAILED;
                WriteLog("Can't Click On Submit");
                return;
            }

            success = await WaitSelector(continueButtonSelector);
            if (!success)
            {
                State = WatcherState.AUTHORIZINGFAILED;
                WriteLog("Not Countinue Button");
                return;
            }

            if (Captcha && CaptchaSolver != null)
            {
                State = WatcherState.CAPTCHA;
                WriteLog("Try Solve Captcha");
                success = await SolvingCaptcha(TwitchUrl);
                if (!success)
                {
                    State = WatcherState.CAPTCHAERROR;
                    WriteLog("Captcha error!");
                    return;
                }
            }

            await Task.Delay(1000);

            string removeDisabledAttr = $"document.querySelector('{continueButtonSelector}').removeAttribute('disabled')";
            success = success && EvaluateScript(removeDisabledAttr);
            string removeDisabledClass = $"document.querySelector('{continueButtonSelector}').classList.remove('tw-button--disabled')";
            success = success && EvaluateScript(removeDisabledClass);
            string focusScript = $"document.querySelector('{continueButtonSelector}').focus()";
            await Task.Delay(500);
            threadRunner.Send(() => { webView.SendKeyEvent(true, KeyCode.Enter); });

            await Task.Delay(1000);

            //bool hasError = await WaitSelector(signinError, 3);
            //if (hasError)
            //{
            //    State = WatcherState.AUTHORIZINGFAILED;
            //    WriteLog("Autorization Error (message)");
            //    return;
            //}

            CurrentUrl = TwitchUrl;

            success = await CheckAutorization();
            if (!success)
            {
                State = WatcherState.AUTHORIZINGFAILED;
                WriteLog("Autorization Error (username)");
                return;
            }

            WriteLog("Autorization Success!");
            IsAuthorized = true;

            CurrentUrl = TargetUrl;

            await FindAndClickButton(matureSelector);

            State = WatcherState.WATCHING;
        }

        private async Task<bool> FillLoginForm()
        {
            WriteLog("Fill Login Form JS");
            bool success = true;

            string loginScript = $"document.querySelector('{loginInputSelector}').value = '{login}'";
            string passwordScript = $"document.querySelector('{passwordInputSelector}').value = '{password}'";

            success = EvaluateScript(loginScript);
            await Task.Delay(1000);
            success = success && EvaluateScript(passwordScript);

            return success;
        }

        private async Task<bool> FillLoginEmulate()
        {
            WriteLog("Fill Login Form Emulate");
            bool success = true;

            if (success)
            {
                try
                {
                    //Login
                    var focusScr = $"document.querySelector('{loginInputSelector}').focus()";
                    EvaluateScript(focusScr);
                    await Task.Delay(500);
                    System.Windows.Application.Current.Dispatcher.Invoke(() => { System.Windows.Clipboard.SetText(login); });
                    await Task.Delay(1000);
                    threadRunner.Send(() => { webView.SendKeyEvent(true, KeyCode.A, EventFlags.ControlDown); });
                    await Task.Delay(1000);
                    threadRunner.Send(() => { webView.SendKeyEvent(true, KeyCode.V, EventFlags.ControlDown); });
                    await Task.Delay(1000);

                    //Password
                    focusScr = $"document.querySelector('{passwordInputSelector}').focus()";
                    EvaluateScript(focusScr);
                    await Task.Delay(500);
                    System.Windows.Application.Current.Dispatcher.Invoke(() => { System.Windows.Clipboard.SetText(password); });
                    await Task.Delay(1000);
                    threadRunner.Send(() => { webView.SendKeyEvent(true, KeyCode.V, EventFlags.ControlDown); });
                    await Task.Delay(1000);
                    System.Windows.Application.Current.Dispatcher.Invoke(() => { System.Windows.Clipboard.Clear(); });
                }
                catch (Exception ex)
                {
                    WriteLog($"FillLoginEmulate Error: {ex.Message}");
                    success = false;
                }
            }

            return success;
        }

        private bool SubmitForm()
        {
            bool success = true;
            WriteLog("Submit Form");
            string clickScript = $"document.querySelector('{submitInputSelector}').click()";
            success = success && EvaluateScript(clickScript);

            return success;
        }

        private async Task<bool> IsSigned()
        {
            if (IsAuthorized)
            {
                WriteLog("Already authorized");
                return true;
            }

            bool isUserName = await CheckAutorization();
            if (isUserName)
            {
                WriteLog($"Already authorized (found user name)");
                return true;
            }

            return false;
        }

        private async Task<bool> CheckAutorization()
        {
            if (IsAuthorized) return true;

            WriteLog("Check Autorization");

            return await WaitSelector(userNameSelector, 10);
        }

        private async Task<bool> FindAndClickButton(string selector)
        {
            bool isFound = await WaitSelector(selector, 5);
            if (isFound)
            {
                string script = $"document.querySelector('{selector}').click()";
                return EvaluateScript(script);
            }

            return false;
        }

        private bool InitLoginCoordinates()
        {
            double k = MainWindow.DPI / 96.0;
            object frameLeft = "0";
            threadRunner.Send(() => { frameLeft = webView.EvalScript($"document.querySelector('{loginDivSelector}').getBoundingClientRect().left"); });
            object frameTop = "0";
            threadRunner.Send(() => { frameTop = webView.EvalScript($"document.querySelector('{loginDivSelector}').getBoundingClientRect().top"); });

            try
            {
                frameX = (int)(Int32.Parse(frameLeft?.ToString()) * k);
                frameY = (int)(Int32.Parse(frameTop?.ToString()) * k);

                loginX = (int)(frameX + 200 * k);
                loginY = (int)(frameY + 180 * k);

                passwordX = (int)(frameX + 200 * k);
                passwordY = (int)(frameY + 250 * k);

                buttonX = (int)(frameX + 200 * k);
                buttonY = (int)(frameY + 330 * k);

                initialized = true;

                return true;
            }
            catch (ArgumentNullException)
            {
                WriteLog($"Init Login Coords - ArgumentNullException");
            }
            catch (FormatException)
            {
                WriteLog($"Init Login Coords - FormatException");
            }
            catch (OverflowException)
            {
                WriteLog($"Init Login Coords - OverflowException");
            }
            catch (Exception ex)
            {
                WriteLog($"Init Login Coords - {ex.Message}");
            }

            return false;
        }

        private void CreateFormControlImage(Image webImage)
        {
            AddFormMarkers(webImage);
            webImage.Save("control.bmp");
        }

        private void AddFormMarkers(Image webImage)
        {
            using (Graphics g = Graphics.FromImage(webImage))
            {
                g.FillEllipse(Brushes.Red, new Rectangle(loginX - 5, loginY - 5, 10, 10));
                g.FillEllipse(Brushes.Red, new Rectangle(passwordX - 5, passwordY - 5, 10, 10));
                g.FillEllipse(Brushes.Red, new Rectangle(buttonX - 5, buttonY - 5, 10, 10));
                g.FillEllipse(Brushes.Red, new Rectangle(frameX - 5, frameY - 5, 10, 10));
            }
        }

        #region JavaScript

        private async Task<bool> WaitSelector(string selector, int waitTimeSeconds = 20)
        {
            State = WatcherState.WAITING;
            int counter = 0;
            int maxIteration = waitTimeSeconds * 1000 / 500;
            bool result = false;
            do
            {
                counter++;
                result = HasElementBySelector(selector);

                await Task.Delay(500);

                if (counter >= maxIteration)
                {
                    break;
                }
            } while (result != true);

            return result;
        }

        private bool HasElementBySelector(string selector)
        {
            string scriptResult = String.Empty;
            string script = $"document.querySelector('{selector}')";

            return EvaluateScriptWithResult(script, out scriptResult);
        }

        private bool EvaluateScript(string script, string frame = null)
        {
            try
            {
                threadRunner.Send(() => webView.EvalScript(script, true));
                return true;
            }
            catch (JSException jsex)
            {
                WriteLog($"Evaluate JavaScript Error: {jsex.Message}");
            }
            catch (Exception ex)
            {
                WriteLog($"Evaluate Script Error: {ex.Message}");
            }

            return false;
        }

        private bool EvaluateScriptWithResult(string script, out string result, string frame = null)
        {
            bool success = false;
            result = String.Empty;
            try
            {
                object obj = null;
                threadRunner.Send(() => obj = webView.EvalScript(script));
                if (obj != null && !(obj is JSUndefined) && !(obj is JSNull))
                {
                    result = obj.ToString();
                    success = true;
                }
            }
            catch (JSException jsex)
            {
                WriteLog($"Evaluate JavaScript With Result Error: {jsex.Message}");
            }
            catch (Exception ex)
            {
                WriteLog($"Evaluate Script With Result Error: {ex.Message}");
            }
           
            return success;
        }

        #endregion

        #region Captcha

        private async Task<bool> SolvingCaptcha(string url)
        {
            bool result = false;

            string k = GetReCaptchaKey();

            if (String.IsNullOrEmpty(k)) return result;

            if (!ShowReCaptchaResponseField()) return result;

            ReCaptchaArgs args = new ReCaptchaArgs(k, url);
            string token = await CaptchaSolver?.GetResult(args);

            if (String.IsNullOrEmpty(token)) return result;

            result = FillReCaptchaToken(token);

            return result;
        }

        private string GetReCaptchaKey()
        {
            string result = String.Empty;
            string kName = "k=";
            string script = $"document.querySelector('{reCaptchaFrameSelector}').src";
            if (EvaluateScriptWithResult(script, out string scriptResult))
            {
                string[] srcArr = scriptResult.Split('?');
                string[] queryParams = srcArr[1].Split('&');
                foreach (var item in queryParams)
                {
                    if (item.Contains(kName))
                    {
                        result = item.Substring(kName.Length);
                        break;
                    }
                }
            }

            return result;
        }

        private bool ShowReCaptchaResponseField()
        {
            string script = $"document.querySelector('{reCaptchaResponse}').style.display = 'block'";

            return EvaluateScript(script);
        }

        private bool FillReCaptchaToken(string token)
        {
            string script = $"document.querySelector('{reCaptchaResponse}').value = '{token}'";

            return EvaluateScript(script);
        }

        #endregion

        #region Helpers

        private bool IsTwitchUrl(string url)
        {
            string twitchWithoutProtocol = TwitchUrl.Replace("http://", "").Replace("https://", "");
            string urlWithoutProtocol = url.Replace("http://", "").Replace("https://", "");

            return urlWithoutProtocol.StartsWith(twitchWithoutProtocol);
        }

        private bool IsTargetUrl(string url)
        {
            string targerWithoutProtocol = TargetUrl.Replace("http://", "").Replace("https://", "");
            string urlWithoutProtocol = url.Replace("http://", "").Replace("https://", "");

            return urlWithoutProtocol.CompareTo(targerWithoutProtocol) == 0;
        }

        private void WriteLog(string message)
        {
            App.LogWriter.WriteLog($"[WATCHER ({ShortID})] - {message}");
        }

        private void UpdateCurrentUrl(string url)
        {
            currentUrl = url;
            OnPropertyChanged("CurrentUrl");
        }

        #endregion

        #endregion

        #region Override

        public override string ToString()
        {
            if (engine == null)
            {
                return $"[WATCHER ({ShortID})]";
            }
            return $"[WATCHER ({ShortID})] - Engine: {engine.Name}";
        }

        #endregion

        #region Callbacks

        private void WebView_NeedClientCertificate(object sender, NeedClientCertificateEventArgs e)
        {
            e.ContinueWithoutCertificate();
        }

        private void WebView_CertificateError(object sender, CertificateErrorEventArgs e)
        {
            e.Continue();
        }
       
        private async void WebView_LoadCompleted(object sender, LoadCompletedEventArgs e)
        {
            WriteLog($"Load Completed: {e.Url}");

            if (IsAuthorized == false && IsTwitchUrl(e.Url) == true && await CheckAutorization() == true)
            {
                WriteLog("Autorized (check in load completed)");
                IsAuthorized = true;
                State = WatcherState.AUTHORIZED;
            }
            else if (IsAuthorized == true && IsTargetUrl(e.Url))
            {
                WriteLog("Watching Target Channel");
                State = WatcherState.WATCHING;
            }
            else
            {
                State = WatcherState.LOADED;
            }
        }

        private void WebView_LoadFailed(object sender, LoadFailedEventArgs e)
        {
            WriteLog($"Load Failed: {e.Url}. Error: {e.ErrorCode}:{e.ErrorMessage}. Http: {e.HttpStatusCode}");
            State = WatcherState.FAILED;
        }

        #endregion
    }
}
