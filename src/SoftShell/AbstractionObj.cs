using System;
using System.Collections.Generic;
using System.Text;

namespace SoftShell
{
    /// <summary>
    /// A class for encapsuling abstract objects exchanged through command interfaces.
    /// </summary>
    /// <remarks>
    /// When implementing commands that interact with the host application, this class can be used for abstraction to allow unit testing.
    /// See examples of core commands using this, e.g. the <see cref="Commands.AssemblyCommand"/> which is unit tested using this abstraction
    /// in its <see cref="Commands.AssemblyCommand.IHost"/> interface The <see cref="Commands.AssemblyCommand.DefaultHost"/> class is the
    /// command's own default IHost implementation. A mock implementation can be injected when unit testing.
    /// </remarks>
    public class AbstractionObj
    {
        /// <summary>
        /// The encapsulated object.
        /// </summary>
        public object Object { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="obj">The object to encapsulate.</param>
        public AbstractionObj(object obj)
        {
            Object = obj;
        }
    }
}
