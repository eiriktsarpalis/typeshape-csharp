using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
using System.Text.Encodings.Web;
using TypeShape.Roslyn;
using TypeShape.SourceGenerator.Model;
using Xunit;

namespace TypeShape.SourceGenerator.UnitTests;

public record TypeShapeSourceGeneratorResult
{
    public required Compilation NewCompilation { get; init; }
    public required ImmutableEquatableArray<TypeShapeProviderModel> GeneratedModels { get; init; }
    public required ImmutableEquatableArray<Diagnostic> Diagnostics { get; init; }
    public IEnumerable<TypeShapeModel> AllGeneratedTypes => GeneratedModels.SelectMany(ctx => ctx.ProvidedTypes.Values);
}

public static class CompilationHelpers
{
    private static readonly CSharpParseOptions s_defaultParseOptions = CreateParseOptions();
    private static readonly Assembly systemRuntimeAssembly = Assembly.Load(new AssemblyName("System.Runtime"));

    public static CSharpParseOptions CreateParseOptions(
        LanguageVersion? version = null,
        DocumentationMode? documentationMode = null)
    {
        return new CSharpParseOptions(
            kind: SourceCodeKind.Regular,
            languageVersion: version ?? LanguageVersion.CSharp12,
            documentationMode: documentationMode ?? DocumentationMode.Parse);
    }

    public static Compilation CreateCompilation(
        string source,
        MetadataReference[]? additionalReferences = null,
        string assemblyName = "TestAssembly",
        CSharpParseOptions? parseOptions = null)
    {
        parseOptions ??= s_defaultParseOptions;
        additionalReferences ??= [];
        
        SyntaxTree[] syntaxTrees = [ CSharpSyntaxTree.ParseText(source, parseOptions) ];
        MetadataReference[] references = 
        [
            MetadataReference.CreateFromFile(typeof(int).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Immutable.ImmutableArray<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(JavaScriptEncoder).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(LinkedList<>).Assembly.Location),
            MetadataReference.CreateFromFile(systemRuntimeAssembly.Location),
            MetadataReference.CreateFromFile(typeof(TypeShape.Abstractions.ITypeShape).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.Unsafe).Assembly.Location),
            .. additionalReferences,
        ];

        return CSharpCompilation.Create(
            assemblyName,
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
    }

    public static CSharpGeneratorDriver CreateTypeShapeSourceGeneratorDriver(Compilation compilation, TypeShapeIncrementalGenerator? generator = null)
    {
        generator ??= new();
        CSharpParseOptions parseOptions = compilation.SyntaxTrees
            .OfType<CSharpSyntaxTree>()
            .Select(tree => tree.Options)
            .FirstOrDefault() ?? s_defaultParseOptions;

        return CSharpGeneratorDriver.Create(
            generators: [generator.AsSourceGenerator()],
            parseOptions: parseOptions,
            driverOptions: new GeneratorDriverOptions(
                disabledOutputs: IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));
    }

    public static TypeShapeSourceGeneratorResult RunTypeShapeSourceGenerator(Compilation compilation, bool disableDiagnosticValidation = false)
    {
        List<TypeShapeProviderModel> generatedModels = [];
        var generator = new TypeShapeIncrementalGenerator
        {
            OnGeneratingSource = generatedModels.Add
        };

        CSharpGeneratorDriver driver = CreateTypeShapeSourceGeneratorDriver(compilation, generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outCompilation, out ImmutableArray<Diagnostic> diagnostics);

        if (!disableDiagnosticValidation)
        {
            outCompilation.GetDiagnostics().AssertMaxSeverity(DiagnosticSeverity.Info);
            diagnostics.AssertMaxSeverity(DiagnosticSeverity.Info);
        }

        return new()
        {
            NewCompilation = outCompilation,
            GeneratedModels = [.. generatedModels],
            Diagnostics = [.. diagnostics],
        };
    }

    /// <summary>
    /// Uses reflection to check for structural equality, returning a path to the first mismatching data when not equal.
    /// </summary>
    public static void AssertStructurallyEqual<T>(T expected, T actual)
    {
        CheckAreEqualCore(expected, actual, new());
        static void CheckAreEqualCore(object? expected, object? actual, Stack<string> path)
        {
            if (expected is null || actual is null)
            {
                if (expected is not null || actual is not null)
                {
                    FailNotEqual();
                }

                return;
            }

            Type type = expected.GetType();
            if (type != actual.GetType())
            {
                FailNotEqual();
                return;
            }

            if (expected is IDictionary leftDict)
            {
                if (actual is not IDictionary rightDict ||
                    leftDict.Count != rightDict.Count)
                {
                    FailNotEqual();
                    return;
                }

                foreach (DictionaryEntry expectedEntry in leftDict)
                {
                    if (rightDict.Contains(expectedEntry.Key))
                    {
                        var actualValue = rightDict[expectedEntry.Key];
                        path.Push($"[{expectedEntry.Key}]");
                        CheckAreEqualCore(expectedEntry.Value, actualValue, path);
                        path.Pop();
                    }
                    else
                    {
                        CheckAreEqualCore(expectedEntry.Key, "<missing key>", path);
                    }
                }

                return;
            }

            if (expected is IEnumerable leftCollection)
            {
                if (actual is not IEnumerable rightCollection)
                {
                    FailNotEqual();
                    return;
                }

                object?[] expectedValues = leftCollection.Cast<object?>().ToArray();
                object?[] actualValues = rightCollection.Cast<object?>().ToArray();

                for (int i = 0; i < Math.Max(expectedValues.Length, actualValues.Length); i++)
                {
                    object? expectedElement = i < expectedValues.Length ? expectedValues[i] : "<end of collection>";
                    object? actualElement = i < actualValues.Length ? actualValues[i] : "<end of collection>";

                    path.Push($"[{i}]");
                    CheckAreEqualCore(expectedElement, actualElement, path);
                    path.Pop();
                }

                return;
            }

            if (type.GetProperty("EqualityContract", BindingFlags.Instance | BindingFlags.NonPublic, null, returnType: typeof(Type), types: Array.Empty<Type>(), null) != null)
            {
                // Type is a C# record, run pointwise equality comparison.
                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    path.Push("." + property.Name);
                    CheckAreEqualCore(property.GetValue(expected), property.GetValue(actual), path);
                    path.Pop();
                }

                return;
            }

            if (!expected.Equals(actual))
            {
                FailNotEqual();
            }

            void FailNotEqual() => Assert.Fail($"Value not equal in ${string.Join("", path.Reverse())}: expected {expected}, but was {actual}.");
        }
    }

    public static void AssertMaxSeverity(this IEnumerable<Diagnostic> diagnostics, DiagnosticSeverity maxSeverity)
    {
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity > maxSeverity);
    }

    public static (int startLine, int startColumn) GetStartPosition(this Location location)
    {
        FileLinePositionSpan lineSpan = location.GetLineSpan();
        return (lineSpan.StartLinePosition.Line, lineSpan.StartLinePosition.Character);
    }

    public static (int startLine, int startColumn) GetEndPosition(this Location location)
    {
        FileLinePositionSpan lineSpan = location.GetLineSpan();
        return (lineSpan.EndLinePosition.Line, lineSpan.EndLinePosition.Character);
    }
}
