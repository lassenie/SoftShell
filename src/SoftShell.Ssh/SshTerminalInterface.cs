using FxSsh;
using FxSsh.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SoftShell.Ssh
{
    /// <summary>
    /// A terminal interface for communicating with SSH clients.
    /// Offers the same limited ANSI terminal emulation as the Telnet terminal interface.
    /// </summary>
    public class SshTerminalInterface : RemoteAnsiTerminalInterface
    {
        // The SSH session this terminal communicates through.
        private readonly Session _session;

        // "Stack" of channels in use. The latest (last) is the one currently communicated with.
        // A stack is kept so that nested channels can be popped back to a previous one when closed.
        private readonly List<SessionChannel> _channelStack = new List<SessionChannel>();

        // Background task that flushes queued output bytes to the current channel.
        private Task? _writeTask;

        // Terminal window size as last reported by the client (null until known).
        private int? _windowWidth = null;
        private int? _windowHeight = null;

        // True when the previous received input character was a CR, used to collapse CR-LF pairs.
        private bool _previousInputWasCr = false;

        /// <inheritdoc/>
        public override string TerminalType => "SSH";

        /// <inheritdoc/>
        public override string TerminalInstanceInfo => BitConverter.ToString(_session.SessionId);

        /// <inheritdoc/>
        public override int? WindowWidth => _windowWidth;

        /// <inheritdoc/>
        public override int? WindowHeight => _windowHeight;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="session">The SSH session to communicate through.</param>
        public SshTerminalInterface(Session session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));

            session.ServiceRegistered += Server_ServiceRegistered;

            // Worker task that flushes queued output bytes to the current channel.
            // Output produced before a channel exists (the session starts before the SSH
            // PTY channel is established) is kept queued until a channel is available.
            _writeTask = Task.Run(() =>
            {
                var buffer = new List<byte>();

                while (!_workerTaskCancellation.IsCancellationRequested)
                {
                    try
                    {
                        var channel = CurrentChannel;

                        // No channel yet: keep the queued bytes for when one becomes available.
                        if (channel == null)
                        {
                            Thread.Sleep(50);
                            continue;
                        }

                        buffer.Clear();

                        while (_bytesToSend.TryDequeue(out var b))
                            buffer.Add(b);

                        if (buffer.Count > 0 && !channel.ClientClosed)
                        {
                            try { channel.SendData(buffer.ToArray()); }
                            catch { }
                        }

                        Thread.Sleep(50);
                    }
                    catch { }
                }
            });
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            _workerTaskCancellation.Cancel();

            // Wait for the worker task to complete
            if (_writeTask != null)
            {
                while (!_writeTask.IsCompleted)
                    Thread.Sleep(100);
            }

            // Close all channels (latest first)
            foreach (var channel in ((IEnumerable<SessionChannel>)_channelStack).Reverse().ToArray())
                CloseChannel(channel);

            base.Dispose();
        }

        /// <summary>
        /// The channel currently in use for communication, or null if none is established yet.
        /// </summary>
        private SessionChannel? CurrentChannel
        {
            get
            {
                lock (_channelStack)
                {
                    return _channelStack.LastOrDefault();
                }
            }
        }

        /// <summary>
        /// Closes a channel, removes it from the stack and, if it was the active one,
        /// resumes communication on the previous channel (if any).
        /// </summary>
        /// <param name="channelToClose">The channel to close.</param>
        private void CloseChannel(SessionChannel? channelToClose)
        {
            lock (_channelStack)
            {
                // Close channel, if any
                if (channelToClose != null && _channelStack.Contains(channelToClose))
                {
                    bool wasLatest = (channelToClose == _channelStack.Last());

                    // Remove from the "stack" (also if not the latest)
                    _channelStack.Remove(channelToClose);

                    StopUsingChannel(channelToClose);

                    if (!channelToClose.ClientClosed)
                    {
                        try { channelToClose.SendClose(); }
                        catch { }
                    }

                    // If another channel is now latest, use that again
                    if (wasLatest)
                        UseLatestChannel();
                }
            }
        }

        /// <summary>
        /// Starts listening for data and close events on the latest (active) channel.
        /// </summary>
        private void UseLatestChannel()
        {
            if (_channelStack.Any())
            {
                _channelStack.Last().DataReceived += Channel_DataReceived;
                _channelStack.Last().CloseReceived += Channel_CloseReceived;
            }
        }

        /// <summary>
        /// Stops listening for data and close events on the given channel.
        /// </summary>
        /// <param name="channel">The channel to stop using.</param>
        private void StopUsingChannel(SessionChannel channel)
        {
            if (channel != null)
            {
                channel.DataReceived -= Channel_DataReceived;
                channel.CloseReceived -= Channel_CloseReceived;
            }
        }

        /// <summary>
        /// Handles SSH services becoming available on the session, hooking up the events
        /// needed for authentication, PTY allocation and window size changes.
        /// </summary>
        private void Server_ServiceRegistered(object? sender, SshService e)
        {
            if (!(sender is Session))
                return;

            if (e is UserauthService authService)
            {
                authService.Userauth += AuthService_UserAuth;
            }
            else if (e is ConnectionService connectionService)
            {
                connectionService.PtyReceived += ConnectionService_PtyReceived;
                connectionService.WindowChange += ConnectionService_WindowChange;
            }
        }

        /// <summary>
        /// Handles a client request for a pseudo terminal (PTY), making its channel the
        /// active one and recording the initial window size.
        /// </summary>
        private void ConnectionService_PtyReceived(object? sender, PtyArgs e)
        {
            lock (_channelStack)
            {
                // Pause using previous channel, if any
                if (_channelStack.Any())
                    StopUsingChannel(_channelStack.Last());

                // Add and use new channel
                _channelStack.Add(e.Channel);
                UseLatestChannel();
            }

            _windowWidth = (int)e.WidthChars;
            _windowHeight = (int)e.HeightRows;
        }

        /// <summary>
        /// Handles the client reporting a new terminal window size.
        /// </summary>
        private void ConnectionService_WindowChange(object? sender, WindowChangeArgs e)
        {
            _windowWidth = (int)e.WidthColumns;
            _windowHeight = (int)e.HeightRows;
        }

        /// <summary>
        /// Handles the client closing a channel.
        /// </summary>
        private void Channel_CloseReceived(object? sender, EventArgs e)
        {
            if (sender is SessionChannel channel)
                CloseChannel(channel);
        }

        /// <summary>
        /// Handles raw data received from the client: parses it into characters and key
        /// actions, normalizes line endings and queues the result for reading.
        /// </summary>
        private void Channel_DataReceived(object? sender, byte[] e)
        {
            foreach (var b in e)
            {
                foreach (var item in _ansiParser.HandleByte(b))
                {
                    var action = item.action;
                    var character = item.character;

                    if (action == KeyAction.Character)
                    {
                        // SSH terminals send a bare CR for the Enter key. Treat CR as line
                        // termination ('\n') and collapse an immediately following LF, so that
                        // both CR and CR-LF result in a single end-of-line (like the Telnet
                        // interface, whose clients send CR-LF).
                        if (character == '\r')
                        {
                            _previousInputWasCr = true;
                            character = '\n';
                        }
                        else if (character == '\n' && _previousInputWasCr)
                        {
                            _previousInputWasCr = false;
                            continue;
                        }
                        else
                        {
                            _previousInputWasCr = false;
                        }

                        // Ctrl-C pressed?
                        if (character == (char)3)
                        {
                            RequestTaskCancel();
                            continue;
                        }
                    }
                    else
                    {
                        _previousInputWasCr = false;
                    }

                    _receivedItems.Enqueue((action, character));
                }
            }
        }

        /// <summary>
        /// Handles SSH-level user authentication. This is accepted unconditionally; the
        /// actual SoftShell authorization happens through user interaction on the terminal.
        /// </summary>
        /// <remarks>
        /// The connection is still encrypted (the SSH key exchange happens before
        /// authentication), but any SSH credentials are accepted, so access control is
        /// entirely up to the SoftShell session running on top of this terminal.
        /// </remarks>
        private void AuthService_UserAuth(object? sender, UserauthArgs e)
        {
            // Just consider authorized at this point - actual SoftShell authorization
            // will happen through user interaction on the terminal.
            e.Result = true;
        }
    }
}
