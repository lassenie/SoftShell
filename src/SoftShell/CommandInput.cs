using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SoftShell
{
    /// <summary>
    /// Arguments, i.e. parameter values, provided for a command execution
    /// </summary>
    public class CommandArgs : CommandInput
    {
        /// <summary>
        /// Constructor. Creates an empty collection of arguments.
        /// </summary>
        public CommandArgs() : base(Enumerable.Empty<KeyValuePair<string, object>>())
        {
        }

        /// <summary>
        /// Constructor. Creates a collection of arguments based on given name/value pairs.
        /// </summary>
        /// <param name="input">Name/value pairs of the parameters.</param>
        public CommandArgs(IEnumerable<KeyValuePair<string, object>> input) : base(input)
        {
        }

        /// <summary>
        /// Gets the argument of the given index.
        /// </summary>
        /// <param name="index">Zero-based index of the argument.</param>
        /// <returns>The argument.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the argument with the given index wasn't found.</exception>
        public object Get(int index)
        {
            if (TryGet(index, out object value))
                return value;

            throw new KeyNotFoundException($"Argument with index {index} not found.");
        }

        /// <summary>
        /// Gets the typed argument of the given index.
        /// </summary>
        /// <param name="index">Zero-based index of the argument.</param>
        /// <returns>The typed argument.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the argument with the given index and type wasn't found.</exception>
        public T GetAs<T>(int index)
        {
            return (T)Get(index);
        }

        /// <summary>
        /// Tries to get the argument of the given index.
        /// </summary>
        /// <param name="name">Zero-based index of the argument.</param>
        /// <param name="value">Argument if found - otherwise null.</param>
        /// <returns>True if the argument was found.</returns>
        public bool TryGet(int index, out object value)
        {
            if (index >= 0 && index < _list.Count)
            {
                value = _list[index].Value;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Tries to get a typed argument of the given index.
        /// </summary>
        /// <param name="name">Zero-based index of the argument.</param>
        /// <param name="value">Typed argument if found - otherwise the default value of the type.</param>
        /// <returns>True if the argument was found with the right type.</returns>
        public bool TryGetAs<T>(int index, out T value)
        {
            if (index >= 0 && index < _list.Count)
            {
                if (_list[index].Value is T objVal)
                {
                    value = objVal;
                    return true;
                }
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Gets the argument of the given index.
        /// </summary>
        /// <param name="index">Zero-based index of the argument.</param>
        /// <returns>The argument.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the argument with the given index wasn't found.</exception>
        public object this[int index]
        {
            get => Get(index);
        }
    }

    /// <summary>
    /// Options provided for a command execution
    /// </summary>
    public class CommandOptions : CommandInput
    {
        /// <summary>
        /// Constructor. Creates an empty collection of option values.
        /// </summary>
        public CommandOptions() : base(Enumerable.Empty<KeyValuePair<string, object>>())
        {
        }

        /// <summary>
        /// Constructor. Creates a collection of option values based on given name/value pairs.
        /// </summary>
        /// <param name="input">Name/value pairs of the options.</param>
        public CommandOptions(IEnumerable<KeyValuePair<string, object>> input) : base(input)
        {
        }

        /// <summary>
        /// Checks if a flag option is set, i.e. if a Boolean option is true.
        /// </summary>
        /// <param name="name">Name of the option.</param>
        /// <returns>True if the flag is set, otherwise false.</returns>
        public bool HasFlag(string name)
        {
            return TryGet(name, out object valObj) && valObj is bool isTrue && isTrue;
        }
    }

    /// <summary>
    /// Base class for arguments or option values provided for a command execution.
    /// </summary>
    public abstract class CommandInput : IEnumerable<(string name, object value)>
    {
        protected List<KeyValuePair<string, object>> _list;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="input">Input parameter/option name and value pairs.</param>
        public CommandInput(IEnumerable<KeyValuePair<string, object>> input)
        {
            _list = input?.ToList() ?? throw new ArgumentNullException(nameof(input));
        }

        /// <summary>
        /// Gets a parameter/option value with a given name.
        /// </summary>
        /// <param name="name">Name of the parameter/option value.</param>
        /// <returns>Value of the parameter/option.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the argument/option value of the given name wasn't found.</exception>
        public object Get(string name)
        {
            if (TryGet(name, out object value))
                return value;

            throw new KeyNotFoundException($"Input item with name {name} not found.");
        }

        /// <summary>
        /// Gets a typed parameter/option value with a given name.
        /// </summary>
        /// <param name="name">Name of the parameter/option value.</param>
        /// <returns>Typed value of the parameter/option.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the argument/option value of the given name and type wasn't found.</exception>
        public T GetAs<T>(string name)
        {
            return (T)Get(name);
        }

        /// <summary>
        /// Tries to get a parameter/option value with a given name.
        /// </summary>
        /// <param name="name">Name of the parameter/option value.</param>
        /// <param name="value">Value of the parameter/option if found - otherwise null.</param>
        /// <returns>True if the value was found.</returns>
        public bool TryGet(string name, out object value)
        {
            var item = _list.FirstOrDefault(kv => string.Equals(kv.Key, name, StringComparison.InvariantCultureIgnoreCase));

            value = item.Value;

            return item.Key != null;
        }

        /// <summary>
        /// Tries to get a typed parameter/option value with a given name.
        /// </summary>
        /// <param name="name">Name of the parameter/option value.</param>
        /// <param name="value">Typed value of the parameter/option if found - otherwise the default value of the type.</param>
        /// <returns>True if the value was found with the right type.</returns>
        public bool TryGetAs<T>(string name, out T value)
        {
            if (TryGet(name, out var obj) && obj is T objVal)
            {
                value = objVal;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Gets a parameter/option value with a given name.
        /// </summary>
        /// <param name="name">Name of the parameter/option value.</param>
        /// <returns>Value of the parameter/option.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the argument/option value of the given name wasn't found.</exception>
        public object this[string name]
        {
            get => Get(name);
        }

        /// <summary>
        /// Gets an enumerator of names/values.
        /// </summary>
        /// <returns>Enumerator of names/values.</returns>
        public IEnumerator<(string name, object value)> GetEnumerator()
        {
            foreach (var kv in _list)
                yield return (name: kv.Key, value: kv.Value);
        }

        /// <summary>
        /// Gets an enumerator of names/values.
        /// </summary>
        /// <returns>Enumerator of names/values.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
