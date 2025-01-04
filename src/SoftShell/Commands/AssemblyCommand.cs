using SoftShell.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SoftShell.Commands
{
    /// <summary>
    /// Command for providing assembly information. See command help info for further details.
    /// </summary>
    public partial class AssemblyCommand : StdCommand
    {
        /// <summary>
        /// Type for passing an abstract assembly object (supports unit testing).
        /// </summary>
        public class AssemblyObj : AbstractionObj { public AssemblyObj(object asm) : base(asm) { } }

        /// <summary>
        /// Type for passing an abstract module object (supports unit testing).
        /// </summary>
        public class ModuleObj : AbstractionObj { public ModuleObj(object module) : base(module) { } }

        /// <summary>
        /// Type for passing an abstract type object (supports unit testing).
        /// </summary>
        public class TypeObj : AbstractionObj { public TypeObj(object type) : base(type) { } }

        /// <summary>
        /// Type for passing an abstract field info object (supports unit testing).
        /// </summary>
        public class FieldInfoObj : AbstractionObj { public FieldInfoObj(object field) : base(field) { } }

        /// <summary>
        /// Type for passing an abstract property info object (supports unit testing).
        /// </summary>
        public class PropertyInfoObj : AbstractionObj { public PropertyInfoObj(object prop) : base(prop) { } }

        /// <summary>
        /// Type for passing an abstract constructor info object (supports unit testing).
        /// </summary>
        public class ConstructorInfoObj : AbstractionObj { public ConstructorInfoObj(object ctor) : base(ctor) { } }

        /// <summary>
        /// Type for passing an abstract method info object (supports unit testing).
        /// </summary>
        public class MethodInfoObj : AbstractionObj { public MethodInfoObj(object method) : base(method) { } }

        /// <summary>
        /// Type for passing an abstract parameter info object (supports unit testing).
        /// </summary>
        public class ParameterInfoObj : AbstractionObj { public ParameterInfoObj(object parameter) : base(parameter) { } }

        /// <summary>
        /// Host interface for providing assembly-related information. Can be mocked for unit testing.
        /// </summary>
        public interface IHost
        {
            /// <summary>
            /// Gets the process's entry assembly as an abstract object.
            /// </summary>
            AssemblyObj GetEntryAssembly();

            /// <summary>
            /// Gets the process's assemblies, except the entry assembly, as abstract objects in name order.
            /// </summary>
            IEnumerable<AssemblyObj> GetOtherAssembliesInNameOrder(AssemblyObj entryAssembly);

            /// <summary>
            /// Gets assembly name info of a given abstract assembly object.
            /// </summary>
            /// <param name="assembly"></param>
            /// <returns></returns>
            AssemblyName GetAssemblyName(AssemblyObj assembly);

            /// <summary>
            /// Gets an abstract assembly object's modules as abstract objects in name order.
            /// </summary>
            IEnumerable<ModuleObj> GetModulesInNameOrder(AssemblyObj assembly);

            /// <summary>
            /// Gets the name of a given abstract module object.
            /// </summary>
            string GetModuleName(ModuleObj module);

            /// <summary>
            /// Gets types of a given abstract module object as abstract objects in name order.
            /// </summary>
            IEnumerable<TypeObj> GetModuleTypesInNameOrder(ModuleObj module);

            /// <summary>
            /// Gets the name of a given abstract type object.
            /// </summary>
            string GetTypeName(TypeObj type);

            /// <summary>
            /// Gets details of a given abstract type object.
            /// </summary>
            /// <returns>
            /// baseTypeFullName: Full name of the type's base type (null if none).
            /// interfaceFullNames: A collection of full names of interfaces that the type implements (empty collection if none).
            /// </returns>
            (string baseTypeFullName, IEnumerable<string> interfaceFullNames) GetTypeDetails(TypeObj type);

            /// <summary>
            /// Gets immediate nested types - as abstract type objects - of a given abstract type object.
            /// </summary>
            IEnumerable<TypeObj> GetNestedTypesInNameOrder(TypeObj type);

            /// <summary>
            /// Gets fields - as abstract field info objects in name order - of a given abstract type object.
            /// </summary>
            IEnumerable<FieldInfoObj> GetFieldsInNameOrder(TypeObj type);

            /// <summary>
            /// Gets info about a given abstract field info object.
            /// </summary>
            /// <returns>The type and name of the field.</returns>
            (Type type, string name) GetFieldInfo(FieldInfoObj field);

            /// <summary>
            /// Gets properties - as abstract property info objects in name order - of a given abstract type object.
            /// </summary>
            IEnumerable<PropertyInfoObj> GetPropertiesInNameOrder(TypeObj type);

            /// <summary>
            /// Gets info about a given abstract property info object.
            /// </summary>
            /// <returns>The type and name of the property.</returns>
            (Type type, string name) GetPropertyInfo(PropertyInfoObj prop);

            /// <summary>
            /// Gets additional details about a given abstract property info object.
            /// </summary>
            /// <returns>Whether the property can be read and/or written.</returns>
            (bool canRead, bool canWrite) GetPropertyDetails(PropertyInfoObj prop);

            /// <summary>
            /// Gets constructors - as abstract constructor info objects in name order - of a given abstract type object.
            /// </summary>
            IEnumerable<ConstructorInfoObj> GetConstructorsInNameOrder(TypeObj type);

            /// <summary>
            /// Gets info about a given abstract constructor info object.
            /// </summary>
            /// <returns>Whether the constructor is static, and the name of the constructor.</returns>
            (bool isStatic, string name) GetConstructorInfo(ConstructorInfoObj ctor);

            /// <summary>
            /// Gets additional details about a given abstract constructor info object.
            /// </summary>
            /// <returns>List of parameters for the constructor.</returns>
            IEnumerable<ParameterInfoObj> GetConstructorDetails(ConstructorInfoObj ctor);

            /// <summary>
            /// Gets methods - as abstract method info objects in name order - of a given abstract type object.
            /// </summary>
            IEnumerable<MethodInfoObj> GetMethodsInNameOrder(TypeObj type, IEnumerable<PropertyInfoObj> knownProperties);

            /// <summary>
            /// Gets info about a given abstract method info object.
            /// </summary>
            /// <returns>Whether the method is static, the return type and the name of the method.</returns>
            (bool isStatic, Type returnType, string name) GetMethodInfo(MethodInfoObj method);

            /// <summary>
            /// Gets additional details about a given abstract method info object.
            /// </summary>
            /// <returns>List of parameters for the method.</returns>
            IEnumerable<ParameterInfoObj> GetMethodDetails(MethodInfoObj method);

            /// <summary>
            /// Gets info about a given abstract parameter info object.
            /// </summary>
            /// <returns>The type and name of the parameter.</returns>
            (Type type, string name) GetParameterInfo(ParameterInfoObj parameter);
        }

        private IHost _host;

        /// <inheritdoc/>
        public override string Name => "Asm";

        /// <inheritdoc/>
        public override string Description => "Lists assemblies for the application domain of the shell, or shows detailed assembly info.";

        /// <summary>
        /// Constructor that creates the command object using a default <see cref="IHost"/> implementation.
        /// </summary>
        internal AssemblyCommand() : this(new DefaultHost()) {}

        /// <summary>
        /// Constructor that creates the command object using a given <see cref="IHost"/> implementation.
        /// </summary>
        internal AssemblyCommand(IHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));

            HasNonSubcommand("Lists assemblies for the host program.");

            HasSubcommand("details", "Shows detailed info about an assembly.")
                .HasRequiredParameter("assembly-name", "(Part of) name of assembly.", val => val);

            HasSubcommand("tree", "Shows an indented structure of modules, classes and members inside a given assembly.")
                .HasRequiredParameter("assembly-name", "Name of the assembly to show content of.", val => val)
                .HasFlagOption("basenames", "Include base names for each item (comprehensive for grep etc.).");
        }

        /// <inheritdoc/>
        protected override Task ExecuteAsync(IStdCommandExecutionContext context, Subcommand subcommand, CommandArgs args, CommandOptions options, string commandLine)
        {
            var entryAssembly = _host.GetEntryAssembly();
            var otherAssemblies = _host.GetOtherAssembliesInNameOrder(entryAssembly);

            var assemblies = new AssemblyObj[] { entryAssembly }.Concat(otherAssemblies).ToList();

            if (!subcommand.IsSubcommand)
                return ShowAssemblyListAsync(context, assemblies);
            else
            {
                switch (subcommand.Name)
                {
                    case "details":
                        {
                            var assembly = GetMatchingAssembly(context, assemblies, 0);
                            return ShowAssemblyDetailsAsync(context, assembly);
                        }

                    case "tree":
                        {
                            var assembly = GetMatchingAssembly(context, assemblies, 0);
                            return ShowAssemblyTreeAsync(context, assembly);
                        }

                    default:
                        throw new Exception($"Unhandled subcommand {subcommand.Name}");
                }
            }
        }

        private AssemblyObj GetMatchingAssembly(IStdCommandExecutionContext context, List<AssemblyObj> assemblies, int argIndex)
        {
            if (!context.Args.TryGetAs<string>(argIndex, out var assemblyName) || string.IsNullOrEmpty(assemblyName))
                throw new Exception("Empty or missing assembly name.");

            var matchingAssemblies = assemblies.Where(asm => _host.GetAssemblyName(asm).FullName.IndexOf(assemblyName, StringComparison.InvariantCultureIgnoreCase) >= 0).ToList();

            if (matchingAssemblies.Count == 0)
            {
                throw new Exception($"No assembly with a name containing '{assemblyName}'");
            }
            else if (matchingAssemblies.Count > 1)
            {
                throw new Exception($"Multiple assemblies with names containing '{assemblyName}' - possible assemblies:");
            }

            // One match!
            return matchingAssemblies[0];
        }

        private async Task ShowAssemblyListAsync(IStdCommandExecutionContext context, IEnumerable<AssemblyObj> assemblies)
        {
            var lines = TextFormatting.GetAlignedColumnStrings(assemblies,
                                                               " ",
                                                               ("Name", asm => _host.GetAssemblyName(asm).Name, TextAlignment.Start),
                                                               ("Version", asm => _host.GetAssemblyName(asm).Version, TextAlignment.Start),
                                                               ("Culture", asm => _host.GetAssemblyName(asm).CultureInfo.Name, TextAlignment.Start));

            foreach (var line in lines)
                await context.Output.WriteLineAsync(line).ConfigureAwait(false);
        }

        private async Task ShowAssemblyDetailsAsync(IStdCommandExecutionContext context, AssemblyObj assembly)
        {
            var name = _host.GetAssemblyName(assembly);

            await context.Output.WriteLineAsync($"Name:                  {name.Name}").ConfigureAwait(false);
            await context.Output.WriteLineAsync($"Full name:             {name.FullName}").ConfigureAwait(false);
            await context.Output.WriteLineAsync($"Version:               {name.Version}").ConfigureAwait(false);
            await context.Output.WriteLineAsync($"Version compatibility: {name.VersionCompatibility}").ConfigureAwait(false);
            await context.Output.WriteLineAsync($"Culture:               {name.CultureName}").ConfigureAwait(false);
            await context.Output.WriteLineAsync($"Code base:             {name.CodeBase}").ConfigureAwait(false);
            await context.Output.WriteLineAsync($"Content type:          {name.ContentType}").ConfigureAwait(false);
            await context.Output.WriteLineAsync($"CPU architecture:      {name.ProcessorArchitecture}").ConfigureAwait(false);
            await context.Output.WriteLineAsync($"Flags:                 {name.Flags}").ConfigureAwait(false);
            await context.Output.WriteLineAsync($"Hash algorithm:        {name.HashAlgorithm}").ConfigureAwait(false);
        }

        private class DefaultHost : IHost
        {
            private const BindingFlags AllMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

            public AssemblyObj GetEntryAssembly() => new AssemblyObj(Assembly.GetEntryAssembly());
            public IEnumerable<AssemblyObj> GetOtherAssembliesInNameOrder(AssemblyObj entryAssembly) => AppDomain.CurrentDomain.GetAssemblies().Where(asm => asm != (Assembly)entryAssembly.Object).OrderBy(asm => asm.GetName().Name).Select(asm => new AssemblyObj(asm)).ToList();
            public AssemblyName GetAssemblyName(AssemblyObj assembly) => ((Assembly)assembly.Object).GetName();
            public IEnumerable<ModuleObj> GetModulesInNameOrder(AssemblyObj assembly) => ((Assembly)assembly.Object).GetModules().OrderBy(mod => mod.Name).Select(mod => new ModuleObj(mod)).ToList();
            public string GetModuleName(ModuleObj module) => Path.GetFileNameWithoutExtension(((Module)module.Object).Name);
            public IEnumerable<TypeObj> GetModuleTypesInNameOrder(ModuleObj module) => ((Module)module.Object).GetTypes().OrderBy(t => t.Name).Select(t => new TypeObj(t)).ToList();
            public string GetTypeName(TypeObj type) => ((Type)type.Object).Name;
            public (string baseTypeFullName, IEnumerable<string> interfaceFullNames) GetTypeDetails(TypeObj type)
            {
                var theType = (Type)type.Object;
                var baseTypeFullName = theType.BaseType?.FullName;
                var interfaceFullNames = theType.GetInterfaces().Select(i => i.FullName).ToList();

                return (baseTypeFullName, interfaceFullNames);
            }
            public IEnumerable<TypeObj> GetNestedTypesInNameOrder(TypeObj type) => ((Type)type.Object).GetNestedTypes(AllMembers).OrderBy(t => t.Name).Select(t => new TypeObj(t)).ToList();

            public IEnumerable<FieldInfoObj> GetFieldsInNameOrder(TypeObj type) => ((Type)type.Object).GetFields(AllMembers).OrderBy(f => f.Name).Select(f => new FieldInfoObj(f)).ToList();
            public (Type type, string name) GetFieldInfo(FieldInfoObj field)
            {
                var fieldInfo = (FieldInfo)field.Object;
                return (type: fieldInfo.FieldType, name: fieldInfo.Name);
            }

            public IEnumerable<PropertyInfoObj> GetPropertiesInNameOrder(TypeObj type) => ((Type)type.Object).GetProperties(AllMembers).OrderBy(p => p.Name).Select(p => new PropertyInfoObj(p)).ToList();
            public (Type type, string name) GetPropertyInfo(PropertyInfoObj prop)
            {
                var propInfo = (PropertyInfo)prop.Object;
                return (type: propInfo.PropertyType, name: propInfo.Name);
            }
            public (bool canRead, bool canWrite) GetPropertyDetails(PropertyInfoObj prop)
            {
                var propInfo = (PropertyInfo)prop.Object;
                return (canRead: propInfo.CanRead, canWrite: propInfo.CanWrite);
            }

            public IEnumerable<ConstructorInfoObj> GetConstructorsInNameOrder(TypeObj type) => ((Type)type.Object).GetConstructors(AllMembers).OrderBy(c => c.Name).Select(c => new ConstructorInfoObj(c)).ToList();
            public (bool isStatic, string name) GetConstructorInfo(ConstructorInfoObj ctor)
            {
                var ctorInfo = (ConstructorInfo)ctor.Object;
                return (isStatic: ctorInfo.IsStatic, name: ctorInfo.Name);
            }
            public IEnumerable<ParameterInfoObj> GetConstructorDetails(ConstructorInfoObj ctor)
            {
                var ctorInfo = (ConstructorInfo)ctor.Object;
                return ctorInfo.GetParameters().Select(p => new ParameterInfoObj(p)).ToList();
            }

            public IEnumerable<MethodInfoObj> GetMethodsInNameOrder(TypeObj type, IEnumerable<PropertyInfoObj> knownProperties)
            {
                var propAccessors = knownProperties.SelectMany(p => ((PropertyInfo)p.Object).GetAccessors(true)).ToList();
                return ((Type)type.Object).GetMethods(AllMembers).Where(m => !propAccessors.Contains(m)).OrderBy(m => m.Name).Select(m => new MethodInfoObj(m)).ToList();
            }

            public (bool isStatic, Type returnType, string name) GetMethodInfo(MethodInfoObj method)
            {
                var methodInfo = (MethodInfo)method.Object;
                return (isStatic: methodInfo.IsStatic, returnType: methodInfo.ReturnType, name: methodInfo.Name);
            }
            public IEnumerable<ParameterInfoObj> GetMethodDetails(MethodInfoObj method)
            {
                var methodInfo = (MethodInfo)method.Object;
                return methodInfo.GetParameters().Select(p => new ParameterInfoObj(p)).ToList();
            }

            public (Type type, string name) GetParameterInfo(ParameterInfoObj parameter)
            {
                var paramInfo = (ParameterInfo)parameter.Object;
                return (type: paramInfo.ParameterType, name: paramInfo.Name);
            }
        }
    }
}
