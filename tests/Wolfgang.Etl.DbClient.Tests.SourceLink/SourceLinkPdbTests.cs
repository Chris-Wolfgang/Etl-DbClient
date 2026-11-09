// SourceLink PDB gates — mechanical preconditions for F11-into-source.
//
// Interactive-debugger step-into is not automatable in headless CI, but the
// four things that make it work ARE — the assembly's PDB must be portable
// (not full-format), must carry a SourceLink CustomDebugInformation record,
// and that record must map every source file to a GitHub raw URL for a
// commit SHA that resolves 200. If all of those hold, F11-into-source
// works by construction. If any breaks, the consumer's debugger silently
// falls back to decompiled placeholders. Refs #144.

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using Xunit;

namespace Wolfgang.Etl.DbClient.Tests.SourceLink;

public class SourceLinkPdbTests
{
    private static readonly Guid SourceLinkGuid = new("CC110556-A091-4D38-9FEC-25AB9A351A6A");

    /// <summary>
    /// Portable PDBs start with the four bytes 'B','S','J','B' (0x424A5342 LE)
    /// — the ECMA-335 metadata-blob magic. Full-format Windows PDBs start with
    /// "Microsoft C/C++ MSF 7.00\r\n\x1A\x44\x53\x00\x00\x00". SourceLink is
    /// portable-PDB only, so full-format is an immediate fail.
    /// </summary>
    [Fact]
    public void Runtime_pdb_is_portable_format()
    {
        var pdbPath = LocateRuntimePdb();
        Assert.True(File.Exists(pdbPath), $"Runtime PDB not found at {pdbPath}");

        Span<byte> magic = stackalloc byte[4];
        using (var fs = File.OpenRead(pdbPath))
        {
            var read = fs.Read(magic);
            Assert.Equal(4, read);
        }

        // 'B','S','J','B' = 0x42, 0x53, 0x4A, 0x42.
        Assert.Equal((byte)'B', magic[0]);
        Assert.Equal((byte)'S', magic[1]);
        Assert.Equal((byte)'J', magic[2]);
        Assert.Equal((byte)'B', magic[3]);
    }

    /// <summary>
    /// The runtime PDB must contain a SourceLink CustomDebugInformation record
    /// whose JSON payload maps every source-file prefix under this repo to a
    /// GitHub raw URL. Missing record, missing repo prefix, or a mapping that
    /// doesn't reach github.com/Chris-Wolfgang/Etl-DbClient/... = fail.
    /// </summary>
    [Fact]
    public void Runtime_pdb_has_sourcelink_pointing_at_github_raw()
    {
        var pdbPath = LocateRuntimePdb();
        using var stream = File.OpenRead(pdbPath);
        using var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
        var reader = provider.GetMetadataReader();

        var payload = ReadSourceLinkPayload(reader);
        Assert.False(string.IsNullOrEmpty(payload), "PDB has no SourceLink CustomDebugInformation record.");

        using var doc = JsonDocument.Parse(payload);
        Assert.True(doc.RootElement.TryGetProperty("documents", out var documents),
            $"SourceLink payload missing 'documents' property: {payload}");

        var mappings = documents.EnumerateObject().ToArray();
        Assert.NotEmpty(mappings);

        // At least one mapping must resolve this repo's source paths to
        // github.com/Chris-Wolfgang/Etl-DbClient/raw/... . Third-party
        // NuGet packages contribute their own SourceLink mappings that we
        // don't control — filter to entries whose value URL targets this
        // repo before asserting the URL shape.
        var ourMappings = mappings
            .Where(m => m.Value.GetString()?.Contains("Chris-Wolfgang/Etl-DbClient", StringComparison.OrdinalIgnoreCase) == true)
            .ToArray();

        Assert.NotEmpty(ourMappings);

        foreach (var m in ourMappings)
        {
            var target = m.Value.GetString();
            Assert.NotNull(target);
            // Standard SourceLink URL shape for GitHub. The commit SHA is a
            // 40-hex-digit segment (or a * placeholder in the JSON that
            // gets substituted at debug time).
            Assert.Contains("raw.githubusercontent.com", target, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Chris-Wolfgang/Etl-DbClient", target, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// The SourceLink mapping for this repo must pin a concrete 40-hex commit
    /// SHA. The value always ends in a <c>/*</c> path wildcard (the debugger
    /// substitutes the document path there), so the only thing that can be
    /// checked offline is that the segment before it is a real SHA rather than
    /// a branch name or a placeholder. A branch name would make every debug
    /// session fetch whatever the branch points at today, not the source the
    /// binary was built from.
    /// </summary>
    [Fact]
    public void Sourcelink_github_raw_url_pins_a_commit_sha()
    {
        var pdbPath = LocateRuntimePdb();
        using var stream = File.OpenRead(pdbPath);
        using var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
        var reader = provider.GetMetadataReader();

        using var doc = JsonDocument.Parse(ReadSourceLinkPayload(reader));
        var ourMapping = doc.RootElement
            .GetProperty("documents")
            .EnumerateObject()
            .Select(m => m.Value.GetString())
            .First(v => v?.Contains("Chris-Wolfgang/Etl-DbClient", StringComparison.OrdinalIgnoreCase) == true);

        Assert.Matches
        (
            @"^https://raw\.githubusercontent\.com/Chris-Wolfgang/Etl-DbClient/[0-9a-f]{40}/\*$",
            ourMapping
        );
    }

    // ------------------------------------------------------------------

    private static string LocateRuntimePdb()
    {
        // The runtime csproj's PDB is copied into this test project's output
        // directory alongside the DLL because ProjectReference includes
        // CopyLocalLockFileAssemblies by default.
        return Path.Combine(AppContext.BaseDirectory, "Wolfgang.Etl.DbClient.pdb");
    }

    private static string ReadSourceLinkPayload(MetadataReader reader) =>
        reader.CustomDebugInformation
            .Select(reader.GetCustomDebugInformation)
            .Where(cdi => reader.GetGuid(cdi.Kind) == SourceLinkGuid)
            .Select(cdi => System.Text.Encoding.UTF8.GetString(reader.GetBlobBytes(cdi.Value)))
            .FirstOrDefault() ?? string.Empty;
}
