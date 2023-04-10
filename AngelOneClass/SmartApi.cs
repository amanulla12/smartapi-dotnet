using System;
using System.Web;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace AngelOneClass
{  
    public class SmartApi
    {
        protected string USER = "USER", SourceID = "WEB", PrivateKey = "";
        static string ClientPublicIP = "", ClientLocalIP = "", MACAddress = "";

        protected string APIURL = "https://apiconnect.angelbroking.com";

        AngelToken Token { get; set; }

        /*Constructors*/
        public SmartApi(string _PrivateKey)
        {
            PrivateKey = _PrivateKey;

            ClientPublicIP = Helpers.GetPublicIPAddress();
            if (ClientPublicIP == "")
                ClientPublicIP = Helpers.GetPublicIPAddress();

            if (ClientPublicIP == "")
                ClientPublicIP = "106.193.147.98";

            ClientLocalIP = Helpers.GetLocalIPAddress();

            if (ClientLocalIP == "")
                ClientLocalIP = "127.0.0.1";

            if (Helpers.GetMacAddress() != null)
                MACAddress = Helpers.GetMacAddress().ToString();
            else
                MACAddress = "fe80::216e:6507:4b90:3719";
        }
        public SmartApi(string _PrivateKey, string _jwtToken = "", string _refreshToken = "")
        {
            PrivateKey = _PrivateKey;

            this.Token = new AngelToken();
            this.Token.jwtToken = _jwtToken;
            this.Token.refreshToken = _refreshToken;
            this.Token.feedToken = "";

            ClientPublicIP = Helpers.GetPublicIPAddress();
            if (ClientPublicIP == "")
                ClientPublicIP = Helpers.GetPublicIPAddress();

            if (ClientPublicIP == "")
                ClientPublicIP = "106.193.147.98";

            ClientLocalIP = Helpers.GetLocalIPAddress();

            if (ClientLocalIP == "")
                ClientLocalIP = "127.0.0.1";

            if (Helpers.GetMacAddress() != null)
                MACAddress = Helpers.GetMacAddress().ToString();
            else
                MACAddress = "fe80::216e:6507:4b90:3719";
        }

        /* Makes a POST request */
        private string POSTWebRequest(AngelToken agr, string URL, string Data)
        {
            try
            {
                //ServicePointManager.SecurityProtocol = (SecurityProtocolType)48 | (SecurityProtocolType)192 | (SecurityProtocolType)768 | (SecurityProtocolType)3072;
                HttpWebRequest httpWebRequest = null;
                httpWebRequest = (HttpWebRequest)WebRequest.Create(URL);
                if (agr != null)
                    httpWebRequest.Headers.Add("Authorization", "Bearer " + agr.jwtToken);
                httpWebRequest.Headers.Add("X-Content-Type-Options", "nosniff");
                httpWebRequest.Headers.Add("X-UserType", USER);
                httpWebRequest.Headers.Add("X-SourceID", SourceID);
                httpWebRequest.Headers.Add("X-ClientLocalIP", ClientLocalIP);
                httpWebRequest.Headers.Add("X-ClientPublicIP", ClientPublicIP);
                httpWebRequest.Headers.Add("X-MACAddress", MACAddress);
                httpWebRequest.Headers.Add("X-PrivateKey", PrivateKey);
                httpWebRequest.Method = "POST";
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Accept = "application/json";

                byte[] byteArray = Encoding.UTF8.GetBytes(Data);
                httpWebRequest.ContentLength = byteArray.Length;
                string Json = "";

                Stream dataStream = httpWebRequest.GetRequestStream();
                // Write the data to the request stream.
                dataStream.Write(byteArray, 0, byteArray.Length);
                // Close the Stream object.
                dataStream.Close();

                WebResponse response = httpWebRequest.GetResponse();
                // Display the status.
                //Console.WriteLine(((HttpWebResponse)response).StatusDescription);

                // Get the stream containing content returned by the server.
                // The using block ensures the stream is automatically closed.
                using (dataStream = response.GetResponseStream())
                {
                    // Open the stream using a StreamReader for easy access.
                    StreamReader reader = new StreamReader(dataStream);
                    // Read the content.
                    Json = reader.ReadToEnd();
                }
                return Json;
            }
            catch (Exception ex)
            {
                return "PostError:" + ex.Message;
            }
        }

        /* Validate Token data internally */
        private bool ValidateToken(AngelToken token)
        {
            bool result = false;
            if (token != null)
            {
                if (token.jwtToken != "" && token.refreshToken != "")
                {
                    result = true;
                }
            }
            else
                result = false;

            return result;
        }

        /*User Calls*/
        public OutputBaseClass GenerateSession(string clientcode, string password,string totp)
        {
            OutputBaseClass res = new OutputBaseClass();
            res.status = true;
            res.http_code = "200";
            try
            {
                AngelTokenResponse agr = new AngelTokenResponse();

                string URL = APIURL + "/rest/auth/angelbroking/user/v1/loginByPassword";

                string PostData = "{\"clientcode\":\"" + clientcode + "\",\"password\":\"" + password + "\",\"totp\":\""+ totp +"\"}";

                string json = POSTWebRequest(null, URL, PostData);
                if (!json.Contains("PostError:"))
                {
                    agr = JsonConvert.DeserializeObject<AngelTokenResponse>(json);
                    res.TokenResponse = agr.data;
                    res.status = agr.status;
                    res.http_error = agr.message;
                    res.http_code = agr.errorcode;
                    this.Token = agr.data;
                }
                else
                {
                    res.status = false;
                    res.http_code = "404";
                    res.http_error = json.Replace("PostError:", "");
                }
            }
            catch (Exception ex)
            {
                res.status = false;
                res.http_code = "404";
                res.http_error = ex.Message;
            }
            return res;
        }
        public OutputBaseClass GenerateToken()
        {
            OutputBaseClass res = new OutputBaseClass();
            res.status = true;
            res.http_code = "200";
            AngelTokenResponse restoken = new AngelTokenResponse();
            try
            {
                AngelToken Token = this.Token;
                if (Token != null)
                {
                    if (ValidateToken(Token))
                    {
                        string URL = APIURL + "/rest/auth/angelbroking/jwt/v1/generateTokens";

                        string PostData = "{\"refreshToken\":\"" + Token.refreshToken + "\"}";

                        string json = POSTWebRequest(Token, URL, PostData);
                        if (!json.Contains("PostError:"))
                        {
                            restoken = JsonConvert.DeserializeObject<AngelTokenResponse>(json);
                            res.TokenResponse = restoken.data;
                            res.status = restoken.status;
                            res.http_error = restoken.message;
                            res.http_code = restoken.errorcode;
                            this.Token = restoken.data;
                        }
                        else
                        {
                            res.status = false;
                            res.http_code = "404";
                            res.http_error = json.Replace("PostError:", "");
                        }
                    }
                    else
                    {
                        res.status = false;
                        res.http_code = "404";
                        res.http_error = "The token is invalid";
                    }
                }
                else
                {
                    res.status = false;
                    res.http_code = "404";
                    res.http_error = "The token is invalid";
                }
            }
            catch (Exception ex)
            {
                res.status = false;
                res.http_code = "404";
                res.http_error = ex.Message;
            }
            return res;
        }


        public OutputBaseClass PlaceOrder(OrderInfo order)
        {
            OutputBaseClass res = new OutputBaseClass();
            res.status = true;
            res.http_code = "200";
            try
            {
                AngelToken Token = this.Token;
                if (Token != null)
                {
                    if (ValidateToken(Token))
                    {
                        string URL = APIURL + "/rest/secure/angelbroking/order/v1/placeOrder";

                        if (order.triggerprice == null || order.triggerprice == "")
                            order.triggerprice = "0";
                        if (order.squareoff == null || order.squareoff == "")
                            order.squareoff = "0";
                        if (order.stoploss == null || order.stoploss == "")
                            order.stoploss = "0";
                        if (order.trailingStopLoss == null || order.trailingStopLoss == "")
                            order.trailingStopLoss = "0";
                        if (order.disclosedquantity == null || order.disclosedquantity == "")
                            order.disclosedquantity = "0";
                        if (order.ordertag == null)
                            order.ordertag = "";

                        string PostData = JsonConvert.SerializeObject(order);

                        string Json = POSTWebRequest(Token, URL, PostData);
                        if (!Json.Contains("PostError:"))
                        {
                            OrderResponse pres = JsonConvert.DeserializeObject<OrderResponse>(Json);
                            res.PlaceOrderResponse = pres;
                            res.status = pres.status;
                            res.http_error = pres.message;
                            res.http_code = pres.errorcode;
                        }
                        else
                        {
                            res.status = false;
                            res.http_code = "404";
                            res.http_error = Json.Replace("PostError:", "");
                        }
                    }
                    else
                    {
                        res.status = false;
                        res.http_code = "404";
                        res.http_error = "The token is invalid";
                    }
                }
                else
                {
                    res.status = false;
                    res.http_code = "404";
                    res.http_error = "The token is invalid";
                }
            }
            catch (Exception ex)
            {
                res.status = false;
                res.http_code = "404";
                res.http_error = ex.Message;
            }
            return res;
        }


    }
}
