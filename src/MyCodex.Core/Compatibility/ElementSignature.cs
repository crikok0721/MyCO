namespace MyCodex.Compatibility;

public sealed record SignatureAncestor
{
    public string TagName { get; init; } = string.Empty;
    public string? Role { get; init; }
}

public sealed record SignatureCapabilities
{
    public bool HasMarkdown { get; init; }
    public bool HasCode { get; init; }
    public bool HasButtons { get; init; }
}

public sealed record SignatureLayout
{
    public string Alignment { get; init; } = "unknown";
    public double WidthRatio { get; init; }
}

public sealed record ElementSignature
{
    public int SchemaVersion { get; init; } = 1;
    public string TagName { get; init; } = string.Empty;
    public string? Role { get; init; }
    public Dictionary<string, string> StableAttributes { get; init; } = [];
    public List<string> StableClasses { get; init; } = [];
    public List<SignatureAncestor> AncestorChain { get; init; } = [];
    public Dictionary<string, int> ChildTagHistogram { get; init; } = [];
    public SignatureCapabilities Capabilities { get; init; } = new();
    public SignatureLayout Layout { get; init; } = new();
    public string Fingerprint { get; init; } = string.Empty;
}
