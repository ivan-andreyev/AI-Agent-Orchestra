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
        {
            return agents;
        }

        var projectDirectories = Directory.GetDirectories(_claudeProjectsPath);

        foreach (var projectDir in projectDirectories)
        {
            var projectName = Path.GetFileName(projectDir);
            var repositoryPath = DecodeProjectPath(projectName);

            if (string.IsNullOrEmpty(repositoryPath) || !Directory.Exists(repositoryPath))
            {
                continue;
            }

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
        {
            return encodedPath;
        }

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

                return decodedPath;
            }
            else if (parts.Length >= 3)
            {
                // For other paths like D:\Projects\My-Project or C:\Users\user-name\Documents\My-Super-Project
                // Special handling for paths with project names containing dashes
                if (parts.Length >= 3 && (remainingPath.Contains("Project") || remainingPath.Contains("project")))
                {
                    // For paths like "Users-user-name-Documents-My-Super-Project"
                    // Special handling for username patterns and known directories
                    var rebuiltParts = new List<string>();
                    var i = 0;

                    while (i < parts.Length)
                    {
                        var currentPart = parts[i];

                        // Handle special case: Users followed by username that might contain dashes
                        if (currentPart.Equals("Users", StringComparison.OrdinalIgnoreCase) &&
                            i + 1 < parts.Length)
                        {
                            rebuiltParts.Add(currentPart);
                            i++;

                            // Collect username parts until we hit a known directory
                            var usernameParts = new List<string>();
                            var knownDirs = new[] { "Documents", "Desktop", "Downloads", "Pictures", "Music", "Videos", "Projects" };

                            while (i < parts.Length &&
                                   !knownDirs.Any(dir => parts[i].Equals(dir, StringComparison.OrdinalIgnoreCase)))
                            {
                                usernameParts.Add(parts[i]);
                                i++;
                            }

                            // Join username parts with dashes
                            if (usernameParts.Count > 0)
                            {
                                rebuiltParts.Add(string.Join("-", usernameParts));
                            }
                        }
                        else
                        {
                            rebuiltParts.Add(currentPart);
                            i++;
                        }
                    }

                    // Now find where project starts - typically after Documents, Desktop, etc.
                    var knownDirectories = new[] { "Documents", "Projects", "Desktop", "Downloads", "Pictures", "Music", "Videos" };
                    int projectStartIndex = -1;

                    for (int j = 0; j < rebuiltParts.Count; j++)
                    {
                        if (knownDirectories.Any(dir => rebuiltParts[j].Equals(dir, StringComparison.OrdinalIgnoreCase)))
                        {
                            projectStartIndex = j + 1;
                            break;
                        }
                    }

                    if (projectStartIndex > 0 && projectStartIndex < rebuiltParts.Count)
                    {
                        var directoryParts = string.Join("\\", rebuiltParts.Take(projectStartIndex));
                        var projectName = string.Join("-", rebuiltParts.Skip(projectStartIndex));
                        return drivePrefix + directoryParts + "\\" + projectName;
                    }
                    else
                    {
                        // Fallback: assume last 2 parts are project name
                        var directoryParts = string.Join("\\", rebuiltParts.Take(rebuiltParts.Count - 2));
                        var projectName = string.Join("-", rebuiltParts.Skip(rebuiltParts.Count - 2));
                        return drivePrefix + directoryParts + "\\" + projectName;
                    }
                }
                else
                {
                    // Simple case: replace all dashes with backslashes
                    return drivePrefix + remainingPath.Replace("-", "\\");
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
        {
            return AgentStatus.Offline;
        }

        if (timeSinceLastUpdate > TimeSpan.FromMinutes(5))
        {
            return AgentStatus.Idle;
        }

        try
        {
            var lastLines = ReadLastLines(sessionFile, 5);
            if (lastLines.Any(line => line.Contains("\"type\":\"assistant\"")))
            {
                return AgentStatus.Working;
            }
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

        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                var allLines = new List<string>();

                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    allLines.Add(line);
                }

                return allLines.TakeLast(lineCount).ToList();
            }
            catch (IOException) when (attempt < 2)
            {
                // File is temporarily locked, wait and retry
                Thread.Sleep(50 * (attempt + 1)); // Exponential backoff: 50ms, 100ms
            }
            catch
            {
                // Other exceptions, return empty list
                break;
            }
        }

        return lines;
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

        Console.WriteLine($"GetAgentHistory called with sessionId: {sessionId}, maxEntries: {maxEntries}");

        if (string.IsNullOrEmpty(sessionId))
        {
            Console.WriteLine("SessionId is null or empty, returning empty history");
            return history;
        }

        // Find the JSONL file for this session by searching recursively
        string? sessionFilePath = null;

        Console.WriteLine($"Searching for session file in: {_claudeProjectsPath}");

        try
        {
            var sessionFiles = Directory.GetFiles(_claudeProjectsPath, $"{sessionId}.jsonl", SearchOption.AllDirectories);
            Console.WriteLine($"Found {sessionFiles.Length} session files matching pattern {sessionId}.jsonl");

            if (sessionFiles.Length > 0)
            {
                sessionFilePath = sessionFiles[0];
                Console.WriteLine($"Using session file: {sessionFilePath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error searching for session file: {ex.Message}");
            return history;
        }

        if (sessionFilePath == null)
        {
            Console.WriteLine("No session file found, returning empty history");
            return history;
        }

        try
        {
            Console.WriteLine($"Attempting to read last {maxEntries * 2} lines from file");
            var lastLines = ReadLastLines(sessionFilePath, maxEntries * 2); // Get more lines to filter properly
            Console.WriteLine($"Read {lastLines.Count} lines from file");

            var processedLines = lastLines.TakeLast(maxEntries);
            Console.WriteLine($"Processing {processedLines.Count()} lines");

            foreach (var line in processedLines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    using var document = JsonDocument.Parse(line);
                    var root = document.RootElement;

                    // Extract timestamp and type from root level
                    if (!root.TryGetProperty("timestamp", out var timestampProp) ||
                        !root.TryGetProperty("type", out var typeProp))
                    {
                        continue;
                    }

                    var timestamp = DateTime.TryParse(timestampProp.GetString(), out var dt) ? dt : DateTime.Now;
                    var type = typeProp.GetString() ?? "unknown";
                    string content = "";

                    // Extract content from the message object
                    if (root.TryGetProperty("message", out var messageProp))
                    {
                        if (messageProp.TryGetProperty("content", out var contentProp))
                        {
                            if (contentProp.ValueKind == JsonValueKind.Array)
                            {
                                var textParts = new List<string>();
                                foreach (var item in contentProp.EnumerateArray())
                                {
                                    if (item.TryGetProperty("type", out var itemType))
                                    {
                                        var itemTypeStr = itemType.GetString();
                                        if (itemTypeStr == "text" && item.TryGetProperty("text", out var textProp))
                                        {
                                            textParts.Add(textProp.GetString() ?? "");
                                        }
                                        else if (itemTypeStr == "tool_use" && item.TryGetProperty("name", out var nameProp))
                                        {
                                            textParts.Add($"[Tool: {nameProp.GetString()}]");
                                        }
                                    }
                                }
                                content = string.Join(" ", textParts);
                            }
                            else if (contentProp.ValueKind == JsonValueKind.String)
                            {
                                content = contentProp.GetString() ?? "";
                            }
                        }
                    }

                    // Add to history if we have valid content
                    if (!string.IsNullOrWhiteSpace(content))
                    {
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