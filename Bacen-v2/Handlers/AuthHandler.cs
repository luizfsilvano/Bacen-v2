using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Bacen_v2.Utils;

namespace Bacen_v2.Handlers
{
    public class AuthHandler
    {
        private readonly dynamic _config;
        private readonly HttpClient _httpClient;
        private string _baseUrl;
        public string SessionId { get; private set; } = string.Empty;
        public string GocSession { get; private set; } = string.Empty;
        public int UserGroupId { get; private set; } = 0;
        public string UserName { get; private set; } = string.Empty;
        public string UserEmail { get; private set; } = string.Empty;

        public AuthHandler(dynamic config)
        {
            _config = config;
            _httpClient = new HttpClient();
            _baseUrl = _config.Environment == "Sandbox"
                ? _config.ServiceDesk.SandboxUrl
                : _config.ServiceDesk.ProductionUrl;
        }

        public async Task LoginServiceDesk()
        {
            try
            {
                var url = $"{_baseUrl}/api/v1/login?user";
                var payload = new
                {
                    user_name = _config.ServiceDesk.Username,
                    password = _config.ServiceDesk.Password
                };

                Logger.Log($"Tentando autenticar no Service Desk com a URL: {url}");
                Logger.Log($"Payload: {JsonConvert.SerializeObject(payload)}");

                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Falha ao autenticar. Detalhes: {errorDetails}");
                }

                var result = await response.Content.ReadAsStringAsync();
                var jsonResponse = JsonConvert.DeserializeObject<dynamic>(result);

                // Capturar JSESSIONID e __goc_session__
                if (response.Headers.Contains("Set-Cookie"))
                {
                    foreach (var cookie in response.Headers.GetValues("Set-Cookie"))
                    {
                        if (cookie.StartsWith("JSESSIONID"))
                        {
                            SessionId = cookie.Split(';')[0].Split('=')[1];
                        }
                        else if (cookie.Contains("__goc_session__"))
                        {
                            GocSession = cookie.Split(';')[0].Split('=')[1];
                        }
                    }
                }

                if (string.IsNullOrEmpty(SessionId))
                    throw new Exception("Falha ao capturar o JSESSIONID.");

                if (string.IsNullOrEmpty(GocSession))
                    throw new Exception("Falha ao capturar o __goc_session__.");


                // Capturar Grupo de Atendimento
                var info = jsonResponse?.user?.info as IEnumerable<dynamic>;
                if (info != null)
                {
                    var userGroupEntry = info.FirstOrDefault(entry => entry.key == "user_groups");
                    if (userGroupEntry?.value != null && userGroupEntry.value.Count > 0)
                    {
                        UserGroupId = userGroupEntry.value[0]?.id ?? 0;
                    }
                }

                if (UserGroupId == 0)
                {
                    Logger.Log("Nenhum grupo de atendimento encontrado no retorno.");
                    throw new Exception("Falha ao capturar o ID do Grupo de Atendimento.");
                }

                // Capturar Nome do Usuário
                if (info != null)
                {
                    var nameEntry = info.FirstOrDefault(entry => entry.key == "first_name");
                    if (nameEntry != null)
                    {
                        UserName = nameEntry.value?.ToString() ?? "Nome não encontrado";
                    }
                    else
                    {
                        UserName = "Nome não encontrado";
                    }
                }
                else
                {
                    UserName = "Nome não encontrado";
                }
                // Capturar E-mail do Usuário
                if (info != null)
                {
                    var emailEntry = info.FirstOrDefault(entry => entry.key == "email_address");
                    if (emailEntry != null)
                    {
                        UserEmail = emailEntry.value?.ToString() ?? "E-mail não encontrado";
                    }
                    else
                    {
                        UserEmail = "E-mail não encontrado";
                    }
                }
                else
                {
                    UserEmail = "E-mail não encontrado";
                }


                Logger.Log("Autenticação concluída com sucesso.");

            }
            catch (Exception ex)
            {
                Logger.Log($"Erro no Login: {ex.Message}");
                throw;
            }
        }
    }
}
