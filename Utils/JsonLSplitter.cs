namespace TodoApi.Utils;

using System.Text;
using System.Text.Json;
 
public class JsonLSplitter
{
    public static List<string> GetText(Stream fileStream)
    {
        var logs = new List<string>();
 
        var reader = new StreamReader(fileStream, Encoding.UTF8, leaveOpen: true);
        string line;
        while ((line = reader.ReadLine()) != null)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    logs.Add(ExtractValuesOnly(line));
                }
            }
        }
 
        return logs;
    }
    public static string ExtractValuesOnly(string jsonLine)
    {
        using var doc = JsonDocument.Parse(jsonLine);
        return ExtractValuesRecursive(doc.RootElement);
    }
    private static string ExtractValuesRecursive(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => string.Join(" ", element.EnumerateObject().Select(p => ExtractValuesRecursive(p.Value))),
            JsonValueKind.Array => string.Join(" ", element.EnumerateArray().Select(ExtractValuesRecursive)),
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => element.ToString(),
            _ => string.Empty
        };
    }
 
}