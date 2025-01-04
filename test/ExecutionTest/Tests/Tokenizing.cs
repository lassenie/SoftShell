using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SoftShell;
using SoftShell.Parsing;

namespace ExecutionTest.Tests
{
    public class Tokenizing
    {
        [Fact]
        public void TestBasicTokens()
        {
            CheckTokenizingException("", typeof(ArgumentException), "Empty command line. (Parameter 'fullCommandLine')");

            CheckTokenizingOk("A", (TokenType.Value, "A", 1));
            CheckTokenizingOk("1æ", (TokenType.Value, "1æ", 1));
            CheckTokenizingOk("Abc_9@", (TokenType.Value, "Abc_9@", 1));
            CheckTokenizingOk(" A", (TokenType.Value, "A", 2));
            CheckTokenizingOk("\" A\"", (TokenType.Value, " A", 1));
            CheckTokenizingOk("\"-A\"", (TokenType.Value, "-A", 1));
            CheckTokenizingException("A\"B", typeof(Exception), "Found quote character in token at position 2.");

            CheckTokenizingOk("-A", (TokenType.OptionName, "-A", 1));
            CheckTokenizingOk(" /0z", (TokenType.OptionName, "/0z", 2));

            CheckTokenizingOk("A -B", (TokenType.Value, "A", 1), (TokenType.OptionName, "-B", 3));
            CheckTokenizingOk("A -B=", (TokenType.Value, "A", 1), (TokenType.OptionName, "-B", 3), (TokenType.EqualSign, "=", 5));
            CheckTokenizingOk("A -B=C", (TokenType.Value, "A", 1), (TokenType.OptionName, "-B", 3), (TokenType.EqualSign, "=", 5), (TokenType.Value, "C", 6));
            CheckTokenizingOk("A /B = C", (TokenType.Value, "A", 1), (TokenType.OptionName, "/B", 3), (TokenType.EqualSign, "=", 6), (TokenType.Value, "C", 8));
            CheckTokenizingOk("A - B = C", (TokenType.Value, "A", 1), (TokenType.OptionName, "-", 3), (TokenType.Value, "B", 5), (TokenType.EqualSign, "=", 7), (TokenType.Value, "C", 9));
            CheckTokenizingOk("A -B=\"C\"", (TokenType.Value, "A", 1), (TokenType.OptionName, "-B", 3), (TokenType.EqualSign, "=", 5), (TokenType.Value, "C", 6));
        }

        [Fact]
        public void TestPipingTokens()
        {
            CheckTokenizingOk("|");
            CheckTokenizingOk(" | ");
            CheckTokenizingOk("||");
            CheckTokenizingOk("| |");
            CheckTokenizingOk("A|", (TokenType.Value, "A", 1));
            CheckTokenizingOk("|A", (TokenType.Value, "A", 2));
            CheckTokenizingOk("|A|", (TokenType.Value, "A", 2));
            CheckTokenizingOk("A|B", (TokenType.Value, "A", 1), (TokenType.VerticalBar, "|", 0), (TokenType.Value, "B", 3));
            CheckTokenizingOk("A | B", (TokenType.Value, "A", 1), (TokenType.VerticalBar, "|", 0), (TokenType.Value, "B", 5));
            CheckTokenizingOk("A | -B", (TokenType.Value, "A", 1), (TokenType.VerticalBar, "|", 0), (TokenType.OptionName, "-B", 5));
            CheckTokenizingOk("A |/B", (TokenType.Value, "A", 1), (TokenType.VerticalBar, "|", 0), (TokenType.OptionName, "/B", 4));
            CheckTokenizingOk("A |\" B\"", (TokenType.Value, "A", 1), (TokenType.VerticalBar, "|", 0), (TokenType.Value, " B", 4));
            CheckTokenizingOk("A| \" B\"", (TokenType.Value, "A", 1), (TokenType.VerticalBar, "|", 0), (TokenType.Value, " B", 4));
        }

        [Fact]
        public void TestRedirectTokens()
        {
            // Output redirect
            CheckTokenizingOk("A>X", (TokenType.Value, "A", 1), (TokenType.Redirect, ">", 2), (TokenType.Value, "X", 3));
            CheckTokenizingOk("A >X", (TokenType.Value, "A", 1), (TokenType.Redirect, ">", 3), (TokenType.Value, "X", 4));
            CheckTokenizingOk("A > X", (TokenType.Value, "A", 1), (TokenType.Redirect, ">", 3), (TokenType.Value, "X", 5));
            CheckTokenizingOk("A 1>X", (TokenType.Value, "A", 1), (TokenType.Redirect, "1>", 3), (TokenType.Value, "X", 5));
            CheckTokenizingOk("A 1> X", (TokenType.Value, "A", 1), (TokenType.Redirect, "1>", 3), (TokenType.Value, "X", 6));
            CheckTokenizingOk("A>\" X\"", (TokenType.Value, "A", 1), (TokenType.Redirect, ">", 2), (TokenType.Value, " X", 3));
            CheckTokenizingOk("A1>X", (TokenType.Value, "A1", 1), (TokenType.Redirect, ">", 3), (TokenType.Value, "X", 4));
            CheckTokenizingOk("A2>X", (TokenType.Value, "A2", 1), (TokenType.Redirect, ">", 3), (TokenType.Value, "X", 4));
            CheckTokenizingOk("A 3>X", (TokenType.Value, "A", 1), (TokenType.Value, "3", 3), (TokenType.Redirect, ">", 4), (TokenType.Value, "X", 5));

            // Error output redirect
            CheckTokenizingOk("A 2>X", (TokenType.Value, "A", 1), (TokenType.Redirect, "2>", 3), (TokenType.Value, "X", 5));
            CheckTokenizingOk("A 2> X", (TokenType.Value, "A", 1), (TokenType.Redirect, "2>", 3), (TokenType.Value, "X", 6));

            // Input redirect
            CheckTokenizingOk("A<X", (TokenType.Value, "A", 1), (TokenType.Redirect, "<", 2), (TokenType.Value, "X", 3));
            CheckTokenizingOk("A <X", (TokenType.Value, "A", 1), (TokenType.Redirect, "<", 3), (TokenType.Value, "X", 4));
            CheckTokenizingOk("A < X", (TokenType.Value, "A", 1), (TokenType.Redirect, "<", 3), (TokenType.Value, "X", 5));

            // Redirect before commands
            CheckTokenizingException("A>X|", typeof(Exception), "Input/output redirects must be after commands - error at token '|', position 4.");
            CheckTokenizingException("A>X B", typeof(Exception), "Redirect '>' can only have one source/target - error at token 'B', position 5.");
        }

        private void CheckTokenizingOk(string inputString,
                                     params (TokenType type, string content, int position)[] expectedTokens)
        {
            static IEnumerable<CommandLineToken> FlattenCommandTokens(IEnumerable<(IEnumerable<CommandLineToken> tokens, int position, string text)> commands)
            {
                if (commands.Any())
                {
                    var commandList = commands.ToList();

                    // Tokens of first command
                    foreach (var token in commandList[0].tokens)
                        yield return token;

                    // Tokens of subsequent commands, separated by fake piping tokens
                    for (int i = 1; i < commandList.Count; i++)
                    {
                        yield return new CommandLineToken(TokenType.VerticalBar, "|", 0); // Fake token to indicate separation of commands

                        foreach (var token in commandList[i].tokens)
                            yield return token;
                    }

                }

                
            }

            var tokenizer = new CommandLineTokenizer();

            var tokenizingResult = tokenizer.Tokenize(inputString);

            var commandTokens = FlattenCommandTokens(tokenizingResult.commands).ToList();
            var redirectTokens = tokenizingResult.redirects.SelectMany(cmd => cmd.tokens).ToList();

            var actualTokens = commandTokens.Concat(redirectTokens).Select(t => (type: t.Type, content: t.Content, position: t.Position)).ToList();

            Assert.Equal(expectedTokens, actualTokens);
        }

        private void CheckTokenizingException(string inputString, Type exceptionType, string exceptionMessage)
        {
            bool gotNoException = false;

            var tokenizer = new CommandLineTokenizer();

            try
            {
                tokenizer.Tokenize(inputString);

                gotNoException = true;
            }
            catch (Exception ex)
            {
                Assert.Equal(exceptionType, ex.GetType());
                Assert.Equal(exceptionMessage, ex.Message);
            }

            if (gotNoException)
                throw new Exception($"Got no exception - expected {exceptionType} with message '{exceptionMessage}'");
        }
    }
}
