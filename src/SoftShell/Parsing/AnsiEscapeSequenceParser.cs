using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Xml;

namespace SoftShell.Parsing
{
    /// <summary>
    /// ANSI escape sequence parser for terminal emulation. Partly implemented as needed for SoftShell.
    // Implemented according to: https://vt100.net/emu/dec_ansi_parser
    // (web page snapshot in this Git repo under: doc/ANSI parser)
    /// </summary>
    public sealed class AnsiEscapeSequenceParser
    {
        /// <summary>
        /// States as used in the diagram.
        /// </summary>
        private enum State
        {
            Ground = 0,
            Escape,
            EscapeIntermediate,
            CsiEntry,
            CsiParam,
            CsiIgnore,
            CsiIntermediate,
            SosPmApcString,
            OscString,
            DcsEntry,
            DcsIntermediate,
            DcsParam,
            DcsIgnore,
            DcsPassthrough
        }

        /// <summary>
        /// Current state.
        /// </summary>
        private State _state = State.Ground;

        /// <summary>
        /// Collected bytes as indicated in the diagram with "... / collect".
        /// </summary>
        private List<byte> _collectedBytes = new List<byte>();

        /// <summary>
        /// Parameter bytes as indicated in the diagram with "... / param".
        /// </summary>
        private List<byte> _parameterBytes = new List<byte>();

        /// <summary>
        /// Handles another byte received from the terminal.
        /// </summary>
        /// <param name="value">Byte value received.</param>
        /// <returns>Sequence of interpreted actions and characters (where relevant). May be empty if no action.</returns>
        public IEnumerable<(KeyAction action, char character)> HandleByte(byte value)
        {
            // Received byte handled in any state (indicated by "anywhere" in the diagram)?
            if (HandleStateIndependentValue(value, out var output))
            {
                return output;
            }

            switch (_state)
            {
                case State.Ground:
                    return HandleGroundByte(value);

                case State.Escape:
                    return HandleEscapeByte(value);

                case State.EscapeIntermediate:
                    return HandleEscapeIntermediateByte(value);

                case State.CsiEntry:
                    return HandleCsiEntryByte(value);

                case State.CsiParam:
                    return HandleCsiParamByte(value);

                case State.CsiIgnore:
                    return HandleCsiIgnoreByte(value);

                case State.CsiIntermediate:
                    return HandleCsiIntermediateByte(value);

                case State.SosPmApcString:
                    return HandleSosPmApcStringByte(value);

                case State.OscString:
                    return HandleOscStringByte(value);

                case State.DcsEntry:
                    return HandleDcsEntryByte(value);

                case State.DcsIntermediate:
                    return HandleDcsIntermediateByte(value);

                case State.DcsParam:
                    return HandleDcsParamByte(value);

                case State.DcsIgnore:
                    return HandleDcsIgnoreByte(value);

                case State.DcsPassthrough:
                    return HandleDcsPassthroughByte(value);

                default:
                    throw new InvalidOperationException($"Unhandled state {_state}");
            }
        }

        private IEnumerable<(KeyAction action, char character)> HandleGroundByte(byte value)
        {
            if (IsValue(value, Range(0x00, 0x17), Val(0x19), Range(0x1C, 0x1F)))
            {
                return Execute(value);
            }
            else if (IsValue(value, Range(0x20, 0x7F)))
            {
                return Print(value);
            }

            return Enumerable.Empty<(KeyAction action, char character)>();
        }

        private IEnumerable<(KeyAction action, char character)> HandleEscapeByte(byte value)
        {
            if (IsValue(value, Range(0x00, 0x17), Val(0x19), Range(0x1C, 0x1F)))
            {
                return Execute(value);
            }
            else if (value == 0x5B) // [
            {
                EnterState(State.CsiEntry);
            }
            else if (IsValue(value, Range(0x20, 0x2F)))
            {
                Collect(value);
                EnterState(State.EscapeIntermediate);
            }
            else if (IsValue(value, Range(0x30, 0x4F), Range(0x51, 0x57), Val(0x59, 0x5A, 0x5C), Range(0x60, 0x7E)))
            {
                return EscDispatch(value);
            }
            else if (IsValue(value, Val(0x58, 0x5E, 0x5F)))
            {
                EnterState(State.SosPmApcString);
            }
            else if (value == 0x5D)
            {
                EnterState(State.OscString);
            }
            else if (value == 0x50)
            {
                EnterState(State.DcsEntry);
            }

            return Enumerable.Empty<(KeyAction action, char character)>();
        }

        private IEnumerable<(KeyAction action, char character)> HandleEscapeIntermediateByte(byte value)
        {
            if (IsValue(value, Range(0x00, 0x17), Val(0x19), Range(0x1C, 0x1F)))
            {
                return Execute(value);
            }
            else if (IsValue(value, Range(0x20, 0x2F)))
            {
                Collect(value);
            }
            else if (IsValue(value, Range(0x30, 0x7E)))
            {
                return EscDispatch(value);
            }

            return Enumerable.Empty<(KeyAction action, char character)>();
        }

        private IEnumerable<(KeyAction action, char character)> HandleCsiEntryByte(byte value)
        {
            if (IsValue(value, Range(0x00, 0x17), Val(0x19), Range(0x1C, 0x1F)))
            {
                return Execute(value);
            }
            else if (IsValue(value, Range(0x20, 0x2F)))
            {
                Collect(value);
                EnterState(State.CsiIntermediate);
            }
            else if (IsValue(value, Range(0x40, 0x7E)))
            {
                var output = CsiDispatch(value);
                EnterState(State.Ground);
                return output;
            }
            else if (IsValue(value, Range(0x30, 0x39), Val(0x3B)))
            {
                Param(value);
                EnterState(State.CsiParam);
            }
            else if (IsValue(value, Range(0x3C, 0x3F)))
            {
                Collect(value);
                EnterState(State.CsiParam);
            }
            else if (value == 0x3A)
            {
                EnterState(State.CsiIgnore);
            }

            return Enumerable.Empty<(KeyAction action, char character)>();
        }

        private IEnumerable<(KeyAction action, char character)> HandleCsiParamByte(byte value)
        {
            if (IsValue(value, Range(0x00, 0x17), Val(0x19), Range(0x1C, 0x1F)))
            {
                return Execute(value);
            }
            else if (IsValue(value, Range(0x20, 0x2F)))
            {
                Collect(value);
                EnterState(State.CsiIntermediate);
            }
            else if (IsValue(value, Range(0x40, 0x7E)))
            {
                var output = CsiDispatch(value);
                EnterState(State.Ground);
                return output;
            }
            else if (IsValue(value, Range(0x30, 0x39), Val(0x3B)))
            {
                Param(value);
            }
            else if (IsValue(value, Range(0x3C, 0x3F), Val(0x3A)))
            {
                EnterState(State.CsiIgnore);
            }

            return Enumerable.Empty<(KeyAction action, char character)>();
        }

        private IEnumerable<(KeyAction action, char character)> HandleCsiIgnoreByte(byte value)
        {
            if (IsValue(value, Range(0x00, 0x17), Val(0x19), Range(0x1C, 0x1F)))
            {
                return Execute(value);
            }
            else if (IsValue(value, Range(0x40, 0x7E)))
            {
                EnterState(State.Ground);
            }

            return Enumerable.Empty<(KeyAction action, char character)>();
        }

        private IEnumerable<(KeyAction action, char character)> HandleCsiIntermediateByte(byte value)
        {
            if (IsValue(value, Range(0x00, 0x17), Val(0x19), Range(0x1C, 0x1F)))
            {
                return Execute(value);
            }
            else if (IsValue(value, Range(0x20, 0x2F)))
            {
                Collect(value);
            }
            else if (IsValue(value, Range(0x40, 0x7E)))
            {
                var output = CsiDispatch(value);
                EnterState(State.Ground);
                return output;
            }
            else if (IsValue(value, Range(0x30, 0x3F)))
            {
                EnterState(State.CsiIgnore);
            }

            return Enumerable.Empty<(KeyAction action, char character)>();
        }

        private IEnumerable<(KeyAction action, char character)> HandleSosPmApcStringByte(byte value)
        {
            if (value == 0x9C)
            {
                EnterState(State.Ground);
            }

            return Enumerable.Empty<(KeyAction action, char character)>();
        }

        private IEnumerable<(KeyAction action, char character)> HandleOscStringByte(byte value)
        {
            if (IsValue(value, Range(0x20, 0x7F)))
            {
                return OscPut(value);
            }
            else if (value == 0x9C)
            {
                EnterState(State.Ground);
            }

            return Enumerable.Empty<(KeyAction action, char character)>();
        }

        private IEnumerable<(KeyAction action, char character)> HandleDcsEntryByte(byte value)
        {
            if (IsValue(value, Range(0x20, 0x2F)))
            {
                Collect(value);
                EnterState(State.DcsIntermediate);
            }
            else if (IsValue(value, Range(0x40, 0x7E)))
            {
                EnterState(State.DcsPassthrough);
            }
            else if (IsValue(value, Range(0x30, 0x39), Val(0x3B)))
            {
                Param(value);
                EnterState(State.DcsParam);
            }
            else if (IsValue(value, Range(0x3C, 0x3F)))
            {
                Collect(value);
                EnterState(State.DcsParam);
            }
            else if (value == 0x3A)
            {
                EnterState(State.DcsIgnore);
            }

            return Enumerable.Empty<(KeyAction action, char character)>();
        }

        private IEnumerable<(KeyAction action, char character)> HandleDcsIntermediateByte(byte value)
        {
            if (IsValue(value, Range(0x20, 0x2F)))
            {
                Collect(value);
            }
            else if (IsValue(value, Range(0x30, 0x3F)))
            {
                EnterState(State.DcsIgnore);
            }
            else if (IsValue(value, Range(0x40, 0x7E)))
            {
                EnterState(State.DcsPassthrough);
            }

            return Enumerable.Empty<(KeyAction action, char character)>();
        }

        private IEnumerable<(KeyAction action, char character)> HandleDcsParamByte(byte value)
        {
            if (IsValue(value, Range(0x30, 0x39), Val(0x3B)))
            {
                Param(value);
            }
            else if (IsValue(value, Range(0x40, 0x7E)))
            {
                EnterState(State.DcsPassthrough);
            }
            else if (IsValue(value, Range(0x20, 0x2F)))
            {
                Collect(value);
                EnterState(State.DcsIntermediate);
            }
            else if (IsValue(value, Val(0x3A), Range(0x3C, 0x3F)))
            {
                EnterState(State.DcsIgnore);
            }

            return Enumerable.Empty<(KeyAction action, char character)>();
        }

        private IEnumerable<(KeyAction action, char character)> HandleDcsIgnoreByte(byte value)
        {
            if (value == 0x9C)
            {
                EnterState(State.Ground);
            }

            return Enumerable.Empty<(KeyAction action, char character)>();
        }

        private IEnumerable<(KeyAction action, char character)> HandleDcsPassthroughByte(byte value)
        {
            if (IsValue(value, Range(0x00, 0x17), Val(0x19), Range(0x1C, 0x1F), Range(0x20, 0x7E)))
            {
                return Put(value);
            }
            else if (value == 0x9C)
            {
                EnterState(State.Ground);
            }

            return Enumerable.Empty<(KeyAction action, char character)>();
        }

        /// <summary>
        /// Handling of bytes independent of current state (indicated in the diagram with "anywhere").
        /// </summary>
        /// <param name="value">Byte value received.</param>
        /// <param name="output">Sequence of actions that should be returned (may be empty).</param>
        /// <returns>
        /// True if the byte value is handled according to the diagram.
        /// Byte values not handled in transitions from "anywhere" give False result and must be handled depending on current state.
        /// </returns>
        private bool HandleStateIndependentValue(byte value, out IEnumerable<(KeyAction action, char character)> output)
        {
            bool handled = true;

            output = null;

            if (IsValue(value, Val(0x18, 0x1A), Range(0x80, 0x8F), Range(0x91, 0x97), Val(0x99, 0x9A)))
            {
                Execute(value);
                EnterState(State.Ground);
            }
            else if (value == 0x9C)
            {
                EnterState(State.Ground);
            }
            else if (value == 0x1B)
            {
                EnterState(State.Escape);
            }
            else if (value == 0x9B)
            {
                EnterState(State.CsiEntry);
            }
            else if (IsValue(value, Val(0x98, 0x9E, 0x9F)))
            {
                EnterState(State.SosPmApcString);
            }
            else if (value == 0x9D)
            {
                EnterState(State.OscString);
            }
            else if (value == 0x90)
            {
                EnterState(State.DcsEntry);
            }
            else
            {
                handled = false;
            }

            output = output ?? Enumerable.Empty<(KeyAction action, char character)>();
            return handled;
        }

        private void EnterState(State newState)
        {
            if (newState == _state)
                return;

            switch (_state)
            {
                case State.OscString:
                    OscEnd();
                    break;

                case State.DcsPassthrough:
                    Unhook();
                    break;
            }

            switch (newState)
            {
                case State.Escape:
                case State.CsiEntry:
                case State.DcsEntry:
                    Clear();
                    break;

                case State.OscString:
                    OscStart();
                    break;

                case State.DcsPassthrough:
                    Hook();
                    break;
            }

            _state = newState;
        }

        #region Actions

        private void Clear()
        {
            _collectedBytes.Clear();
            _parameterBytes.Clear();
        }

        private void Collect(byte value)
        {
            _collectedBytes.Add(value);
        }

        private void Param(byte value)
        {
            _parameterBytes.Add(value);
        }

        private IEnumerable<(KeyAction action, char character)> Print(byte value)
        {
            return new[] { (KeyAction.Character, (char)value) };
        }

        private IEnumerable<(KeyAction action, char character)> Execute(byte value)
        {
            switch (value)
            {
                case 0x03: // Ctrl-C / ETX character (Cancel)
                    return new[] { (action: KeyAction.Character, character: (char)value) };

                case 0x04: // Ctrl-D / EOT character (EOF in Linux)
                    return new[] { (action: KeyAction.Character, character: (char)value) };

                case 0x08: // '\b'
                    return new[] { (action: KeyAction.Character, character: '\b') };

                case 0x0A: // '\n'
                    return new[] { (action: KeyAction.Character, character: '\n') };

                case 0x0D: // '\r'
                    return new[] { (action: KeyAction.Character, character: '\r') };

                case 0x1A: // Ctrl-Z / SUB character (EOF in Windows)
                    return new[] { (action: KeyAction.Character, character: (char)value) };
            }

            return Enumerable.Empty<(KeyAction action, char character)>();
        }

        private IEnumerable<(KeyAction action, char character)> CsiDispatch(byte value)
        {
            // CSI (Control Sequence Introducer) sequence complete

            switch (value)
            {
                case 0x41: // A
                    return new[] { (action: KeyAction.ArrowUp, character: '\0') };

                case 0x42: // B
                    return new[] { (action: KeyAction.ArrowDown, character: '\0') };

                case 0x43: // C
                    return new[] { (action: KeyAction.ArrowForward, character: '\0') };

                case 0x44: // D
                    return new[] { (action: KeyAction.ArrowBack, character: '\0') };

                case 0x46: // F
                    return new[] { (action: KeyAction.End, character: '\0') };

                case 0x48: // H
                    return new[] { (action: KeyAction.Home, character: '\0') };

                case 0x7E: // ~ (vt sequences)
                    switch (new string(_parameterBytes.Select(b => (char)b).ToArray()))
                    {
                        case "1":
                        case "7":
                            return new[] { (action: KeyAction.Home, character: '\0') };

                        case "4":
                        case "8":
                            return new[] { (action: KeyAction.End, character: '\0') };
                    }
                    break;
            }

            Clear();

            return Enumerable.Empty<(KeyAction action, char character)>();
        }

        private IEnumerable<(KeyAction action, char character)> EscDispatch(byte value)
        {
            // Escape sequence complete

            // Just ignore collected+param bytes for now
            Clear();

            // Escape sequences not supported yet

            return Enumerable.Empty<(KeyAction action, char character)>();
        }

        private void OscStart()
        {
        }

        private IEnumerable<(KeyAction action, char character)> OscPut(byte value)
        {
            return Enumerable.Empty<(KeyAction action, char character)>();
        }

        private void OscEnd()
        {
        }

        private void Hook()
        {
        }

        private IEnumerable<(KeyAction action, char character)> Put(byte value)
        {
            return Enumerable.Empty<(KeyAction action, char character)>();
        }

        private void Unhook()
        {
        }

        #endregion

        #region Helper methods

        bool IsValue(byte value, params IEnumerable<byte>[] valueCollections)
        {
            return valueCollections.Any(valueCollection => valueCollection.Contains(value));
        }

        IEnumerable<byte> Val(params byte[] values) => values;

        IEnumerable<byte> Range(byte min, byte max)
        {
            for (var value = min; value <= max; value++)
                yield return value;
        }

        #endregion
    }
}
