using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SoftShell.Execution
{
    internal sealed class Redirect
    {
        private const string NullSourceOrTargetName = "null";

        public RedirectType Type { get; }

        public bool IsAppend { get; }

        public string Source { get; }

        public string Target { get; }

        /// <summary>
        /// The starting character position of the command line.
        /// For a non-piped command, or the first command in a pipe, this will be 1.
        /// </summary>
        public int CommandLineStartPosition { get; }

        public IEnumerable<CommandLineToken> Tokens { get; }

        public Redirect(int commandLineStartPosition, IEnumerable<CommandLineToken> tokens)
        {
            CommandLineStartPosition = commandLineStartPosition;
            Tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));

            var firstToken = tokens.FirstOrDefault();
            var secondToken = tokens.Skip(1).FirstOrDefault();

            // Haven't got both tokens?
            if (string.IsNullOrWhiteSpace(firstToken?.Content) || string.IsNullOrWhiteSpace(secondToken?.Content))
            {
                if (firstToken?.Type == TokenType.Redirect)
                    throw new Exception($"Internal error - missing token for redirect '{firstToken.Content}' at position {firstToken.Position}.");
                else
                    throw new Exception("Internal error - missing token(s) for redirect.");
            }

            if (firstToken?.Type != TokenType.Redirect)
                throw new Exception($"Internal error - unexpected token '{firstToken.Content}' at position {firstToken.Position}.");

            if (secondToken?.Type != TokenType.Value)
                throw new Exception($"Internal error - unexpected token '{secondToken.Content}' at position {secondToken.Position}.");

            var redirectOperator = firstToken.Content.Trim();
            var sourceOrTarget = secondToken.Content.Trim();

            switch (redirectOperator)
            {
                case "<":
                    Type = RedirectType.Input;
                    IsAppend = false;
                    Source = !string.IsNullOrEmpty(sourceOrTarget) ? sourceOrTarget : throw new ArgumentException("Empty input redirect source.", nameof(sourceOrTarget));
                    if (Source == NullSourceOrTargetName)
                    {
                        Source = string.Empty;
                    }
                    Target = null;
                    break;

                case ">":
                case "1>":
                case ">>":
                case "1>>":
                    Type = RedirectType.Output;
                    IsAppend = redirectOperator.Contains(">>");
                    Source = null;
                    Target = !string.IsNullOrEmpty(sourceOrTarget) ? sourceOrTarget : throw new ArgumentException("Empty output redirect target.", nameof(sourceOrTarget));
                    if (Target == NullSourceOrTargetName)
                    {
                        Target = string.Empty;
                    }
                    break;

                case "2>":
                case "2>>":
                    Type = RedirectType.ErrorOutput;
                    IsAppend = redirectOperator.Contains(">>");
                    Source = null;
                    Target = !string.IsNullOrEmpty(sourceOrTarget) ? sourceOrTarget : throw new ArgumentException("Empty error output redirect target.", nameof(sourceOrTarget));
                    if (Target == NullSourceOrTargetName)
                    {
                        Target = string.Empty;
                    }
                    break;

                default:
                    if (string.IsNullOrEmpty(redirectOperator))
                        throw new ArgumentException("Empty redirect type.", nameof(redirectOperator));
                    else
                        throw new ArgumentException($"Invalid redirect type '{redirectOperator}'.", nameof(redirectOperator));
            }
        }
    }
}
