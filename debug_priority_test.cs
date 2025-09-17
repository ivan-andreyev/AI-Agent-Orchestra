using Orchestra.Core;

Console.WriteLine("Testing TaskPriority enum values:");
Console.WriteLine($"Low = {(int)TaskPriority.Low}");
Console.WriteLine($"Normal = {(int)TaskPriority.Normal}");
Console.WriteLine($"High = {(int)TaskPriority.High}");
Console.WriteLine($"Critical = {(int)TaskPriority.Critical}");

Console.WriteLine("\nTesting TaskRequest creation:");
var task = new TaskRequest("test", "agent", "command", "path", DateTime.Now, TaskPriority.High);
Console.WriteLine($"Created task with priority: {task.Priority} (value: {(int)task.Priority})");

var orchestrator = new SimpleOrchestrator("debug-test.json");
orchestrator.RegisterAgent("agent1", "Agent 1", "claude-code", @"C:\TestRepo");
orchestrator.QueueTask("Test", @"C:\TestRepo", TaskPriority.Low);

var state = orchestrator.GetCurrentState();
var queuedTask = state.TaskQueue.FirstOrDefault();
if (queuedTask != null)
{
    Console.WriteLine($"Queued task priority: {queuedTask.Priority} (value: {(int)queuedTask.Priority})");
}
else
{
    Console.WriteLine("No task found in queue");
}