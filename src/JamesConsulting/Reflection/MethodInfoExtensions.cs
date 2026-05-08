using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using JamesConsulting.Internal;

namespace JamesConsulting.Reflection
{
    /// <summary>
    /// Provides extension methods for <see cref="MethodInfo"/> to assist with diagnostics and logging.
    /// </summary>
    public static class MethodInfoExtensions
    {
        private static readonly ConcurrentDictionary<MethodInfo, (ParameterInfo[] Parameters, string Template)> MethodTemplates = new();

        /// <summary>
        /// Builds a string representing the invocation of the method including parameter types, names and supplied values.
        /// </summary>
        /// <param name="methodInfo">The method being invoked.</param>
        /// <param name="parameterValues">Values corresponding to the method's formal parameters.</param>
        /// <returns>A formatted string describing the invocation.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="methodInfo"/> is <c>null</c>.</exception>
        /// <example>
        /// Format invocation details.
        /// <code>
        /// var methodInfo = typeof(string).GetMethod("Insert", new[]{ typeof(int), typeof(string) });
        /// var invocation = methodInfo!.ToInvocationString(3, "testing");
        /// // "String.Insert(System.Int32 startIndex : 3, System.String value : \"testing\")"
        /// var invocation2 = methodInfo.ToInvocationString(3, "testing"); // cached template
        /// </code>
        /// </example>
        public static string ToInvocationString(this MethodInfo methodInfo, params object[] parameterValues)
        {
            Guard.NotNull(methodInfo);
            if (!MethodTemplates.ContainsKey(methodInfo)) MethodTemplates[methodInfo] = methodInfo.GetMethodTemplate();
            var (parameters, template) = MethodTemplates[methodInfo];
            return BindTemplate(template, parameters, parameterValues);
        }

        private static string BindTemplate(
            string template,
            IEnumerable<ParameterInfo> parameters,
            IReadOnlyList<object> parameterValues)
        {
            return string.Format(template, parameters.Select((x, idx) => GetValue(x, parameterValues[idx])).ToArray());
        }

        private static (ParameterInfo[] Parameters, string Template) GetMethodTemplate(this MethodBase methodInfo)
        {
            var stringBuilder = new StringBuilder($"{methodInfo.DeclaringType!.FullName}.{methodInfo.Name}(");
            var parameterInfo = methodInfo.GetParameters();
            stringBuilder.Append(string.Join(", ", parameterInfo.Select(ToInvocationString)));
            stringBuilder.Append(')');
            return (parameterInfo, stringBuilder.ToString());
        }

        private static object? GetValue(ParameterInfo parameterInfo, object? parameterValue)
        {
            if (parameterValue == null || parameterInfo.ParameterType.IsPrimitive) return parameterValue;
            return parameterValue is string ? $"\"{parameterValue}\"" : parameterValue.ToJson();
        }

        private static string ToInvocationString(ParameterInfo parameterInfo, int index)
        {
            return $"{parameterInfo.ParameterType.FullName} {parameterInfo.Name} : {{{index}}}";
        }
    }
}