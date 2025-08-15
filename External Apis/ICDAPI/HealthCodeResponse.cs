using System.Text.Json.Serialization;

namespace ICDAPI;

public class HealthCodeResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("stemId")]
    public string StemId { get; set; }

    [JsonPropertyName("isLeaf")]
    public bool IsLeaf { get; set; }

    [JsonPropertyName("postcoordinationAvailability")]
    public int PostcoordinationAvailability { get; set; }

    [JsonPropertyName("hasCodingNote")]
    public bool HasCodingNote { get; set; }

    [JsonPropertyName("hasMaternalChapterLink")]
    public bool HasMaternalChapterLink { get; set; }

    [JsonPropertyName("hasPerinatalChapterLink")]
    public bool HasPerinatalChapterLink { get; set; }

    [JsonPropertyName("propertiesTruncated")]
    public bool PropertiesTruncated { get; set; }

    [JsonPropertyName("isResidualOther")]
    public bool IsResidualOther { get; set; }

    [JsonPropertyName("isResidualUnspecified")]
    public bool IsResidualUnspecified { get; set; }

    [JsonPropertyName("chapter")]
    public string Chapter { get; set; }

    [JsonPropertyName("theCode")]
    public string TheCode { get; set; }

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("titleIsASearchResult")]
    public bool TitleIsASearchResult { get; set; }

    [JsonPropertyName("titleIsTopScore")]
    public bool TitleIsTopScore { get; set; }

    [JsonPropertyName("entityType")]
    public int EntityType { get; set; }

    [JsonPropertyName("important")]
    public bool Important { get; set; }

    [JsonPropertyName("descendants")]
    public List<object> Descendants { get; set; }
}