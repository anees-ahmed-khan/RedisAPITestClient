using System.Text.Json.Serialization;

namespace RedisAPITestClient
{
    public class Security
    {
        [JsonPropertyName("PpaSecurityId")]
        public string SecurityId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("ticker")]
        public string Ticker { get; set; }

        [JsonPropertyName("cusip")]
        public string Cusip { get; set; }

        [JsonPropertyName("sedol")]
        public string Sedol { get; set; }

        [JsonPropertyName("isin")]
        public string Isin { get; set; }

        [JsonPropertyName("apir")]
        public string Apir { get; set; }

        [JsonPropertyName("productTypeLevel1Name")]
        public string ProductTypeLevel1Name { get; set; }

        [JsonPropertyName("productTypeLevel6Name")]
        public string ProductTypeLevel6Name { get; set; }

        [JsonPropertyName("country")]
        public string Country { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; }

        [JsonPropertyName("exchangeCode")]
        public string ExchangeCode { get; set; }

        [JsonPropertyName("price")]
        public decimal? Price { get; set; }

        [JsonPropertyName("priceCurrency")]
        public string PriceCurrency { get; set; }
    }
}
