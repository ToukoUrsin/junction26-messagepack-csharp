// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

public class CountersignFinding1
{
    private const string UsageSource = """
using MessagePack;

namespace TempProject
{
    [MessagePackObject]
    public class MyObject
    {
        [Key(0)]
        public MyGeneric<int> Value { get; set; }
    }

    [GeneratedMessagePackResolver]
    partial class MyResolver { }
}
""";

    [Fact]
    public async Task GenericClassFromMetadata_UnkeyedMember_ReportsMsgPack004()
    {
        ImmutableArray<Diagnostic> diags = await RunAnalyzerAsync("public class MyGeneric<T>");
        Assert.Contains(diags, d => d.Id == "MsgPack004");
    }

    [Fact]
    public async Task GenericStructFromMetadata_UnkeyedMember_ReportsMsgPack004()
    {
        ImmutableArray<Diagnostic> diags = await RunAnalyzerAsync("public struct MyGeneric<T>");
        Assert.Contains(diags, d => d.Id == "MsgPack004");
    }

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(string typeHeader)
    {
        string defineSource = "using MessagePack;\nnamespace TempProject\n{\n    [MessagePackObject]\n    " + typeHeader + "\n    {\n        [Key(0)]\n        public string Key { get; set; }\n\n        public int Unkeyed { get; set; }\n    }\n}\n";

        ImmutableArray<MetadataReference> fx = await ReferenceAssemblies.Net.Net80.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var refs = fx.Concat(ReferencesHelper.GetReferences(ReferencesSet.MessagePack)).ToArray();
        var parse = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp7_3);
        var lib = CSharpCompilation.Create("DefiningProject", new[] { CSharpSyntaxTree.ParseText(defineSource, parse) }, refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var emit = lib.Emit(stream);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        MetadataReference libRef = MetadataReference.CreateFromImage(stream.ToArray());

        var app = CSharpCompilation.Create("App", new[] { CSharpSyntaxTree.ParseText(UsageSource, parse) }, refs.Append(libRef), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var withAnalyzers = app.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MessagePack.SourceGenerator.Analyzers.MsgPack00xMessagePackAnalyzer()));
        ImmutableArray<Diagnostic> all = await withAnalyzers.GetAllDiagnosticsAsync();
        Assert.DoesNotContain(all, d => d.Id == "AD0001");

        // Document that the consuming project's generator still emits a formatter for the referenced generic type.
        GeneratorDriverRunResult run = CSharpGeneratorDriver.Create(new MessagePack.SourceGenerator.MessagePackGenerator().AsSourceGenerator()).RunGenerators(app).GetRunResult();
        string generated = string.Join("\n", run.Results.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString()));
        Assert.Contains("MyGeneric", generated);
        Assert.Contains(".Key", generated);
        Assert.DoesNotContain("Unkeyed", generated);
        return all;
    }
}
