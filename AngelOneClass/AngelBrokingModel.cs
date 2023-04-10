using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AngelOneClass
{
    class AngelBrokingModel
    {
    }
    #region AngelBrokingModel 
    /* Output Classes*/
    public class AngelToken
    {
        public string jwtToken { get; set; }
        public string refreshToken { get; set; }
        public string feedToken { get; set; }
    }
    class AngelTokenResponse
    {
        public bool status { get; set; }
        public string message { get; set; }
        public string errorcode { get; set; }
        public AngelToken data { get; set; }
    }
    public class ProfileData
    {
        public string clientcode { get; set; }
        public string totp { get; set; }
        public string name { get; set; }
        public string email { get; set; }
        public string mobileno { get; set; }
        public List<string> exchanges { get; set; }
        public List<string> products { get; set; }
        public string lastlogintime { get; set; }
        public string brokerid { get; set; }
    }
     
    public class OutputBaseClass
    {
        public bool status { get; set; }
        public string http_code { get; set; }
        public string http_error { get; set; }
        public AngelToken TokenResponse { get; set; }

        public OrderResponse PlaceOrderResponse { get; set; }

    }
    public class OrderData
    {
        public string script { get; set; }
        public long orderid { get; set; }
    }
    public class OrderResponse
    {
        public bool status { get; set; }
        public string message { get; set; }
        public string errorcode { get; set; }
        public OrderData data { get; set; }
    }

    /* Input Classes*/
    public class OrderInfo
    {
        public string orderid { get; set; }
        public string variety { get; set; }
        public string tradingsymbol { get; set; }
        public string symboltoken { get; set; }
        public string transactiontype { get; set; }
        public string exchange { get; set; }
        public string ordertype { get; set; }
        public string producttype { get; set; }
        public string duration { get; set; }
        public string price { get; set; }
        public string squareoff { get; set; }
        public string stoploss { get; set; }
        public string quantity { get; set; }
        public string triggerprice { get; set; }
        public string trailingStopLoss { get; set; }
        public string disclosedquantity { get; set; }
        public string ordertag { get; set; }
    }

    #endregion
}
