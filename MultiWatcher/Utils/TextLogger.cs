using OeBrowser.Interfaces;
using System;
using System.Diagnostics;
using System.Windows.Controls.Primitives;

namespace MultiWatcher.Utils
{
    class TextLogger : ILogger
    {
        private TextBoxBase textElement;

        public TextLogger(TextBoxBase t)
        {
            textElement = t;
        }

        public void WriteLog(string message)
        {
            if (textElement == null) return;

            string time = DateTime.Now.ToString("HH:mm:ss");
            try
            {                
                textElement.Dispatcher.Invoke(() => textElement.AppendText($"{time} - {message}" + Environment.NewLine));
                textElement.Dispatcher.Invoke(() => textElement.ScrollToEnd());
            }
            catch (OperationCanceledException ex)
            {
                Debug.WriteLine($"LOGGER: {ex.Message}");
            }
        }
    }
}
