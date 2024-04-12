namespace Transformer_.Data.NASA
{
    /// <summary>
    /// An exoplanet Object
    /// </summary>
    public class Exoplanet
    {
        public string pl_name { get; set; }
        public int disc_year { get; set; }
        public string discoverymethod { get; set; }
        public string hostname { get; set; }
        public string disc_facility { get; set; }
        public string disc_instrument { get; set; }
        public string pl_orbper_reflink { get; set; }
    }
}
