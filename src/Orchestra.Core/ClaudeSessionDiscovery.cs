using System.Text.Json;
using System.IO;

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
        return encodedPath.Replace("--", @"\").Replace("-", ":");
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
}