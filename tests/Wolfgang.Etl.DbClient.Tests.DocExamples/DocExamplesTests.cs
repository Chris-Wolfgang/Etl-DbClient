using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.DocExamples;

// Doc-example rot detection.
//
// Every <example><code>...</code></example> block (and every bare <code> block
// under a <remarks>) in XML doc comments across src/Wolfgang.Etl.DbClient/**/*.cs
// gets extracted, wrapped in a synthetic harness, and Roslyn-compiled. Any
// diagnostic at Error severity fails the corresponding [Theory] row.
//
// Rationale: XML doc examples drift silently as the public API evolves —
// `await source.OldName(...)` outlives the renamed method by months. This
// gate catches that drift at PR time. Refs #141.
public sealed class DocExamplesTests
{
    // xUnit MemberData wants IEnumerable<object?[]>. Each row: [file, line, snippet].
    // Discovered once, at test-discovery time.
    public static IEnumerable<object?[]> Snippets()
    {
        var srcRoot = LocateSrcRoot();
        foreach (var file in Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
        {
            // Skip generated / obj / bin.
            var rel = Path.GetRelativePath(srcRoot, file).Replace('\\', '/');
            if (rel.Contains("/bin/") || rel.Contains("/obj/") || rel.EndsWith(".g.cs"))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            foreach (var (line, snippet) in ExtractCodeBlocks(text))
            {
                yield return new object?[] { rel, line, snippet };
            }
        }
    }

    [Theory]
    [MemberData(nameof(Snippets))]
    public void XmlDocCodeBlock_compiles(string file, int line, string snippet)
    {
        var wrapped = Harness.Wrap(file, line, snippet);
        var tree = CSharpSyntaxTree.ParseText(wrapped, path: $"{file}:{line}");

        var references = Harness.MetadataReferences();
        var compilation = CSharpCompilation.Create(
            assemblyName: $"DocExample_{Path.GetFileNameWithoutExtension(file)}_{line}",
            syntaxTrees: [tree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: false,
                nullableContextOptions: NullableContextOptions.Enable));

        var errors = compilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        if (errors.Length > 0)
        {
            var msg = new StringBuilder();
            msg.AppendLine($"Doc example at {file}:{line} does not compile.");
            msg.AppendLine();
            msg.AppendLine("Original snippet:");
            msg.AppendLine("---");
            msg.AppendLine(snippet);
            msg.AppendLine("---");
            msg.AppendLine();
            msg.AppendLine("Roslyn diagnostics:");
            foreach (var d in errors)
            {
                msg.AppendLine($"  {d.Id}: {d.GetMessage()}");
                var span = d.Location.GetLineSpan();
                if (span.IsValid)
                {
                    msg.AppendLine($"    at {span.Path} line {span.StartLinePosition.Line + 1}");
                }
            }
            Assert.Fail(msg.ToString());
        }
    }

    // ------------------------------------------------------------------
    // Extraction

    // Matches XML-doc <code> blocks that appear inside `///` comment lines.
    // Non-greedy body match to handle multiple blocks per file.
    private static readonly Regex CodeBlockRegex = new(
        @"///\s*<code>(?<body>.*?)///\s*</code>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static IEnumerable<(int Line, string Snippet)> ExtractCodeBlocks(string source)
    {
        foreach (Match m in CodeBlockRegex.Matches(source))
        {
            var line = source.Take(m.Index).Count(c => c == '\n') + 1;
            var raw = m.Groups["body"].Value;

            // Strip the leading `///` (and optional single space) from every line;
            // XML entities that C# would trip on (`&lt;`, `&gt;`, `&amp;`) get
            // un-escaped so the snippet parses as real C#.
            var lines = raw.Split('\n');
            var stripped = new StringBuilder();
            foreach (var l in lines)
            {
                var t = l.TrimEnd('\r');
                var slashIdx = t.IndexOf("///", StringComparison.Ordinal);
                if (slashIdx >= 0)
                {
                    t = t[(slashIdx + 3)..];
                    if (t.StartsWith(' '))
                    {
                        t = t[1..];
                    }
                }
                stripped.AppendLine(t);
            }

            var snippet = stripped.ToString()
                .Replace("&lt;", "<", StringComparison.Ordinal)
                .Replace("&gt;", ">", StringComparison.Ordinal)
                .Replace("&amp;", "&", StringComparison.Ordinal)
                .Trim();

            if (!string.IsNullOrWhiteSpace(snippet))
            {
                yield return (line, snippet);
            }
        }
    }

    // ------------------------------------------------------------------
    // Repo layout

    private static string LocateSrcRoot()
    {
        // Walk up from AppContext.BaseDirectory (the test host's output
        // directory) looking for a folder that contains src/Wolfgang.Etl.DbClient/.
        // Deliberately does NOT use CallerFilePath: CI's deterministic-path
        // rewrite turns that into "/_/" — see reference_callerfilepath_ci_deterministic_paths.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Wolfgang.Etl.DbClient");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            "Could not locate src/Wolfgang.Etl.DbClient/ walking up from " + AppContext.BaseDirectory);
    }
}

// Synthetic wrapper — every extracted snippet compiles as the body of an async
// method inside a fresh class, with a fat preamble of usings + stub types for
// commonly-referenced local names (Order, MyRecord) that snippets can't be
// expected to declare inline. Adding a new stub is cheaper than telling every
// doc writer to include one.
internal static class Harness
{
    public static string Wrap(string file, int line, string snippet)
    {
        var className = "Snippet_" + Math.Abs(HashCode.Combine(file, line));
        var sb = new StringBuilder();
        sb.AppendLine("#pragma warning disable CS8321  // Local function never used");
        sb.AppendLine("#pragma warning disable CS0219  // Variable assigned but never used");
        sb.AppendLine("#pragma warning disable IDE0044 // Add readonly modifier");
        sb.AppendLine("#pragma warning disable CA1852  // Type can be sealed");
        sb.AppendLine("#pragma warning disable CS0168  // Variable declared but never used");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Data;");
        sb.AppendLine("using System.Data.Common;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Dapper;");
        sb.AppendLine("using Wolfgang.Etl.DbClient;");
        sb.AppendLine("namespace DocExamplesHarness;");
        sb.AppendLine($"file class {className}");
        sb.AppendLine("{");
        // Stub types every consumer-shaped example uses. Add sparingly.
        sb.AppendLine("    private sealed record Order(int Id, string Name);");
        sb.AppendLine("    private sealed record MyRecord(int Id, string Name);");
        sb.AppendLine("    private async Task RunAsync(DbConnection conn)");
        sb.AppendLine("    {");
        sb.AppendLine("        _ = conn;");
        // Indent the snippet by 8 spaces so it slots into the method body.
        foreach (var l in snippet.Split('\n'))
        {
            sb.Append("        ").AppendLine(l.TrimEnd('\r'));
        }
        // Suppress "declared but not used" for local Order/MyRecord types.
        sb.AppendLine("        _ = new Order(0, \"\");");
        sb.AppendLine("        _ = new MyRecord(0, \"\");");
        sb.AppendLine("        await Task.CompletedTask;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    public static IReadOnlyList<MetadataReference> MetadataReferences()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var refs = new List<MetadataReference>();

        void Add(string dll)
        {
            var name = Path.GetFileNameWithoutExtension(dll);
            if (!seen.Add(name))
            {
                return;
            }
            // Skip xunit + Roslyn + Microsoft.TestPlatform DLLs so the
            // compilation isn't polluted with test-only types. Include
            // everything else — runtime BCL + Dapper + Wolfgang.Etl.DbClient.
            if (name.StartsWith("xunit", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Microsoft.CodeAnalysis", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Microsoft.TestPlatform", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Microsoft.VisualStudio.TestPlatform", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".Tests.DocExamples", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            try
            {
                refs.Add(MetadataReference.CreateFromFile(dll));
            }
            catch (BadImageFormatException) { /* native / resource-only */ }
        }

        // 1. The trusted-platform-assemblies list — every BCL DLL the runtime
        //    loaded us against (System.Runtime, System.Private.CoreLib, etc.).
        //    This is the only reliable way to get the full framework reference
        //    set at test-runtime; scanning bin/ misses everything under the
        //    runtime pack.
        var tpa = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        if (!string.IsNullOrEmpty(tpa))
        {
            foreach (var dll in tpa.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                Add(dll);
            }
        }

        // 2. Everything in the test host's output directory — picks up the
        //    runtime csproj + Dapper + System.Linq.Async that TPA doesn't
        //    duplicate.
        foreach (var dll in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll"))
        {
            Add(dll);
        }

        return refs;
    }
}
