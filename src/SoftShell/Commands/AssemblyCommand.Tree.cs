using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using SoftShell.Helpers;

namespace SoftShell.Commands
{
    public partial class AssemblyCommand
    {
        private async Task ShowAssemblyTreeAsync(IStdCommandExecutionContext context, AssemblyObj assembly)
        {
            foreach (var module in _host.GetModulesInNameOrder(assembly))
                await OutputModuleAsync(context,
                                        module,
                                        context.Options.HasFlag("basenames") ? string.Empty : null).ConfigureAwait(false);
        }

        private string BaseName(string oldBaseName, string subName)
        {
            if (oldBaseName is null)
                return null;

            return $"{oldBaseName}{subName}.";
        }

        private async Task OutputModuleAsync(IStdCommandExecutionContext context, ModuleObj module, string baseName)
        {
            var moduleName = _host.GetModuleName(module);

            await context.Output.WriteLineAsync($"Module: {baseName ?? string.Empty}{moduleName}").ConfigureAwait(false);

            foreach (var type in _host.GetModuleTypesInNameOrder(module))
                await OutputTypeAsync(context, type, BaseName(baseName, moduleName), 1).ConfigureAwait(false);
        }

        private async Task OutputTypeAsync(IStdCommandExecutionContext context, TypeObj type, string baseName, int nestingLevel)
        {
            var indentStr = string.Empty.PadRight(2 * nestingLevel);

            await context.Output.WriteLineAsync($"{indentStr}Type: {baseName ?? string.Empty}{_host.GetTypeName(type)}{GetTypeDetails(type)}").ConfigureAwait(false);

            // Nested types
            foreach (var nestedtype in _host.GetNestedTypesInNameOrder(type))
                await OutputTypeAsync(context, nestedtype, BaseName(baseName, _host.GetTypeName(type)), nestingLevel + 1).ConfigureAwait(false);

            // Fields
            foreach (var field in _host.GetFieldsInNameOrder(type))
                await OutputFieldAsync(context, field, BaseName(baseName, _host.GetTypeName(type)), nestingLevel + 1).ConfigureAwait(false);

            // Properties
            var properties = _host.GetPropertiesInNameOrder(type);
            foreach (var prop in properties)
                await OutputPropertyAsync(context, prop, BaseName(baseName, _host.GetTypeName(type)), nestingLevel + 1).ConfigureAwait(false);

            // Constructors
            foreach (var ctor in _host.GetConstructorsInNameOrder(type))
                await OutputConstructorAsync(context, ctor, BaseName(baseName, _host.GetTypeName(type)), nestingLevel + 1).ConfigureAwait(false);

            // Methods (except property accessors)
            foreach (var method in _host.GetMethodsInNameOrder(type, properties))
                await OutputMethodAsync(context, method, BaseName(baseName, _host.GetTypeName(type)), nestingLevel + 1).ConfigureAwait(false);
        }

        private Task OutputFieldAsync(IStdCommandExecutionContext context, FieldInfoObj field, string baseName, int nestingLevel)
        {
            var indentStr = string.Empty.PadRight(2 * nestingLevel);
            (var fieldType, var fieldName) = _host.GetFieldInfo(field);

            return context.Output.WriteLineAsync($"{indentStr}Field: {fieldType.Name} {baseName ?? string.Empty}{fieldName}");
        }

        private Task OutputPropertyAsync(IStdCommandExecutionContext context, PropertyInfoObj prop, string baseName, int nestingLevel)
        {
            var indentStr = string.Empty.PadRight(2 * nestingLevel);
            (var propType, var propName) = _host.GetPropertyInfo(prop);

            return context.Output.WriteLineAsync($"{indentStr}Property: {propType.Name} {baseName ?? string.Empty}{propName}{GetPropertyDetails(prop)}");
        }

        private Task OutputConstructorAsync(IStdCommandExecutionContext context, ConstructorInfoObj ctor, string baseName, int nestingLevel)
        {
            var indentStr = string.Empty.PadRight(2 * nestingLevel);
            (var isStatic, var ctorName) = _host.GetConstructorInfo(ctor);

            return context.Output.WriteLineAsync($"{indentStr}Constructor: {(isStatic ? "static " : string.Empty)}{baseName ?? string.Empty}{ctorName}{GetConstructorDetails(ctor)}");
        }

        private Task OutputMethodAsync(IStdCommandExecutionContext context, MethodInfoObj method, string baseName, int nestingLevel)
        {
            var indentStr = string.Empty.PadRight(2 * nestingLevel);
            (var isStatic, var returnType, var methodName) = _host.GetMethodInfo(method);

            return context.Output.WriteLineAsync($"{indentStr}Method: {(isStatic ? "static " : string.Empty)}{returnType.Name} {baseName ?? string.Empty}{methodName}{GetMethodDetails(method)}");
        }

        private string GetTypeDetails(TypeObj type)
        {
            (var baseTypeName, var interfaces) = _host.GetTypeDetails(type);
            var inherited = (!string.IsNullOrEmpty(baseTypeName) ? new string[] { baseTypeName } : new string[0]).Union(interfaces).Where(x => !string.IsNullOrEmpty(x)).ToList();

            return $"{(inherited.Any() ? " : " + string.Join(", ", inherited) : string.Empty)}";
        }

        private string GetPropertyDetails(PropertyInfoObj prop)
        {
            (var canRead, var canWrite) = _host.GetPropertyDetails(prop);
            return $" {{ {(canRead ? "get; " : string.Empty)}{(canWrite ? "set; " : string.Empty)}}}";
        }

        private string GetConstructorDetails(ConstructorInfoObj ctor)
        {
            return $"({string.Join(", ", _host.GetConstructorDetails(ctor).Select(p => _host.GetParameterInfo(p)).Select(pi => $"{pi.type} {pi.name}"))})";
        }

        private string GetMethodDetails(MethodInfoObj method)
        {
            return $"({string.Join(", ", _host.GetMethodDetails(method).Select(p => _host.GetParameterInfo(p)).Select(pi => $"{pi.type} {pi.name}"))})";
        }
    }
}
