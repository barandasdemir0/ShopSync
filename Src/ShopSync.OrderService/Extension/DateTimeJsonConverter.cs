using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShopSync.OrderService.Extension;

public sealed class DateTimeJsonConverter : JsonConverter<DateTime>
{

    private readonly string _format = "yyyy-MM-dd HH:mm:ss";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DateTime.Parse(reader.GetString()!);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        // Tarihi yerel saate (bilgisayarının saat dilimine) çevirip formatlıyoruz
        var localTime = value.ToLocalTime();
        writer.WriteStringValue(localTime.ToString(_format));
    }
}
