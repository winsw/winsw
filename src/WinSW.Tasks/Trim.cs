using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;

namespace WinSW.Tasks
{
    public sealed class Trim : Task
    {
        private readonly HashSet<TypeDefinition> usedTypes = new HashSet<TypeDefinition>();

        [Required]
        public string Path { get; set; }

        public override bool Execute()
        {
            using var module = ModuleDefinition.ReadModule(this.Path, new() { ReadWrite = true, ReadSymbols = true });

            foreach (var t in module.CustomAttributeTypes())
            {
                this.WalkType(t);
            }

            this.WalkType(module.EntryPoint.DeclaringType);

            var types = module.Types;
            for (int i = types.Count - 1; i >= 0; i--)
            {
                var type = types[i];
                if (type.FullName.StartsWith("WinSW.Plugins"))
                {
                    this.WalkType(type);
                }
            }

            for (int i = types.Count - 1; i >= 0; i--)
            {
                var type = types[i];
                if (type.FullName == "<Module>")
                {
                    continue;
                }

                if (this.usedTypes.Contains(type))
                {
                    continue;
                }

                this.Log.LogMessage(MessageImportance.High, type.FullName);
                types.RemoveAt(i);
            }

            module.Write(new WriterParameters { WriteSymbols = true });

            return true;
        }

        private void WalkType(TypeReference typeRef)
        {
            if (typeRef is TypeSpecification typeSpec)
            {
                this.WalkType(typeSpec.ElementType);

                if (typeRef is GenericInstanceType genericType)
                {
                    foreach (var genericArg in genericType.GenericArguments)
                    {
                        this.WalkType(genericArg);
                    }
                }
                else if (typeRef is IModifierType modifierType)
                {
                    this.WalkType(modifierType.ModifierType);
                }

                return;
            }

            if (typeRef is TypeDefinition typeDef)
            {
                if (!this.usedTypes.Add(typeDef))
                {
                    return;
                }

                if (typeDef.DeclaringType != null)
                {
                    this.WalkType(typeDef.DeclaringType);
                }

                if (typeDef.BaseType != null)
                {
                    this.WalkType(typeDef.BaseType);
                }

                var methods = typeDef.Methods.ToList();
                var bodies = methods.Where(m => m.HasBody).Select(m => m.Body);
                var operands = bodies.SelectMany(b => b.Instructions).Select(i => i.Operand);

                var types = typeDef.CustomAttributeTypes()
                    .Union(typeDef.GenericParameters.SelectMany(p => p.CustomAttributeTypes()))
                    .Union(typeDef.Interfaces.Select(i => i.InterfaceType))
                    .Union(typeDef.Fields.SelectMany(f => f.CustomAttributeTypes()))
                    .Union(typeDef.Fields.Select(f => f.FieldType))
                    .Union(typeDef.Properties.SelectMany(p => p.CustomAttributeTypes()))
                    .Union(typeDef.Events.SelectMany(e => e.CustomAttributeTypes()))
                    .Union(typeDef.Events.Select(e => e.EventType))
                    .Union(methods.SelectMany(m => m.CustomAttributeTypes()))
                    .Union(methods.SelectMany(m => m.GenericParameters).SelectMany(p => p.CustomAttributeTypes()))
                    .Union(methods.SelectMany(m => m.MethodReturnType.CustomAttributeTypes()))
                    .Union(methods.Select(m => m.ReturnType))
                    .Union(methods.SelectMany(m => m.Parameters).SelectMany(p => p.CustomAttributeTypes()))
                    .Union(methods.SelectMany(m => m.Parameters).Select(p => p.ParameterType))
                    .Union(bodies.SelectMany(b => b.Variables).Select(v => v.VariableType))
                    .Union(operands.OfType<TypeReference>())
                    .Union(operands.OfType<MemberReference>().Select(m => m.DeclaringType))
                    .Union(operands.OfType<GenericInstanceMethod>().SelectMany(m => m.GenericArguments));

                foreach (var t in types)
                {
                    this.WalkType(t);
                }
            }
        }
    }

    internal static class Extensions
    {
        internal static IEnumerable<TypeReference> CustomAttributeTypes(this ICustomAttributeProvider provider)
        {
            return provider.HasCustomAttributes ? provider.CustomAttributes.Select(a => a.AttributeType) : Enumerable.Empty<TypeReference>();
        }
    }
}
