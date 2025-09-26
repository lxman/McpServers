using System.Text.Json;
using System.Xml.Linq;
using YamlDotNet.Serialization;

namespace McpCodeEditor.Services;

public class ConversionService
{
    public static async Task<string> ConvertContentAsync(
        string content,
        string fromExt,
        string toExt,
        Dictionary<string, object> settings)
    {
        // Convert extensions to lower case for comparison
        fromExt = fromExt.ToLowerInvariant();
        toExt = toExt.ToLowerInvariant();

        try
        {
            // Text-based conversions
            if (IsTextBasedConversion(fromExt, toExt))
            {
                return await ConvertTextBasedAsync(content, fromExt, toExt);
            }

            // Data format conversions
            if (IsDataFormatConversion(fromExt, toExt))
            {
                return await ConvertDataFormatAsync(content, fromExt, toExt);
            }

            // Configuration file conversions
            if (IsConfigurationConversion(fromExt, toExt))
            {
                return await ConvertConfigurationAsync(content, fromExt, toExt);
            }

            // Encoding conversions (same extension, different encoding)
            if (fromExt == toExt && settings.ContainsKey("encoding"))
            {
                return await ConvertEncodingAsync(content, settings["encoding"].ToString() ?? "utf-8");
            }

            // Fallback: simple copy with basic transformation
            return content;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to convert content from {fromExt} to {toExt}: {ex.Message}", ex);
        }
    }

    public static bool IsConversionSupported(string fromExt, string toExt)
    {
        fromExt = fromExt.ToLowerInvariant();
        toExt = toExt.ToLowerInvariant();

        // Same extension (encoding conversion)
        if (fromExt == toExt)
            return true;

        // Text-based conversions
        var textExtensions = new[] { ".txt", ".md", ".rst", ".adoc" };
        if (textExtensions.Contains(fromExt) && textExtensions.Contains(toExt))
            return true;

        // Data format conversions
        var dataExtensions = new[] { ".json", ".yaml", ".yml", ".xml" };
        if (dataExtensions.Contains(fromExt) && dataExtensions.Contains(toExt))
            return true;

        // Configuration conversions
        var configExtensions = new[] { ".json", ".yaml", ".yml", ".xml", ".ini", ".cfg", ".conf", ".properties" };
        if (configExtensions.Contains(fromExt) && configExtensions.Contains(toExt))
            return true;

        return false;
    }

    public static List<string> GetSupportedConversions()
    {
        return
        [
            "Text formats: .txt ? .md ? .rst ? .adoc",
            "Data formats: .json ? .yaml ? .yml ? .xml",
            "Config formats: .json ? .yaml ? .ini ? .cfg ? .properties",
            "Encoding conversions: same extension, different encoding"
        ];
    }

    private static bool IsTextBasedConversion(string fromExt, string toExt)
    {
        var textExtensions = new[] { ".txt", ".md", ".rst", ".adoc" };
        return textExtensions.Contains(fromExt) && textExtensions.Contains(toExt);
    }

    private static bool IsDataFormatConversion(string fromExt, string toExt)
    {
        var dataExtensions = new[] { ".json", ".yaml", ".yml", ".xml" };
        return dataExtensions.Contains(fromExt) && dataExtensions.Contains(toExt);
    }

    private static bool IsConfigurationConversion(string fromExt, string toExt)
    {
        var configExtensions = new[] { ".json", ".yaml", ".yml", ".xml", ".ini", ".cfg", ".conf", ".properties" };
        return configExtensions.Contains(fromExt) && configExtensions.Contains(toExt);
    }

    private static async Task<string> ConvertTextBasedAsync(string content, string fromExt, string toExt)
    {
        // For now, text-based conversions are mostly pass-through
        // In the future, we could add markdown conversion, etc.
        return content;
    }

    private static async Task<string> ConvertDataFormatAsync(string content, string fromExt, string toExt)
    {
        try
        {
            object data;

            // Parse from source format
            switch (fromExt)
            {
                case ".json":
                    data = JsonSerializer.Deserialize<object>(content);
                    break;
                case ".yaml":
                case ".yml":
                    IDeserializer deserializer = new DeserializerBuilder().Build();
                    data = deserializer.Deserialize<object>(content);
                    break;
                case ".xml":
                    XDocument xDoc = XDocument.Parse(content);
                    data = XmlToObject(xDoc.Root);
                    break;
                default:
                    throw new NotSupportedException($"Source format {fromExt} not supported");
            }

            // Convert to target format
            switch (toExt)
            {
                case ".json":
                    return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                case ".yaml":
                case ".yml":
                    ISerializer serializer = new SerializerBuilder().Build();
                    return serializer.Serialize(data);
                case ".xml":
                    return ObjectToXml(data).ToString();
                default:
                    throw new NotSupportedException($"Target format {toExt} not supported");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Data format conversion failed: {ex.Message}", ex);
        }
    }

    private static async Task<string> ConvertConfigurationAsync(string content, string fromExt, string toExt)
    {
        // For configuration files, use the data format conversion as base
        return await ConvertDataFormatAsync(content, fromExt, toExt);
    }

    private static async Task<string> ConvertEncodingAsync(string content, string targetEncoding)
    {
        // For encoding conversion, we'd need to handle byte-level operations
        // For now, return as-is since we're working with strings
        return content;
    }

    private static object XmlToObject(XElement? element)
    {
        if (element == null) return new object();

        var result = new Dictionary<string, object>();

        // Add attributes
        foreach (XAttribute attr in element.Attributes())
        {
            result[$"@{attr.Name}"] = attr.Value;
        }

        // Add elements
        foreach (XElement child in element.Elements())
        {
            object childData = XmlToObject(child);
            if (result.ContainsKey(child.Name.LocalName))
            {
                // Convert to array if multiple elements with same name
                if (result[child.Name.LocalName] is not List<object> list)
                {
                    list = [result[child.Name.LocalName]];
                    result[child.Name.LocalName] = list;
                }
                list.Add(childData);
            }
            else
            {
                result[child.Name.LocalName] = childData;
            }
        }

        // Add text content if no child elements
        if (!element.Elements().Any() && !string.IsNullOrWhiteSpace(element.Value))
        {
            return element.Value;
        }

        return result;
    }

    private static XElement ObjectToXml(object obj, string elementName = "root")
    {
        var element = new XElement(elementName);

        if (obj is Dictionary<string, object> dict)
        {
            foreach (KeyValuePair<string, object> kvp in dict)
            {
                if (kvp.Key.StartsWith("@"))
                {
                    // Attribute
                    element.SetAttributeValue(kvp.Key[1..], kvp.Value.ToString());
                }
                else
                {
                    // Child element
                    if (kvp.Value is List<object> list)
                    {
                        foreach (object item in list)
                        {
                            element.Add(ObjectToXml(item, kvp.Key));
                        }
                    }
                    else
                    {
                        element.Add(ObjectToXml(kvp.Value, kvp.Key));
                    }
                }
            }
        }
        else
        {
            element.Value = obj.ToString() ?? "";
        }

        return element;
    }
}
