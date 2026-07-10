using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShopSync.Web.Services;

public class LenientDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Gelen değer bir metinse, katı kurallar yerine esnek (TryParse) yöntemiyle okumayı dener
        if (reader.TokenType == JsonTokenType.String)
        {
            if (DateTime.TryParse(reader.GetString(), out DateTime date))
            {
                return date;
            }
        }

        // Gelen değer sayı (Unix timestamp vs) ise varsayılan metoda bırak
        return reader.GetDateTime();
    }
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("O"));
    }
}
