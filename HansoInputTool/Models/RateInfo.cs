using Newtonsoft.Json;

namespace HansoInputTool.Models
{
    public class RateInfo
    {
        [JsonProperty("基本料金")]
        public int BaseFee { get; set; }

        [JsonProperty("走行料金")]
        public int MileageFee { get; set; }

        [JsonProperty("深夜固定")]
        public int LateNightFixedFee { get; set; }

        [JsonProperty("深夜単価")]
        public int LateNightUnitFee { get; set; }
    }
}