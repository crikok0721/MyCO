using System.Text.Json;
using MyCO.Compatibility;
using MyCO.Configuration;
using MyCO.Diagnostics;
using MyCO.Injection;

namespace MyCO.Tests;

public sealed class SecurityBoundaryTests
{
    [Fact]
    public void BuildMetadataComesFromTheSharedVersionSource()
    {
        Assert.Equal("0.99.1", BuildInfo.Version);
        Assert.Equal(1, BuildInfo.ProtocolVersion);
        Assert.Equal(4, BuildInfo.ConfigSchemaVersion);
        Assert.Equal(1, BuildInfo.CalibrationSchemaVersion);
    }

    [Fact]
    public void CalibrationNormalizationDropsUnknownDataAndRebuildsFingerprint()
    {
        var normalized = ElementSignatureValidator.Normalize(
            new ElementSignature
            {
                SampleCount = 3,
                ContextFingerprint = "main;;thread",
                TagName = "ARTICLE",
                Role = "article",
                StableAttributes = new Dictionary<string, string>
                {
                    ["data-message-author-role"] = "assistant",
                    ["aria-label"] = "private conversation text"
                },
                StableClasses = ["message", "hash-123"],
                Fingerprint = "attacker-controlled"
            });

        Assert.Equal("article", normalized.TagName);
        Assert.DoesNotContain("aria-label", normalized.StableAttributes.Keys);
        Assert.DoesNotContain("private conversation text", normalized.Fingerprint);
        Assert.NotEqual("attacker-controlled", normalized.Fingerprint);
        Assert.Equal(3, normalized.SampleCount);
        Assert.Equal("main;;thread", normalized.ContextFingerprint);
    }

    [Fact]
    public void RuntimeErrorEventKeepsOnlyAnAllowlistedCode()
    {
        using var document = JsonDocument.Parse(
            """{"code":"runtime.failed","message":"private prompt","path":"C:\\secret"}""");
        var normalized = RuntimeEventValidator.Normalize(
            new RuntimeHostEvent(
                "error",
                document.RootElement.Clone(),
                BuildInfo.ProtocolVersion,
                DateTimeOffset.MinValue));

        Assert.Equal("runtime.failed", normalized.Payload.GetProperty("code").GetString());
        Assert.False(normalized.Payload.TryGetProperty("message", out _));
        Assert.False(normalized.Payload.TryGetProperty("path", out _));
        Assert.True(normalized.At > DateTimeOffset.MinValue);
    }

    [Fact]
    public void DiagnosticsClampCountsAndDiscardErrorMessages()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "version":"test",
              "protocolVersion":1,
              "compatibility":"compatible",
              "scannedTurns":999999999,
              "assistantBubbleBlocks":27,
              "averageConfidence":9,
              "errors":[{"code":"runtime.failed","message":"private prompt"}]
            }
            """);

        var normalized = RuntimeDiagnosticsValidator.Normalize(document.RootElement);

        Assert.Equal(100_000, normalized.GetProperty("scannedTurns").GetInt32());
        Assert.Equal(27, normalized.GetProperty("assistantBubbleBlocks").GetInt32());
        Assert.Equal(1, normalized.GetProperty("averageConfidence").GetDouble());
        Assert.False(
            normalized.GetProperty("errors")[0].TryGetProperty("message", out _));
    }

    [Fact]
    public void CalibrationRejectsStructurallyAmbiguousRoles()
    {
        var signature = ElementSignatureValidator.Normalize(
            new ElementSignature
            {
                TagName = "div",
                StableAttributes =
                {
                    ["data-content-search-unit-key"] = "present"
                },
                Capabilities = new SignatureCapabilities { HasMarkdown = true }
            });

        Assert.False(ElementSignatureValidator.AreDistinctRoles(signature, signature));
    }

    [Fact]
    public void CalibrationAcceptsTextFreeUserBubbleAndAssistantProseEvidence()
    {
        var user = ElementSignatureValidator.Normalize(
            new ElementSignature
            {
                TagName = "div",
                StableAttributes =
                {
                    ["data-content-search-unit-key"] = "present",
                    ["data-user-message-bubble"] = "present"
                },
                Capabilities = new SignatureCapabilities { HasMarkdown = true }
            });
        var assistant = ElementSignatureValidator.Normalize(
            new ElementSignature
            {
                TagName = "div",
                StableAttributes =
                {
                    ["data-content-search-unit-key"] = "present",
                    ["data-content-type"] = "prose"
                },
                Capabilities = new SignatureCapabilities { HasMarkdown = true }
            });

        Assert.True(ElementSignatureValidator.AreDistinctRoles(user, assistant));
        Assert.NotEqual(user.Fingerprint, assistant.Fingerprint);
    }

    [Fact]
    public async Task OversizedConfigIsMovedToBackupWithoutBeingCopied()
    {
        using var directory = new TempDirectory();
        var paths = new ConfigPaths(directory.Path);
        paths.EnsureDirectories();
        await File.WriteAllTextAsync(paths.ConfigFile, new string('x', 300_000));

        var result = await new ConfigStore(paths).LoadAsync();

        Assert.NotNull(result.CorruptBackupPath);
        Assert.True(File.Exists(result.CorruptBackupPath));
        Assert.Equal(300_000, new FileInfo(result.CorruptBackupPath!).Length);
        Assert.True(new FileInfo(paths.ConfigFile).Length < 256 * 1024);
    }

    [Fact]
    public void LoggerRedactsSecretsPathsAndUnknownProperties()
    {
        using var directory = new TempDirectory();
        var logger = new PrivacySafeLogger(directory.Path);
        logger.Info(
            "sample",
            new Dictionary<string, object?>
            {
                ["state"] = "token=top-secret C:\\Users\\someone\\private.txt",
                ["conversation"] = "must never be logged"
            });
        logger.Error(
            "failure",
            new InvalidOperationException(
                "authorization: bearer-value test@example.com C:\\private\\file.txt"));

        var content = string.Join(
            Environment.NewLine,
            Directory.GetFiles(directory.Path, "*.jsonl")
                .Select(File.ReadAllText));
        Assert.DoesNotContain("top-secret", content);
        Assert.DoesNotContain("bearer-value", content);
        Assert.DoesNotContain("test@example.com", content);
        Assert.DoesNotContain("must never be logged", content);
        Assert.DoesNotContain(@"C:\private", content);
        Assert.Contains("[redacted]", content);
    }
}
