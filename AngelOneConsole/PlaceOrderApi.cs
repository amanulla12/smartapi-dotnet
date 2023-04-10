using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using AngelOneClass;
using Newtonsoft.Json.Linq;


namespace Websocket2
{
    class PlaceOrderApi
    {
      
        static Ticker ticker;
        static string Client_code = "";
        static string Password = "";
        static string MyAPIKey = "";
        static string JWTToken = "";  // optional
        static string RefreshToken = ""; // optional
        static string totp = "";


        static void Main(string[] args)
        {         

            SmartApi connect = new SmartApi(MyAPIKey, JWTToken, RefreshToken);

            OutputBaseClass obj = new OutputBaseClass();

            //Login by client code and password
            obj = connect.GenerateSession(Client_code, Password, totp);
            AngelToken sagr = obj.TokenResponse;

            //Get Token
            obj = connect.GenerateToken();
            sagr = obj.TokenResponse;

            Console.WriteLine("------GenerateSession call output-------------");
            Console.WriteLine(JsonConvert.SerializeObject(sagr));
            Console.WriteLine("----------------------------------------------");

            ////Place Order
            //OrderInfo sord = new OrderInfo();
            //sord.variety = Constants.VARIETY_NORMAL;
            //sord.tradingsymbol = "SBIN-EQ";
            //sord.symboltoken = "3045";
            //sord.transactiontype = Constants.TRANSACTION_TYPE_BUY;
            //sord.exchange = Constants.EXCHANGE_NSE;
            //sord.ordertype = Constants.ORDER_TYPE_LIMIT;
            //sord.producttype = Constants.PRODUCT_TYPE_INTRADAY;
            //sord.duration = Constants.VALIDITY_DAY.ToString();
            //sord.price = "19500";
            //sord.squareoff = "0";
            //sord.stoploss = "0";
            //sord.quantity = "1";

            //obj = connect.PlaceOrder(sord);
            //OrderResponse sOres = obj.PlaceOrderResponse;

            //Console.WriteLine("------Place Order-------------");
            //Console.WriteLine(JsonConvert.SerializeObject(sOres));
            //Console.WriteLine("---------------------------------");

            //Initialize WebSocket ticker
            initTicker(sagr.jwtToken, MyAPIKey, Client_code, sagr.feedToken);


        }

        private static void initTicker(string jwttoken, string MyAPIKey, string client_code, string feedtoken)
        {
            ticker = new Ticker(jwttoken, MyAPIKey, client_code, feedtoken);

            ticker.OnTick += OnTick;
            ticker.OnReconnect += OnReconnect;
            ticker.OnNoReconnect += OnNoReconnect;
            ticker.OnError += OnError;
            ticker.OnClose += OnClose;
            ticker.OnConnect += OnConnect;        

            ticker.EnableReconnect(Interval: 5, Retries: 50);
            ticker.Connect(); 

            ticker.SetMode(Tokens: new UInt32[] { 1232 }, Mode: Constants.MODE_LTP);

        }

        static void WriteResult(object sender, MessageEventArgs e)
        {
            Console.WriteLine("Tick Received : " + e.Message);

        }

        private static void OnConnect()
        {
            Console.WriteLine("Connected ticker");
        }

        private static void OnClose()
        {
            Console.WriteLine("Closed ticker");
        }

        private static void OnError(string Message)
        {
            Console.WriteLine("Error: " + Message);
        }

        private static void OnNoReconnect()
        {
            Console.WriteLine("Not reconnecting");
        }

        private static void OnReconnect()
        {
            Console.WriteLine("Reconnecting");
        }

        private static void OnTick(Tick TickData)
        {
            Console.WriteLine("Tick " + Utils.JsonSerialize(TickData));
        }
    }
}
