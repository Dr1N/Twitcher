using OeBrowser.Interfaces;
using System.Diagnostics;

namespace OeBrowser.Utils
{
    public class SimpleLogger : ILogger
    {
        public void WriteLog(string message)
        {
            Debug.WriteLine(message);
        }
    }
}
