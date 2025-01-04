using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace SoftShell.Parsing
{
    /// <summary>
    /// Parser of command line tokens. Used by <see cref="StdCommand"/>-derived commands for arguments and option values/flags.
    /// </summary>
    internal static class CommandLineParser
    {
        /// <summary>
        /// Parses a given sequence of command line tokens.
        /// </summary>
        /// <param name="tokens">Tokens (assuming the first being the command.</param>
        /// <returns>
        /// <ul>
        /// <li>args: Arguments after the command itself. The first argument may be a subcommand (command-dependent).</li>
        /// <li>options: Option keys/values.</li>
        /// </ul>
        /// </returns>
        public static (IEnumerable<string> args, Dictionary<string, string> options) Parse(IEnumerable<CommandLineToken> tokens)
        {
            bool awaitingOptionValue = false;
            string optionName = null;
            var argList = new List<string>();
            var optionDictionary = new Dictionary<string, string>();

            var tokenList = tokens?.ToList() ?? throw new ArgumentNullException(nameof(tokens));

            for (int i = 0; i < tokenList.Count; i++)
            {
                // The command is first token - skip it
                if (i > 0)
                {
                    if (awaitingOptionValue)
                    {
                        switch (tokenList[i].Type)
                        {
                            case TokenType.Value:
                                optionDictionary.Add(optionName, tokenList[i].Content);
                                awaitingOptionValue = false;
                                break;

                            case TokenType.EqualSign:
                                throw new Exception($"Unexpected token '{tokenList[i].Content}' at position {tokenList[i].Position}.");

                            default:
                                throw new Exception($"Internal error - unhandled token type {tokenList[i].Type} at position {tokenList[i].Position}.");
                        }
                    }
                    else
                    {
                        switch (tokenList[i].Type)
                        {
                            case TokenType.Value:
                                argList.Add(tokenList[i].Content);
                                break;

                            case TokenType.OptionName:
                                if (tokenList[i].Content.Length >= 2)
                                {
                                    // Option with value, i.e. next token an equal sign?
                                    if ((i < tokenList.Count - 1) && (tokenList[i + 1].Type == TokenType.EqualSign))
                                    {
                                        // Any token after the equal sign?
                                        if (i < tokenList.Count - 2)
                                        {
                                            optionName = tokenList[i].Content.Substring(1);
                                            awaitingOptionValue = true;

                                            // Skip the equal sign and go for the option value token
                                            i++;
                                        }
                                        else
                                        {
                                            // Equal sign with nothing after it
                                            throw new Exception($"Unexpected token '{tokenList[i + 1].Content}' at position {tokenList[i + 1].Position}.");
                                        }
                                    }
                                    else // No value - just a flag option
                                    {
                                        optionDictionary.Add(tokenList[i].Content.Substring(1), null); // Null means a flag option without value
                                    }
                                }
                                else
                                {
                                    throw new Exception($"Unexpected token '{tokenList[i].Content}' at position {tokenList[i].Position}.");
                                }
                                break;

                            case TokenType.EqualSign:
                                throw new Exception($"Unexpected token '{tokenList[i].Content}' at position {tokenList[i].Position}.");

                            case TokenType.VerticalBar:
                            default:
                                throw new Exception($"Internal error - unhandled token type {tokenList[i].Type} at position {tokenList[i].Position}.");
                        }
                    }
                }
            }

            return (args: argList, options: optionDictionary);
        }
    }
}
