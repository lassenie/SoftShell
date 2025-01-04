using System;
using System.Collections.Generic;
using System.Text;

namespace SoftShell
{
    /// <summary>
    /// Represents a logical group of SoftShell commands.
    /// </summary>
    public sealed class CommandGroup
    {
        /// <summary>
        /// Prefix that can be used to fully qualify commands in the group, using the form groupprefix.commandname .
        /// Full qualification of a command is needed if two commands in different groups have the same name.
        /// </summary>
        public string Prefix { get; }

        /// <summary>
        /// Name of the command.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Is this the the core command group?
        /// </summary>
        public bool IsCore { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="prefix">Prefix that can be used to fully qualify commands in the group, using the form groupprefix.commandname .</param>
        /// <param name="name">Name of the command.</param>
        /// <param name="isCore">Is this the the core command group?</param>
        internal CommandGroup(string prefix, string name, bool isCore)
        {
            Prefix = prefix ?? string.Empty;
            Name = name ?? throw new ArgumentNullException(name);
            IsCore = isCore;
        }
    }
}
