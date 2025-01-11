using SoftShell.Commands;
using SoftShell.Helpers;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// Allow unit tests to access internals
[assembly: InternalsVisibleTo("CommandTest")]
[assembly: InternalsVisibleTo("ExecutionTest")]

namespace SoftShell
{
    public sealed class SoftShellHost : ISessionCreator, ISessionHost, IDisposable, HelpCommand.ICommandCollectionProvider, SessionCommand.ISessionCollectionProvider
    {
        public const string SoftShellName = "SoftShell";

        private const string CoreGroupPrefix = "core";
        private const string CoreGroupName = "Core";

        private List<ITerminalListener> _terminalListeners = new List<ITerminalListener>();
        private List<Command> _commands = new List<Command>();
        private Dictionary<string, CommandGroup> _commandGroups = new Dictionary<string, CommandGroup>();
        private Func<string, IEnumerable<string>> _sessionStartInfoFunc;

        // Using this concurrent dictionary as dummy for a concurrent list
        private ConcurrentDictionary<ISession, ISession> _sessions = new ConcurrentDictionary<ISession, ISession>();

        /// <inheritdoc/>
        public IUserAuthentication UserAuthentication { get; private set; }

        /// <inheritdoc/>
        public IEnumerable<Command> Commands => _commands;

        internal IEnumerable<ISession> Sessions => new List<ISession>(_sessions.Select(kv => kv.Key).ToList()); // Make a shallow copy to work on

        /// <summary>
        /// Constructor using entry assembly version info.
        /// </summary>
        /// <param name="userAuthentication">Interface for providing user authentication in the host application.</param>
        public SoftShellHost(IUserAuthentication userAuthentication)
            : this(userAuthentication, GetDefaultInfoFunc(wantsInfoAtSessionStart: true))
        {
        }

        /// <summary>
        /// Constructor using entry assembly version info.
        /// </summary>
        /// <param name="userAuthentication">Interface for providing user authentication in the host application.</param>
        public SoftShellHost(IUserAuthentication userAuthentication, bool wantsInfoAtSessionStart)
            : this(userAuthentication, GetDefaultInfoFunc(wantsInfoAtSessionStart))
        {
        }

        private static Func<string, string, bool, IEnumerable<string>> GetDefaultInfoFunc(bool wantsInfoAtSessionStart)
        {
            var appVersionInfo = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location);

            var appName = appVersionInfo.ProductName;;
            var appVersion = appVersionInfo.ProductVersion;
            var appCompanyName = appVersionInfo.CompanyName;
            var appCopyright = appVersionInfo.LegalCopyright;

            string appString = string.Empty;

            if (!string.IsNullOrWhiteSpace(appName))
            {
                appString = appName;

                if (!string.IsNullOrWhiteSpace(appVersion)) appString = $"{appString} {appVersion}";
                if (!string.IsNullOrWhiteSpace(appCompanyName)) appString = $"{appString} by {appCompanyName}";
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(appCompanyName)) appString = appCompanyName;
            }

            return (string softShellName, string softShellVersion, bool isInfoForSessionStart) =>
            {
                if (isInfoForSessionStart && !wantsInfoAtSessionStart)
                    return Enumerable.Empty<string>();

                return !string.IsNullOrWhiteSpace(appCopyright)
                    ? new List<string>
                      {
                          appString,
                          appCopyright,
                          $"{softShellName} {softShellVersion}"
                      }
                    : new List<string>
                      {
                          appString,
                          $"{softShellName} {softShellVersion}"
                      };
            };
        }

        /// <summary>
        /// Constructor using entry assembly version info.
        /// </summary>
        /// <param name="userAuthentication">Interface for providing user authentication in the host application.</param>
        /// <param name="infoFunc">
        /// Delegate for providing information to the user about the app and SoftShell.
        /// Parameters:
        /// - SoftShell name.
        /// - SoftShell version.
        /// - Is getting the text for session start? False if getting for the Info command.
        /// Returns: Lines of text to show (null or empty collection if nothing).
        /// </param>
        public SoftShellHost(IUserAuthentication userAuthentication, Func<string, string, bool, IEnumerable<string>> infoFunc)
        {
            UserAuthentication = userAuthentication ?? SoftShell.UserAuthentication.None;
            _sessionStartInfoFunc = softShellVersion => infoFunc?.Invoke(SoftShellName, softShellVersion, true /* for session start */ )
                                                        ?? Enumerable.Empty<string>();

            // Add core commands needing special construction
            AddCoreCommand(new InformationCommand(GetInfoCommandInfoFunc(infoFunc)));
            AddCoreCommand(new HelpCommand(this));
            AddCoreCommand(new SessionCommand(this));

            // Add remaining core commands assumed to have default constructors
            AddCoreCommands(this.GetType().Assembly);
        }

        private Func<IEnumerable<string>> GetInfoCommandInfoFunc(Func<string, string, bool, IEnumerable<string>> infoFunc)
        {
            if (infoFunc is null)
                return () => Enumerable.Empty<string>();

            var shellVerInfo = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);

            return () => infoFunc(SoftShellName, shellVerInfo.FileVersion, false /* for info command */);
        }

        /// <summary>
        /// Adds and starts a terminal listener.
        /// </summary>
        /// <param name="listener">Terminal listener.</param>
        /// <param name="start"></param>
        /// <returns></returns>
        public ITerminalListener AddTerminalListener(ITerminalListener listener)
        {
            if (listener != null)
            {
                _terminalListeners.Add(listener);
                listener.TerminalConnected += TerminalConnected;

                listener.RunAsync(this);
            }

            return listener;
        }

        private async void TerminalConnected(object sender, TerminalConnectedEventArgs e)
        {
            _sessions.AddOrUpdate(e.Session, e.Session, (k, v) => v);

            try
            {
                await ((Session)e.Session).RunAsync().ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    ((Session)e.Session).Dispose();
                }
                finally
                {
                    _sessions.TryRemove(e.Session, out var _);
                }
            }
        }

        public void RemoveTerminalListener(ITerminalListener listener, bool dispose)
        {
            if (listener != null)
            {
                _terminalListeners.Remove(listener);

                if (dispose)
                    listener.Dispose();
            }
        }

        /// <inheritdoc/>
        public ISession CreateSession(ITerminalInterface terminal)
        {
            return new Session(this, terminal);
        }

        /// <inheritdoc/>
        public IEnumerable<string> GetSessionStartInfo()
        {
            if (_sessionStartInfoFunc is null)
                return Enumerable.Empty<string>();

            var shellVerInfo = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);

            return _sessionStartInfoFunc(shellVerInfo.FileVersion);
        }

        /// <inheritdoc/>
        public void TerminateSession(int sessionId, bool hard)
        {
            var session = Sessions.FirstOrDefault(s => s.Id == sessionId);

            if (session != null)
            {
                if (hard)
                {
                    ((Session)session).Dispose(); // Also disposes the connected terminal
                    _sessions.TryRemove(session, out var _);
                }
                else
                {
                    session.RequestEnd();
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            foreach (var listener in _terminalListeners)
                listener.Dispose();
        }

        public void AddCommands(string groupPrefix, string groupName, Assembly assembly) => AddCommandsInternal(groupPrefix, groupName, false, assembly);

        public void AddCommand(string groupPrefix, string groupName, Command command) => AddCommandInternal(groupPrefix, groupName, false, command);

        internal void AddCoreCommands(Assembly assembly) => AddCommandsInternal(CoreGroupPrefix, CoreGroupName, true, assembly);

        internal void AddCoreCommand(Command command) => AddCommandInternal(CoreGroupPrefix, CoreGroupName, true, command);

        #region HelpCommand.ICommandCollectionProvider

        IEnumerable<Command> HelpCommand.ICommandCollectionProvider.GetCommands()
        {
            return new List<Command>(_commands);
        }

        #endregion

        #region SessionCommand.ISessionCollectionProvider

        IEnumerable<ISession> SessionCommand.ISessionCollectionProvider.GetSessions()
        {
            return Sessions;
        }

        #endregion

        private void AddCommandsInternal(string groupPrefix, string groupName, bool isCore, Assembly assembly)
        {
            foreach (var commandType in assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(Command)) && !type.IsAbstract))
            {
                // Command not already added?
                if (!_commands.Any(cmd => cmd.GetType() == commandType))
                {
                    var ctor = commandType.GetConstructor(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance, null, new Type[0], new ParameterModifier[0]);

                    // Having a default constructor?
                    if (ctor != null)
                        AddCommandInternal(groupPrefix, groupName, isCore, ctor.Invoke(new object[0]) as Command);
                }
            }
        }

        private void AddCommandInternal(string groupPrefix, string groupName, bool isCore, Command command)
        {
            // An anonymous (non-user-invokable) command?
            if (command.IsAnonymous)
                return;

            command.PostConstruct();

            if (!isCore && (groupPrefix == null)) throw new ArgumentNullException(nameof(groupPrefix));
            if (!isCore && (groupName == null)) throw new ArgumentNullException(nameof(groupName));
            if (command == null) throw new ArgumentNullException(nameof(command));

            if (!isCore && string.IsNullOrWhiteSpace(groupPrefix)) throw new ArgumentException("Empty command group prefix", nameof(groupPrefix));
            if (!isCore && string.IsNullOrWhiteSpace(groupName)) throw new ArgumentException("Empty command group name", nameof(groupName));

            groupPrefix = groupPrefix?.ToLowerInvariant() ?? CoreGroupPrefix;
            groupName = groupName ?? CoreGroupName;

            // Check if command/aliases clash
            var existingNames = _commands.SelectMany(cmd => cmd.CommandNames).ToList();
            var newFullName = $"{groupPrefix}.{command.CommandName}";
            if (existingNames.Any(name => string.Equals(name, newFullName, StringComparison.InvariantCultureIgnoreCase)))
                throw new InvalidOperationException($"Failed to add command {newFullName}. A command with the same name or alias already exists.");

            if (!_commandGroups.TryGetValue(groupPrefix, out var cmdGroup))
                _commandGroups[groupPrefix] = cmdGroup = new CommandGroup(groupPrefix, groupName, isCore);

            command.Group = cmdGroup;
            _commands.Add(command);
        }
    }
}
