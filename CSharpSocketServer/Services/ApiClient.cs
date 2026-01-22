using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

public static class ApiClient
{
    private static readonly HttpClient _client = new HttpClient();

    // API 서버 기본 URL 설정
    static ApiClient()
    {
        _client.BaseAddress = new Uri("http://localhost:5146/"); 
    }

    // POST 요청 보내기
    public static async Task<bool> Post<T>(string url, T body)
    {
        var response = await _client.PostAsJsonAsync(url, body);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"API Error: {response.StatusCode}");
            var msg = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Message: {msg}");
            return false;
        }

        return true;
    }
}
