using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SoftShell
{
    /// <summary>
    /// Static class with properties for common user authentication purposes.
    /// </summary>
    public static class UserAuthentication
    {
        /// <summary>
        /// Gets an <see cref="IUserAuthentication"/> implementation that requires no user authentication.
        /// </summary>
        public static IUserAuthentication None => new NoUserAuthentication();

        private class NoUserAuthentication : IUserAuthentication
        {
            public bool WantsUserAuthentication => false;

            public bool WantsUserName => throw new NotImplementedException();

            public bool WantsPassword => throw new NotImplementedException();

            public Task<bool> AuthenticateUserAsync(string userName, string password, CancellationToken cancelToken, IProgress<string> status)
            {
                throw new NotImplementedException();
            }
        }
    }
}
