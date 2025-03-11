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
    Task SearchKnowledgeBaseAsync(string scope, string query);
}

public class AzureAISearchService : IAzureAISearchService
{
    private ILogger<AzureAISearchService> _logger;

    const string vectorSearchHnswProfile = "knowledgebase-vector-profile";
    const string vectorSearchHnswConfig = "knowledgebaseHnsw";
    const string vectorSearchVectorizer = "knowledgebaseOpenAIVectorizer";
    const string semanticSearchConfig = "knowledgebase-semantic-config";

    private readonly string _indexName;
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

        SearchDocument searchDocument = await GetSearchDocumentAsync(knowledgeBase);
        var searchClient = _searchIndexClient.GetSearchClient(_indexName);
        var indexResponse = await searchClient.IndexDocumentsAsync(IndexDocumentsBatch.Upload(new List<SearchDocument> { searchDocument }));

        return indexResponse.GetRawResponse().Status.ToString();
    }

    private async Task<SearchDocument> GetSearchDocumentAsync(KnowledgeBase knowledgeBase)
    {
        var searchDocument = new SearchDocument();

        searchDocument["problem_id"] = knowledgeBase.problem_id;
        searchDocument["title"] = knowledgeBase.title;

        var embeddingClient = _azureOpenAIClient.GetEmbeddingClient(_azureOpenAIEmbeddingDeployment);

        string textForEmbedding = $"title: {knowledgeBase.title}, " +
                                  $"description: {knowledgeBase.description} ";

        OpenAIEmbedding embedding = await embeddingClient.GenerateEmbeddingAsync(textForEmbedding).ConfigureAwait(false);

        searchDocument["vectorContent"] = embedding.ToFloats().ToArray().ToList();

        return searchDocument;
    }

    public async Task SearchKnowledgeBaseAsync(string scope, string query)
    {
        throw new NotImplementedException();
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
                    new SearchableField("title") { IsFilterable = true, IsSortable = true },
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