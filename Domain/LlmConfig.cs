namespace SquishySim.Domain;

public class LlmConfig
{
    public string Model   { get; set; } = "phi3";
    public string BaseUrl { get; set; } = "http://localhost:11434";

    // PROTOTYPE: api_key is write-only — never returned in GET responses
    private string? _apiKey;
    public void SetApiKey(string? key) => _apiKey = key;
    public string? GetApiKeyInternal()  => _apiKey; // internal use only
    public bool HasApiKey               => !string.IsNullOrEmpty(_apiKey);
}
