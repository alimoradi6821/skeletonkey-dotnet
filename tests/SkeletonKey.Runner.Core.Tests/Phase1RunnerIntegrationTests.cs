using System.Text.Json.Nodes;
using SkeletonKey.Runner.Core;
using SkeletonKey.State.Abstractions;
using SkeletonKey.State.Sqlite;

namespace SkeletonKey.Runner.Core.Tests;

/// <summary>Exercises Phase 1 capabilities through the real runner composition boundary.</summary>
public sealed class Phase1RunnerIntegrationTests
{
    /// <summary>Verifies the runner composes durable-state nodes and persists workflow-scoped state.</summary>
    [Fact]
    public async Task RunnerExecutesStatePutWithConfiguredDatabase()
    {
        string root = Path.Combine(Path.GetTempPath(), "skeletonkey-phase1-runner", Guid.NewGuid().ToString("N"));
        string workflowPath = Path.Combine(root, "state.workflow.json");
        string databasePath = Path.Combine(root, "state.db");
        Directory.CreateDirectory(root);
        try
        {
            await File.WriteAllTextAsync(workflowPath, """
                {
                  "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
                  "specVersion": "0.1.0",
                  "id": "phase1-state-runner",
                  "name": "phase1-state-runner",
                  "inputs": {},
                  "variables": {},
                  "nodes": [
                    {
                      "id": "start",
                      "type": "core.start",
                      "typeVersion": 1,
                      "disabled": false,
                      "parameters": {}
                    },
                    {
                      "id": "persist",
                      "type": "state.put",
                      "typeVersion": 1,
                      "disabled": false,
                      "parameters": {
                        "key": "messages/1",
                        "value": {
                          "status": "detected"
                        }
                      }
                    },
                    {
                      "id": "return",
                      "type": "core.return",
                      "typeVersion": 1,
                      "disabled": false,
                      "parameters": {
                        "outcome": {
                          "kind": "skipped",
                          "code": "phase1-complete"
                        }
                      }
                    }
                  ],
                  "connections": [
                    {
                      "from": {
                        "node": "start",
                        "port": "main"
                      },
                      "to": {
                        "node": "persist",
                        "port": "main"
                      }
                    },
                    {
                      "from": {
                        "node": "persist",
                        "port": "continue"
                      },
                      "to": {
                        "node": "return",
                        "port": "main"
                      }
                    }
                  ],
                  "outputs": {}
                }
                """);

            using StringReader input = new(string.Empty);
            using StringWriter output = new();
            using StringWriter error = new();
            SkeletonKeyRunner runner = new(input, output, error);
            int exitCode = await runner.ExecuteAsync(
            [
                "run",
                "--file",
                workflowPath,
                "--execution-id",
                "phase1-state-execution",
                "--state-database",
                databasePath,
            ]);

            Assert.Equal(RunnerExitCodes.Success, exitCode);
            using SqliteWorkflowStateStore store = new(databasePath);
            WorkflowStateEntry? entry = await store.GetAsync(new WorkflowStateAddress(WorkflowStateScopeKind.Workflow, "phase1-state-runner", "messages/1"));
            Assert.NotNull(entry);
            Assert.Equal("detected", entry!.Value!["status"]!.GetValue<string>());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
