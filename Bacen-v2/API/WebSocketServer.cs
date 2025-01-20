using System.Net;
using System.Net.WebSockets;
using System.Text;

namespace Bacen_v2.API
{
    class WebSocketServer
    {
        private static bool isRunning = false; // Flag global para representar o status da automação

        public static async Task StartServer()
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:5000/ws/");
            listener.Start();
            Console.WriteLine("Servidor WebSocket iniciado em ws://localhost:5000/ws/");

            while (true)
            {
                HttpListenerContext context = await listener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    HttpListenerWebSocketContext wsContext = await context.AcceptWebSocketAsync(null);
                    Console.WriteLine("Conexão WebSocket aceita.");
                    _ = HandleConnection(wsContext.WebSocket); // Gerenciar conexão em uma task separada
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
        }

        private static async Task HandleConnection(WebSocket webSocket)
        {
            byte[] buffer = new byte[1024];
            while (webSocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                string receivedMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                Console.WriteLine($"Mensagem recebida: {receivedMessage}");

                if (receivedMessage == "status")
                {
                    // Responder com o status atual da automação
                    string responseMessage = isRunning ? "isRunning: true" : "isRunning: false";
                    byte[] responseBuffer = Encoding.UTF8.GetBytes(responseMessage);
                    await webSocket.SendAsync(new ArraySegment<byte>(responseBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
                    Console.WriteLine($"Status enviado: {responseMessage}");
                }
                else
                {
                    // Mensagem desconhecida
                    string responseMessage = "Comando desconhecido.";
                    byte[] responseBuffer = Encoding.UTF8.GetBytes(responseMessage);
                    await webSocket.SendAsync(new ArraySegment<byte>(responseBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }

            Console.WriteLine("Conexão WebSocket encerrada.");
        }

        public static void AtualizarStatus(bool status)
        {
            isRunning = status;
        }
    }
}