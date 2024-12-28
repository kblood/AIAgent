using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;

public class WebScraper
{
    private readonly HttpClient _httpClient;

    public WebScraper()
    {
        _httpClient = new HttpClient();
    }

    public async Task<string> ScrapeWebpage(string url)
    {
        try
        {
            var html = await _httpClient.GetStringAsync(url);
            if(!Pages.ContainsKey(url))
                Pages.Add(url, html);
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);

            var bodyNode = htmlDocument.DocumentNode.SelectSingleNode("//body");
            if (bodyNode != null)
            {
                return bodyNode.InnerText.Trim();
            }
            return "No content found.";
        }
        catch (Exception ex)
        {
            return $"Error scraping {url}: {ex.Message}";
        }
    }

    Dictionary<string, string> Pages = new Dictionary<string, string>();

    //public List<string> ExtractLinks(string baseUrl)
    //{
    //    var html = Pages[baseUrl];
    //    var htmlDocument = new HtmlDocument();
    //    htmlDocument.LoadHtml(html);

    //    var links = new List<string>();
    //    var linkNodes = htmlDocument.DocumentNode.SelectNodes("//a[@href]");

    //    if (linkNodes != null)
    //    {
    //        foreach (var linkNode in linkNodes)
    //        {
    //            var href = linkNode.GetAttributeValue("href", "");
    //            if (!string.IsNullOrWhiteSpace(href))
    //            {
    //                if (Uri.TryCreate(new Uri(baseUrl), href, out Uri absoluteUri))
    //                {
    //                    if (!links.Contains(absoluteUri.ToString()))
    //                        links.Add(absoluteUri.ToString());
    //                }
    //            }
    //        }
    //    }

    //    return links;
    //}
    public List<LinkInfo> ExtractLinks(string baseUrl)
    {
        if(!Pages.ContainsKey(baseUrl))
            return new List<LinkInfo>();
        var html = Pages[baseUrl];
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(html);

        var links = new Dictionary<string, LinkInfo>();
        var linkNodes = htmlDocument.DocumentNode.SelectNodes("//a[@href]");

        if (linkNodes != null)
        {
            foreach (var linkNode in linkNodes)
            {
                var href = linkNode.GetAttributeValue("href", "");
                if (!string.IsNullOrWhiteSpace(href) && Uri.TryCreate(new Uri(baseUrl), href, out Uri absoluteUri))
                {
                    var url = absoluteUri.ToString();
                    if (!links.ContainsKey(url))
                    {
                        var title = ExtractTitle(linkNode);
                        var description = ExtractDescription(linkNode);

                        links[url] = new LinkInfo
                        {
                            Url = url,
                            Title = title,
                            Description = description
                        };
                    }
                }
            }
        }

        return links.Values.ToList();
    }

    private string ExtractTitle(HtmlNode linkNode)
    {
        // Try to get title from the link text
        var title = linkNode.InnerText.Trim();

        // If link text is empty, try to get title from title attribute
        if (string.IsNullOrWhiteSpace(title))
        {
            title = linkNode.GetAttributeValue("title", "");
        }

        // If still empty, try to get from aria-label
        if (string.IsNullOrWhiteSpace(title))
        {
            title = linkNode.GetAttributeValue("aria-label", "");
        }

        return title;
    }

    private string ExtractDescription(HtmlNode linkNode)
    {
        // Try to get description from surrounding paragraph
        var parentParagraph = linkNode.Ancestors("p").FirstOrDefault();
        if (parentParagraph != null)
        {
            return parentParagraph.InnerText.Trim();
        }

        // If no surrounding paragraph, try to get from next sibling paragraph
        var nextParagraph = linkNode.NextSibling;
        while (nextParagraph != null && nextParagraph.Name != "p")
        {
            nextParagraph = nextParagraph.NextSibling;
        }

        if (nextParagraph != null && nextParagraph.Name == "p")
        {
            return nextParagraph.InnerText.Trim();
        }

        // If still no description, return empty string
        return "";
    }
}

public class LinkInfo
{
    public string Url { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }

    public override string ToString()
    {
        return $"URL: {Url}\nTitle: {Title}\nDescription: {Description}\n";
    }
}