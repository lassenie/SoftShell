using System;
using System.Collections.Generic;
using System.Text;

namespace SoftShell
{
    /// <summary>
    /// Represents a parsed command-line token.
    /// </summary>
    public sealed class CommandLineToken
    {
        /// <summary>
        /// Type of token.
        /// </summary>
        public TokenType Type { get; }

        /// <summary>
        /// Content of the token.
        /// </summary>
        public string Content { get; }

        /// <summary>
        /// Character position where the token starts. First character has the position 1.
        /// </summary>
        public int Position { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="type">Type of token.</param>
        /// <param name="content">Content of the token.</param>
        /// <param name="position">Character position where the token starts. First character has the position 1.</param>
        public CommandLineToken(TokenType type, string content, int position)
        {
            Type = type;
            Content = content ?? throw new ArgumentNullException(nameof(content));
            Position = position;
        }
    }

    /// <summary>
    /// Type of command line token.
    /// </summary>
    public enum TokenType
    {
        /// <summary>
        /// A value or symbol used as a command line argument, option value or redirect source/target.
        /// </summary>
        Value,

        /// <summary>
        /// Name of a command line option.
        /// </summary>
        OptionName,

        /// <summary>
        /// Equal sign '='.
        /// </summary>
        EqualSign,

        /// <summary>
        /// Vertical bar '|'. Used to separate commands in a pipe.
        /// </summary>
        VerticalBar,

        /// <summary>
        /// Redirection: "<", ">", ">>", "2>", or "2>>".
        /// </summary>
        Redirect
    }
}

