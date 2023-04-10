using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Threading;
using System.Net;
using System.IO;
using System.Security.Policy;
using System.Xml.Linq;
using System.Reflection.PortableExecutable;
using Microsoft.VisualBasic.FileIO;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using Newtonsoft.Json;
using System.Buffers.Text;
using System.Collections;
using static System.Net.Mime.MediaTypeNames;
using System.Runtime.InteropServices;
using System.IO.Compression;
using System.Text.Json;

namespace AngelOneClass
{
    // Delegates for events
    public delegate void OnConnectHandler();
    public delegate void OnCloseHandler();
    public delegate void OnErrorHandler(string Message);
    public delegate void OnDataHandler(byte[] Data, int Count, string MessageType);


    public class WebSocket : IWebSocket
    {
        ClientWebSocket _ws;
        string _url;
        int _bufferLength; // Length of buffer to keep binary chunk

        // Events that can be subscribed
        public event OnConnectHandler OnConnect;
        public event OnCloseHandler OnClose;
        public event OnDataHandler OnData;
        public event OnErrorHandler OnError;

        public event EventHandler<MessageEventArgs> MessageReceived;

        public WebSocket(int BufferLength = 2000000)
        {
            _bufferLength = BufferLength;

        }

        public bool IsConnected()
        {
            if (_ws is null)
                return false;

            return _ws.State == WebSocketState.Open;
        }

       
        public void Connect(string Url, Dictionary<string, string> headers = null)
        {
            _url = Url;
            try
            {
                // Initialize ClientWebSocket instance and connect with Url
                _ws = new ClientWebSocket();
                if (headers != null)
                {
                    foreach (string key in headers.Keys)
                    {
                       _ws.Options.SetRequestHeader(key, headers[key]);
                    }
                }
              
                _ws.ConnectAsync(new Uri(_url), CancellationToken.None).Wait();
                
                OnConnect?.Invoke();
            }
            catch (Exception e)
            {
                OnError?.Invoke("Error while recieving data. Message:  " + e.Message);
            }
        }

        public async void Send(string Message)
        {

            byte[] buffer = new byte[_bufferLength];
            if (_ws.State == WebSocketState.Open)
                try
                {
                   _ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(Message)), WebSocketMessageType.Text, true, CancellationToken.None).Wait();

                    if (IsConnected())
                    {
                       await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    }              

                    int count = buffer.Count();
                    var msg = buffer.GetType();

                    OnData?.Invoke(buffer, count, "Binary");

                }
                catch (Exception e)
                {
                    OnError?.Invoke("Error while sending data. Message:  " + e.Message);
                }
        }

        /// <summary>
        /// Close the WebSocket connection
        /// </summary>
        /// <param name="Abort">If true WebSocket will not send 'Close' signal to server. Used when connection is disconnected due to netork issues.</param>
        public void Close(bool Abort = false)
        {
            if (_ws.State == WebSocketState.Open)
            {
                try
                {
                    if (Abort)
                        _ws.Abort();
                    else
                    {
                        _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).Wait();
                        OnClose?.Invoke();
                    }
                }
                catch (Exception e)
                {
                    OnError?.Invoke("Error while closing connection. Message: " + e.Message);
                }
            }
        }


    }
}
