using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

public class NamespaceArchitectureExporter
{
    [MenuItem("Tools/Export CyberPickle Namespace Architecture")]
    public static void ExportNamespaceArchitecture()
    {
        StringBuilder sb = new StringBuilder();
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies)
        {
            // Get only the types that belong to the CyberPickle namespace
            var types = assembly.GetTypes()
                .Where(t => (t.IsClass || t.IsEnum || t.IsInterface)
                            && t.Namespace != null
                            && t.Namespace.StartsWith("CyberPickle")
                            && !t.IsDefined(typeof(CompilerGeneratedAttribute), false)
                            && !t.Name.StartsWith("<")
                            && !t.IsNestedPrivate); // Exclude private nested types

            var namespaceGroups = types.GroupBy(t => t.Namespace).OrderBy(g => g.Key);

            foreach (var namespaceGroup in namespaceGroups)
            {
                sb.AppendLine($"Namespace: {namespaceGroup.Key}");
                foreach (var type in namespaceGroup)
                {
                    if (type.IsEnum)
                    {
                        sb.AppendLine($" Enum: {type.Name}");
                        var enumValues = Enum.GetValues(type);
                        foreach (var value in enumValues)
                        {
                            sb.AppendLine($"   Value: {value} = {(int)value}");
                        }
                        continue;
                    }

                    var className = type.Name;
                    if (type.IsGenericType)
                    {
                        var genericArgs = type.GetGenericArguments();
                        var genericArgNames = string.Join(", ", genericArgs.Select(GetTypeName));
                        className = $"{type.Name.Substring(0, type.Name.IndexOf('`'))}<{genericArgNames}>";
                    }

                    var baseType = type.BaseType != null && type.BaseType != typeof(object) ? GetTypeName(type.BaseType) : null;
                    var interfaces = type.GetInterfaces()
                                         .Where(i => i.Namespace != "System" && i.Namespace != "UnityEngine")
                                         .Select(GetTypeName)
                                         .ToList();
                    var inheritanceInfo = "";
                    if (baseType != null)
                    {
                        inheritanceInfo = $" : {baseType}";
                        if (interfaces.Count > 0)
                        {
                            inheritanceInfo += ", " + string.Join(", ", interfaces);
                        }
                    }
                    else if (interfaces.Count > 0)
                    {
                        inheritanceInfo = $" : " + string.Join(", ", interfaces);
                    }

                    var typeKeyword = type.IsInterface ? "Interface" : "Class";
                    sb.AppendLine($" {typeKeyword}: {className}{inheritanceInfo}");

                    // Fields
                    var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                                     .Where(f => f.DeclaringType == type
                                                 && !Attribute.IsDefined(f, typeof(CompilerGeneratedAttribute))
                                                 && !f.Name.Contains("<")); // Exclude compiler-generated fields
                    foreach (var field in fields)
                    {
                        var accessModifier = GetAccessModifier(field);
                        var staticModifier = field.IsStatic ? "static " : "";
                        sb.AppendLine($"   Field: {accessModifier} {staticModifier}{GetTypeName(field.FieldType)} {field.Name}");
                    }

                    // Constructors
                    var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                                           .Where(c => c.DeclaringType == type
                                                       && !(c.IsPrivate && type.IsAbstract && type.IsSealed)); // Exclude private constructors of static classes
                    foreach (var constructor in constructors)
                    {
                        var accessModifier = GetAccessModifier(constructor);
                        var parameters = constructor.GetParameters();
                        var parameterDescriptions = string.Join(", ", parameters.Select(p => $"{GetTypeName(p.ParameterType)} {p.Name}"));
                        sb.AppendLine($"   Constructor: {accessModifier} {className}({parameterDescriptions})");
                    }

                    // Methods
                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                                      .Where(m => m.DeclaringType == type
                                                  && !m.IsSpecialName
                                                  && m.GetBaseDefinition().DeclaringType != typeof(object)
                                                  && !Attribute.IsDefined(m, typeof(CompilerGeneratedAttribute))
                                                  && !m.Name.Contains("<")); // Exclude compiler-generated methods
                    foreach (var method in methods)
                    {
                        var accessModifier = GetAccessModifier(method);
                        var modifiers = "";
                        if (method.IsAbstract) modifiers += "abstract ";
                        else if (method.IsVirtual && !method.IsFinal && method.GetBaseDefinition() != method) modifiers += "override ";
                        else if (method.IsVirtual && !method.IsFinal) modifiers += "virtual ";
                        else if (method.IsFinal && method.IsVirtual) modifiers += "sealed ";
                        if (method.IsStatic) modifiers += "static ";
                        if (typeof(Task).IsAssignableFrom(method.ReturnType)) modifiers += "async ";
                        var returnType = GetTypeName(method.ReturnType);
                        var parameters = method.GetParameters();
                        var parameterDescriptions = string.Join(", ", parameters.Select(p => $"{GetTypeName(p.ParameterType)} {p.Name}"));
                        sb.AppendLine($"   Method: {accessModifier} {modifiers}{returnType} {method.Name}({parameterDescriptions})");
                    }

                    // Properties
                    var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                                         .Where(p => p.DeclaringType == type);
                    foreach (var prop in properties)
                    {
                        var getMethod = prop.GetMethod;
                        var setMethod = prop.SetMethod;
                        var accessModifier = getMethod != null ? GetAccessModifier(getMethod) : GetAccessModifier(setMethod);
                        var staticModifier = ((getMethod?.IsStatic ?? false) || (setMethod?.IsStatic ?? false)) ? "static " : "";
                        sb.AppendLine($"   Property: {accessModifier} {staticModifier}{GetTypeName(prop.PropertyType)} {prop.Name} {{ {(getMethod != null ? "get; " : "")}{(setMethod != null ? "set; " : "")}}}");
                    }

                    // Events
                    var events = type.GetEvents(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                                     .Where(e => e.DeclaringType == type);
                    foreach (var evt in events)
                    {
                        var accessModifier = GetAccessModifier(evt.AddMethod);
                        var staticModifier = evt.AddMethod.IsStatic ? "static " : "";
                        sb.AppendLine($"   Event: {accessModifier} {staticModifier}{GetTypeName(evt.EventHandlerType)} {evt.Name}");
                    }
                }
            }
        }

        string filePath = "CyberPickle_NamespaceArchitecture.txt";
        File.WriteAllText(filePath, sb.ToString());
        Debug.Log($"CyberPickle namespace architecture exported to {filePath}");
    }

    private static string GetAccessModifier(MethodBase method)
    {
        if (method.IsPublic) return "public";
        if (method.IsPrivate) return "private";
        if (method.IsFamily) return "protected";
        if (method.IsAssembly) return "internal";
        if (method.IsFamilyOrAssembly) return "protected internal";
        return "private protected";
    }

    private static string GetAccessModifier(FieldInfo field)
    {
        if (field.IsPublic) return "public";
        if (field.IsPrivate) return "private";
        if (field.IsFamily) return "protected";
        if (field.IsAssembly) return "internal";
        if (field.IsFamilyOrAssembly) return "protected internal";
        return "private protected";
    }

    private static string GetTypeName(Type type)
    {
        if (type == typeof(void)) return "void";
        if (type == typeof(int)) return "int";
        if (type == typeof(string)) return "string";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(float)) return "float";
        if (type == typeof(double)) return "double";
        if (type == typeof(object)) return "object";
        if (type == typeof(decimal)) return "decimal";
        if (type == typeof(char)) return "char";
        if (type == typeof(byte)) return "byte";
        if (type == typeof(sbyte)) return "sbyte";
        if (type == typeof(short)) return "short";
        if (type == typeof(ushort)) return "ushort";
        if (type == typeof(uint)) return "uint";
        if (type == typeof(long)) return "long";
        if (type == typeof(ulong)) return "ulong";
        if (type == typeof(Task)) return "Task";
        if (type.IsArray)
        {
            return $"{GetTypeName(type.GetElementType())}[]";
        }
        if (type.IsGenericType)
        {
            var genericTypeName = type.GetGenericTypeDefinition().Name;
            genericTypeName = genericTypeName.Substring(0, genericTypeName.IndexOf('`'));
            var genericArgs = string.Join(", ", type.GetGenericArguments().Select(GetTypeName));
            return $"{genericTypeName}<{genericArgs}>";
        }
        return type.Name;
    }
}
