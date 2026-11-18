using System;
using System.Threading.Tasks;

namespace Procurement.Core.Interfaces
{
    /// <summary>
    /// A lock that holds across processes, so the desktop app and the web app (and several web users)
    /// can't do the same critical thing at the same time (web app plan, 18 nov 2026). Used for placing
    /// orders and for refreshing DigiKey's rotating refresh token. Dispose the result to release.
    /// </summary>
    public interface IAppLock
    {
        /// <exception cref="TimeoutException">Someone else held the lock for longer than <paramref name="timeout"/>.</exception>
        Task<IDisposable> AcquireAsync(string resource, TimeSpan timeout);
    }
}
