using Azure.AI.OpenAI;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using support_assistant_agent_func.Models;
using Azure.Search.Documents.Indexes.Models;
using Azure;

namespace support_assistant_agent_func.Services;

public interface IAzureAISearchService
{
    Task IndexKnowledgeBaseAsync();
    Task SearchKnowledgeBaseAsync();
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

    public async Task IndexKnowledgeBaseAsync()
    {
        // if the index doesn't exist, create it
        try
        {
            Response<SearchIndex> response = _searchIndexClient.GetIndex(_indexName);
        }
        catch (RequestFailedException ex)
        {
            if (ex.Status == 404)
            {
                _logger.LogInformation("Creating Index...");
            }
        }

        throw new NotImplementedException();
    }

    public async Task SearchKnowledgeBaseAsync()
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
                                new SemanticField("test"),
                            }
                        })
                    }
                },
                Fields =
                {
                    new SimpleField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
                    new SearchableField("test") { IsFilterable = true, IsSortable = true },
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