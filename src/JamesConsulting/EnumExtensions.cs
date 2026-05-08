using System;
using System.ComponentModel;

namespace JamesConsulting;

/// <summary>
/// Provides extension methods for <see cref="Enum" /> values.
/// </summary>
public static class EnumExtensions
{
    /// <summary>
    /// Gets the <see cref="DescriptionAttribute" /> text applied to the enumeration field, if any.
    /// Falls back to the enum member name when no description attribute is defined.
    /// </summary>
    /// <param name="enumValue">The enumeration value whose description should be retrieved.</param>
    /// <returns>The description text, the enum member name, or <c>null</c> if the field cannot be resolved.</returns>
    /// <exception cref="System.Reflection.AmbiguousMatchException">More than one of the requested attributes was found.</exception>
    /// <exception cref="TypeLoadException">A custom attribute type cannot be loaded.</exception>
    /// <example>
    /// Retrieve descriptions from enum members.
    /// <code>
    /// private enum MyOptions
    /// {
    ///     [Description("Testing")]
    ///     With,
    ///     Without
    /// }
    /// 
    /// var description1 = MyOptions.With.GetDescription();    // "Testing"
    /// var description2 = MyOptions.Without.GetDescription(); // "Without"
    /// var invalid = EnumExtensions.GetDescription((MyOptions)3); // null
    /// </code>
    /// </example>
    public static string? GetDescription(this Enum enumValue)
    {
        var fieldInfo = enumValue.GetType().GetField(enumValue.ToString());
        if (fieldInfo == null) return null;
        return Attribute.GetCustomAttribute(fieldInfo, typeof(DescriptionAttribute)) is not DescriptionAttribute
            attribute
            ? Enum.GetName(enumValue.GetType(), enumValue)
            : attribute.Description;
    }
}