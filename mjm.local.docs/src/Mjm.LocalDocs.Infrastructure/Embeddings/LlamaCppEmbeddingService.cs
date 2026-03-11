using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mjm.LocalDocs.Core.Abstractions;
using Mjm.LocalDocs.Core.Configuration;

namespace Mjm.LocalDocs.Infrastructure.Embeddings;

/// <summary>
/// Embedding service that launches and communicates with a local llama-server.exe process.
/// Communicates with llama-server's native /embedding API endpoint.
/// </summary>
public sealed class LlamaCppEmbeddingService : IEmbeddingService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly Process? _process;
    private readonly int _port;
    private bool _disposed;

    /// <inheritdoc />
    public int EmbeddingDimension { get; }

    /// <summary>
    /// Creates a new llama.cpp embedding service, launching llama-server.exe with the specified configuration.
    /// </summary>
    /// <param name="options">Configuration options for llama.cpp.</param>
    /// <param name="embeddingDimension">The dimension of the embedding vectors (default: 1536).</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    /// <exception cref="ArgumentException">Thrown when required configuration values are invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when llama-server fails to start or become ready.</exception>
    public LlamaCppEmbeddingService(LlamaCppEmbeddingsOptions options, int embeddingDimension = 1536)
    {
        if (options is null)
            throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(options.ModelPath))
            throw new ArgumentException("ModelPath is required.", nameof(options));

        if (options.Port <= 0 || options.Port > 65535)
            throw new ArgumentException("Port must be between 1 and 65535.", nameof(options));

        if (options.ContextSize <= 0)
            throw new ArgumentException("ContextSize must be positive.", nameof(options));

        // Resolve relative paths using application base directory
        var resolvedExecutablePath = ResolvePath(options.ExecutablePath);
        var resolvedModelPath = ResolvePath(options.ModelPath);

        // Create a new options instance with resolved paths
        var resolvedOptions = new LlamaCppEmbeddingsOptions
        {
            ExecutablePath = resolvedExecutablePath,
            ModelPath = resolvedModelPath,
            Port = options.Port,
            ContextSize = options.ContextSize,
            BatchSize = options.BatchSize,
            UBatchSize = options.UBatchSize,
            Threads = options.Threads,
            GpuLayers = options.GpuLayers
        };

        EmbeddingDimension = embeddingDimension;
        _port = options.Port;

        // Initialize HttpClient
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri($"http://localhost:{_port}"),
            Timeout = TimeSpan.FromSeconds(60)
        };

        // Launch llama-server process
        _process = LaunchLlamaServer(resolvedOptions);

        // Wait for server to become ready
        WaitForServerReady();
    }

    /// <inheritdoc />
    public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LlamaCppEmbeddingService));

        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be null or whitespace.", nameof(text));

        try
        {
            var embeddings = await GenerateEmbeddingsInternalAsync([text], cancellationToken);
            return embeddings[0];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException($"Failed to generate embedding: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LlamaCppEmbeddingService));

        if (texts is null)
            throw new ArgumentNullException(nameof(texts));

        var textList = texts.ToList();

        if (textList.Count == 0)
            return [];

        if (textList.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Texts cannot contain null or whitespace entries.", nameof(texts));

        try
        {
            return await GenerateEmbeddingsInternalAsync(textList, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException($"Failed to generate embeddings: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Resolves a path to an absolute path. Relative paths are resolved using the application base directory.
    /// </summary>
    /// <param name="path">The path to resolve (can be absolute or relative).</param>
    /// <returns>The absolute path.</returns>
    private static string ResolvePath(string path)
    {
        // If already absolute, return as-is
        if (Path.IsPathRooted(path))
            return path;

        // Resolve relative to application base directory
        var basePath = AppContext.BaseDirectory;
        var resolvedPath = Path.GetFullPath(Path.Combine(basePath, path));
        
        Console.WriteLine($"[LlamaCpp] Path resolution: '{path}' -> '{resolvedPath}'");
        
        return resolvedPath;
    }

    /// <summary>
    /// Launches the llama-server process with the specified configuration.
    /// </summary>
    private Process LaunchLlamaServer(LlamaCppEmbeddingsOptions options)
    {
        // Validate that paths exist before launching
        if (!File.Exists(options.ExecutablePath))
            throw new InvalidOperationException($"llama-server executable not found at: {options.ExecutablePath}");
        
        if (!File.Exists(options.ModelPath))
            throw new InvalidOperationException($"Model file not found at: {options.ModelPath}");

        var startInfo = new ProcessStartInfo
        {
            FileName = options.ExecutablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(options.ExecutablePath) ?? Environment.CurrentDirectory
        };

        // Build arguments using ArgumentList for proper escaping
        startInfo.ArgumentList.Add("--embeddings");
        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add(options.Port.ToString());
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(options.ContextSize.ToString());
        startInfo.ArgumentList.Add("-b");
        startInfo.ArgumentList.Add(options.BatchSize.ToString());
        startInfo.ArgumentList.Add("--ubatch-size");
        startInfo.ArgumentList.Add(options.UBatchSize.ToString());
        startInfo.ArgumentList.Add("-m");
        startInfo.ArgumentList.Add(options.ModelPath);  // Absolute path, no quotes needed
        startInfo.ArgumentList.Add("-ngl");
        startInfo.ArgumentList.Add(options.GpuLayers.ToString());

        if (options.Threads.HasValue && options.Threads.Value > 0)
        {
            startInfo.ArgumentList.Add("-t");
            startInfo.ArgumentList.Add(options.Threads.Value.ToString());
        }

        // Log the command for debugging
        var argsDebug = string.Join(" ", startInfo.ArgumentList.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
        Console.WriteLine($"[LlamaCpp] Launching: {options.ExecutablePath}");
        Console.WriteLine($"[LlamaCpp] Arguments: {argsDebug}");
        Console.WriteLine($"[LlamaCpp] WorkingDir: {startInfo.WorkingDirectory}");

        try
        {
            var process = Process.Start(startInfo);
            if (process is null)
                throw new InvalidOperationException("Failed to start llama-server process.");

            return process;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to launch llama-server.exe: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Polls the /embedding endpoint with a test request until the server is ready or timeout occurs.
    /// </summary>
    private void WaitForServerReady()
    {
        var timeout = TimeSpan.FromSeconds(60); // Increased from 30s to 60s
        var initialDelay = TimeSpan.FromSeconds(5); // Wait for model to load
        var pollInterval = TimeSpan.FromSeconds(2); // Increased from 500ms to 2s
        var stopwatch = Stopwatch.StartNew();
        var attemptNumber = 0;

        // Give the model time to load before we start polling
        Thread.Sleep(initialDelay);

        while (stopwatch.Elapsed < timeout)
        {
            attemptNumber++;
            Console.WriteLine($"[LlamaCpp] Health check attempt #{attemptNumber} at {stopwatch.Elapsed.TotalSeconds:F1}s");

            // Check if process has exited before attempting request
            if (_process?.HasExited == true)
            {
                // Read process output to help diagnose the issue
                var stderr = _process.StandardError.ReadToEnd();
                var stdout = _process.StandardOutput.ReadToEnd();
                
                throw new InvalidOperationException(
                    $"llama-server process exited unexpectedly with code {_process.ExitCode}.\n" +
                    $"STDERR: {stderr}\n" +
                    $"STDOUT: {stdout}");
            }

            try
            {
                // Test the actual /embedding endpoint
                var request = new LlamaCppEmbeddingRequest { Content = "test" };
                var requestJson = JsonSerializer.Serialize(request);
                var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
                
                var response = _httpClient.PostAsync("/embedding", content, CancellationToken.None).GetAwaiter().GetResult();
                Console.WriteLine($"[LlamaCpp] Health check response: HTTP {(int)response.StatusCode} {response.StatusCode}");
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("[LlamaCpp] Server is ready!");
                    return; // Server is ready
                }
                
                // Log response body when request fails to help diagnose issues
                var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                Console.WriteLine($"[LlamaCpp] Health check failed - Response body: {responseBody}");
            }
            catch (Exception ex)
            {
                // Server not ready yet, continue polling
                Console.WriteLine($"[LlamaCpp] Health check exception: {ex.GetType().Name} - {ex.Message}");
            }

            Thread.Sleep(pollInterval);
        }

        throw new InvalidOperationException(
            "llama-server did not become ready within 60 seconds.");
    }

    /// <summary>
    /// Internal method to generate embeddings by calling the llama-server native /embedding endpoint.
    /// </summary>
    private async Task<IReadOnlyList<ReadOnlyMemory<float>>> GenerateEmbeddingsInternalAsync(
        List<string> texts,
        CancellationToken cancellationToken)
    {
        var embeddings = new List<ReadOnlyMemory<float>>();

        foreach (var text in texts)
        {
            // Build request for single text
            var request = new LlamaCppEmbeddingRequest { Content = text };
            var requestJson = JsonSerializer.Serialize(request);
            var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");

            // Send to /embedding endpoint (singular)
            var response = await _httpClient.PostAsync("/embedding", content, cancellationToken);
            
            // Capture detailed error if request fails
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var textPreview = text.Length > 100 ? text[..100] + "..." : text;
                throw new InvalidOperationException(
                    $"llama-server returned {(int)response.StatusCode} {response.ReasonPhrase}. " +
                    $"Error: {errorContent}. Text length: {text.Length} chars. Preview: '{textPreview}'");
            }

            // Parse response - llama-server returns an array with a single embedding
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"[LlamaCpp] Raw Response: Status={response.StatusCode}, Length={responseJson.Length}, Body={responseJson}");
            var embeddingResponses = JsonSerializer.Deserialize<LlamaCppEmbeddingResponse[]>(responseJson);

            if (embeddingResponses is null || embeddingResponses.Length == 0)
                throw new InvalidOperationException("Received empty embedding response from llama-server.");

            var embeddingResponse = embeddingResponses[0];

            if (embeddingResponse.Embedding is null || embeddingResponse.Embedding.Length == 0 || embeddingResponse.Embedding[0] is null)
                throw new InvalidOperationException("Received invalid embedding data from llama-server.");

            // Convert to ReadOnlyMemory<float> - extract the first embedding from the nested array
            embeddings.Add(new ReadOnlyMemory<float>(embeddingResponse.Embedding[0]));
        }

        return embeddings;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            // Kill the llama-server process
            if (_process is not null && !_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(5000);
            }

            _process?.Dispose();
        }
        catch
        {
            // Best effort cleanup
        }

        _httpClient?.Dispose();
    }

    #region JSON DTOs for llama-server native API

    /// <summary>
    /// Request payload for llama-server native /embedding endpoint.
    /// </summary>
    private sealed class LlamaCppEmbeddingRequest
    {
        [JsonPropertyName("content")]
        public required string Content { get; init; }
    }

    /// <summary>
    /// Response from llama-server native /embedding endpoint.
    /// </summary>
    private sealed class LlamaCppEmbeddingResponse
    {
        [JsonPropertyName("index")]
        public required int Index { get; init; }

        [JsonPropertyName("embedding")]
        public required float[][] Embedding { get; init; }
    }

    #endregion
}
