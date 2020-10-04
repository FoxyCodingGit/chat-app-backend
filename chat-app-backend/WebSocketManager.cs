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
                Message message = CreateMessageObject(buffer, result);

                if (message.Type != MessageType.ALL_MESSAGES)
                {
                    conversation.Add(message);
                }

                if (message.Type == MessageType.ALL_MESSAGES)
                {
                    try
                    {
                        await SendAllMessagesToWebSocket(webSocket);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                    
                }
                else
                {
                    try
                    {
                        await SendToAllOpenWebSockets(buffer, result);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Exception raised: '{e}'. Most likely the message does not include a '|' symbol to differentiate username and message body.");
                    }
                }

                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }

        private async Task SendAllMessagesToWebSocket(WebSocket webSocket)
        {
            if (conversation.Count == 0)
            {
                return;
            }

            byte[] converstationByteArray = ConvertConversationToByteArray();
            await webSocket.SendAsync(new ArraySegment<byte>(converstationByteArray, 0, converstationByteArray.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task SendToAllOpenWebSockets(byte[] buffer, WebSocketReceiveResult result)
        {
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

        private static Message CreateMessageObject(byte[] buffer, WebSocketReceiveResult result)
        {
            char messageSplitSymbol = '|';

            byte[] byteArray = new ArraySegment<byte>(buffer, 0, result.Count).ToArray();
            string message = Encoding.Default.GetString(byteArray);
            string[] messageSplit = message.Split(messageSplitSymbol, 3);

            return new Message
            {
                Type = FindType(messageSplit[0]),
                Sender = messageSplit[1],
                Body = messageSplit[2],
            };
        }

        private static MessageType FindType(string keyword)
        {
            if (keyword == "ALL_MESSAGES")
            {
                return MessageType.ALL_MESSAGES;
            }
            if (keyword == "UTILITY")
            {
                return MessageType.UTILITY;
            }
            return MessageType.MESSAGE;
        }

        private byte[] ConvertConversationToByteArray()
        {
            string converstationStringFormat = ConvertConversationToStringFormat();
            byte[] bytes = Encoding.ASCII.GetBytes(converstationStringFormat);
            return bytes;
        }

        private string ConvertConversationToStringFormat()
        {
            string output = "";
            
            foreach (Message message in conversation)
            {
                output += message.Type + "|" + message.Sender + "|" + message.Body + "~";
            }

            output = output.Remove(output.Length - 1);
            return output;
        }
    }
}
