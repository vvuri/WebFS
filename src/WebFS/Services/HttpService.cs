using System.Net;

namespace WebFS;

public class HttpService
{
    private readonly HttpClient _httpClient;

    public HttpService()
    {
        _httpClient = new HttpClient();
    }

    public async Task<HttpStatusCode> IsAvailableAsync(string url)
    {
        try
        {
            //var response = await _httpClient.GetAsync(url);
            using var request = new HttpRequestMessage(HttpMethod.Head, url);

            var response = await _httpClient.SendAsync(request);
            
            return response.StatusCode;
        }
        catch
        {
            return HttpStatusCode.NotFound;
        }
    }
}