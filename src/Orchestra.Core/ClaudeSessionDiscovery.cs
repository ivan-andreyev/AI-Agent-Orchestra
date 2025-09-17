using System.Text.Json;
using System.IO;
using System.Linq;
using Orchestra.Core.Models;

namespace Orchestra.Core;

public class ClaudeSessionDiscovery
{
    private readonly string _claudeProjectsPath;

    public ClaudeSessionDiscovery(string claudeProjectsPath = @"C:\Users\mrred\.claude\projects")
    {
        _claudeProjectsPath = claudeProjectsPath;
    }

    public List<AgentInfo> DiscoverActiveSessions()
    {
        var agents = new List<AgentInfo>();

        if (!Directory.Exists(_claudeProjectsPath))
            return agents;

        var projectDirectories = Directory.GetDirectories(_claudeProjectsPath);

        foreach (var projectDir in projectDirectories)
        {
            var projectName = Path.GetFileName(projectDir);
            var repositoryPath = DecodeProjectPath(projectName);

            if (string.IsNullOrEmpty(repositoryPath) || !Directory.Exists(repositoryPath))
                continue;

            var sessionFiles = Directory.GetFiles(projectDir, "*.jsonl");

            foreach (var sessionFile in sessionFiles)
            {
                var sessionId = Path.GetFileNameWithoutExtension(sessionFile);
                var lastWriteTime = File.GetLastWriteTime(sessionFile);

                var agentId = $"{Path.GetFileName(repositoryPath)}_{sessionId}";
                var agentName = $"Claude Code - {Path.GetFileName(repositoryPath)} ({sessionId[..8]})";

                var status = DetermineSessionStatus(sessionFile, lastWriteTime);

                var agent = new AgentInfo(
                    agentId,
                    agentName,
                    "claude-code",
                    repositoryPath,
                    status,
                    lastWriteTime,
                    null,
                    sessionId
                );

                agents.Add(agent);
            }
        }

        return agents;
    }

    private string DecodeProjectPath(string encodedPath)
    {
        if (string.IsNullOrEmpty(encodedPath))
            return encodedPath;

        // First handle drive letter replacement (e.g., "C--" -> "C:\" or "c--" -> "C:\")
        if (encodedPath.Length >= 3 && char.IsLetter(encodedPath[0]) && encodedPath[1] == '-' && encodedPath[2] == '-')
        {
            var drivePrefix = char.ToUpper(encodedPath[0]) + ":\\";
            var remainingPath = encodedPath.Substring(3);

            // Handle double dashes first as they definitely represent directory separators
            remainingPath = remainingPath.Replace("--", "\\");

            // Split the remaining path and reconstruct intelligently
            var parts = remainingPath.Split('-');

            if (parts.Length >= 4 && remainingPath.Contains("RiderProjects"))
            {
                // For typical structure: Users-username-RiderProjects-ProjectName
                // Join first 3 parts as directories and rest as project name
                var directoryParts = string.Join("\\", parts.Take(3));
                var projectName = string.Join("-", parts.Skip(3));
                var decodedPath = drivePrefix + directoryParts + "\\" + projectName;

                // If the path doesn't exist, try with underscores instead of hyphens
                if (!Directory.Exists(decodedPath))
                {
                    var projectNameWithUnderscores = string.Join("_", parts.Skip(3));
                    var alternativePath = drivePrefix + directoryParts + "\\" + projectNameWithUnderscores;
                    if (Directory.Exists(alternativePath))
                    {
                        return alternativePath;
                    }
                }

                return decodedPath;
            }
            else if (parts.Length >= 3)
            {
                // For other paths like D:\Projects\My-Project
                // Special handling for paths with project names containing dashes
                if (parts.Length >= 3 && (remainingPath.Contains("Project") || remainingPath.Contains("project")))
                {
                    // For paths like "Projects-My-Project", keep project name with dashes
                    var directoryParts = string.Join("\\", parts.Take(parts.Length - 2));
                    var projectName = string.Join("-", parts.Skip(parts.Length - 2));
                    return drivePrefix + directoryParts + "\\" + projectName;
                }
                else
                {
                    // Join first N-1 parts as directories and last part as file/folder name
                    var directoryParts = string.Join("\\", parts.Take(parts.Length - 1));
                    var lastName = parts.Last();
                    return drivePrefix + directoryParts + "\\" + lastName;
                }
            }
            else
            {
                // Simple case: replace all remaining dashes with backslashes
                return drivePrefix + remainingPath.Replace("-", "\\");
            }
        }

        // If no drive letter pattern, just replace double dashes
        return encodedPath.Replace("--", @"\");
    }

    private AgentStatus DetermineSessionStatus(string sessionFile, DateTime lastWriteTime)
    {
        var timeSinceLastUpdate = DateTime.Now - lastWriteTime;

        if (timeSinceLastUpdate > TimeSpan.FromMinutes(10))
            return AgentStatus.Offline;

        if (timeSinceLastUpdate > TimeSpan.FromMinutes(5))
            return AgentStatus.Idle;

        try
        {
            var lastLines = ReadLastLines(sessionFile, 5);
            if (lastLines.Any(line => line.Contains("\"type\":\"assistant\"")))
                return AgentStatus.Working;
        }
        catch
        {
            // Ignore file read errors
        }

        return AgentStatus.Idle;
    }

    private List<string> ReadLastLines(string filePath, int lineCount)
    {
        var lines = new List<string>();

        try
        {
            using var reader = new StreamReader(filePath);
            var allLines = new List<string>();

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                allLines.Add(line);
            }

            return allLines.TakeLast(lineCount).ToList();
        }
        catch
        {
            return lines;
        }
    }

    public Dictionary<string, RepositoryInfo> GroupAgentsByRepository(List<AgentInfo> agents)
    {
        var repositories = new Dictionary<string, RepositoryInfo>();

        var groupedAgents = agents.GroupBy(a => a.RepositoryPath);

        foreach (var group in groupedAgents)
        {
            var repositoryPath = group.Key;
            var repositoryName = Path.GetFileName(repositoryPath);
            var repositoryAgents = group.ToList();

            var idleCount = repositoryAgents.Count(a => a.Status == AgentStatus.Idle);
            var workingCount = repositoryAgents.Count(a => a.Status == AgentStatus.Working);
            var errorCount = repositoryAgents.Count(a => a.Status == AgentStatus.Error);
            var offlineCount = repositoryAgents.Count(a => a.Status == AgentStatus.Offline);

            var repositoryInfo = new RepositoryInfo(
                repositoryName,
                repositoryPath,
                repositoryAgents,
                idleCount,
                workingCount,
                errorCount,
                offlineCount,
                DateTime.Now
            );

            repositories[repositoryName] = repositoryInfo;
        }

        return repositories;
    }

    public List<AgentHistoryEntry> GetAgentHistory(string sessionId, int maxEntries = 50)
    {
        var history = new List<AgentHistoryEntry>();

        if (string.IsNullOrEmpty(sessionId))
            return history;

        // Find the JSONL file for this session
        var projectDirectories = Directory.GetDirectories(_claudeProjectsPath);
        string? sessionFilePath = null;

        foreach (var projectDir in projectDirectories)
        {
            var sessionFile = Path.Combine(projectDir, $"{sessionId}.jsonl");
            if (File.Exists(sessionFile))
            {
                sessionFilePath = sessionFile;
                break;
            }
        }

        if (sessionFilePath == null)
            return history;

        try
        {
            var lastLines = ReadLastLines(sessionFilePath, maxEntries * 2); // Get more lines to filter properly

            foreach (var line in lastLines.TakeLast(maxEntries))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    using var document = JsonDocument.Parse(line);
                    var root = document.RootElement;

                    if (root.TryGetProperty("timestamp", out var timestampProp) &&
                        root.TryGetProperty("type", out var typeProp) &&
                        root.TryGetProperty("content", out var contentProp))
                    {
                        var timestamp = DateTime.TryParse(timestampProp.GetString(), out var dt) ? dt : DateTime.Now;
                        var type = typeProp.GetString() ?? "unknown";
                        var content = contentProp.GetString() ?? "";

                        // For assistant messages, try to get the text content
                        if (type == "assistant" && root.TryGetProperty("content", out var assistantContent))
                        {
                            if (assistantContent.ValueKind == JsonValueKind.Array)
                            {
                                var textParts = new List<string>();
                                foreach (var item in assistantContent.EnumerateArray())
                                {
                                    if (item.TryGetProperty("type", out var itemType) &&
                                        itemType.GetString() == "text" &&
                                        item.TryGetProperty("text", out var textProp))
                                    {
                                        textParts.Add(textProp.GetString() ?? "");
                                    }
                                }
                                content = string.Join(" ", textParts);
                            }
                        }
                        // For human messages
                        else if (type == "human" && root.TryGetProperty("content", out var humanContent))
                        {
                            if (humanContent.ValueKind == JsonValueKind.String)
                            {
                                content = humanContent.GetString() ?? "";
                            }
                            else if (humanContent.ValueKind == JsonValueKind.Array)
                            {
                                var textParts = new List<string>();
                                foreach (var item in humanContent.EnumerateArray())
                                {
                                    if (item.TryGetProperty("type", out var itemType) &&
                                        itemType.GetString() == "text" &&
                                        item.TryGetProperty("text", out var textProp))
                                    {
                                        textParts.Add(textProp.GetString() ?? "");
                                    }
                                }
                                content = string.Join(" ", textParts);
                            }
                        }

                        // Truncate very long content
                        if (content.Length > 500)
                        {
                            content = content.Substring(0, 500) + "...";
                        }

                        history.Add(new AgentHistoryEntry(timestamp, type, content));
                    }
                }
                catch
                {
                    // Skip malformed JSON lines
                }
            }
        }
        catch
        {
            // Return empty history on file read errors
        }

        return history.OrderBy(h => h.Timestamp).ToList();
    }
}