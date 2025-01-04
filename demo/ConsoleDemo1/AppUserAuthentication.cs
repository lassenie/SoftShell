using SoftShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleDemo1
{
    internal class AppUserAuthentication : IUserAuthentication
    {
        public bool WantsUserAuthentication => true;

        public bool WantsUserName => true;

        public bool WantsPassword => true;

        public async Task<bool> AuthenticateUserAsync(string userName, string password, CancellationToken cancelToken, IProgress<string> status)
        {
            status.Report("Authenticating user...");
            await Task.Delay(1000, cancelToken);

            // Dummy validation
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            {
                status.Report("Missing user name or password.");
                return false;
            }

            // User valid
            return true;
        }
    }
}
