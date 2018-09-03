using System;

namespace MultiWatcher.Code
{
    public enum FileOperation
    {
        CREATE,
        SAVE,
        LOAD
    }

    public class WatcherStateChangedEventArgs : EventArgs
    {
        public WatcherState State { get; private set; }

        public WatcherStateChangedEventArgs(WatcherState state)
        {
            State = state;
        }
    }

    public class WatcherManagerFileOperationEventArgs : EventArgs
    {
        public FileOperation Operation { get; private set; }

        public bool Success { get; private set; }

        public string ErrorMessage { get; private set; }

        public WatcherManagerFileOperationEventArgs(FileOperation o, bool success, string error = "")
        {
            Operation = o;
            Success = success;
        }
    }
}
