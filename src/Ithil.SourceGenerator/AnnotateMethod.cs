using Microsoft.CodeAnalysis;

namespace Ithil.SourceGenerator;

internal class AnnotatedMethod
{
    internal IMethodSymbol Method { get; }
    internal AttributeData Attribute { get; }

    internal AnnotatedMethod(IMethodSymbol method, AttributeData attribute)
    {
        Method = method;
        Attribute = attribute;
    }
}
