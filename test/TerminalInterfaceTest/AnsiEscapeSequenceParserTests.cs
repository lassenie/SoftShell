using System.Collections.Generic;
using System.Linq;

using SoftShell;
using SoftShell.Parsing;

namespace TerminalInterfaceTest
{
    /// <summary>
    /// Tests for <see cref="AnsiEscapeSequenceParser"/>, the input parser shared by the
    /// Telnet and SSH terminal interfaces (both feed received bytes through it).
    /// </summary>
    public class AnsiEscapeSequenceParserTests
    {
        private readonly AnsiEscapeSequenceParser _parser = new AnsiEscapeSequenceParser();

        /// <summary>
        /// Feeds a sequence of bytes through the parser and returns the flattened result.
        /// </summary>
        private List<(KeyAction action, char character)> Feed(params byte[] bytes)
            => bytes.SelectMany(b => _parser.HandleByte(b)).ToList();

        private List<(KeyAction action, char character)> Feed(string ascii)
            => Feed(ascii.Select(c => (byte)c).ToArray());

        [Fact]
        public void PrintableCharacters_AreReturnedAsCharacters()
        {
            var result = Feed("Hi!");

            Assert.Equal(
                new[]
                {
                    (KeyAction.Character, 'H'),
                    (KeyAction.Character, 'i'),
                    (KeyAction.Character, '!'),
                },
                result);
        }

        [Theory]
        [InlineData((byte)0x03, (char)0x03)] // Ctrl-C / ETX
        [InlineData((byte)0x04, (char)0x04)] // Ctrl-D / EOT
        [InlineData((byte)0x08, '\b')]       // Backspace
        [InlineData((byte)0x0A, '\n')]       // Line feed
        [InlineData((byte)0x0D, '\r')]       // Carriage return
        public void ControlCharacters_AreExecutedAsCharacters(byte input, char expected)
        {
            var result = Feed(input);

            Assert.Equal(new[] { (KeyAction.Character, expected) }, result);
        }

        [Theory]
        [InlineData((byte)0x18)] // CAN
        [InlineData((byte)0x1A)] // SUB (Ctrl-Z)
        public void SequenceAbortingControlCharacters_ProduceNoOutput(byte input)
        {
            // 0x18/0x1A are handled by the parser's state-independent ("anywhere") path, which
            // aborts any in-progress sequence and returns to ground without emitting a character.
            var result = Feed(input);

            Assert.Empty(result);
        }

        [Theory]
        [InlineData((byte)0x41, KeyAction.ArrowUp)]      // ESC [ A
        [InlineData((byte)0x42, KeyAction.ArrowDown)]    // ESC [ B
        [InlineData((byte)0x43, KeyAction.ArrowForward)] // ESC [ C
        [InlineData((byte)0x44, KeyAction.ArrowBack)]    // ESC [ D
        [InlineData((byte)0x46, KeyAction.End)]          // ESC [ F
        [InlineData((byte)0x48, KeyAction.Home)]         // ESC [ H
        public void CsiCursorSequences_AreMappedToKeyActions(byte finalByte, KeyAction expected)
        {
            var result = Feed(0x1B, (byte)'[', finalByte);

            Assert.Equal(new[] { (expected, '\0') }, result);
        }

        [Theory]
        [InlineData("1", KeyAction.Home)]
        [InlineData("7", KeyAction.Home)]
        [InlineData("4", KeyAction.End)]
        [InlineData("8", KeyAction.End)]
        public void CsiVtSequences_AreMappedToKeyActions(string parameter, KeyAction expected)
        {
            // ESC [ <param> ~
            var bytes = new List<byte> { 0x1B, (byte)'[' };
            bytes.AddRange(parameter.Select(c => (byte)c));
            bytes.Add((byte)'~');

            var result = Feed(bytes.ToArray());

            Assert.Equal(new[] { (expected, '\0') }, result);
        }

        [Fact]
        public void EscapeSequence_ProducesNoSpuriousCharacters()
        {
            // The bytes making up an arrow-key escape sequence must not leak through as
            // printable characters; only the final key action should be produced.
            var result = Feed(0x1B, (byte)'[', (byte)'A');

            Assert.Equal(new[] { (KeyAction.ArrowUp, '\0') }, result);
        }

        [Fact]
        public void TextInterleavedWithEscapeSequence_IsParsedCorrectly()
        {
            // "ab", Up arrow, "cd"
            var bytes = new List<byte> { (byte)'a', (byte)'b', 0x1B, (byte)'[', (byte)'A', (byte)'c', (byte)'d' };

            var result = Feed(bytes.ToArray());

            Assert.Equal(
                new[]
                {
                    (KeyAction.Character, 'a'),
                    (KeyAction.Character, 'b'),
                    (KeyAction.ArrowUp, '\0'),
                    (KeyAction.Character, 'c'),
                    (KeyAction.Character, 'd'),
                },
                result);
        }

        [Fact]
        public void IncompleteEscapeSequence_ProducesNoOutput()
        {
            // ESC [ with no final byte yet: nothing should be emitted.
            var result = Feed(0x1B, (byte)'[');

            Assert.Empty(result);
        }
    }
}
