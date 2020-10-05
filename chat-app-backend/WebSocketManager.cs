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
        private Dictionary<WebSocket, string> webSocketUsernameAssociation = new Dictionary<WebSocket, string>();
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
                        webSocketUsernameAssociation.Add(webSocket, "username not set yet");
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
                await PerformSendingLogic(webSocket, buffer, result);

                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }

        private async Task PerformSendingLogic(WebSocket webSocket, byte[] buffer, WebSocketReceiveResult result)
        {
            Message message = CreateMessageObject(buffer, result);

            if (message.Type != MessageType.ALL_MESSAGES && message.Type != MessageType.ASSIGN_USER)
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
            else if (message.Type == MessageType.ASSIGN_USER)
            {
                webSocketUsernameAssociation[webSocket] = message.Sender;
            }
            else if (message.Type == MessageType.CLOSE)
            {
                var usernameThatHasLeft = webSocketUsernameAssociation[webSocket];
                webSockets.Remove(webSocket);
                SendUserHasDisconnectedMessage(MessageType.CLOSE, usernameThatHasLeft, "has left the chat");
            }
            else if (message.Type == MessageType.UTILITY || message.Type == MessageType.MESSAGE)
            {
                try
                {
                    await SendToAllOpenWebSockets(buffer, result);
                }
                catch (Exception e)
                {
                    Console.WriteLine(
                        $"Exception raised: '{e}'. Most likely the message does not include a '|' symbol to differentiate username and message body.");
                }
            }
        }

        private async Task SendAllMessagesToWebSocket(WebSocket webSocket)
        {
            if (conversation.Count == 0)
            {
                return;
            }

            byte[] conversationByteArray = ConvertConversationToByteArray();
            await webSocket.SendAsync(new ArraySegment<byte>(conversationByteArray, 0, conversationByteArray.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task SendToAllOpenWebSockets(byte[] buffer, WebSocketReceiveResult result)
        {
            CheckForUserDisconnect();

            for (int i = webSockets.Count - 1; i >= 0; i--)
            {
                if (webSockets[i].State == WebSocketState.Open)
                {
                    await webSockets[i].SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage, CancellationToken.None);
                }
            }
        }

        private void CheckForUserDisconnect()
        {
            for (int i = webSockets.Count - 1; i >= 0; i--)
            {
                if (webSockets[i].State != WebSocketState.Open)
                {
                    var disconnectedUsername = webSocketUsernameAssociation[webSockets[i]];
                    webSockets.RemoveAt(i);
                    SendUserHasDisconnectedMessage(MessageType.UTILITY, disconnectedUsername, "has disconnected");
                }
            }
        }

        private async void SendUserHasDisconnectedMessage(MessageType messageType, string sender, string body)
        {
            Message message = new Message()
            {
                Type = messageType,
                Sender = sender,
                Body = body
            };
            byte[] bytes = Encoding.ASCII.GetBytes(MessageToStringMapping(message));

            foreach (var webSocket in webSockets)
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    await webSocket.SendAsync(new ArraySegment<byte>(bytes, 0, bytes.Length), WebSocketMessageType.Text, true, CancellationToken.None);
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
            return keyword switch
            {
                "ALL_MESSAGES" => MessageType.ALL_MESSAGES,
                "UTILITY" => MessageType.UTILITY,
                "ASSIGN_USER" => MessageType.ASSIGN_USER,
                "CLOSE" => MessageType.CLOSE,
                _ => MessageType.MESSAGE
            };
        }

        private byte[] ConvertConversationToByteArray()
        {
            string conversationStringFormat = ConvertConversationToStringFormat();
            byte[] bytes = Encoding.ASCII.GetBytes(conversationStringFormat);
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

        private string MessageToStringMapping(Message message)
        {
            return message.Type + "|" + message.Sender + "|" + message.Body;
        }
    }
}
