using OeBrowser.Interfaces;

namespace MultiWatcher.Interfaces
{
    public interface ILogWriter : ILogger
    {
        void AddLogger(ILogger logger);

        void RemoveLogger(ILogger logger);

        void Clear();
    }
}
