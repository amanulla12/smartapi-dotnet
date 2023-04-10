using Newtonsoft.Json.Linq;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Numerics;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;

namespace AngelOneClass
{
    
    public class Ticker
    {
        // If set to true will print extra debug information
        private bool _debug = false;

        // Root domain for ticker
        private string _root = "ws://smartapisocket.angelone.in/smart-stream";

       // Configurations to create ticker connection
        private string _apiKey;
        private string _accessToken;
        private string _socketUrl = "";
        private bool _isReconnect = false;
        private int _interval = 5;
        private int _retries = 50;
        private int _retryCount = 0;
        private string _clientcode;
        private string _feedtoken;

        // A watchdog timer for monitoring the connection of ticker.
        System.Timers.Timer _timer;
        int _timerTick = 5;

        // Instance of WebSocket class that wraps .Net version
        private IWebSocket _ws;

        // Dictionary that keeps instrument_token -> mode data
        private Dictionary<UInt32, string> _subscribedTokens;

        private Dictionary<string, string> _headers = null;

      

        /// <summary>
        /// Delegate for OnConnect event
        /// </summary>
        public delegate void OnConnectHandler();

        /// <summary>
        /// Delegate for OnClose event
        /// </summary>
        public delegate void OnCloseHandler();

        /// <summary>
        /// Delegate for OnTick event
        /// </summary>
        /// <param name="TickData">Tick data</param>
        public delegate void OnTickHandler(Tick TickData);

        /// <summary>
        /// Delegate for OnOrderUpdate event
        /// </summary>
        /// <param name="OrderData">Order data</param>
        //public delegate void OnOrderUpdateHandler(Order OrderData);

        /// <summary>
        /// Delegate for OnError event
        /// </summary>
        /// <param name="Message">Error message</param>
        public delegate void OnErrorHandler(string Message);

        /// <summary>
        /// Delegate for OnReconnect event
        /// </summary>
        public delegate void OnReconnectHandler();

        /// <summary>
        /// Delegate for OnNoReconnect event
        /// </summary>
        public delegate void OnNoReconnectHandler();

        // Events that can be subscribed
        /// <summary>
        /// Event triggered when ticker is connected
        /// </summary>
        public event OnConnectHandler OnConnect;

       // public event OnDataHandler OnData;

        /// <summary>
        /// Event triggered when ticker is disconnected
        /// </summary>
        public event OnCloseHandler OnClose;

        /// <summary>
        /// Event triggered when ticker receives a tick
        /// </summary>
        public event OnTickHandler OnTick;

        /// <summary>
        /// Event triggered when ticker receives an order update
        /// </summary>
       // public event OnOrderUpdateHandler OnOrderUpdate;

        /// <summary>
        /// Event triggered when ticker encounters an error
        /// </summary>
        public event OnErrorHandler OnError;

        /// <summary>
        /// Event triggered when ticker is reconnected
        /// </summary>
        public event OnReconnectHandler OnReconnect;

        /// <summary>
        /// Event triggered when ticker is not reconnecting after failure
        /// </summary>
        public event OnNoReconnectHandler OnNoReconnect;

      
       public Ticker(string jwttoken, string APIKey, string client_code, string feedtoken, string Root = null, bool Reconnect = false, int ReconnectInterval = 5, int ReconnectTries = 50, bool Debug = false, IWebSocket CustomWebSocket = null)
        {
            _debug = Debug;
            _apiKey = APIKey;
            _accessToken = jwttoken;
            _clientcode = client_code;
            _feedtoken = feedtoken;
            _subscribedTokens = new Dictionary<UInt32, string>();
            _interval = ReconnectInterval;
            _timerTick = ReconnectInterval;
            _retries = ReconnectTries;
            if (!String.IsNullOrEmpty(Root))
                _root = Root;
            _isReconnect = Reconnect;
            _socketUrl = _root; 
            // initialize websocket
            if (CustomWebSocket != null)
            {
                _ws = CustomWebSocket;
            }
            else
            {
                _ws = new WebSocket();
            }

            _ws.OnConnect += _onConnect;
            _ws.OnData += _onData;
            _ws.OnClose += _onClose;
            _ws.OnError += _onError;

            // initializing  watchdog timer
            _timer = new System.Timers.Timer();
            _timer.Elapsed += _onTimerTick;
            _timer.Interval = 30000; // checks connection every second
        }



        private void _onError(string Message)
        {
            // pipe the error message from ticker to the events
            OnError?.Invoke(Message);
        }

        private void _onClose()
        {
            // stop the timer while normally closing the connection
            _timer.Stop();
            OnClose?.Invoke();
        }

        /// <summary>
        /// Reads 2 byte short int from byte stream
        /// </summary>
        private ushort ReadShort(byte[] b, ref int offset)
        {
            ushort data = (ushort)(b[offset + 1] + (b[offset] << 8));
            offset += 2;
            return data;
        }

        /// <summary>
        /// Reads 4 byte int32 from byte stream
        /// </summary>
        private UInt32 ReadInt(byte[] b, ref int offset)
        {
            UInt32 data = (UInt32)BitConverter.ToUInt32(new byte[] { b[offset + 3], b[offset + 2], b[offset + 1], b[offset + 0] }, 0);
            offset += 4;
            return data;
        }

        private UInt32 ReadInt1(byte[] b, ref int offset)
        {
            UInt32 data = (UInt32)BitConverter.ToUInt32(new byte[] { b[offset + 3], b[offset + 2], b[offset + 1], b[offset + 0] }, 0);
            offset += 4;
            return data;
        }

    

        /// <summary>
        /// Reads an ltp mode tick from raw binary data
        /// </summary>
        private Tick ReadLTP(byte[] response)
        {
            Tick tick = new Tick();
            tick.Mode = Constants.MODE_LTP;

            int SubscriptionMode = response[0];
            tick.subscription_mode = Convert.ToUInt16(SubscriptionMode);
            //Console.WriteLine("SubscriptionMode : {0}", SubscriptionMode);

            int ExchangeType = response[1];
            tick.exchange_type = Convert.ToUInt16(ExchangeType);
           // Console.WriteLine("ExchangeType : {0}", ExchangeType);

            var token = Encoding.UTF8.GetString(response.Skip(2).Take(25).ToArray());
            tick.token = Encoding.UTF8.GetString(response.Skip(2).Take(25).ToArray());
           // Console.WriteLine("Token Is  : {0}", token);

            var sequenceNumber = BitConverter.ToInt64(response.Skip(27).Take(8).ToArray(), 0);
            tick.sequence_number = BitConverter.ToInt64(response.Skip(27).Take(8).ToArray(), 0);
           // Console.WriteLine("Sequence Number : {0}", sequenceNumber);

            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            TimeZoneInfo istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            var epocSeconds = BitConverter.ToInt64(response.Skip(35).Take(8).ToArray(), 0);
            var currentDateTimeData = TimeZoneInfo.ConvertTimeFromUtc(epoch.AddMilliseconds(epocSeconds), istZone);
           // Console.WriteLine("Exchange Timestamp : {0}", currentDateTimeData.ToString("dd-MMM-yyyy H:mm:ss"));
            tick.ExchangeTimestam = TimeZoneInfo.ConvertTimeFromUtc(epoch.AddMilliseconds(epocSeconds), istZone);

            var ltp = BitConverter.ToInt64(response.Skip(43).Take(8).ToArray(), 0);
            tick.last_traded_price = ltp / 100.00;
           // Console.WriteLine("Last Traded Price (LTP) : {0}", ltp / 100.00);
            return tick;
        }

        /// <summary>
        /// Reads a quote mode tick from raw binary data
        /// </summary>
        private Tick ReadQuote(byte[] response)
        {
            Tick tick = new Tick();
            tick.Mode = Constants.MODE_QUOTE;

            int SubscriptionMode = response[0];
            tick.subscription_mode = Convert.ToUInt16(SubscriptionMode);
           // Console.WriteLine("SubscriptionMode : {0}", SubscriptionMode);

            int ExchangeType = response[1];
            tick.exchange_type = Convert.ToUInt16(ExchangeType);
            //Console.WriteLine("ExchangeType : {0}", ExchangeType);

            var token = Encoding.UTF8.GetString(response.Skip(2).Take(25).ToArray());
            tick.token = Encoding.UTF8.GetString(response.Skip(2).Take(25).ToArray());
            //Console.WriteLine("Token Is  : {0}", token);

            var sequenceNumber = BitConverter.ToInt64(response.Skip(27).Take(8).ToArray(), 0);
            tick.sequence_number = BitConverter.ToInt64(response.Skip(27).Take(8).ToArray(), 0);
            //Console.WriteLine("Sequence Number : {0}", sequenceNumber);

            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            TimeZoneInfo istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            var epocSeconds = BitConverter.ToInt64(response.Skip(35).Take(8).ToArray(), 0);
            var currentDateTimeData = TimeZoneInfo.ConvertTimeFromUtc(epoch.AddMilliseconds(epocSeconds), istZone);
            //Console.WriteLine("Exchange Timestamp : {0}", currentDateTimeData.ToString("dd-MMM-yyyy H:mm:ss"));
            tick.ExchangeTimestam = TimeZoneInfo.ConvertTimeFromUtc(epoch.AddMilliseconds(epocSeconds), istZone);

            var ltp = BitConverter.ToInt64(response.Skip(43).Take(8).ToArray(), 0);
            tick.last_traded_price = ltp / 100.00;
            //Console.WriteLine("Last Traded Price (LTP) : {0}", ltp / 100.00);

            var ltq = BitConverter.ToInt64(response.Skip(50).Take(8).ToArray());
            tick.last_traded_quantity = ltq / 100.00;

            var atp = BitConverter.ToInt64(response.Skip(58).Take(8).ToArray());
            tick.avg_traded_price = atp / 100.00;

            var vt = BitConverter.ToInt64(response.Skip(66).Take(8).ToArray());
            tick.vol_traded = vt / 100.00;

            var tvq = BitConverter.ToInt64(response.Skip(74).Take(8).ToArray());
            tick.total_buy_quantity = BitConverter.ToInt64(response.Skip(74).Take(8).ToArray());
            // tick.total_buy_quantity = tvq / 100.00;

            var tsq = BitConverter.ToInt64(response.Skip(82).Take(8).ToArray());
            tick.total_sell_quantity = BitConverter.ToInt64(response.Skip(82).Take(8).ToArray());

            var opd = BitConverter.ToInt64(response.Skip(90).Take(8).ToArray());
            tick.open_price_day = opd / 100.00;

            var hpd = BitConverter.ToInt64(response.Skip(98).Take(8).ToArray());
            tick.high_price_day = hpd / 100.00;

            var lpd = BitConverter.ToInt64(response.Skip(106).Take(8).ToArray());
            tick.low_price_day = lpd / 100.00;
               
            var cp = BitConverter.ToInt64(response.Skip(114).Take(8).ToArray());
            tick.close_price = cp/ 100.00;
            return tick;

        }

        /// <summary>
        /// Reads a full mode tick from raw binary data
        /// </summary>
        private Tick ReadFull(byte[] response)
        {
            Tick tick = new Tick();
            tick.Mode = Constants.MODE_FULL;

            int SubscriptionMode = response[0];
            tick.subscription_mode = Convert.ToUInt16(SubscriptionMode);
           // Console.WriteLine("SubscriptionMode : {0}", SubscriptionMode);

            int ExchangeType = response[1];
            tick.exchange_type = Convert.ToUInt16(ExchangeType);
            //Console.WriteLine("ExchangeType : {0}", ExchangeType);

            var token = Encoding.UTF8.GetString(response.Skip(2).Take(25).ToArray());
            tick.token = Encoding.UTF8.GetString(response.Skip(2).Take(25).ToArray());
            //Console.WriteLine("Token Is  : {0}", token);

            var sequenceNumber = BitConverter.ToInt64(response.Skip(27).Take(8).ToArray(), 0);
            tick.sequence_number = BitConverter.ToInt64(response.Skip(27).Take(8).ToArray(), 0);
           // Console.WriteLine("Sequence Number : {0}", sequenceNumber);

            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            TimeZoneInfo istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            var epocSeconds = BitConverter.ToInt64(response.Skip(35).Take(8).ToArray(), 0);
            var currentDateTimeData = TimeZoneInfo.ConvertTimeFromUtc(epoch.AddMilliseconds(epocSeconds), istZone);
           // Console.WriteLine("Exchange Timestamp : {0}", currentDateTimeData.ToString("dd-MMM-yyyy H:mm:ss"));
            tick.ExchangeTimestam = TimeZoneInfo.ConvertTimeFromUtc(epoch.AddMilliseconds(epocSeconds), istZone);

            var ltp = BitConverter.ToInt64(response.Skip(43).Take(8).ToArray(), 0);
            tick.last_traded_price = ltp / 100.00;
           // Console.WriteLine("Last Traded Price (LTP) : {0}", ltp / 100.00);

            var ltq = BitConverter.ToInt64(response.Skip(50).Take(8).ToArray());
            tick.last_traded_quantity = ltq / 100.00;

            var atp = BitConverter.ToInt64(response.Skip(58).Take(8).ToArray());
            tick.avg_traded_price = atp / 100.00;

            var vt = BitConverter.ToInt64(response.Skip(66).Take(8).ToArray());
            tick.vol_traded = vt / 100.00;

            var tvq = BitConverter.ToInt64(response.Skip(74).Take(8).ToArray());
            tick.total_buy_quantity = BitConverter.ToInt64(response.Skip(74).Take(8).ToArray());
            // tick.total_buy_quantity = tvq / 100.00;

            var tsq = BitConverter.ToInt64(response.Skip(82).Take(8).ToArray());
            tick.total_sell_quantity = BitConverter.ToInt64(response.Skip(82).Take(8).ToArray());

            var opd = BitConverter.ToInt64(response.Skip(90).Take(8).ToArray());
            tick.open_price_day = opd / 100.00;

            var hpd = BitConverter.ToInt64(response.Skip(98).Take(8).ToArray());
            tick.high_price_day = hpd / 100.00;

            var lpd = BitConverter.ToInt64(response.Skip(106).Take(8).ToArray());
            tick.low_price_day = lpd / 100.00;

            var cp = BitConverter.ToInt64(response.Skip(114).Take(8).ToArray());
            tick.close_price = cp / 100.00;

          
            var epocSeconds_sq = BitConverter.ToInt64(response.Skip(114).Take(8).ToArray(), 0);
            var currentDateTimeData_sq = TimeZoneInfo.ConvertTimeFromUtc(epoch.AddMilliseconds(epocSeconds_sq), istZone);
            tick.last_traded_timestamp = TimeZoneInfo.ConvertTimeFromUtc(epoch.AddMilliseconds(epocSeconds_sq), istZone);

            tick.open_interest = BitConverter.ToInt64(response.Skip(130).Take(8).ToArray());
            tick.open_interest_change = BitConverter.ToDouble(response.Skip(138).Take(8).ToArray());
            tick.upper_circuit = BitConverter.ToInt16(response.Skip(346).Take(8).ToArray());
            tick.lower_circuit = BitConverter.ToInt16(response.Skip(354).Take(8).ToArray());
            tick.fiftytwo_week_high = BitConverter.ToInt16(response.Skip(362).Take(8).ToArray());
            tick.fiftytwo_week_low = BitConverter.ToInt16(response.Skip(370).Take(8).ToArray());
           // tick.best_five_data = new List<BestFive>();
            return tick;
        }


        private void _onData(byte[] Data, int Count, string MessageType)
        {
            _timerTick = _interval;
            if (MessageType == "Binary")
            {

                int offset = 0;
                ushort count = ReadShort(Data, ref offset);

                var sub_mod = Data.Skip(0).Take(1).ToArray();
                Tick tick = new Tick();
                if (sub_mod[0] == 1) 
                    tick = ReadLTP(Data);
                else if (sub_mod[0] == 2) 
                    tick = ReadQuote(Data);
                else if (sub_mod[0] == 3)                           
                    tick = ReadFull(Data);
                if (count > 0)
                {
                    OnTick(tick);
                }               
            }
            else if (MessageType == "Text")
            {
                string message = Encoding.UTF8.GetString(Data.Take(Count).ToArray());
                if (_debug) Console.WriteLine("WebSocket Message: " + message);
            }
            else if (MessageType == "Close")
            {
                Close();
            }

        }

        private void _onTimerTick(object sender, System.Timers.ElapsedEventArgs e)
        {
            // For each timer tick count is reduced. If count goes below 0 then reconnection is triggered.
            _timerTick--;
            if (_timerTick < 0)
            {
                _timer.Stop();
                if (_isReconnect)
                    Reconnect();
            }
            if (_debug) Console.WriteLine(_timerTick);
        }

        private void _onConnect()
        {
            // Reset timer and retry counts and resubscribe to tokens.
            _retryCount = 0;
            _timerTick = _interval;
            _timer.Start();
            if (_subscribedTokens.Count > 0)
                ReSubscribe();
            OnConnect?.Invoke();
        }

        /// <summary>
        /// Tells whether ticker is connected to server not.
        /// </summary>
        public bool IsConnected
        {
            get { return _ws.IsConnected(); }
        }

        /// <summary>
        /// Start a WebSocket connection
        /// </summary>
        public void Connect()
        {
            _timerTick = _interval;
            _timer.Start();
            if (!IsConnected)
            {
                _ws.Connect(_socketUrl, new Dictionary<string,string>() { ["Authorization"] = _accessToken, ["x-api-key"] = _apiKey, ["x-client-code"] = _clientcode, ["x-feed-token"] = _feedtoken });      
                
            }
        }

        public void Send(string msg)
        {
            if (IsConnected)
            {
                _ws.Send(msg);
            }
        }
       // wirtten by me

        /// <summary>
        /// Close a WebSocket connection
        /// </summary>
        public void Close()
        {
            _timer.Stop();
            _ws.Close();
        }

        /// <summary>
        /// Reconnect WebSocket connection in case of failures
        /// </summary>
        private void Reconnect()
        {
            if (IsConnected)
                _ws.Close(true);

            if (_retryCount > _retries)
            {
                _ws.Close(true);
                DisableReconnect();
                OnNoReconnect?.Invoke();
            }
            else
            {
                OnReconnect?.Invoke();
                _retryCount += 1;
                _ws.Close(true);
                Connect();
                _timerTick = (int)Math.Min(Math.Pow(2, _retryCount) * _interval, 60);
                if (_debug) Console.WriteLine("New interval " + _timerTick);
                _timer.Start();
            }
        }

        /// <summary>
        /// Subscribe to a list of instrument_tokens.
        /// </summary>
        /// <param name="Tokens">List of instrument instrument_tokens to subscribe</param>
        public void Subscribe(UInt32[] Tokens)
        {
            if (Tokens.Length == 0) return;

            string msg = "{\"a\":\"subscribe\",\"v\":[" + String.Join(",", Tokens) + "]}";
            if (_debug) Console.WriteLine(msg.Length);

            if (IsConnected)
                _ws.Send(msg);

            

            foreach (UInt32 token in Tokens)
                if (!_subscribedTokens.ContainsKey(token))
                    _subscribedTokens.Add(token, "quote");
        }

        /// <summary>
        /// Unsubscribe the given list of instrument_tokens.
        /// </summary>
        /// <param name="Tokens">List of instrument instrument_tokens to unsubscribe</param>
        public void UnSubscribe(UInt32[] Tokens)
        {
            if (Tokens.Length == 0) return;

            string msg = "{\"a\":\"unsubscribe\",\"v\":[" + String.Join(",", Tokens) + "]}";
            if (_debug) Console.WriteLine(msg);

            if (IsConnected)
                _ws.Send(msg);
            foreach (UInt32 token in Tokens)
                if (_subscribedTokens.ContainsKey(token))
                    _subscribedTokens.Remove(token);
        }

        
        public void SetMode(UInt32[] Tokens, string Mode)
        {
            if (Tokens.Length == 0) return;

            string msg = "{\r\n     \"correlationID\": \"abcde12345\",\r\n     \"action\": 1,\r\n     \"params\": {\r\n          \"mode\": 3,\r\n          \"tokenList\": [\r\n               {\r\n                    \"exchangeType\": 1,\r\n                    \"tokens\": [\r\n                         \"1232\"\r\n                    ]\r\n               }\r\n          ]\r\n     }\r\n}";
                
            //string msg1= "{\r\n  " +
            //    "\"correlationID\": \"abcde12345\",\r\n     " +
            //    "\"action\": 1,\r\n     \"params\": {\r\n         " +
            //    " \"mode\": " + Mode + ",\r\n  " +
            //    " \"tokenList\": [\r\n {\r\n" +
            //    " \"exchangeType\": 1,\r\n" +
            //    " \"tokens\": [\r\n \"" + Tokens + "\"\r\n ]\r\n }\r\n ]\r\n }\r\n}";
            
            if (_debug) Console.WriteLine(msg);

            if (IsConnected)
                _ws.Send(msg); 
            
        }

        /// <summary>
        /// Resubscribe to all currently subscribed tokens. Used to restore all the subscribed tokens after successful reconnection.
        /// </summary>
        public void ReSubscribe()
        {
            if (_debug) Console.WriteLine("Resubscribing");
            UInt32[] all_tokens = _subscribedTokens.Keys.ToArray();

            UInt32[] ltp_tokens = all_tokens.Where(key => _subscribedTokens[key] == "ltp").ToArray();
            UInt32[] quote_tokens = all_tokens.Where(key => _subscribedTokens[key] == "quote").ToArray();
            UInt32[] full_tokens = all_tokens.Where(key => _subscribedTokens[key] == "full").ToArray();

            UnSubscribe(all_tokens);
            Subscribe(all_tokens);

            //SetMode(ltp_tokens, "ltp");
            //SetMode(quote_tokens, "quote");
            //SetMode(full_tokens, "full");
        }

        /// <summary>
        /// Enable WebSocket autreconnect in case of network failure/disconnection.
        /// </summary>
        /// <param name="Interval">Interval between auto reconnection attemptes. `onReconnect` callback is triggered when reconnection is attempted.</param>
        /// <param name="Retries">Maximum number reconnection attempts. Defaults to 50 attempts. `onNoReconnect` callback is triggered when number of retries exceeds this value.</param>
        public void EnableReconnect(int Interval = 5, int Retries = 50)
        {
            _isReconnect = true;
            _interval = Math.Max(Interval, 5);
            _retries = Retries;

            _timerTick = _interval;
            if (IsConnected)
                _timer.Start();
        }

        /// <summary>
        /// Disable WebSocket autreconnect.
        /// </summary>
        public void DisableReconnect()
        {
            _isReconnect = false;
            if (IsConnected)
                _timer.Stop();
            _timerTick = _interval;
        }
    }
}
