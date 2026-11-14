// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using VerifyCS = CSharpCodeFixVerifier<MessagePack.SourceGenerator.Analyzers.MsgPack00xMessagePackAnalyzer, MessagePack.Analyzers.CodeFixes.MessagePackCodeFixProvider>;

public class CountersignFinding2
{
    [Fact]
    public async Task NullKeyOnPositionalRecordPropertyReportsMsgPack005()
    {
        string input = @"
using MessagePack;

[MessagePackObject]
public partial record {|MsgPack007:R|}([property: Key(null)] string {|MsgPack005:X|});
";

        await new VerifyCS.Test
        {
            TestCode = input,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net60,
            MarkupOptions = MarkupOptions.UseFirstDescriptor,
        }.RunAsync();
    }

    [Fact]
    public async Task MetadataGenericStructAllowPrivate_NoBackingFieldDiagnostics()
    {
        string defineSource = @"
using MessagePack;
namespace Lib
{
    [MessagePackObject(AllowPrivate = true)]
    public partial struct Item<T>
    {
        [Key(0)]
        public T Value { get; set; }
    }
}
";
        string usageSource = @"
using MessagePack;
using Lib;
namespace App
{
    [MessagePackObject]
    public class Holder
    {
        [Key(0)]
        public Item<int> M { get; set; }
    }
}
";
        ImmutableArray<MetadataReference> fx = await ReferenceAssemblies.Net.Net80.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var refs = fx.Concat(ReferencesHelper.GetReferences(ReferencesSet.MessagePack)).ToArray();
        var parse = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var lib = CSharpCompilation.Create("Lib", new[] { CSharpSyntaxTree.ParseText(defineSource, parse) }, refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var emit = lib.Emit(stream);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));

        // Sanity: lib itself is clean under the analyzer.
        var libDiags = await lib.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MessagePack.SourceGenerator.Analyzers.MsgPack00xMessagePackAnalyzer())).GetAnalyzerDiagnosticsAsync();
        Assert.True(libDiags.IsEmpty, "lib: " + string.Join(Environment.NewLine, libDiags));

        MetadataReference libRef = MetadataReference.CreateFromImage(stream.ToArray());
        var app = CSharpCompilation.Create("App", new[] { CSharpSyntaxTree.ParseText(usageSource, parse) }, refs.Append(libRef), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var appDiags = await app.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MessagePack.SourceGenerator.Analyzers.MsgPack00xMessagePackAnalyzer())).GetAnalyzerDiagnosticsAsync();
        Assert.True(appDiags.IsEmpty, "app: " + string.Join(Environment.NewLine, appDiags));
    }
}
