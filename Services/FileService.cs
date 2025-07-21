using System.ComponentModel;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using TodoApi.Models;

namespace WebApplication2.Services;

public class FileService
{
    private readonly TodoContext _context;
    
     public static string FlattenJson(JsonElement element)
    {
        var sb = new StringBuilder();

        void Recurse(JsonElement el)
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in el.EnumerateObject())
                    {
                        sb.Append(property.Name).Append(": ");
                        Recurse(property.Value);
                        sb.AppendLine();
                    }
                    break;

                case JsonValueKind.Array:
                    foreach (var item in el.EnumerateArray())
                    {
                        Recurse(item);
                        sb.AppendLine();
                    }
                    break;

                case JsonValueKind.String:
                    sb.Append(el.GetString());
                    break;

                case JsonValueKind.Number:
                    sb.Append(el.GetRawText());
                    break;

                case JsonValueKind.True:
                case JsonValueKind.False:
                    sb.Append(el.GetBoolean());
                    break;

                case JsonValueKind.Null:
                    sb.Append("null");
                    break;
            }
        }

        Recurse(element);
        return sb.ToString();
    }
     
    public static List<string> SplitTextIntoChunks(string text, int maxChars)
    {
        var chunks = new List<string>();

        for (int i = 0; i < text.Length; i += maxChars)
        {
            int length = Math.Min(maxChars, text.Length - i);
            chunks.Add(text.Substring(i, length));
        }

        return chunks;
    }

}