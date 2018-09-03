using MultiWatcher.Interfaces;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace MultiWatcher.Utils
{
    public class ReCaptchaSettings
    {
        public string ApiKey { get; set; }
        public int FirstDelay { get; set; }
        public int SecondDelay { get; set; }
        public int Attempts { get; set; }

        public bool IsValid()
        {
            return !String.IsNullOrEmpty(ApiKey)
                   && FirstDelay >= 0
                   && SecondDelay >=0
                   && Attempts >= 0;
        }
    }

    public class ReCaptchaArgs
    {
        private string k;
        private string url;

        public string KValue => k;
        public string Url => url;

        public ReCaptchaArgs(string key, string target)
        {
            k = key;
            url = target;
        }
    }

    public class ReCaptchaV2 : ISolver, IDisposable
    {
        private readonly string RuCaptchaRequestTeamplate = @"http://rucaptcha.com/in.php?key={0}&method=userrecaptcha&googlekey={1}&pageurl={2}";
        private readonly string RuCaptchaResultTemplate = @"http://rucaptcha.com/res.php?key={0}&action=get&id={1}";

        private int firstDelay;
        private int secondDelay;
        private int attempts;
        private string apiKey;

        private HttpClient client = new HttpClient();

        #region Life

        public ReCaptchaV2(ReCaptchaSettings settings)
        {
            apiKey = settings.ApiKey;
            firstDelay = settings.FirstDelay;
            secondDelay = settings.SecondDelay;
            attempts = settings.Attempts;
        }

        #endregion

        #region Public

        public async Task<string> GetResult(object args)
        {
            if (args is ReCaptchaArgs param)
            {
                if (String.IsNullOrEmpty(param.KValue) || String.IsNullOrEmpty(param.Url)) return String.Empty;
                
                return await GetResult(param.KValue, param.Url);
            }

            return String.Empty;
        }

        #endregion

        #region Private

        private async Task<string> GetResult(string k, string url)
        {
            string result = string.Empty;

            string captchaID = await RequestCapthaID(k, url);

            if (String.IsNullOrEmpty(captchaID)) return result;

            await Task.Delay(TimeSpan.FromSeconds(firstDelay));

            result = await RequsetCaptchaToken(captchaID);

            return result;
        }

        private async Task<string> RequestCapthaID(string k, string url)
        {
            string result = String.Empty;
            try
            {
                string requestUrl = String.Format(RuCaptchaRequestTeamplate, apiKey, k, url);
                string captchaResponce = await client.GetStringAsync(requestUrl);

                if (!captchaResponce.Contains("OK"))
                {
                    WriteLog($"Request Captcha Error: {captchaResponce}");
                    return result;
                }

                result = captchaResponce.Split('|')[1];

                WriteLog($"CaptchaID Success: {result}");
            }
            catch (HttpRequestException httpex)
            {
                WriteLog($"HTTP Error: {httpex.Message}");
            }
            catch (Exception ex)
            {
                WriteLog($"Captcha ID Error: {ex.Message}");
            }

            return result;
        }

        private async Task<string> RequsetCaptchaToken(string captchaID)
        {
            string result = string.Empty;

            string requestUrl = String.Format(RuCaptchaResultTemplate, apiKey, captchaID);
            int cnt = 0;
            do
            {
                try
                {
                    cnt++;
                    string captchaTokenResponse = await client.GetStringAsync(requestUrl);

                    if (captchaTokenResponse.Contains("ERROR_"))
                    {
                        WriteLog($"Token Request Error:{captchaTokenResponse}");
                        break;
                    }

                    if (captchaTokenResponse == "CAPCHA_NOT_READY")
                    {
                        WriteLog($"CAPCHA_NOT_READY: {cnt}");
                        await Task.Delay(TimeSpan.FromSeconds(secondDelay));
                        continue;
                    }

                    WriteLog($"Captcha Token Success!");

                    result = captchaTokenResponse;
                    break;
                }
                catch (HttpRequestException httpex)
                {
                    WriteLog($"HTTP Error: {httpex.Message}");
                    break;
                }
                catch (Exception ex)
                {
                    WriteLog($"Captcha Token Error: {ex.Message}");
                    break;
                }
            } while (cnt < attempts);

            return result;
        }

        private void WriteLog(string message)
        {
            App.LogWriter.WriteLog($"[CAPTCHA] - {message}");
        }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    client.Dispose();
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
    }
}
