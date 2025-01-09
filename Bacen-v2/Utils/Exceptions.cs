using Newtonsoft.Json.Linq;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bacen_v2.Utils {
    internal static class Exceptions
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly dynamic Config = ConfigLoader.Load("Configs/appsettings.json");

        public static async Task HandleErrorAsync(Exception ex)
        {
            try
            {
                // Log do erro
                Logger.Log($"Erro capturado: {ex.Message}\n{ex.StackTrace}");

                // Enviar e-mail com detalhes do erro
                await SendErrorEmailAsync(ex);
            }
            catch (Exception emailEx)
            {
                Logger.Log($"Falha ao enviar notificação de erro: {emailEx.Message}");
            }
        }

        private static async Task SendErrorEmailAsync(Exception ex)
        {
            var apiUrl = (string)Config.Mailgun.ApiBaseUrl;
            var apiKey = (string)Config.Mailgun.ApiKey;
            var sender = (string)Config.Mailgun.SenderEmail;
            var recipients = JArray.FromObject(Config.Mailgun.Recipients);

            var subject = "Erro no Bacen_v2";
            var message = $@"
                <h2>Erro capturado no Bacen_v2</h2>
                <p><strong>Mensagem:</strong> {ex.Message}</p>
                <p><strong>Stack Trace:</strong></p>
                <pre>{ex.StackTrace}</pre>
            ";

            foreach (var recipient in recipients)
            {
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("from", sender),
                    new KeyValuePair<string, string>("to", recipient.ToString()),
                    new KeyValuePair<string, string>("subject", subject),
                    new KeyValuePair<string, string>("html", message)
                });

                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",
                        Convert.ToBase64String(Encoding.ASCII.GetBytes($"api:{apiKey}")));

                var response = await _httpClient.PostAsync(apiUrl, content);
                if (!response.IsSuccessStatusCode)
                {
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Falha ao enviar e-mail. Detalhes: {errorDetails}");
                }
            }
        }
    }
}
