using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Text.Json;
using System.Threading.Tasks;
using HtmlAgilityPack;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using static System.Runtime.InteropServices.JavaScript.JSType;

public interface ISearchService : IDisposable
{
    Task<List<SearchResult>> PerformWebSearchList(string query);
}

public class UnifiedSearchService : ISearchService
{
    private readonly List<ISearchService> _searchServices;
    private bool _disposed = false;

    public UnifiedSearchService()
    {
        _searchServices = [new GoogleSearchService(), new DuckDuckGoSearchService()];
    }

    public UnifiedSearchService(List<ISearchService> searchServices)
    {
        _searchServices = searchServices;
    }

    public async Task<List<SearchResult>> PerformWebSearchList(string query)
    {
        var tasks = _searchServices.Select(service => service.PerformWebSearchList(query));
        var results = await Task.WhenAll(tasks);
        //return results.SelectMany(r => r).DistinctBy(r => r.Url).Take(10).ToList();
        return results.SelectMany(r => r).DistinctBy(r => r.Url).ToList();
    }

    public static string FormatResultsForLLM(List<SearchResult> results)
    {
        var formattedResults = new System.Text.StringBuilder();
        formattedResults.AppendLine("Search Results:");
        int i = 1;
        foreach (var result in results)
        {
            formattedResults.AppendLine($"{i}:");
            formattedResults.AppendLine($"Title: {result.Title}");
            formattedResults.AppendLine($"URL: {result.Url}");
            formattedResults.AppendLine($"Snippet: {result.Snippet}");
            formattedResults.AppendLine($"Content: {result.Content}");
            formattedResults.AppendLine();
            i++;
        }
        return formattedResults.ToString();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                foreach (var service in _searchServices)
                {
                    service.Dispose();
                }
            }

            _disposed = true;
        }
    }
}

public class BingSearchService
{
    private readonly HttpClient _httpClient;
    private readonly string _subscriptionKey;
    private const string _endpoint = "https://api.bing.microsoft.com/v7.0/search";

    public BingSearchService(string subscriptionKey)
    {
        _subscriptionKey = subscriptionKey;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _subscriptionKey);
    }

    public async Task<List<SearchResult>> PerformWebSearch(string query)
    {
        var response = await _httpClient.GetAsync($"{_endpoint}?q={Uri.EscapeDataString(query)}&count=5");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();

        return ParseSearchResults(content);
    }

    private List<SearchResult> ParseSearchResults(string jsonContent)
    {
        var results = new List<SearchResult>();
        using (JsonDocument doc = JsonDocument.Parse(jsonContent))
        {
            JsonElement root = doc.RootElement;
            JsonElement webPages = root.GetProperty("webPages");
            JsonElement value = webPages.GetProperty("value");

            foreach (JsonElement item in value.EnumerateArray())
            {
                results.Add(new SearchResult
                {
                    Title = item.GetProperty("name").GetString(),
                    Url = item.GetProperty("url").GetString(),
                    Snippet = item.GetProperty("snippet").GetString()
                });
            }
        }
        return results;
    }
}

public class GoogleSearchService : ISearchService
{
    private readonly IWebDriver _driver;

    public GoogleSearchService()
    {
        var options = new ChromeOptions();
        options.AddArgument("headless");
        options.AddArgument("disable-gpu");
        options.AddArgument("no-sandbox");
        options.AddArgument("disable-dev-shm-usage");
        options.AddArgument("window-size=1920,1080");
        options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

        var driverService = ChromeDriverService.CreateDefaultService();
        driverService.HideCommandPromptWindow = true;

        _driver = new ChromeDriver(driverService, options);
    }

    

    public async Task<string> PerformWebSearchRaw(string query)
    {
        var searchUrl = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}";
        await Task.Run(() => _driver.Navigate().GoToUrl(searchUrl));

        // Wait for the search results to load
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
        var testsource = _driver.PageSource;
        return testsource;
    }

    public async Task<List<SearchResult>> PerformWebSearchList(string query)
    {
        await Task.Run(() => _driver.Navigate().GoToUrl($"https://www.google.com/search?q={Uri.EscapeDataString(query)}"));

        // Wait for the search results to load
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
        await Task.Run(() => wait.Until(d => d.FindElements(By.CssSelector("div.g")).Count > 0));

        return await Task.Run(() => ExtractSearchResults());
    }

    public async Task<string> PerformWebSearch(string query)
    {
        var searchUrl = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}";
        await Task.Run(() => _driver.Navigate().GoToUrl(searchUrl));

        // Wait for the search results to load
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
        await Task.Run(() => wait.Until(d => d.FindElements(By.CssSelector("div.g")).Count > 0));

        // Extract search result snippets
        var snippets = await Task.Run(() =>
            _driver.FindElements(By.CssSelector("div.g .aCOpRe"))
                .Take(3)
                .Select(e => e.Text)
                .ToList()
        );

        if (snippets.Any())
        {
            return string.Join("\n", snippets);
        }

        return "No results found.";
    }

    private List<SearchResult> ExtractSearchResults()
    {
        var results = new List<SearchResult>();
        var resultElements = _driver.FindElements(By.CssSelector("div.g"));

        //var source = _driver.PageSource;

        foreach (var element in resultElements)
        {
            try
            {
                var elementText = element.Text;
                var titleElement = element.FindElement(By.CssSelector("h3"));
                var titleElementText = titleElement.GetAttribute("innerText");

                var citeElement = element.FindElement(By.CssSelector("cite"));
                var citeText = citeElement.GetAttribute("innerText");
                var linkElement = element.FindElement(By.CssSelector("a"));
                var Urltext = linkElement.GetAttribute("href");

                var snippetElement = element.FindElement(By.CssSelector("div.VwiC3b"));
                var snippet = snippetElement.GetAttribute("innerText");

                var result = new SearchResult
                {
                    Title = titleElementText,
                    Url = linkElement.GetAttribute("href"),
                    Snippet = snippet
                };

                results.Add(result);
            }
            catch (NoSuchElementException)
            {
                // Skip this result if we can't find all required elements
                continue;
            }
            /*
            if (results.Count >= 5)
            {
                break; // Limit to 5 results
            }
            */
        }

        return results;
    }

    public void Dispose()
    {
        _driver?.Quit();
        _driver?.Dispose();
    }
}

public class SearchResult
{
    public string Title { get; set; }
    public string Url { get; set; }
    public string Snippet { get; set; }
    public string Content { get; set; }
    public List<LinkInfo> Links { get; set; }

    public override string ToString()
    {
        return $"Title: {Title}\nURL: {Url}\nSnippet: {Snippet}\n";
    }
}

public class DuckDuckGoSearchService : ISearchService
{
    private readonly IWebDriver _driver;

    public DuckDuckGoSearchService()
    {
        var options = new ChromeOptions();
        options.AddArgument("headless");
        options.AddArgument("disable-gpu");
        options.AddArgument("no-sandbox");
        options.AddArgument("disable-dev-shm-usage");
        options.AddArgument("window-size=1920,1080");
        options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

        var driverService = ChromeDriverService.CreateDefaultService();
        driverService.HideCommandPromptWindow = true;

        _driver = new ChromeDriver(driverService, options);
    }

    public async Task<string> PerformWebSearchRaw(string query)
    {
        var searchUrl = $"https://duckduckgo.com/?q={Uri.EscapeDataString(query)}";
        await Task.Run(() => _driver.Navigate().GoToUrl(searchUrl));

        // Wait for the search results to load
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
        var testsource = _driver.PageSource;
        return testsource;
    }

    public async Task<List<SearchResult>> PerformWebSearchList(string query)
    {
        return await PerformExtendedWebSearchList(query, 15);

        await Task.Run(() => _driver.Navigate().GoToUrl($"https://duckduckgo.com/?q={Uri.EscapeDataString(query)}"));

        // Wait for the search results to load
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
        await Task.Run(() => wait.Until(d => d.FindElements(By.CssSelector("article.yQDlj3B5DI5YO8c8Ulio")).Count > 0));

        return await Task.Run(() => ExtractSearchResults());
    }

    public async Task<string> PerformWebSearch(string query)
    {
        var searchUrl = $"https://duckduckgo.com/?q={Uri.EscapeDataString(query)}";
        await Task.Run(() => _driver.Navigate().GoToUrl(searchUrl));

        // Wait for the search results to load
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
        await Task.Run(() => wait.Until(d => d.FindElements(By.CssSelector("article.yQDlj3B5DI5YO8c8Ulio")).Count > 0));

        // Extract search result snippets
        var snippets = await Task.Run(() =>
            _driver.FindElements(By.CssSelector("div.OgdwYG6KE2qthn9XQWFC span.kY2IgmnCmOGjharHErah"))
                .Take(3)
                .Select(e => e.Text)
                .ToList()
        );

        if (snippets.Any())
        {
            return string.Join("\n", snippets);
        }

        return "No results found.";
    }

    public async Task<List<SearchResult>> PerformExtendedWebSearchList(string query, int desiredResultCount)
    {
        var results = new List<SearchResult>();

        await Task.Run(() => _driver.Navigate().GoToUrl($"https://duckduckgo.com/?q={Uri.EscapeDataString(query)}"));

        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));

        while (results.Count < desiredResultCount)
        {
            await Task.Run(() => wait.Until(d => d.FindElements(By.CssSelector("article[data-testid='result']")).Count > 0));
            var newResults = await Task.Run(() => ExtractSearchResults());
            
            results.AddRange(newResults.Where(r => !results.Any(existing => existing.Url == r.Url)));

            if (newResults.Count == 0 || results.Count < desiredResultCount) break; // No more results
            
            // Try to find and click the "More results" button using multiple methods
            try
            {
                var buttonLocator = By.CssSelector("button#more-results");
                var moreResultsButton = wait.Until(d => d.FindElement(buttonLocator));
                moreResultsButton.Click();
            }
            catch (WebDriverTimeoutException)
            {
                Console.WriteLine("Timeout waiting for 'More results' button.");
                break;
            }
            catch (NoSuchElementException)
            {
                Console.WriteLine("'More results' button not found.");
                break;
            }
        }

        return results.Take(desiredResultCount).ToList();
    }
    private List<SearchResult> ExtractSearchResults()
    {
        var results = new List<SearchResult>();
        var resultElements = _driver.FindElements(By.CssSelector("article[data-testid='result']"));

        foreach (var element in resultElements)
        {
            try
            {
                var titleElement = element.FindElement(By.CssSelector("h2 a"));
                var snippetElement = element.FindElement(By.CssSelector("div[data-result='snippet']"));

                var result = new SearchResult
                {
                    Title = titleElement.Text,
                    Url = titleElement.GetAttribute("href"),
                    Snippet = snippetElement.Text
                };

                results.Add(result);
            }
            catch (NoSuchElementException)
            {
                // Skip this result if we can't find all required elements
                continue;
            }
        }

        return results;
    }

    public void Dispose()
    {
        _driver?.Quit();
        _driver?.Dispose();
    }
}