using MultiWatcher.Interfaces;
using OeBrowser.Interfaces;
using OeBrowser.Utils;
using System.Collections;
using System.Collections.Generic;

namespace MultiWatcher.Utils
{
    public class LoggerManager : ILogger, ILogWriter, IEnumerable<ILogger>
    {
        private List<ILogger> loggers = new List<ILogger>();

        public void AddLogger(ILogger logger)
        {
            loggers.Add(logger);
        }

        public void WriteLog(string message)
        {
            foreach (ILogger logger in loggers)
            {
                logger.WriteLog(message);
            }
        }

        public IEnumerator<ILogger> GetEnumerator()
        {
            return loggers.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return loggers.GetEnumerator();
        }

        public void RemoveLogger(ILogger logger)
        {
            if (logger == null) return;

            loggers.Remove(logger);
        }

        public void Clear()
        {
            loggers.RemoveAll((l) => !(l is SimpleLogger));
        }
    }
}
