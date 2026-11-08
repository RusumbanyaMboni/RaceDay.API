
using System.Net.Http;

namespace RaceDay.MVC.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;

        public ApiService(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory
                .CreateClient("RaceDayAPI");
        }

        public async Task<bool> IsApiAvailableAsync()
        {
            try
            {
                using var response = await _httpClient.GetAsync(
                    "swagger/v1/swagger.json");

                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException)
            {
                return false;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
        }
    }
}
