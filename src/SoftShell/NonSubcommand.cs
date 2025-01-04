using System;
using System.Collections.Generic;
using System.Text;

namespace SoftShell
{
    internal class NonSubcommand : Subcommand
    {
        public NonSubcommand(string description) : base(string.Empty, description)
        {
        }
    }
}
