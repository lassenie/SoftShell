using SoftShell.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace SoftShell
{
    public class Subcommand
    {
        private List<(string name, string description, Func<string, object> toObject)> _requiredParameters = new List<(string name, string description, Func<string, object> toObject)>();
        private List<(string name, string description, Func<string, object> toObject)> _optionalParameters = new List<(string name, string description, Func<string, object> toObject)>();

        private Dictionary<string, (string description, bool hasValue, Func<string, object> toObject)> _requiredOptions = new Dictionary<string, (string description, bool hasValue, Func<string, object> toObject)>();
        private Dictionary<string, (string description, bool hasValue, Func<string, object> toObject)> _options = new Dictionary<string, (string description, bool hasValue, Func<string, object> toObject)>();

        public string Name { get; }

        public string Description { get; }

        public bool IsSubcommand => !string.IsNullOrEmpty(Name);

        public ImmutableList<(string name, string description, Func<string, object> toObject)> RequiredParameters => ImmutableList<(string name, string description, Func<string, object> toObject)>.Empty.AddRange(_requiredParameters);
        public ImmutableList<(string name, string description, Func<string, object> toObject)> OptionalParameters => ImmutableList<(string name, string description, Func<string, object> toObject)>.Empty.AddRange(_optionalParameters);

        public ImmutableDictionary<string, (string description, bool hasValue, Func<string, object> toObject)> RequiredOptions => ImmutableDictionary<string, (string description, bool hasValue, Func<string, object> toObject)>.Empty.AddRange(_requiredOptions);
        public ImmutableDictionary<string, (string description, bool hasValue, Func<string, object> toObject)> OptionalOptions => ImmutableDictionary<string, (string description, bool hasValue, Func<string, object> toObject)>.Empty.AddRange(_options);

        public Subcommand(string name, string description)
        {
            // Allow the name to be empty for a non-subcommand, but not null
            if (name is null)
                throw new ArgumentNullException(nameof(name));

            Name = new string(name?.ToLowerInvariant().Where(ch => !char.IsWhiteSpace(ch)).ToArray() ?? new char[0]);

            Description = description ?? throw new ArgumentNullException(nameof(description));
        }

        public Subcommand HasRequiredParameter(string name, string description, Func<string, object> toObject)
        {
            AddRequiredParameter(name, description, toObject);
            return this;
        }

        public Subcommand HasOptionalParameter(string name, string description, Func<string, object> toObject)
        {
            AddOptionalParameter(name, description, toObject);
            return this;
        }

        public Subcommand HasRequiredValueOption(string name, string description, Func<string, object> toObject)
        {
            AddRequiredValueOption(name, description, toObject);
            return this;
        }

        public Subcommand HasValueOption(string name, string description, Func<string, object> toObject)
        {
            AddValueOption(name, description, toObject);
            return this;
        }

        public Subcommand HasFlagOption(string name, string description)
        {
            AddFlagOption(name, description);
            return this;
        }

        /// <summary>
        /// Helper method to get an exception message text for an unknown subcommand.
        /// </summary>
        /// <param name="subcommandName">Given unknown subcommand.</param>
        /// <param name="candidates">Possible subcommands.</param>
        /// <returns>Exception message text.</returns>
        public static string GetUnknownSubommandExceptionText(string subcommandName, IEnumerable<Subcommand> candidates)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Unknown subcommand '{subcommandName}'. Possible subcommands:");

            foreach (var line in TextFormatting.GetAlignedColumnStrings(candidates,
                                                                        " ",
                                                                        ("", subcmd => $"  {subcmd.Name}:", TextAlignment.Start),
                                                                        ("", subcmd => subcmd.Description,  TextAlignment.Start)))
            {
                sb.AppendLine(line);
            }

            return sb.ToString().TrimEnd(new[] { '\r', '\n' });
        }

        private void AddRequiredParameter(string name, string description, Func<string, object> toObject)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Missing parameter name.", nameof(name));

            if (_optionalParameters.Any())
                throw new InvalidOperationException($"Required parameter '{name}' could not be declared - all required parameters must be declared before optional parameters.");

            _requiredParameters.Add((name.ToLowerInvariant(), description ?? string.Empty, toObject ?? (val => val)));
        }

        private void AddOptionalParameter(string name, string description, Func<string, object> toObject)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Missing parameter name.", nameof(name));

            _optionalParameters.Add((name.ToLowerInvariant(), description ?? string.Empty, toObject ?? (val => val)));
        }

        private void AddRequiredValueOption(string name, string description, Func<string, object> toObject)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Missing option name.", nameof(name));
            if (name.Any(ch => char.IsWhiteSpace(ch) || char.IsControl(ch))) throw new ArgumentException($"Invalid option name '{name}'.", nameof(name));

            _requiredOptions[name.ToLowerInvariant()] = (description ?? string.Empty, true, toObject ?? (val => val));
        }

        private void AddValueOption(string name, string description, Func<string, object> toObject)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Missing option name.", nameof(name));
            if (name.Any(ch => char.IsWhiteSpace(ch) || char.IsControl(ch))) throw new ArgumentException($"Invalid option name '{name}'.", nameof(name));

            _options[name.ToLowerInvariant()] = (description ?? string.Empty, true, toObject ?? (val => val));
        }

        private void AddFlagOption(string name, string description)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Missing option name.", nameof(name));
            if (name.Any(ch => char.IsWhiteSpace(ch) || char.IsControl(ch))) throw new ArgumentException($"Invalid option name '{name}'.", nameof(name));

            _options[name.ToLowerInvariant()] = (description ?? string.Empty, false, _ => true);
        }
    }
}
