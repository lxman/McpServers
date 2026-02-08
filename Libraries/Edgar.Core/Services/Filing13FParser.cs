using System.Xml.Linq;
using Edgar.Core.Models;
using Microsoft.Extensions.Logging;

namespace Edgar.Core.Services;

public class Filing13FParser(ILogger<Filing13FParser> logger)
{
    private static readonly XNamespace Ns13F =
        "http://www.sec.gov/edgar/document/thirteenf/informationtable";

    public List<Holding> Parse(string xml)
    {
        var holdings = new List<Holding>();

        try
        {
            XDocument doc = XDocument.Parse(xml);
            XElement? root = doc.Root;
            if (root is null) return holdings;

            // Handle both namespaced and non-namespaced elements
            List<XElement> infoEntries = root.Descendants(Ns13F + "infoTable").ToList();
            if (infoEntries.Count == 0)
            {
                // Try without namespace
                infoEntries = root.Descendants("infoTable").ToList();
            }

            foreach (XElement entry in infoEntries)
            {
                try
                {
                    Holding? holding = ParseEntry(entry);
                    if (holding is not null) holdings.Add(holding);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to parse a holding entry, skipping");
                }
            }

            logger.LogInformation("Parsed {Count} holdings from 13F XML", holdings.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse 13F XML document");
            throw;
        }

        return holdings;
    }

    private Holding? ParseEntry(XElement entry)
    {
        string? nameOfIssuer = GetElementValue(entry, "nameOfIssuer");
        string? cusip = GetElementValue(entry, "cusip");
        if (string.IsNullOrEmpty(nameOfIssuer) || string.IsNullOrEmpty(cusip))
            return null;

        XElement? shrsOrPrnAmt = GetDescendant(entry, "shrsOrPrnAmt");
        long shares = 0;
        string shareType = "SH";

        if (shrsOrPrnAmt is not null)
        {
            string? amtStr = GetElementValue(shrsOrPrnAmt, "sshPrnamt");
            if (long.TryParse(amtStr, out long amt)) shares = amt;
            shareType = GetElementValue(shrsOrPrnAmt, "sshPrnamtType") ?? "SH";
        }

        string? valueStr = GetElementValue(entry, "value");
        long.TryParse(valueStr, out long value);

        return new Holding
        {
            NameOfIssuer = nameOfIssuer,
            TitleOfClass = GetElementValue(entry, "titleOfClass") ?? "",
            Cusip = cusip,
            ReportedValue = value,
            SharesOrPrincipalAmount = shares,
            SharesOrPrincipalAmountType = shareType
        };
    }

    private static string? GetElementValue(XElement parent, string localName)
    {
        // Try namespaced first, then unnamespaced
        XElement? el = parent.Element(Ns13F + localName) ?? parent.Element(localName);
        return el?.Value?.Trim();
    }

    private static XElement? GetDescendant(XElement parent, string localName)
    {
        return parent.Descendants(Ns13F + localName).FirstOrDefault()
            ?? parent.Descendants(localName).FirstOrDefault();
    }
}
