using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace chat_app_backend
{
    public class WebSocketManager
    {
        private List<WebSocket> webSockets = new List<WebSocket>();
        private List<Message> conversation = new List<Message>();

        public void SetupWebSocketConfiguration(IApplicationBuilder app)
        {
            app.UseWebSockets();

            app.Use(async (context, next) =>
            {
                if (context.Request.Path == "/chat")
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                        webSockets.Add(webSocket);
                        await Echo(context, webSocket);
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                    }
                }
                else
                {
                    await next();
                }
            });
        }

        private async Task Echo(HttpContext context, WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            while (!result.CloseStatus.HasValue)
            {
                try
                {
                    await SendToAllOpenWebSockets(buffer, result);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Exception raised: '{e}'. Most likely the message does not include a '|' symbol to differentiate username and message body.");
                }

                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }

        private async Task SendToAllOpenWebSockets(byte[] buffer, WebSocketReceiveResult result)
        {
            Message message = GetMessageObject(buffer, result);
            conversation.Add(message);

            for (int i = webSockets.Count - 1; i >= 0; i--)
            {
                if (webSockets[i].State == WebSocketState.Open)
                {
                    await webSockets[i].SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage, CancellationToken.None);
                }
                else
                {
                    webSockets.RemoveAt(i);
                }
            }
        }

        private static Message GetMessageObject(byte[] buffer, WebSocketReceiveResult result)
        {
            char messageSplitSymbol = '|';

            byte[] byteArray = new ArraySegment<byte>(buffer, 0, result.Count).ToArray();
            string message = Encoding.Default.GetString(byteArray);
            string[] messageSplit = message.Split(messageSplitSymbol, 2);

            return new Message
            {
                Sender = messageSplit[0],
                Body = messageSplit[1],
            };
        }
    }
}
