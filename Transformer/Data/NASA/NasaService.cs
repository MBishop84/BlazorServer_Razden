using Newtonsoft.Json;

namespace Transformer_.Data.NASA
{
    public class NasaService(IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        private readonly string _url = configuration["NASA:ExoplanetRequestUrl"] ?? "";
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

        public async Task<IEnumerable<Exoplanet>> GetExoPlanetsAsync()
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync(_url);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadAsStringAsync();
                var exoplanets = JsonConvert.DeserializeObject<IEnumerable<Exoplanet>>(result);
                return exoplanets ?? [];
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return [];
            }
        }
    }
}
