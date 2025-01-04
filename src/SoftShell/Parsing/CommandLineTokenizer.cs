using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Transactions;

using static SoftShell.Command;

namespace SoftShell.Parsing
{
    /// <summary>
    /// Tokenizer for command lines.
    /// Used as standard tokenizing and provided to all commands when executed.
    /// Custom commands may do their own tokenizing.
    /// </summary>
    internal class CommandLineTokenizer
    {
        private enum State
        {
            Normal = 0,
            TokenUnquoted,
            TokenQuoted,
            TokenQuotedFoundQuote,
            TokenOne,
            TokenTwo,
            TokenOneRedirect,
            TokenTwoRedirect,
            TokenRedirect
        }

        State _state = State.Normal;
        List<CommandLineToken> _tokens = new List<CommandLineToken>();
        StringBuilder _currentToken = new StringBuilder();
        int? _currentTokenStartPosition = null;

        /// <summary>
        /// Tokenizes a full command line (with or without piping).
        /// </summary>
        /// <remarks>
        /// When this method returns the class is in a non-default state and cannot be used for multiple <see cref="Tokenize(string)"/> calls.
        /// </remarks>
        /// <param name="fullCommandLine">The full command line.</param>
        /// <returns>Sequence of tokens, grouped by commands (if piped) and redirects. For each command/redirect the following is provided:
        /// <ul>
        ///     <li>tokens:   Tokens for the command.</li>
        ///     <li>position: Character position in the full command line (1 for first character).</li>
        ///     <li>text:     Complete text for the individual command or redirect.</li>
        /// </ul>
        /// </returns>
        public (IEnumerable<(IEnumerable<CommandLineToken> tokens, int position, string text)> commands,
                IEnumerable<(IEnumerable<CommandLineToken> tokens, int position, string text)> redirects)
            Tokenize(string fullCommandLine)
        {
            const int MaxCharIterations = 2;

            if (fullCommandLine == null) throw new ArgumentNullException(nameof(fullCommandLine));
            if (string.IsNullOrWhiteSpace(fullCommandLine)) throw new ArgumentException("Empty command line.", nameof(fullCommandLine));

            int charNo = 1;
            foreach (char ch in fullCommandLine)
            {
                int iterationsThisCharacter = 0;
                bool reIterateCharacter = false;

                do
                {
                    if (++iterationsThisCharacter > MaxCharIterations)
                    {
                        throw new Exception($"Internal error - tokenizing stuck at position {charNo}.");
                    }

                    reIterateCharacter = false;

                    switch (_state)
                    {
                        // Looking for next token?
                        case State.Normal:
                            if (ch == '"') // Starting a quoted token?
                            {
                                TokenBeginOrAppend(null, charNo);
                                _state = State.TokenQuoted;
                            }
                            else if (ch == '=') // Found equal sign as a complete token?
                            {
                                TokenBeginOrAppend(ch, charNo);
                                TokenEnd(TokenType.EqualSign);
                            }
                            else if (ch == '|') // Found vertical bar as a complete token?
                            {
                                TokenBeginOrAppend(ch, charNo);
                                TokenEnd(TokenType.VerticalBar);
                            }
                            else if (ch == '<') // Found less than character as a complete token?
                            {
                                TokenBeginOrAppend(ch, charNo);
                                TokenEnd(TokenType.Redirect);
                            }
                            else if (ch == '>') // Found greater than character (either a > or >> token)?
                            {
                                TokenBeginOrAppend(ch, charNo);
                                _state = State.TokenRedirect;
                            }
                            else if (ch == '1') // Found the character '1' (either the beginning of 1>, 1>> or a value token)?
                            {
                                TokenBeginOrAppend(ch, charNo);
                                _state = State.TokenOne;
                            }
                            else if (ch == '2') // Found the character '2' (either the beginning of 2>, 2>> or a value token)?
                            {
                                TokenBeginOrAppend(ch, charNo);
                                _state = State.TokenTwo;
                            }
                            else if (!char.IsControl(ch) && !char.IsWhiteSpace(ch)) // Found some other unquoted token?
                            {
                                TokenBeginOrAppend(ch, charNo);
                                _state = State.TokenUnquoted;
                            }
                            else
                            {
                                // Still looking for next token
                            }
                            break;

                        // Within an unquoted token, or a perhaps finishing quoted token?
                        case State.TokenUnquoted:
                        case State.TokenQuotedFoundQuote:

                            if (ch == '"')
                            {
                                if (_state == State.TokenQuotedFoundQuote) // Escaped quote character within a quoted token?
                                    TokenBeginOrAppend(ch, charNo);
                                else
                                    throw new Exception($"Found quote character in token at position {charNo}.");
                            }
                            else if (char.IsControl(ch) || char.IsWhiteSpace(ch) ||
                                     (ch == '=') || (ch == '|') || (ch == '<') || (ch == '>')) // End of token, perhaps starting a new token directly?
                            {
                                // Token considered ended as option name or value
                                TokenEnd(TokenNameStartsLikeOptionName() && (_state == State.TokenUnquoted)
                                            ? TokenType.OptionName
                                            : TokenType.Value);

                                // Process the character in normal state
                                _state = State.Normal;
                                reIterateCharacter = true;
                            }
                            else
                            {
                                TokenBeginOrAppend(ch, charNo);
                            }
                            break;

                        // Within a quoted token?
                        case State.TokenQuoted:
                            if (ch == '"') // Quoted token - found another quote (either the end or an escaped quote character)?
                            {
                                _state = State.TokenQuotedFoundQuote;
                            }
                            else if (!char.IsControl(ch)) // An additional character, may even be whitespace inside a quoted token
                            {
                                TokenBeginOrAppend(ch, charNo);
                            }
                            else
                            {
                                throw new Exception($"Missing end of quoted token at position {charNo}.");
                            }
                            break;

                        // Within a token starting with "1" or "2"?
                        case State.TokenOne:
                        case State.TokenTwo:
                            if (ch == '>') // 1> or 2> found?
                            {
                                TokenBeginOrAppend(ch, charNo);
                                _state = (_state == State.TokenOne) ? State.TokenOneRedirect : State.TokenTwoRedirect; // See if we get 1>> or 2>>
                            }
                            else if (!char.IsControl(ch) && !char.IsWhiteSpace(ch)) // Not end of token?
                            {
                                TokenBeginOrAppend(ch, charNo);
                                _state = State.TokenUnquoted; // Token starting with 2 and something, but not 2>
                            }
                            else // End of token - just the value 1 or 2
                            {
                                TokenEnd(TokenType.Value);
                                _state = State.Normal;
                            }
                            break;

                        // Within a token starting with "1>" or "2>"?
                        case State.TokenOneRedirect:
                        case State.TokenTwoRedirect:
                            if (ch == '>') // 1>> or 2>> found?
                            {
                                TokenBeginOrAppend(ch, charNo);
                                TokenEnd(TokenType.Redirect);
                                _state = State.Normal;
                            }
                            else // End of token
                            {
                                TokenEnd(TokenType.Redirect); // Just 2>

                                // Process the character in normal state
                                _state = State.Normal;
                                reIterateCharacter = true;
                            }
                            break;

                        case State.TokenRedirect:
                            if (ch == '>') // >> found?
                            {
                                TokenBeginOrAppend(ch, charNo);
                                TokenEnd(TokenType.Redirect);
                                _state = State.Normal;
                            }
                            else // End of token
                            {
                                TokenEnd(TokenType.Redirect); // Just >

                                // Process the character in normal state
                                _state = State.Normal;
                                reIterateCharacter = true;
                            }
                            break;

                        default:
                            throw new Exception($"Internal error - unhandled tokenizing state {_state} at position {charNo}.");
                    }

                    if (!reIterateCharacter)
                        charNo++;
                }
                while (reIterateCharacter);
            }

            // All characters iterated - handle end of string
            switch (_state)
            {
                case State.Normal:
                    // OK, do nothing
                    break;

                case State.TokenUnquoted:
                case State.TokenOne:
                case State.TokenTwo:
                    TokenEnd(TokenNameStartsLikeOptionName() ? TokenType.OptionName : TokenType.Value);
                    break;

                case State.TokenQuotedFoundQuote:
                    TokenEnd(TokenType.Value);
                    break;

                case State.TokenQuoted:
                    throw new Exception($"Missing ending quote of '{_currentToken}'.");

                case State.TokenOneRedirect:
                case State.TokenTwoRedirect:
                case State.TokenRedirect:
                    TokenEnd(TokenType.Redirect);
                    break;

                default:
                    throw new Exception($"Internal error - unhandled tokenizing state {_state} at last position.");
            }

            // Return tokens grouped in commands and redirects
            (var commands, var redirects) = GetCommandGroupedTokens(fullCommandLine);
            return (commands:  commands.Select(cmd => (tokens: cmd.tokens.AsEnumerable(), cmd.position, cmd.text)).ToList(),
                    redirects: redirects.Select(cmd => (tokens: cmd.tokens.AsEnumerable(), cmd.position, cmd.text)).ToList());
        }

        private void TokenBeginOrAppend(char? ch, int position)
        {
            if (ch.HasValue)
                _currentToken.Append(ch.Value);

            if (!_currentTokenStartPosition.HasValue)
                _currentTokenStartPosition = position;
        }

        private void TokenEnd(TokenType type)
        {
            if (_currentToken.Length > 0)
            {
                _tokens.Add(new CommandLineToken(type, _currentToken.ToString(), _currentTokenStartPosition ?? 0));
                _currentToken.Clear();
            }

            _currentTokenStartPosition = null;
        }

        private bool TokenNameStartsLikeOptionName()
        {
            var token = _currentToken.ToString();

            if (string.IsNullOrEmpty(token))
                return false;

            switch (token[0])
            {
                case '-':
                case '/':
                    return true;

                default:
                    return false;
            }
        }

        private (IEnumerable<(List<CommandLineToken> tokens, int position, string text)> commands,
                 IEnumerable<(List<CommandLineToken> tokens, int position, string text)> redirects)
            GetCommandGroupedTokens(string commandLine)
        {
            int lastGroupStartPosition = 1;

            var commands = new List<(List<CommandLineToken> tokens, int position, string text)>();
            var redirects = new List<(List<CommandLineToken> tokens, int position, string text)>();

            var commandTokens = new List<CommandLineToken>();
            var redirectTokens = new List<CommandLineToken>();

            void CheckCurrentRedirectTokens(CommandLineToken subsequentToken)
            {
                if (redirectTokens.Count < 2)
                {
                    if (subsequentToken != null)
                        throw new Exception($"Redirect '{redirectTokens[0].Content}' needs a source/target - error at token '{subsequentToken.Content}', position {subsequentToken.Position}.");
                    else
                        throw new Exception($"Redirect '{redirectTokens[0].Content}' needs a source/target.");
                }
                else if (redirectTokens.Count > 2)
                {
                    throw new Exception($"Redirect '{redirectTokens[0].Content}' can only have one source/target - error at token '{redirectTokens[2].Content}', position {redirectTokens[2].Position}.");
                }
            }

            foreach (var token in _tokens)
            {
                if (token.Type == TokenType.VerticalBar)
                {
                    if (redirects.Any() || redirectTokens.Any())
                    {
                        throw new Exception($"Input/output redirects must be after commands - error at token '{token.Content}', position {token.Position}.");
                    }

                    // End of previous command
                    if (commandTokens.Any())
                    {
                        lastGroupStartPosition = commandTokens.First().Position;
                        var cmdText = commandLine.Substring(lastGroupStartPosition - 1, token.Position - lastGroupStartPosition).Trim();
                        commands.Add((tokens: commandTokens, position: lastGroupStartPosition, text: cmdText));

                        // Prepare for next command
                        commandTokens = new List<CommandLineToken>();
                    }
                }
                else if (token.Type == TokenType.Redirect)
                {
                    // End of previous redirect, if any
                    if (redirectTokens.Any())
                    {
                        CheckCurrentRedirectTokens(token);

                        lastGroupStartPosition = redirectTokens.Any() ? redirectTokens.First().Position : token.Position;
                        var redirText = commandTokens.Any() ? commandLine.Substring(lastGroupStartPosition - 1, token.Position - lastGroupStartPosition).Trim() : string.Empty;
                        redirects.Add((tokens: redirectTokens, position: lastGroupStartPosition, text: redirText));

                        // Prepare for next redirect
                        redirectTokens = new List<CommandLineToken>();
                    }

                    // Add first token of the new redirect
                    redirectTokens.Add(token);
                }
                else
                {
                    if (redirects.Any() || redirectTokens.Any())
                    {
                        redirectTokens.Add(token);
                    }
                    else
                    {
                        commandTokens.Add(token);
                    }
                }
            }

            // Add the last bunch of command tokens if any, or if no tokens exist at all, add at least one empty bunch
            if (commandTokens.Any() || !_tokens.Any())
            {
                if (commandTokens.Any())
                    lastGroupStartPosition = commandTokens.First().Position;

                var cmdText = commandTokens.Any() ? commandLine.Substring(lastGroupStartPosition - 1).Trim() : string.Empty;

                commands.Add((tokens: commandTokens, position: lastGroupStartPosition, text: cmdText));
            }

            // Add the last bunch of redirect tokens if any
            if (redirectTokens.Any())
            {
                CheckCurrentRedirectTokens(redirectTokens.Count > 2 ? redirectTokens[2] : null);

                lastGroupStartPosition = redirectTokens.First().Position;

                var redirText = commandTokens.Any() ? commandLine.Substring(lastGroupStartPosition - 1).Trim() : string.Empty;

                redirects.Add((tokens: redirectTokens, position: lastGroupStartPosition, text: redirText));
            }

            return (commands, redirects);
        }
    }
}
