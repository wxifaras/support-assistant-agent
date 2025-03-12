using Azure.AI.OpenAI;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using support_assistant_agent_func.Models;
using Azure.Search.Documents.Indexes.Models;
using Azure;
using OpenAI.Embeddings;
using Azure.Search.Documents.Models;

namespace support_assistant_agent_func.Services;

public interface IAzureAISearchService
{
    Task<string> IndexKnowledgeBaseAsync(KnowledgeBase knowledgeBase);
    Task<List<SearchDocument>> SearchKnowledgeBaseAsync(string scope, string query);
}

public class AzureAISearchService : IAzureAISearchService
{
    private ILogger<AzureAISearchService> _logger;

    const string vectorSearchHnswProfile = "knowledgebase-vector-profile";
    const string vectorSearchHnswConfig = "knowledgebaseHnsw";
    const string vectorSearchVectorizer = "knowledgebaseOpenAIVectorizer";
    const string semanticSearchConfig = "knowledgebase-semantic-config";

    private readonly string _indexName;
    private readonly double _rerankerThreshold;
    private readonly string _azureOpenAIEndpoint;
    private readonly string _azureOpenAIKey;
    private readonly string _azureOpenAIEmbeddingDimensions;
    private readonly string _azureOpenAIEmbeddingDeployment;
    private readonly SearchIndexClient _searchIndexClient;
    private readonly AzureOpenAIClient _azureOpenAIClient;

    public AzureAISearchService(
       ILogger<AzureAISearchService> logger,
       IOptions<AzureAISearchOptions> azureAISearchOptions,
       IOptions<AzureOpenAIOptions> azureOpenAIOptions,
       SearchIndexClient indexClient,
       AzureOpenAIClient azureOpenAIClient)
    {
        _indexName = azureAISearchOptions.Value.IndexName ?? throw new ArgumentNullException(nameof(azureAISearchOptions.Value.IndexName));
        string rerankerThreshold = azureAISearchOptions.Value.RerankerThreshold ?? throw new ArgumentNullException(nameof(azureAISearchOptions.Value.RerankerThreshold));
        _rerankerThreshold = Double.Parse(rerankerThreshold);
        _azureOpenAIEndpoint = azureOpenAIOptions.Value.AzureOpenAIEndPoint ?? throw new ArgumentNullException(nameof(azureOpenAIOptions.Value.AzureOpenAIEndPoint));
        _azureOpenAIKey = azureOpenAIOptions.Value.AzureOpenAIKey ?? throw new ArgumentNullException(nameof(azureOpenAIOptions.Value.AzureOpenAIKey));
        _azureOpenAIEmbeddingDimensions = azureOpenAIOptions.Value.AzureOpenAIEmbeddingDimensions ?? throw new ArgumentNullException(nameof(azureOpenAIOptions.Value.AzureOpenAIEmbeddingDimensions));
        _azureOpenAIEmbeddingDeployment = azureOpenAIOptions.Value.AzureOpenAIEmbeddingDeployment ?? throw new ArgumentNullException(nameof(azureOpenAIOptions.Value.AzureOpenAIEmbeddingDeployment));
        _searchIndexClient = indexClient ?? throw new ArgumentNullException(nameof(indexClient));
        _azureOpenAIClient = azureOpenAIClient ?? throw new ArgumentNullException(nameof(azureOpenAIClient));
        
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> IndexKnowledgeBaseAsync(KnowledgeBase knowledgeBase)
    {
        try
        {
            // tests whether the index exists or not
            _searchIndexClient.GetIndex(_indexName);
        }
        catch (RequestFailedException ex)
        {
            // if the index doesn't exist, create it
            if (ex.Status == 404)
            {
                _logger.LogInformation("Creating Index...");

                await CreateAISearchIndexAsync();
            }
        }

        var searchDocument = await GetSearchDocumentAsync(knowledgeBase);

        var actions = new List<IndexDocumentsAction<SearchDocument>>
        {
            IndexDocumentsAction.MergeOrUpload(searchDocument)
        };

        var searchClient = _searchIndexClient.GetSearchClient(_indexName);
        var batch = IndexDocumentsBatch.Create(actions.ToArray());
        var indexResponse = await searchClient.IndexDocumentsAsync(batch);

        return indexResponse.GetRawResponse().Status.ToString();
    }

    private async Task<SearchDocument> GetSearchDocumentAsync(KnowledgeBase knowledgeBase)
    {
        var searchDocument = new SearchDocument();

        searchDocument["id"] = knowledgeBase.problem_id; ;
        searchDocument["problem_id"] = knowledgeBase.problem_id;
        searchDocument["title"] = knowledgeBase.title;
        searchDocument["description"] = knowledgeBase.description;
        searchDocument["status"] = knowledgeBase.status;
        searchDocument["priority"] = knowledgeBase.priority;
        searchDocument["impact"] = knowledgeBase.impact;
        searchDocument["category"] = knowledgeBase.category;
        searchDocument["reported_date"] = knowledgeBase.reported_date;
        searchDocument["resolved_date"] = knowledgeBase.resolved_date;
        searchDocument["assigned_to"] = knowledgeBase.assigned_to;
        searchDocument["reported_by"] = knowledgeBase.reported_by;
        searchDocument["root_cause"] = knowledgeBase.root_cause;
        searchDocument["workaround"] = knowledgeBase.workaround;
        searchDocument["resolution"] = knowledgeBase.resolution;
        searchDocument["related_incidents"] = knowledgeBase.related_incidents;
        searchDocument["Scope"] = knowledgeBase.Scope;
        searchDocument["attachments"] = knowledgeBase.attachments;
        searchDocument["comments"] = knowledgeBase.comments;
        searchDocument["Summary"] = knowledgeBase.Summary;

        var embeddingClient = _azureOpenAIClient.GetEmbeddingClient(_azureOpenAIEmbeddingDeployment);

        string textForEmbedding = $"title: {knowledgeBase.title}, " +
                                  $"description: {knowledgeBase.description}";

        OpenAIEmbedding embedding = await embeddingClient.GenerateEmbeddingAsync(textForEmbedding).ConfigureAwait(false);

        searchDocument["vectorContent"] = embedding.ToFloats().ToArray().ToList();

        return searchDocument;
    }

    public async Task<List<SearchDocument>> SearchKnowledgeBaseAsync(string scope, string query)
    {
        // Perform the vector similarity search  
        var searchOptions = new SearchOptions
        {
            Filter = $"Scope/any(s: search.in(s, '{scope.Replace(" ", "")}', ','))",
            Size = 3, // number of results to return
            Select = { "title", "problem_id", "description", "status", "root_cause", "workaround", "resolution", "Summary" },
            IncludeTotalCount = true
        };

        // configure vector search
        searchOptions.VectorSearch = new()
        {
            Queries = {
                new VectorizableTextQuery(text: query)
                {
                    KNearestNeighborsCount = 5,
                    Fields = { "vectorContent" },
                    Exhaustive = false
                },
            },
        };

        // configure semantic search
        searchOptions.QueryType = SearchQueryType.Semantic;
        searchOptions.SemanticSearch = new SemanticSearchOptions
        {
            SemanticConfigurationName = semanticSearchConfig,
            QueryCaption = new QueryCaption(QueryCaptionType.Extractive),
            QueryAnswer = new QueryAnswer(QueryAnswerType.Extractive),
        };

        SearchClient searchClient = _searchIndexClient.GetSearchClient(_indexName);
        SearchResults<SearchDocument> response = await searchClient.SearchAsync<SearchDocument>(query, searchOptions);

        var knowledgeBaseResultsList = new List<SearchDocument>();

        await foreach (SearchResult<SearchDocument> result in response.GetResultsAsync())
        {
            // add the document result if it meets the reranker score threshold
            if (result.SemanticSearch.RerankerScore >= _rerankerThreshold)
            {
                _logger.LogInformation($"Reranker Score: {result.SemanticSearch.RerankerScore}\n");
                _logger.LogInformation($"Problem ID: {result.Document["problem_id"]}");
                _logger.LogInformation($"Description: {result.Document["description"]}");
                _logger.LogInformation($"Status: {result.Document["status"]}");
                _logger.LogInformation($"Root Cause: {result.Document["root_cause"]}");
                _logger.LogInformation($"Workaround: {result.Document["workaround"]}");
                _logger.LogInformation($"Resolution: {result.Document["resolution"]}");
                _logger.LogInformation($"Title: {result.Document["title"]}");
                _logger.LogInformation($"Problem ID: {result.Document["problem_id"]}");
                _logger.LogInformation($"Title: {result.Document["title"]}");
                _logger.LogInformation($"Summary: {result.Document["Summary"]}");

                if (result.SemanticSearch?.Captions?.Count > 0)
                {
                    QueryCaptionResult firstCaption = result.SemanticSearch.Captions[0];
                    _logger.LogInformation($"First Caption Highlights: {firstCaption.Highlights}");
                    _logger.LogInformation($"First Caption Text: {firstCaption.Text}");
                }

                knowledgeBaseResultsList.Add(result.Document);
            }
        }

        return knowledgeBaseResultsList;
    }
    
    private async Task CreateAISearchIndexAsync()
    {
        try
        {
            SearchIndex searchIndex = new(_indexName)
            {
                VectorSearch = new()
                {
                    Profiles =
                    {
                        new VectorSearchProfile(vectorSearchHnswProfile, vectorSearchHnswConfig)
                        {
                            VectorizerName = vectorSearchVectorizer
                        }
                    },
                    Algorithms =
                    {
                        new HnswAlgorithmConfiguration(vectorSearchHnswConfig)
                        {
                            Parameters = new HnswParameters
                            {
                                M = 4,
                                EfConstruction = 400,
                                EfSearch = 500,
                                Metric = "cosine"
                            }
                        }
                    },
                    Vectorizers =
                    {
                        new AzureOpenAIVectorizer(vectorSearchVectorizer)
                        {
                            Parameters = new AzureOpenAIVectorizerParameters
                            {
                                ResourceUri = new Uri(_azureOpenAIEndpoint),
                                ModelName = _azureOpenAIEmbeddingDeployment,
                                DeploymentName = _azureOpenAIEmbeddingDeployment,
                                ApiKey = _azureOpenAIKey
                            }
                        }
                    }
                },
                SemanticSearch = new()
                {
                    Configurations =
                    {
                        new SemanticConfiguration(semanticSearchConfig, new()
                        {
                            ContentFields =
                            {
                                new SemanticField("title"),
                                new SemanticField("description"),
                            },
                            TitleField = new SemanticField(fieldName: "title"),
                            KeywordsFields =
                            {
                                new SemanticField("title")
                            }
                        })
                    }
                },
                Fields =
                {
                    new SimpleField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true, IsSortable = true, IsFacetable = true },
                    new SearchableField("problem_id") { IsFilterable = true, IsSortable = true },
                    new SearchableField("title") { IsFilterable = true, IsSortable = true },
                    new SearchableField("description") { IsFilterable = true, IsSortable = true },
                    new SearchableField("status") { IsFilterable = true, IsSortable = true },
                    new SearchableField("priority") { IsFilterable = true, IsSortable = true },
                    new SearchableField("impact") { IsFilterable = true, IsSortable = true },
                    new SearchableField("category") { IsFilterable = true, IsSortable = true },
                    new SimpleField("reported_date", SearchFieldDataType.DateTimeOffset) { IsKey = false, IsFilterable = true, IsSortable = true, IsFacetable = true },
                    new SimpleField("resolved_date", SearchFieldDataType.DateTimeOffset) { IsKey = false, IsFilterable = true, IsSortable = true, IsFacetable = true },
                    new SearchableField("assigned_to") { IsFilterable = true, IsSortable = true },
                    new SearchableField("reported_by") { IsFilterable = true, IsSortable = true },
                    new SearchableField("root_cause") { IsFilterable = true, IsSortable = true },
                    new SearchableField("workaround") { IsFilterable = true, IsSortable = true },
                    new SearchableField("resolution") { IsFilterable = true, IsSortable = true },
                    new SimpleField("related_incidents", SearchFieldDataType.Collection(SearchFieldDataType.String))
                    {
                        IsFilterable = true,
                        IsFacetable = true
                    },
                    new SimpleField("Scope", SearchFieldDataType.Collection(SearchFieldDataType.String))
                    {
                        IsFilterable = true,
                        IsFacetable = true
                    },
                    new ComplexField("attachments",collection: true)
                    {
                        Fields =
                        {
                            new SimpleField("file_name", SearchFieldDataType.String)
                            {
                                IsFilterable = true,
                                IsFacetable = false
                            },
                            new SimpleField("file_url", SearchFieldDataType.String)
                            {
                                IsFilterable = true,
                                IsFacetable = false
                            }
                        }
                    },
                    new ComplexField("comments",collection: true)
                    {
                        Fields =
                        {
                            new SimpleField("comment_id", SearchFieldDataType.String)
                            {
                               IsFilterable = true,
                               IsFacetable = false
                            },
                            new SimpleField("comment_text", SearchFieldDataType.String)
                            {
                                IsFilterable = true,
                                IsFacetable = false
                            },
                            new SimpleField("commented_by", SearchFieldDataType.String)
                            {
                                IsFilterable = true,
                                IsFacetable = false
                            },
                            new SimpleField("commented_date", SearchFieldDataType.DateTimeOffset)
                            {
                                IsFilterable = true,
                                IsFacetable = false
                            }
                        }
                    },
                    new SearchableField("Summary") { IsFilterable = true, IsSortable = true },
                    new SearchField("vectorContent", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                    {
                        IsSearchable = true,
                        VectorSearchDimensions = int.Parse(_azureOpenAIEmbeddingDimensions!),
                        VectorSearchProfileName = vectorSearchHnswProfile
                    }
                }
            };

            await _searchIndexClient.CreateOrUpdateIndexAsync(searchIndex).ConfigureAwait(false);

            _logger.LogInformation($"Completed creating index {searchIndex}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating AI search index.");
            throw;
        }
    }
}