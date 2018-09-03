using OeBrowser.Interfaces;
using System;
using System.Diagnostics;
using System.IO;

namespace MultiWatcher.Utils
{
    public class FileLogger : ILogger, IDisposable
    {
        private StreamWriter stream;

        public FileLogger(string fileName)
        {
            try
            {
                stream = File.CreateText(fileName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("LOGGER: " + ex.Message);
            }
        }

        #region IDisposable Support

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (stream != null)
                    {
                        stream.Flush();
                        stream.Close();
                    }
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion

        public void WriteLog(string message)
        {
            if (stream == null) return;
           
            string time = DateTime.Now.ToString("HH:mm:ss");
            try
            {
                stream.WriteLine($"{time} : {message}");
                stream.Flush();
            }
            catch (IOException io)
            {
                Debug.WriteLine("LOGGER IO: " + io.Message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("LOGGER: " + ex.Message);
            }
        }
    }
}
