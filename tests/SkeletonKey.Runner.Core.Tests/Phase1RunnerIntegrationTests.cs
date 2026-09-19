using System.Net;
using System.Net.Sockets;
using System.Text;
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

    /// <summary>Verifies the real runner can POST JSON, bind the parsed response, persist it, and redact sensitive response headers.</summary>
    [Fact]
    public async Task RunnerExecutesHttpRequestAndConsumesJsonOutput()
    {
        string root = Path.Combine(Path.GetTempPath(), "skeletonkey-phase1-http-runner", Guid.NewGuid().ToString("N"));
        string workflowPath = Path.Combine(root, "http.workflow.json");
        string databasePath = Path.Combine(root, "state.db");
        Directory.CreateDirectory(root);
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task<string> server = ServeJsonResponseAsync(listener);
        try
        {
            await File.WriteAllTextAsync(workflowPath, $$"""
                {
                  "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
                  "specVersion": "0.1.0",
                  "id": "phase1-http-runner",
                  "name": "phase1-http-runner",
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
                      "id": "request",
                      "type": "http.request",
                      "typeVersion": 1,
                      "disabled": false,
                      "parameters": {
                        "method": "POST",
                        "url": "http://127.0.0.1:{{port}}/respond",
                        "json": {
                          "message": "hello"
                        }
                      }
                    },
                    {
                      "id": "persist",
                      "type": "state.put",
                      "typeVersion": 1,
                      "disabled": false,
                      "parameters": {
                        "key": "responses/1",
                        "value": {
                          "$binding": {
                            "source": "node",
                            "node": "request",
                            "port": "json"
                          }
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
                          "kind": "success",
                          "code": "phase1-http-complete"
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
                        "node": "request",
                        "port": "main"
                      }
                    },
                    {
                      "from": {
                        "node": "request",
                        "port": "continue"
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
                  "outputs": {
                    "responseHeaders": {
                      "mode": "single",
                      "from": {
                        "node": "request",
                        "port": "headers"
                      }
                    }
                  }
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
                "phase1-http-execution",
                "--state-database",
                databasePath,
            ]);

            string received = await server;
            Assert.Equal(RunnerExitCodes.Success, exitCode);
            Assert.Contains("POST /respond HTTP/", received, StringComparison.Ordinal);
            Assert.Contains("{\"message\":\"hello\"}", received, StringComparison.Ordinal);
            Assert.DoesNotContain("phase1-super-secret", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("[REDACTED]", output.ToString(), StringComparison.Ordinal);

            using SqliteWorkflowStateStore store = new(databasePath);
            WorkflowStateEntry? entry = await store.GetAsync(new WorkflowStateAddress(WorkflowStateScopeKind.Workflow, "phase1-http-runner", "responses/1"));
            Assert.NotNull(entry);
            Assert.Equal("ok", entry!.Value!["answer"]!.GetValue<string>());
        }
        finally
        {
            listener.Stop();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>Verifies the default runner resolves secret wrappers from the configured environment prefix without echoing plaintext.</summary>
    [Fact]
    public async Task RunnerResolvesEnvironmentSecretForDataHash()
    {
        const string secret = "phase4-runner-sensitive-value";
        const string environmentName = "PHASE4_TEST_API_KEY";
        string? previous = Environment.GetEnvironmentVariable(environmentName);
        string root = Path.Combine(Path.GetTempPath(), "skeletonkey-phase4-secret-runner", Guid.NewGuid().ToString("N"));
        string workflowPath = Path.Combine(root, "secret.workflow.json");
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable(environmentName, secret);
        try
        {
            await File.WriteAllTextAsync(workflowPath, """
                {
                  "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
                  "specVersion": "0.1.0",
                  "id": "phase4-secret-runner",
                  "name": "phase4-secret-runner",
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
                      "id": "hash",
                      "type": "data.hash",
                      "typeVersion": 1,
                      "disabled": false,
                      "parameters": {
                        "value": {
                          "$secret": "API_KEY"
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
                          "kind": "success",
                          "code": "phase4-secret-complete"
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
                        "node": "hash",
                        "port": "main"
                      }
                    },
                    {
                      "from": {
                        "node": "hash",
                        "port": "continue"
                      },
                      "to": {
                        "node": "return",
                        "port": "main"
                      }
                    }
                  ],
                  "outputs": {
                    "hash": {
                      "mode": "single",
                      "from": {
                        "node": "hash",
                        "port": "hash"
                      }
                    }
                  }
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
                "phase4-secret-execution",
                "--secret-env-prefix",
                "PHASE4_TEST_",
            ]);

            Assert.Equal(RunnerExitCodes.Success, exitCode);
            Assert.DoesNotContain(secret, output.ToString(), StringComparison.Ordinal);
            Assert.Contains("\"hash\"", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentName, previous);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>Verifies a secret can populate an HTTP authorization header without being echoed by the runner.</summary>
    [Fact]
    public async Task RunnerResolvesEnvironmentSecretIntoHttpAuthorizationHeader()
    {
        const string secret = "Bearer phase4-http-sensitive-value";
        const string environmentName = "PHASE4_HTTP_AUTH";
        string? previous = Environment.GetEnvironmentVariable(environmentName);
        string root = Path.Combine(Path.GetTempPath(), "skeletonkey-phase4-http-secret-runner", Guid.NewGuid().ToString("N"));
        string workflowPath = Path.Combine(root, "http-secret.workflow.json");
        Directory.CreateDirectory(root);
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task<string> server = ServeJsonResponseAsync(listener);
        Environment.SetEnvironmentVariable(environmentName, secret);
        try
        {
            await File.WriteAllTextAsync(workflowPath, $$"""
                {
                  "$schema": "https://schemas.skeletonkey.dev/workflow/0.1/schema.json",
                  "specVersion": "0.1.0",
                  "id": "phase4-http-secret-runner",
                  "name": "phase4-http-secret-runner",
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
                      "id": "request",
                      "type": "http.request",
                      "typeVersion": 1,
                      "disabled": false,
                      "parameters": {
                        "url": "http://127.0.0.1:{{port}}/authorized",
                        "headers": {
                          "Authorization": {
                            "$secret": "AUTH"
                          }
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
                          "kind": "success",
                          "code": "phase4-http-secret-complete"
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
                        "node": "request",
                        "port": "main"
                      }
                    },
                    {
                      "from": {
                        "node": "request",
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
                "phase4-http-secret-execution",
                "--secret-env-prefix",
                "PHASE4_HTTP_",
            ]);

            string received = await server;
            Assert.Equal(RunnerExitCodes.Success, exitCode);
            Assert.Contains("Authorization: " + secret, received, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(secret, output.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(secret, error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentName, previous);
            listener.Stop();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static async Task<string> ServeJsonResponseAsync(TcpListener listener)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync();
        await using NetworkStream stream = client.GetStream();
        string request = await ReadHttpRequestAsync(stream);
        byte[] body = Encoding.UTF8.GetBytes("{\"answer\":\"ok\"}");
        byte[] response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\n" +
            "Content-Type: application/json\r\n" +
            "Set-Cookie: session=phase1-super-secret; HttpOnly\r\n" +
            "Content-Length: " + body.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\r\n" +
            "Connection: close\r\n\r\n");
        await stream.WriteAsync(response);
        await stream.WriteAsync(body);
        return request;
    }

    private static async Task<string> ReadHttpRequestAsync(NetworkStream stream)
    {
        using MemoryStream buffer = new();
        byte[] chunk = new byte[1024];
        int headerEnd = -1;
        int contentLength = 0;
        while (true)
        {
            int read = await stream.ReadAsync(chunk);
            if (read == 0)
            {
                break;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read));
            string snapshot = Encoding.UTF8.GetString(buffer.GetBuffer(), 0, checked((int)buffer.Length));
            if (headerEnd < 0)
            {
                headerEnd = snapshot.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                if (headerEnd >= 0)
                {
                    foreach (string line in snapshot[..headerEnd].Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        {
                            contentLength = int.Parse(line["Content-Length:".Length..].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                        }
                    }
                }
            }

            if (headerEnd >= 0 && buffer.Length >= headerEnd + 4 + contentLength)
            {
                return snapshot;
            }
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
