CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);

BEGIN TRANSACTION;
CREATE TABLE "ChatSessions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ChatSessions" PRIMARY KEY,
    "UserId" TEXT NULL,
    "InstanceId" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "LastMessageAt" TEXT NOT NULL
);

CREATE TABLE "Repositories" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Repositories" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Path" TEXT NOT NULL,
    "Description" TEXT NULL,
    "Type" INTEGER NOT NULL,
    "IsActive" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    "LastAccessedAt" TEXT NULL,
    "SettingsJson" TEXT NULL,
    "DefaultBranch" TEXT NULL,
    "AllowedOperationsJson" TEXT NOT NULL,
    "TotalTasks" INTEGER NOT NULL,
    "SuccessfulTasks" INTEGER NOT NULL,
    "FailedTasks" INTEGER NOT NULL,
    "TotalExecutionTime" INTEGER NOT NULL,
    "AllowedOperations" TEXT NOT NULL
);

CREATE TABLE "TaskTemplates" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_TaskTemplates" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Description" TEXT NULL,
    "CommandTemplate" TEXT NOT NULL,
    "DefaultPriority" INTEGER NOT NULL,
    "Category" TEXT NULL,
    "IsActive" INTEGER NOT NULL,
    "ParametersJson" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL
);

CREATE TABLE "WorkflowDefinitions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_WorkflowDefinitions" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Description" TEXT NULL,
    "DefinitionJson" TEXT NOT NULL,
    "IsActive" INTEGER NOT NULL,
    "Version" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL
);

CREATE TABLE "ChatMessages" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ChatMessages" PRIMARY KEY,
    "SessionId" TEXT NOT NULL,
    "Author" TEXT NOT NULL,
    "Content" TEXT NOT NULL,
    "MessageType" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "Metadata" TEXT NULL,
    CONSTRAINT "FK_ChatMessages_ChatSessions_SessionId" FOREIGN KEY ("SessionId") REFERENCES "ChatSessions" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Agents" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Agents" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Type" TEXT NOT NULL,
    "RepositoryPath" TEXT NOT NULL,
    "Status" INTEGER NOT NULL,
    "LastPing" TEXT NOT NULL,
    "CurrentTask" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    "IsDeleted" INTEGER NOT NULL,
    "ConfigurationJson" TEXT NULL,
    "MaxConcurrentTasks" INTEGER NOT NULL,
    "HealthCheckInterval" INTEGER NOT NULL,
    "TotalTasksCompleted" INTEGER NOT NULL,
    "TotalTasksFailed" INTEGER NOT NULL,
    "TotalExecutionTime" INTEGER NOT NULL,
    "AverageExecutionTime" REAL NOT NULL,
    "SessionId" TEXT NULL,
    "RepositoryId" TEXT NULL,
    CONSTRAINT "FK_Agents_Repositories_RepositoryId" FOREIGN KEY ("RepositoryId") REFERENCES "Repositories" ("Id") ON DELETE SET NULL
);

CREATE TABLE "UserPreferences" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_UserPreferences" PRIMARY KEY,
    "UserId" TEXT NOT NULL,
    "Key" TEXT NOT NULL,
    "Value" TEXT NOT NULL,
    "Type" INTEGER NOT NULL,
    "Category" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    "RepositoryId" TEXT NULL,
    CONSTRAINT "FK_UserPreferences_Repositories_RepositoryId" FOREIGN KEY ("RepositoryId") REFERENCES "Repositories" ("Id") ON DELETE CASCADE
);

CREATE TABLE "PerformanceMetrics" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_PerformanceMetrics" PRIMARY KEY,
    "AgentId" TEXT NOT NULL,
    "MetricName" TEXT NOT NULL,
    "Value" REAL NOT NULL,
    "Unit" TEXT NULL,
    "MeasuredAt" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    "Description" TEXT NULL,
    CONSTRAINT "FK_PerformanceMetrics_Agents_AgentId" FOREIGN KEY ("AgentId") REFERENCES "Agents" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Tasks" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Tasks" PRIMARY KEY,
    "Command" TEXT NOT NULL,
    "AgentId" TEXT NULL,
    "RepositoryPath" TEXT NOT NULL,
    "Priority" INTEGER NOT NULL,
    "Status" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "StartedAt" TEXT NULL,
    "CompletedAt" TEXT NULL,
    "ExecutionDuration" INTEGER NULL,
    "Result" TEXT NULL,
    "ErrorMessage" TEXT NULL,
    "RetryCount" INTEGER NOT NULL,
    "CorrelationId" TEXT NULL,
    "WorkflowId" TEXT NULL,
    "ParentTaskId" TEXT NULL,
    "WorkflowStep" INTEGER NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    "RepositoryId" TEXT NULL,
    CONSTRAINT "FK_Tasks_Agents_AgentId" FOREIGN KEY ("AgentId") REFERENCES "Agents" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_Tasks_Repositories_RepositoryId" FOREIGN KEY ("RepositoryId") REFERENCES "Repositories" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_Tasks_Tasks_ParentTaskId" FOREIGN KEY ("ParentTaskId") REFERENCES "Tasks" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Tasks_WorkflowDefinitions_WorkflowId" FOREIGN KEY ("WorkflowId") REFERENCES "WorkflowDefinitions" ("Id") ON DELETE SET NULL
);

CREATE TABLE "OrchestrationLogs" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_OrchestrationLogs" PRIMARY KEY,
    "EventType" TEXT NOT NULL,
    "Message" TEXT NOT NULL,
    "Level" INTEGER NOT NULL,
    "AgentId" TEXT NULL,
    "TaskId" TEXT NULL,
    "RepositoryId" TEXT NULL,
    "AdditionalData" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_OrchestrationLogs_Agents_AgentId" FOREIGN KEY ("AgentId") REFERENCES "Agents" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_OrchestrationLogs_Repositories_RepositoryId" FOREIGN KEY ("RepositoryId") REFERENCES "Repositories" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_OrchestrationLogs_Tasks_TaskId" FOREIGN KEY ("TaskId") REFERENCES "Tasks" ("Id") ON DELETE SET NULL
);

CREATE INDEX "IX_Agents_RepositoryId" ON "Agents" ("RepositoryId");

CREATE INDEX "IX_Agents_RepositoryPath" ON "Agents" ("RepositoryPath");

CREATE INDEX "IX_Agents_SessionId" ON "Agents" ("SessionId");

CREATE INDEX "IX_Agents_Status" ON "Agents" ("Status");

CREATE INDEX "IX_Agents_Status_RepositoryPath" ON "Agents" ("Status", "RepositoryPath");

CREATE INDEX "IX_ChatMessages_CreatedAt" ON "ChatMessages" ("CreatedAt");

CREATE INDEX "IX_ChatMessages_SessionId" ON "ChatMessages" ("SessionId");

CREATE INDEX "IX_ChatMessages_SessionId_CreatedAt" ON "ChatMessages" ("SessionId", "CreatedAt");

CREATE INDEX "IX_ChatSessions_CreatedAt" ON "ChatSessions" ("CreatedAt");

CREATE INDEX "IX_ChatSessions_LastMessageAt" ON "ChatSessions" ("LastMessageAt");

CREATE INDEX "IX_ChatSessions_UserId" ON "ChatSessions" ("UserId");

CREATE INDEX "IX_ChatSessions_UserId_InstanceId" ON "ChatSessions" ("UserId", "InstanceId");

CREATE INDEX "IX_OrchestrationLogs_AgentId_CreatedAt" ON "OrchestrationLogs" ("AgentId", "CreatedAt");

CREATE INDEX "IX_OrchestrationLogs_CreatedAt" ON "OrchestrationLogs" ("CreatedAt");

CREATE INDEX "IX_OrchestrationLogs_EventType" ON "OrchestrationLogs" ("EventType");

CREATE INDEX "IX_OrchestrationLogs_Level" ON "OrchestrationLogs" ("Level");

CREATE INDEX "IX_OrchestrationLogs_RepositoryId" ON "OrchestrationLogs" ("RepositoryId");

CREATE INDEX "IX_OrchestrationLogs_TaskId_CreatedAt" ON "OrchestrationLogs" ("TaskId", "CreatedAt");

CREATE INDEX "IX_PerformanceMetrics_AgentId" ON "PerformanceMetrics" ("AgentId");

CREATE INDEX "IX_PerformanceMetrics_AgentId_MetricName_MeasuredAt" ON "PerformanceMetrics" ("AgentId", "MetricName", "MeasuredAt");

CREATE INDEX "IX_PerformanceMetrics_MeasuredAt" ON "PerformanceMetrics" ("MeasuredAt");

CREATE INDEX "IX_PerformanceMetrics_MetricName" ON "PerformanceMetrics" ("MetricName");

CREATE INDEX "IX_Repositories_IsActive" ON "Repositories" ("IsActive");

CREATE UNIQUE INDEX "IX_Repositories_Path" ON "Repositories" ("Path");

CREATE INDEX "IX_Repositories_Type" ON "Repositories" ("Type");

CREATE INDEX "IX_Tasks_AgentId" ON "Tasks" ("AgentId");

CREATE INDEX "IX_Tasks_AgentId_Status" ON "Tasks" ("AgentId", "Status");

CREATE INDEX "IX_Tasks_CorrelationId" ON "Tasks" ("CorrelationId");

CREATE INDEX "IX_Tasks_CreatedAt" ON "Tasks" ("CreatedAt");

CREATE INDEX "IX_Tasks_ParentTaskId" ON "Tasks" ("ParentTaskId");

CREATE INDEX "IX_Tasks_RepositoryId" ON "Tasks" ("RepositoryId");

CREATE INDEX "IX_Tasks_Status" ON "Tasks" ("Status");

CREATE INDEX "IX_Tasks_Status_Priority" ON "Tasks" ("Status", "Priority");

CREATE INDEX "IX_Tasks_WorkflowId" ON "Tasks" ("WorkflowId");

CREATE INDEX "IX_TaskTemplates_Category" ON "TaskTemplates" ("Category");

CREATE INDEX "IX_TaskTemplates_IsActive" ON "TaskTemplates" ("IsActive");

CREATE INDEX "IX_TaskTemplates_Name" ON "TaskTemplates" ("Name");

CREATE INDEX "IX_UserPreferences_Category" ON "UserPreferences" ("Category");

CREATE INDEX "IX_UserPreferences_RepositoryId" ON "UserPreferences" ("RepositoryId");

CREATE UNIQUE INDEX "IX_UserPreferences_UserId_Key_RepositoryId" ON "UserPreferences" ("UserId", "Key", "RepositoryId");

CREATE INDEX "IX_UserPreferences_UserId_Type" ON "UserPreferences" ("UserId", "Type");

CREATE INDEX "IX_WorkflowDefinitions_IsActive" ON "WorkflowDefinitions" ("IsActive");

CREATE INDEX "IX_WorkflowDefinitions_Name" ON "WorkflowDefinitions" ("Name");

CREATE INDEX "IX_WorkflowDefinitions_Name_Version" ON "WorkflowDefinitions" ("Name", "Version");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20250922204129_AddChatTables', '9.0.6');

COMMIT;

