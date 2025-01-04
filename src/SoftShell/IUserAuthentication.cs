using System;
using System.Collections.Generic;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SoftShell
{
    /// <summary>
    /// Optional user authentication at login. To be implemented by the host application.
    /// </summary>
    public interface IUserAuthentication
    {
        /// <summary>
        /// Is user authentication needed at all?
        /// If false, no further calls will be made on the interface.
        /// </summary>
        bool WantsUserAuthentication { get; }

        /// <summary>
        /// Is user name wanted?
        /// </summary>
        bool WantsUserName { get; }

        /// <summary>
        /// Is password wanted?
        /// </summary>
        bool WantsPassword { get; }

        /// <summary>
        /// Authenticates the user, given wanted credentials.
        /// </summary>
        /// <remarks>
        /// If two-factor authentication is needed it can be done by the host application before the method returns.
        /// Timeout should be handled and result in returning false.
        /// </remarks>
        /// <param name="userName">User name entered by the user, or null if <see cref="WantsUserName"/> returned false.</param>
        /// <param name="password">Password entered by the user, or null if <see cref="WantsPassword"/> returned false.</param>
        /// <param name="cancelToken">Cancellation token that is signalled if the operation should be cancelled.</param>
        /// <param name="status">Interface for optional textual updates/messages to show on the terminal.</param>
        /// <returns>True if the user is authentic. If true, the session will run - otherwise it is terminated.</returns>
        Task<bool> AuthenticateUserAsync(string userName, string password, CancellationToken cancelToken, IProgress<string> status);
    }
}
