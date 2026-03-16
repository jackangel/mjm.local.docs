using System.ComponentModel;
using ModelContextProtocol.Server;
using Mjm.LocalDocs.Core.Models;
using Mjm.LocalDocs.Core.Services;

namespace Mjm.LocalDocs.Server.McpTools;

/// <summary>
/// MCP Tool for semantic search in documentation.
/// </summary>
[McpServerToolType]
public sealed class SearchDocsTool
{
    private readonly DocumentService _documentService;

    public SearchDocsTool(DocumentService documentService)
    {
        _documentService = documentService;
    }

    [McpServerTool(Name = "search_docs")]
    [Description("Search for documents using semantic search across all projects. Returns relevant document chunks based on the query.")]
    public async Task<string> SearchDocsAsync(
        [Description("The search query in natural language")] string query,
        [Description("Maximum number of results to return (default: 5, max: 20)")] int limit = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            limit = Math.Clamp(limit, 1, 20);

            var results = await _documentService.SearchAsync(query, null, limit, cancellationToken);

            return FormatSearchResults(results);
        }
        catch (Exception ex)
        {
            return $"Error searching documents: {ex.Message}";
        }
    }

    [McpServerTool(Name = "search_project_docs")]
    [Description("Search for documents within a specific project by **ID**. Returns relevant document chunks.")]
    public async Task<string> SearchProjectDocsAsync(
        [Description("The project ID (not title) to search within")] string projectId,
        [Description("The search query in natural language")] string query,
        [Description("Maximum number of results to return (default: 5, max: 20)")] int limit = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            limit = Math.Clamp(limit, 1, 20);

            var results = await _documentService.SearchAsync(query, projectId, limit, cancellationToken);

            return FormatSearchResults(results);
        }
        catch (Exception ex)
        {
            return $"Error searching documents in project '{projectId}': {ex.Message}";
        }
    }

    [McpServerTool(Name = "search_docs_by_project_title")]
    [Description("Search for documents by project title (name). If title doesn't match, searches all projects.")]
    public async Task<string> SearchDocsByProjectTitleAsync(
        [Description("The search query in natural language")] string query,
        [Description("Optional project title/name to filter results. If not found, searches all projects.")] string? projectTitle = null,
        [Description("Maximum number of results to return (default: 5, max: 20)")] int limit = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            limit = Math.Clamp(limit, 1, 20);

            var results = await _documentService.SearchByProjectTitleAsync(query, projectTitle, limit, cancellationToken);

            return FormatSearchResults(results);
        }
        catch (Exception ex)
        {
            return $"Error searching documents by project title: {ex.Message}";
        }
    }

    private static string FormatSearchResults(IReadOnlyList<SearchResult> results)
    {
        if (results.Count == 0)
        {
            return "No documents found matching your query.";
        }

        var response = $"Found {results.Count} relevant document(s):\n\n";

        for (var i = 0; i < results.Count; i++)
        {
            var result = results[i];
            response += $"## Result {i + 1} (Score: {result.Score:F2})\n";
            response += $"**Source**: {result.Chunk.FileName ?? result.Chunk.DocumentId}\n";
            response += $"**Document ID**: {result.Chunk.DocumentId}\n";
            response += $"**Content**:\n{result.Chunk.Content}\n\n";
            response += "---\n\n";
        }

        return response;
    }
}
