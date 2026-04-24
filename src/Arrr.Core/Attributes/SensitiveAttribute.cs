namespace Arrr.Core.Attributes;

/// <summary>
/// Marks a string property as sensitive. LoadConfigAsync will decrypt it on read;
/// SaveConfigAsync will encrypt it on write.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SensitiveAttribute : Attribute;
