#:property TargetFramework=net10.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

const string PackageName = "codex-copilot-pr-review-agent";
const string LocalReviewerName = "local-reviewer";
const string ReviewPlannerName = "review-planner";
const string SparkImplementerName = "spark-implementer";

var options = ParseArguments(args);
if (options is null || options.ShowHelp || string.IsNullOrWhiteSpace(options.TargetRepoRoot))
{
    ShowUsage();
    if (options is not null && options.HasError)
    {
        return 2;
    }

    return 0;
}

if (options.HasError)
{
    ShowUsage();
    return 2;
}

if (options.DryRun)
{
    Console.WriteLine("[dry-run] No files will be written.");
}

if (options.CheckOnly)
{
    Console.WriteLine("[check-only] No files will be written.");
}

var targetRepoRoot = Path.GetFullPath(options.TargetRepoRoot);
if (!Directory.Exists(targetRepoRoot))
{
    Console.WriteLine($"Error: target repository not found: {targetRepoRoot}");
    return 2;
}

var packageRoot = ResolvePackageRoot(options.PackageRoot, GetSourceFilePath());
if (packageRoot is null)
{
    Console.WriteLine("Error: package source was not found.");
    Console.WriteLine("Run from this repository root, or pass --package-root with the repository path.");
    return 2;
}

var sourceAgentMarkdownDir = Path.Combine(packageRoot, ".apm", "agents");
var sourceSkill = Path.Combine(packageRoot, ".apm", "skills", PackageName);
var sourceInstallerScript = Path.Combine(packageRoot, "scripts", "install-codex-copilot-pr-review-agent-local.cs");
var sourceSkillScript = Path.Combine(sourceSkill, "scripts");

if (!Directory.Exists(sourceAgentMarkdownDir)
    || !HasRequiredAgentMarkdowns(sourceAgentMarkdownDir)
    || !Directory.Exists(sourceSkill)
    || !Directory.Exists(sourceSkillScript)
    || !File.Exists(sourceInstallerScript))
{
    Console.WriteLine("Error: required package inputs are missing.");
    return 2;
}

var logs = new List<string>();
var blockers = new List<string>();

try
{
    CopyProfileConfig(
        targetRepoRoot,
        options.DryRun,
        options.CheckOnly,
        logs,
        blockers);

    CopyTomlAgentsFromMarkdown(
        sourceAgentMarkdownDir,
        targetRepoRoot,
        options.DryRun,
        options.CheckOnly,
        options.Force,
        logs,
        blockers);

    CopySkillDirectory(
        sourceSkill,
        targetRepoRoot,
        options.DryRun,
        options.CheckOnly,
        options.Force,
        logs,
        blockers);

        CopySingleFile(
            sourceInstallerScript,
            Path.Combine(targetRepoRoot, ".agents", "skills", PackageName, "scripts", Path.GetFileName(sourceInstallerScript)),
            ".agents/skills/" + PackageName + "/scripts/" + Path.GetFileName(sourceInstallerScript),
            options.DryRun,
            options.CheckOnly,
            options.Force,
        logs,
        blockers);
}
catch (Exception ex)
{
    Trace.TraceError(ex.ToString());
    Console.Error.WriteLine("Error: installation failed.");
    Console.Error.WriteLine(ex.ToString());
    return 2;
}

Console.WriteLine();
Console.WriteLine("=== Installation report ===");
foreach (var log in logs)
{
    Console.WriteLine(log);
}

if (blockers.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("=== Blockers ===");
    foreach (var blocker in blockers)
    {
        Console.WriteLine(blocker);
    }

    Console.WriteLine();
    Console.WriteLine("Resolve blockers and rerun. Use --force to overwrite conflicting files.");
    return 2;
}

if (options.DryRun || options.CheckOnly)
{
    Console.WriteLine();
    Console.WriteLine(options.CheckOnly ? "check-only complete." : "dry-run complete.");
}
else
{
    Console.WriteLine();
    Console.WriteLine("install complete.");
}

return 0;

static InstallOptions ParseArguments(string[] args)
{
    var options = new InstallOptions();
    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        if (arg is "--dry-run" or "-n")
        {
            options.DryRun = true;
            continue;
        }

        if (arg is "--check" or "--check-only")
        {
            options.CheckOnly = true;
            continue;
        }

        if (arg is "--force" or "-f")
        {
            options.Force = true;
            continue;
        }

        if (arg is "--verbose" or "-v")
        {
            options.Verbose = true;
            continue;
        }

        if (arg is "--help" or "-h")
        {
            options.ShowHelp = true;
            continue;
        }

        if ((arg is "--package-root" or "-p") && i + 1 < args.Length)
        {
            options.PackageRoot = args[++i];
            continue;
        }

        if (arg.StartsWith("-", StringComparison.Ordinal))
        {
            options.HasError = true;
            options.ShowHelp = true;
            continue;
        }

        if (string.IsNullOrWhiteSpace(options.TargetRepoRoot))
        {
            options.TargetRepoRoot = arg;
            continue;
        }

        options.HasError = true;
        options.ShowHelp = true;
    }

    if (options.DryRun && options.CheckOnly)
    {
        options.HasError = true;
        options.ShowHelp = true;
    }

    return options;
}

static void ShowUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --file scripts/install-codex-copilot-pr-review-agent-local.cs -- <target-repo-root> [options]");
    Console.WriteLine();
    Console.WriteLine("Arguments:");
    Console.WriteLine("  <target-repo-root>      Target repository root path.");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --dry-run, -n           Show planned actions only.");
    Console.WriteLine("  --check-only, --check    Validate required files and differences only.");
    Console.WriteLine("  --force, -f             Overwrite conflicting files.");
    Console.WriteLine("  --verbose, -v           Show detailed errors.");
    Console.WriteLine("  --package-root <dir>     Explicit package root path.");
    Console.WriteLine("  --help, -h              Show this help.");
}

static string GetSourceFilePath([CallerFilePath] string path = "")
{
    return path;
}

static string? ResolvePackageRoot(string? overrideRoot, string sourceFilePath)
{
    if (!string.IsNullOrWhiteSpace(overrideRoot))
    {
        var explicitRoot = Path.GetFullPath(overrideRoot);
        return IsPackageRoot(explicitRoot) ? explicitRoot : null;
    }

    var candidates = new List<string>();
    if (!string.IsNullOrWhiteSpace(sourceFilePath))
    {
        AddPackageRootCandidates(Path.GetDirectoryName(Path.GetFullPath(sourceFilePath)), candidates);
    }

    AddPackageRootCandidates(Path.GetFullPath(Directory.GetCurrentDirectory()), candidates);

    foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
    {
        if (IsPackageRoot(candidate))
        {
            return Path.GetFullPath(candidate);
        }
    }

    return null;
}

static void AddPackageRootCandidates(string? startDirectory, List<string> candidates)
{
    if (string.IsNullOrWhiteSpace(startDirectory))
    {
        return;
    }

    var current = new DirectoryInfo(startDirectory);
    while (current is not null)
    {
        candidates.Add(current.FullName);
        current = current.Parent;
    }
}

static bool IsPackageRoot(string dir)
{
    var sourceAgentMarkdownDir = Path.Combine(dir, ".apm", "agents");
    return HasRequiredAgentMarkdowns(sourceAgentMarkdownDir)
        && File.Exists(Path.Combine(dir, ".apm", "skills", PackageName, "SKILL.md"));
}

static bool HasRequiredAgentMarkdowns(string sourceAgentMarkdownDir)
{
    return File.Exists(Path.Combine(sourceAgentMarkdownDir, $"{LocalReviewerName}.agent.md"))
        && File.Exists(Path.Combine(sourceAgentMarkdownDir, $"{ReviewPlannerName}.agent.md"))
        && File.Exists(Path.Combine(sourceAgentMarkdownDir, $"{SparkImplementerName}.agent.md"));
}

static void CopyProfileConfig(
    string targetRepoRoot,
    bool dryRun,
    bool checkOnly,
    List<string> logs,
    List<string> blockers)
{
    var targetPath = Path.Combine(targetRepoRoot, ".codex", "config.toml");
    var requiredValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["model"] = "\"gpt-5.5\"",
        ["model_reasoning_effort"] = "\"medium\"",
    };
    var sourceText = BuildConfigToml(requiredValues);

    if (!File.Exists(targetPath))
    {
        if (checkOnly)
        {
            blockers.Add(".codex/config.toml: file is missing");
            return;
        }

        WriteOrDryRun(sourceText, targetPath, dryRun, ".codex/config.toml: add", logs);
        if (!dryRun)
        {
            logs.Add(".codex/config.toml: added");
        }

        return;
    }

    var targetText = File.ReadAllText(targetPath);
    var mergedText = MergeTomlContent(targetText, requiredValues);
    if (Normalize(targetText) == Normalize(mergedText))
    {
        logs.Add(".codex/config.toml: no change");
        return;
    }

    if (checkOnly)
    {
        AddTomlValueBlockers(".codex/config.toml", ParseTomlValues(targetText), requiredValues, blockers);
        return;
    }

    WriteOrDryRun(mergedText, targetPath, dryRun, ".codex/config.toml: merge and update", logs);
    if (!dryRun)
    {
        logs.Add(".codex/config.toml: merged");
    }
}

static void CopyTomlAgentsFromMarkdown(
    string sourceAgentDir,
    string targetRepoRoot,
    bool dryRun,
    bool checkOnly,
    bool force,
    List<string> logs,
    List<string> blockers)
{
    var targetDir = Path.Combine(targetRepoRoot, ".codex", "agents");

    foreach (var sourcePath in Directory.GetFiles(sourceAgentDir, "*.agent.md").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
    {
        var agent = ParseAgentMarkdown(sourcePath);
        var fileName = agent.Name + ".toml";
        var display = ".codex/agents/" + fileName;
        var targetPath = Path.Combine(targetDir, fileName);
        var sourceText = BuildAgentToml(agent);
        var sourceValues = ParseTomlValues(sourceText);

        if (!File.Exists(targetPath))
        {
            if (checkOnly)
            {
                blockers.Add($"{display}: file is missing");
                continue;
            }

            WriteOrDryRun(sourceText, targetPath, dryRun, $"{display}: add", logs);
            if (!dryRun)
            {
                logs.Add($"{display}: added");
            }

            continue;
        }

        var targetText = File.ReadAllText(targetPath);
        var targetValues = ParseTomlValues(targetText);
        ValidateTopLevelKeys(sourceValues, targetValues, display, force, dryRun, checkOnly, logs, blockers);

        if (Normalize(sourceText) == Normalize(targetText))
        {
            logs.Add($"{display}: no change");
            continue;
        }

        if (!force)
        {
            blockers.Add($"{display}: file differs. Run with --force to overwrite.");
            continue;
        }

        if (checkOnly)
        {
            blockers.Add($"{display}: overwrite is required. Run with --force.");
            continue;
        }

        if (dryRun)
        {
            logs.Add($"[dry-run] {display}: overwrite");
            continue;
        }

        WriteOrDryRun(sourceText, targetPath, dryRun, $"{display}: overwrite", logs);
        logs.Add($"{display}: overwritten");
    }
}

static void CopySkillDirectory(
    string sourceSkillDir,
    string targetRepoRoot,
    bool dryRun,
    bool checkOnly,
    bool force,
    List<string> logs,
    List<string> blockers)
{
    var targetDir = Path.Combine(targetRepoRoot, ".agents", "skills", PackageName);
    foreach (var sourcePath in Directory.GetFiles(sourceSkillDir, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
    {
        var relative = Path.GetRelativePath(sourceSkillDir, sourcePath);
        var normalizedRelative = relative.Replace(Path.DirectorySeparatorChar, '/');
        var targetPath = Path.Combine(targetDir, relative);
        CopySingleFile(
            sourcePath,
            targetPath,
            ".agents/skills/" + PackageName + "/" + normalizedRelative,
            dryRun,
            checkOnly,
            force,
            logs,
            blockers);
    }
}

static void CopySingleFile(
    string sourcePath,
    string targetPath,
    string displayPath,
    bool dryRun,
    bool checkOnly,
    bool force,
    List<string> logs,
    List<string> blockers)
{
    var sourceText = File.ReadAllText(sourcePath);
        if (!File.Exists(targetPath))
        {
            if (checkOnly)
            {
                blockers.Add($"{displayPath}: file is missing");
                return;
            }

            WriteOrDryRun(sourceText, targetPath, dryRun, $"{displayPath}: add", logs);
            if (!dryRun)
            {
                logs.Add($"{displayPath}: added");
            }

        return;
    }

    var targetText = File.ReadAllText(targetPath);
    if (Normalize(sourceText) == Normalize(targetText))
    {
        logs.Add($"{displayPath}: no change");
        return;
    }

    if (!force)
    {
        blockers.Add($"{displayPath}: file differs. Run with --force to overwrite.");
        return;
    }

    if (checkOnly)
    {
        blockers.Add($"{displayPath}: overwrite is required. Run with --force.");
        return;
    }

    if (dryRun)
    {
        logs.Add($"[dry-run] {displayPath}: overwrite");
        return;
    }

    WriteOrDryRun(sourceText, targetPath, dryRun, $"{displayPath}: overwrite", logs);
    logs.Add($"{displayPath}: overwritten");
}

static void WriteOrDryRun(string content, string targetPath, bool dryRun, string log, List<string> logs)
{
    if (dryRun)
    {
        logs.Add($"[dry-run] {log}");
        return;
    }

    var dir = Path.GetDirectoryName(targetPath);
    if (!string.IsNullOrWhiteSpace(dir))
    {
        Directory.CreateDirectory(dir);
    }

    File.WriteAllText(targetPath, content, Encoding.UTF8);
}

static void ValidateTopLevelKeys(
    Dictionary<string, string> sourceValues,
    Dictionary<string, string> targetValues,
    string display,
    bool force,
    bool dryRun,
    bool checkOnly,
    List<string> logs,
    List<string> blockers)
{
    var missingOrDifferent = sourceValues
        .Where(item => !targetValues.TryGetValue(item.Key, out var targetValue) || !string.Equals(targetValue, item.Value, StringComparison.Ordinal))
        .Select(item => item)
        .ToList();

    if (missingOrDifferent.Count == 0)
    {
        return;
    }

    if (force && !dryRun && !checkOnly)
    {
        var prefix = string.Join(", ", missingOrDifferent.Select(kv => $"`{kv.Key}`"));
        logs.Add($"[repair] {display}: top-level keys need correction ({prefix})");
        return;
    }

    foreach (var item in missingOrDifferent)
    {
        blockers.Add($"{display}: top-level `{item.Key}` is `{(targetValues.TryGetValue(item.Key, out var value) ? value : "missing")}`, expected `{item.Value}`");
    }
}

static AgentDefinition ParseAgentMarkdown(string path)
{
    var lines = File.ReadAllLines(path);
    if (lines.Length == 0 || lines[0].Trim() != "---")
    {
        throw new InvalidOperationException($"Agent front matter is missing: {path}");
    }

    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 1; i < lines.Length; i++)
    {
        var line = lines[i].Trim();
        if (line == "---")
        {
            break;
        }

        var index = line.IndexOf(':');
        if (index <= 0)
        {
            continue;
        }

        var key = line[..index].Trim();
        var value = TrimQuotes(line[(index + 1)..].Trim());
        values[key] = value;
    }

    if (!values.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name)
        || !values.TryGetValue("description", out var description) || string.IsNullOrWhiteSpace(description)
        || !values.TryGetValue("model", out var model) || string.IsNullOrWhiteSpace(model))
    {
        throw new InvalidOperationException($"Agent front matter must include name, description, and model: {path}");
    }

    return new AgentDefinition(name, description, model);
}

static string BuildConfigToml(Dictionary<string, string> requiredValues)
{
    var builder = new StringBuilder();
    builder.AppendLine("# Codex local defaults generated from codex-copilot-pr-review-agent agent front matter.");
    builder.AppendLine($"model = {requiredValues["model"]}");
    builder.AppendLine($"model_reasoning_effort = {requiredValues["model_reasoning_effort"]}");
    return builder.ToString();
}

static string BuildAgentToml(AgentDefinition agent)
{
    var builder = new StringBuilder();
    builder.AppendLine($"name = \"{EscapeTomlString(agent.Name)}\"");
    builder.AppendLine($"description = \"{EscapeTomlString(agent.Description)}\"");
    builder.AppendLine($"model = \"{EscapeTomlString(agent.Model)}\"");

    var reasoningEffort = GetModelReasoningEffort(agent.Name);
    if (!string.IsNullOrWhiteSpace(reasoningEffort))
    {
        builder.AppendLine($"model_reasoning_effort = \"{EscapeTomlString(reasoningEffort)}\"");
    }

    builder.AppendLine($"sandbox_mode = \"{EscapeTomlString(GetSandboxMode(agent.Name))}\"");
    return builder.ToString();
}

static string? GetModelReasoningEffort(string agentName)
{
    return agentName switch
    {
        LocalReviewerName or ReviewPlannerName => "medium",
        SparkImplementerName => "high",
        _ => null,
    };
}

static string GetSandboxMode(string agentName)
{
    return agentName switch
    {
        LocalReviewerName or ReviewPlannerName => "read-only",
        SparkImplementerName => "workspace-write",
        _ => throw new InvalidOperationException($"Unsupported agent name: {agentName}"),
    };
}

static string EscapeTomlString(string value)
{
    return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}

static Dictionary<string, string> ParseTomlValues(string text)
{
    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    using var reader = new StringReader(text);
    string? line;
    while ((line = reader.ReadLine()) is not null)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal))
        {
            continue;
        }

        if (trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            break;
        }

        var index = trimmed.IndexOf('=');
        if (index <= 0)
        {
            continue;
        }

        var key = trimmed[..index].Trim();
        var value = trimmed[(index + 1)..].Trim();
        if (key is "model" or "model_reasoning_effort" or "sandbox_mode")
        {
            values[key] = value;
        }
    }

    return values;
}

static string MergeTomlContent(string targetText, Dictionary<string, string> sourceValues)
{
    var lines = targetText.Replace("\r\n", "\n").Split('\n').ToList();
    var changed = lines.ToList();

    int FindInsertIndex()
    {
        var firstSection = changed.FindIndex(line => line.StartsWith("[", StringComparison.Ordinal));
        return firstSection >= 0 ? firstSection : changed.Count;
    }

    foreach (var sourceValue in sourceValues)
    {
        UpsertTopLevelValue(changed, sourceValue.Key, sourceValue.Value, FindInsertIndex);
    }

    return string.Join(Environment.NewLine, changed.Where(line => line is not null));
}

static void UpsertTopLevelValue(List<string> lines, string key, string expectedValue, Func<int> findInsertIndex)
{
    var topLevelEnd = findInsertIndex();
    for (var i = 0; i < topLevelEnd; i++)
    {
        if (IsTopLevelAssignment(lines[i], key))
        {
            lines[i] = $"{key} = {expectedValue}";
            return;
        }
    }

    lines.Insert(topLevelEnd, $"{key} = {expectedValue}");
}

static void AddTomlValueBlockers(
    string display,
    Dictionary<string, string> targetValues,
    Dictionary<string, string> sourceValues,
    List<string> blockers)
{
    foreach (var item in sourceValues)
    {
        if (!targetValues.TryGetValue(item.Key, out var targetValue))
        {
            blockers.Add($"{display}: top-level `{item.Key}` is missing, expected `{item.Value}`");
            continue;
        }

        if (!string.Equals(targetValue, item.Value, StringComparison.Ordinal))
        {
            blockers.Add($"{display}: top-level `{item.Key}` is `{targetValue}`, expected `{item.Value}`");
        }
    }
}

static string TrimQuotes(string value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return value;
    }

    if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
    {
        return value[1..^1];
    }

    return value;
}

static bool IsTopLevelAssignment(string line, string key)
{
    return line.TrimStart().StartsWith($"{key} =", StringComparison.OrdinalIgnoreCase)
        || line.TrimStart().StartsWith($"{key}=", StringComparison.OrdinalIgnoreCase);
}

static string Normalize(string text)
{
    return text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
}

sealed class InstallOptions
{
    public string? TargetRepoRoot { get; set; }
    public string? PackageRoot { get; set; }
    public bool DryRun { get; set; }
    public bool CheckOnly { get; set; }
    public bool Force { get; set; }
    public bool Verbose { get; set; }
    public bool ShowHelp { get; set; }
    public bool HasError { get; set; }
}

sealed record AgentDefinition(string Name, string Description, string Model);
