using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using RedisAPITestClient;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

class Program
{
    static async Task Main(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // The API enpoints
        string apiUrlForResolve = config["ApiUrls:Resolve"]!;
        string apiUrl = config["ApiUrls:SearchKeyword"]!;
        string apiUrlSearch = config["ApiUrls:Search"]!;
        string driverFilePath = config["FilePaths:DriverFile"]!;
        string logOutFilePathForResolve = string.Format(config["FilePaths:LogOutFileForResolve"]!, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
        string logOutFilePathForSearch = string.Format(config["FilePaths:LogOutFileForSearch"]!, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
        string bearerToken = config["BearerToken"]!;

        HttpClient client = new();
        client.DefaultRequestHeaders.Add("accept", "application/json;odata.metadata=minimal;odata.streaming=true");
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {bearerToken}");

        //await apiTest_resolveMethod(apiUrlForResolve, bearerToken, client, driverFilePath, logOutFilePathForResolve);
        //await api_search(apiUrl, driverFilePath, bearerToken, client, logOutFilePathForSearch);

        await Task.WhenAll(
            ApiTest_resolveMethod(apiUrlForResolve, bearerToken, client, driverFilePath, logOutFilePathForResolve),
            Api_searchKeyword(apiUrl, driverFilePath, bearerToken, client, logOutFilePathForSearch),
            Api_search(apiUrlSearch, driverFilePath, bearerToken, client, logOutFilePathForSearch)
        );
        client.Dispose();
    }

    private static async Task Api_search(string apiUrl, string driverFilePath, string bearerToken, HttpClient client, string outFilePath)
    {
        List<string> keywords = BuildIdList(driverFilePath);
        StreamWriter _writer = new(outFilePath);

        string tempURL = $"{apiUrl}";

        bool localOnly = true;
        List<string> assetClassesToInclude = ["Stock/Fund"];

        var requestBody = new
        {
            keywords = keywords.Count > 500 ? keywords.GetRange(0, 100) : keywords,
            localOnly,
            assetClassesToInclude
        };

        var apitimer = Stopwatch.StartNew();

        var response = await MakePostRequest(apiUrl, bearerToken, requestBody, client);

        apitimer.Stop();

        JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true
        };
        var data = System.Text.Json.JsonSerializer.Deserialize<List<Security>>(response, options);

        string result_message = tempURL + " found " + data.Count + " in " + apitimer.ElapsedMilliseconds.ToString();
        Console.WriteLine(result_message);
        _writer.WriteLine(result_message);
        Thread.Sleep(50);

        _writer.Close();
    }

    private static async Task Api_searchKeyword(string apiUrl, string driverFilePath, string bearerToken, HttpClient client, string outFilePath)
    {
        List<string> _secList = BuildIdList(driverFilePath);
        StreamWriter _writer = new StreamWriter(outFilePath);
        foreach (string security in _secList)
        {
            string tempURL = $"{apiUrl}{security}?Limit=10&LocalOnly=true";
            var apitimer = Stopwatch.StartNew();
            var result = await ApiTest_searchByKeyword(tempURL, bearerToken, client);
            apitimer.Stop();
            string result_message = result.retURL + " found " + result.itemsCount + " in " + apitimer.ElapsedMilliseconds.ToString();
            Console.WriteLine(result_message);
            _writer.WriteLine(result_message);
            Thread.Sleep(500);
        }
        _writer.Close();
    }

    private static async Task<(int itemsCount, string retURL)> ApiTest_searchByKeyword(string apiUrl, string bearerToken, HttpClient httpClient)
    {
        int itemsCount = 0;
        string retURL = "";
        try
        {
            HttpResponseMessage response = await httpClient.GetAsync(apiUrl);
            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync();
                JsonArray items = (JsonArray)JsonArray.Parse(content)!;
                itemsCount = items.Count;
                retURL = apiUrl;
            }
            else
            {
                Console.WriteLine($"Error: {response.StatusCode} - {response.ReasonPhrase}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
        }
        return (itemsCount, retURL);
    }

    private static async Task ApiTest_resolveMethod(string apiUrl, string bearerToken, HttpClient httpClient, string driverFilePath, string outFilePath)
    {
        List<string> _idsToResolve = BuildIdList(driverFilePath);
        StreamWriter _writer = new StreamWriter(outFilePath);
        int count = 0;
        foreach (string id in _idsToResolve)
        {
            var requestPayload = new
            {
                securityIds = new[]
                {
                    new
                    {
                        securityIdentifierType = "TICKER",
                        securityIdentifier = id
                    }
                },
                localOnly = true
            };
            var apitimer = Stopwatch.StartNew();
            var response = await MakePostRequest(apiUrl, bearerToken, requestPayload, httpClient);
            apitimer.Stop();
            JsonSerializerOptions options = new()
            {
                PropertyNameCaseInsensitive = true
            };
            var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, List<Security>>>(response, options);
            var key = requestPayload.securityIds[0].securityIdentifierType + "-" + requestPayload.securityIds[0].securityIdentifier;
            int totalSecurities = 0;

            string ppaIds = string.Empty;
            if (data != null && data.TryGetValue(key, out List<Security>? value))
            {
                var securities = value;
                totalSecurities = securities.Count;
                var securityIds = new List<string>();
                foreach (var security in securities)
                {
                    if (security.SecurityId != null)
                    {
                        securityIds.Add(security.SecurityId);
                    }
                }
                ppaIds = string.Join(" | ", securityIds);
            }
            count++;
            string logLine = $"{count}- {apiUrl}:{id} resolved in [{apitimer.ElapsedMilliseconds} ms], [Security count = {totalSecurities}, PPA Ids = {ppaIds}]";
            Console.WriteLine(logLine);
            _writer.WriteLine(logLine);
            Thread.Sleep(50);
        }
        _writer.Close();
    }

    private static async Task<string> MakePostRequest(string apiUrl, string bearerToken, object payload, HttpClient httpClient)
    {
        string jsonPayload = JsonConvert.SerializeObject(payload);
        StringContent content = new(jsonPayload, Encoding.UTF8, "application/json");
        httpClient.Timeout = TimeSpan.FromMinutes(5);
        try
        {
            HttpResponseMessage response = await httpClient.PostAsync(apiUrl, content);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
            else
            {
                return $"Error: {response.StatusCode} - {response.ReasonPhrase}";
            }
        }
        catch (TaskCanceledException ex) when (!ex.CancellationToken.IsCancellationRequested)
        {
            return "Error: Request timed out.";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    static List<string> BuildIdList(string driverFilePath)
    {
        string _filePath = driverFilePath;
        List<string> _secList = new List<string>();
        try
        {
            string[] lines = File.ReadAllLines(_filePath);
            foreach (string line in lines)
            {
                _secList.Add(line);
            }
        }
        catch { }

        return _secList;
    }
}
