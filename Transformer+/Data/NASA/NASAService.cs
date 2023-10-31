using Newtonsoft.Json;

namespace Transformer_.Data.NASA
{
    public class NASAService
    {
        private readonly string url;

        public NASAService(IConfiguration configuration)
        {
            url = configuration["NASA:ExoplanetRequestUrl"];
        }

        public async Task<IEnumerable<Exoplanet>> GetExoPlanetsAsync()
        {
            try
            {
                using var client = new HttpClient();
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadAsStringAsync();
                var exoplanets = JsonConvert.DeserializeObject<IEnumerable<Exoplanet>>(result);
                return exoplanets ?? Enumerable.Empty<Exoplanet>();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return Enumerable.Empty<Exoplanet>();
            }
        }
    }
}
