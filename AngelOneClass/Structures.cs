using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace AngelOneClass
{
    /// <summary>
    /// Tick data structure
    /// </summary>
    public struct Tick
    {
        public String Mode { get; set; }
        public UInt16 subscription_mode { get; set; }
        public UInt16 exchange_type { get; set; }
        public String token { get; set; }
        public Int64 sequence_number { get; set; }
        //public DateTime? ExchangeTimestam;
        public DateTime ExchangeTimestam { get; set; }
        public Double last_traded_price { get; set; }
        public Double last_traded_quantity { get; set; } 
        public Double avg_traded_price { get; set; }
        public Double vol_traded { get; set; }
        public Int64 total_buy_quantity { get; set; }
        public Int64 total_sell_quantity { get; set; }
        public Double open_price_day { get; set; }
        public Double high_price_day { get; set; }
        public Double low_price_day { get; set; }
        public Double close_price { get; set; }
        public DateTime? last_traded_timestamp { get; set; }
        public Int64 open_interest { get; set; }
        public Double open_interest_change { get; set; }
        public Int16 upper_circuit { get; set; }
        public Int16 lower_circuit { get; set; }
        public Int16 fiftytwo_week_high { get; set; }
        public Int16 fiftytwo_week_low { get; set; }
        public BestFive[] best_five_data { get; set; }

    }

    public struct BestFive
    {
        public BestFive(Dictionary<string, dynamic> data)
        {
            buy_sell = Convert.ToUInt16(data["buy_sell"]);
            
        }

        public UInt16 buy_sell { get; set; }
       
    }   
}
