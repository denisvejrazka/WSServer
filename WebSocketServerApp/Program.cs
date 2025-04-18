using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

class Program
{
    static ConcurrentBag<WebSocket> clients = new();

    static async Task Main()
    {
        HttpListener server = new();
        server.Prefixes.Add("http://+:5004/ws/");
        server.Start();
        Console.WriteLine("WebSocket server běží");

        while (true)
        {
            var context = await server.GetContextAsync();
            if (context.Request.IsWebSocketRequest)
            {
                var wsContext = await context.AcceptWebSocketAsync(null);
                var socket = wsContext.WebSocket;
                clients.Add(socket);
                _ = HandleClient(socket);
            }
            else
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
            }
        }
    }

    static async Task HandleClient(WebSocket socket)
    {
        var buffer = new byte[1024];

        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                return;
            }

            var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
            Console.WriteLine($"Přijato: {msg}");

            // Poslat zprávu všem klientům kromě odesílatele
            foreach (var client in clients)
            {
                if (client != socket && client.State == WebSocketState.Open)
                {
                    var data = Encoding.UTF8.GetBytes(msg);
                    await client.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }
    }
}