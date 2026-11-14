// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
}
