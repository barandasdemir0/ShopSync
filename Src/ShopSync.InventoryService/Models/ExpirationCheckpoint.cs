using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ShopSync.InventoryService.Models;

public sealed class ExpirationCheckpoint
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; private set; } = string.Empty;


    // Bu checkpoint'in ait olduğu job adı.
    [BsonElement("jobName")]
    public string JobName { get; private set; } = string.Empty;


    // En son başarıyla taranmış olan zaman eşiği.
    [BsonElement("lastProcessedThreshold")]
    public DateTime LastProcessedThreshold { get; private set; }


    // Bu checkpoint'in ne zaman güncellendiği.
    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; private set; }


    private ExpirationCheckpoint() { }
    public ExpirationCheckpoint(string jobName, DateTime lastProcessedThreshold)
    {
        if (string.IsNullOrWhiteSpace(jobName))
        {
            throw new ArgumentException("Job adı boş olamaz.", nameof(jobName));
        }
 
        JobName = jobName.Trim();
        LastProcessedThreshold = lastProcessedThreshold;
        UpdatedAt = DateTime.UtcNow;
    }
    // Checkpoint değerini güncelle
    public void Update(DateTime newThreshold)
    {
        LastProcessedThreshold = newThreshold;
        UpdatedAt = DateTime.UtcNow;
    }

}
