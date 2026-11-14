// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack.SourceGenerator.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

public class CountersignFinding3
{
    private readonly ITestOutputHelper logger;

    public CountersignFinding3(ITestOutputHelper logger) => this.logger = logger;

    [Fact]
    public async Task MetadataGenericClassWithoutUsableCtor_StillReportsMsgPack007InConsumer()
    {
        string lib = """
using MessagePack;
namespace Lib
{
    [MessagePackObject]
    public class Box<T>
    {
        [Key(0)]
        public T Value { get; set; }

        public Box(string other) { }
    }
}
""";
        string app = """
using MessagePack;
using Lib;
namespace App
{
    [MessagePackObject]
    public class Holder
    {
        [Key(0)]
        public Box<int> Item { get; set; }
    }
}
""";
        ImmutableArray<MetadataReference> fx = await ReferenceAssemblies.Net.Net80.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var refs = fx.Concat(ReferencesHelper.GetReferences(ReferencesSet.MessagePack)).ToList();
        var libComp = CSharpCompilation.Create("Lib", new[] { CSharpSyntaxTree.ParseText(lib) }, refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var ms = new MemoryStream();
        var emit = libComp.Emit(ms);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
        refs.Add(MetadataReference.CreateFromImage(ms.ToArray()));

        var appComp = CSharpCompilation.Create("App", new[] { CSharpSyntaxTree.ParseText(app) }, refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var withAnalyzers = appComp.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MsgPack00xMessagePackAnalyzer()));
        ImmutableArray<Diagnostic> diags = await withAnalyzers.GetAllDiagnosticsAsync();
        foreach (Diagnostic d in diags)
        {
            this.logger.WriteLine(d.ToString());
        }

        Assert.DoesNotContain(diags, d => d.Id == "AD0001");
        Assert.Contains(diags, d => d.Id == MsgPack00xMessagePackAnalyzer.DeserializingConstructorId);
    }

    // Control: same types in one compilation. Proves the analyzer path reaches the MsgPack007 check.
    [Fact]
    public async Task Control_SourceGenericClassWithoutUsableCtor_ReportsMsgPack007()
    {
        string src = """
using MessagePack;
namespace App
{
    [MessagePackObject]
    public class Box<T>
    {
        [Key(0)]
        public T Value { get; set; }

        public Box(string other) { }
    }

    [MessagePackObject]
    public class Holder
    {
        [Key(0)]
        public Box<int> Item { get; set; }
    }
}
""";
        ImmutableArray<MetadataReference> fx = await ReferenceAssemblies.Net.Net80.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var refs = fx.Concat(ReferencesHelper.GetReferences(ReferencesSet.MessagePack)).ToList();
        var appComp = CSharpCompilation.Create("App", new[] { CSharpSyntaxTree.ParseText(src) }, refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        ImmutableArray<Diagnostic> diags = await appComp.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MsgPack00xMessagePackAnalyzer())).GetAllDiagnosticsAsync();
        foreach (Diagnostic d in diags)
        {
            this.logger.WriteLine(d.ToString());
        }

        Assert.Contains(diags, d => d.Id == MsgPack00xMessagePackAnalyzer.DeserializingConstructorId);
    }
}
