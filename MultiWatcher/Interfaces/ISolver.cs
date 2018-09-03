using System;
using System.Threading.Tasks;

namespace MultiWatcher.Interfaces
{
    public interface ISolver : IDisposable
    {
        Task<string> GetResult(object args);
    }
}
