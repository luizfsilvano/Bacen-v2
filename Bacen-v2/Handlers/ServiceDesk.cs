using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium.Chrome;
using System.Globalization;
using System.Text;

namespace Bacen_v2.Handlers
{
    public class ServiceDesk
    {
        private readonly HttpClient _httpClient;
        private readonly dynamic _config;
        private string _baseUrl;

        public ServiceDesk(dynamic config, string jsessionId, string gocSession)
        {
            _config = config;
            _httpClient = new HttpClient();
            _baseUrl = _config.Environment == "Sandbox"
                ? _config.ServiceDesk.SandboxUrl
                : _config.ServiceDesk.ProductionUrl;

            // Adicionar o cookie JSESSIONID no header
            _httpClient.DefaultRequestHeaders.Add("Cookie", $"JSESSIONID={jsessionId}; __goc_session__={gocSession}");
        }

        public async Task<List<JObject>> GetChamadosEncaminhadoN2Async()
        {
            try
            {
                // Fazer a requisição GET para obter os chamados
                var url = $"{_baseUrl}/api/v1/sr?assigned_group=36";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Erro ao buscar chamados: {response.StatusCode}");
                    return new List<JObject>();
                }

                // Ler e parsear o JSON de resposta
                var responseContent = await response.Content.ReadAsStringAsync();
                var chamados = JArray.Parse(responseContent);

                // Filtrar chamados com status "ENCAMINHADO N2 ATENDIMENTO"
                var chamadosFiltrados = chamados
                    .Where(chamado =>
                    {
                        // Acessar a lista "info"
                        var infoArray = chamado["info"] as JArray;
                        if (infoArray == null)
                            return false;

                        // Verificar se existe um item "status" com o "valueCaption" desejado
                        return infoArray.Any(info =>
                            info["key"]?.ToString() == "status" &&
                            info["valueCaption"]?.ToString() == "ENCAMINHADO N2 ATENDIMENTO");
                    })
                    .Cast<JObject>()
                    .ToList();

                return chamadosFiltrados;
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Erro ao processar chamados: {ex.Message}");
                return new List<JObject>();
            }
        }

        public async Task<JObject> GetDetalhesChamadoAsync(int chamadoId)
        {
            try
            {
                // Construir a URL para o chamado específico
                var url = $"{_baseUrl}/api/v1/sr/{chamadoId}";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Erro ao buscar detalhes do chamado {chamadoId}: {response.StatusCode}");
                    return null;
                }

                // Ler e parsear o JSON de resposta
                var responseContent = await response.Content.ReadAsStringAsync();
                var chamadoDetalhes = JObject.Parse(responseContent);

                // Exemplo: Extraindo algumas informações úteis
                var detalhesUteis = new JObject
                {
                    ["id"] = chamadoDetalhes["id"],
                    ["titulo"] = chamadoDetalhes["info"]?.FirstOrDefault(info => info["key"]?.ToString() == "title")?["valueCaption"],
                    ["descricao"] = chamadoDetalhes["info"]?.FirstOrDefault(info => info["key"]?.ToString() == "description")?["valueCaption"],
                    ["status"] = chamadoDetalhes["info"]?.FirstOrDefault(info => info["key"]?.ToString() == "status")?["valueCaption"],
                    ["de"] = chamadoDetalhes["info"]?.FirstOrDefault(info => info["key"]?.ToString() == "CustomColumn155sr")?["valueCaption"],
                    ["usuarioSolicitante"] = chamadoDetalhes["info"]?.FirstOrDefault(info => info["key"]?.ToString() == "request_user")?["valueCaption"],
                    ["prioridade"] = chamadoDetalhes["info"]?.FirstOrDefault(info => info["key"]?.ToString() == "priority")?["valueCaption"],
                    ["categoria"] = chamadoDetalhes["info"]?.FirstOrDefault(info => info["key"]?.ToString() == "problem_type")?["valueCaption"],
                    ["sla"] = chamadoDetalhes["info"]?.FirstOrDefault(info => info["key"]?.ToString() == "due_date")?["valueCaption"],
                    ["notas"] = chamadoDetalhes["info"]?.FirstOrDefault(info => info["key"]?.ToString() == "notes")?["value"]
                };

                // Processar e formatar CustomColumns
                var customColumns = chamadoDetalhes["info"]?
                    .Where(info => info["key"]?.ToString().StartsWith("CustomColumn") == true);

                if (customColumns != null)
                {
                    var customColumnsStringBuilder = new StringBuilder();

                    foreach (var column in customColumns)
                    {
                        var keyCaption = column["keyCaption"]?.ToString() ?? "Sem Nome";
                        var valueCaption = column["valueCaption"]?.ToString() ?? "Não preenchido";
                        var valores = new[] { "Instituição Requerente", "Config SLA Data Prazo Análise", "Contador N2", "Etapa(nome e versão api)", "Status SLA", "SLA Horas Úteis", "Usuário Solicitante:", "Equipe solucionadora", "Especialista atribuído:", "Destinatário", "Contador Requisitante", "Tipo do Chamado", "Contador Prazo SLA", "VIP" };

                        if (valueCaption != "" && IsValido(keyCaption, valores))
                        {
                            customColumnsStringBuilder.AppendLine($"{keyCaption}: {valueCaption}\n");
                        }

                    }

                    // Adicionar CustomColumns formatadas ao detalhesUteis
                    detalhesUteis["customColumns"] = customColumnsStringBuilder.ToString();
                }
                else
                {
                    detalhesUteis["customColumns"] = "Nenhuma informação adicional encontrada.";
                }

                string ToCamelCase(string text)
                {
                    if (string.IsNullOrEmpty(text))
                        return text;

                    // Remove espaços, capitaliza as palavras e mantém a primeira em minúsculo
                    var words = text.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                    var camelCase = string.Concat(
                        words[0].ToLower(CultureInfo.InvariantCulture),
                        string.Join("", words.Skip(1).Select(word => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(word.ToLower())))
                    );

                    return camelCase;
                }

                return detalhesUteis;
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Erro ao processar detalhes do chamado {chamadoId}: {ex.Message}");
                return null;
            }
        }

        private static bool IsValido(string keyCaption, string[] valores)
        {
            return !Array.Exists(valores, s => s == keyCaption);
        }

        public async Task<List<JObject>> GetAnexosDoChamadoAsync(int chamadoId)
        {
            try
            {
                // URL para obter anexos
                var url = $"{_baseUrl}/api/v1/sr/{chamadoId}?fields=attachments";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Erro ao buscar anexos do chamado {chamadoId}: {response.StatusCode}");
                    return new List<JObject>();
                }

                // Ler e parsear o JSON de resposta
                var responseContent = await response.Content.ReadAsStringAsync();
                var chamadoDetalhes = JObject.Parse(responseContent);

                // Obter a lista de anexos
                var anexos = chamadoDetalhes["info"]?
                    .FirstOrDefault(info => info["key"]?.ToString() == "attachments")?["value"] as JArray;

                return anexos?.Cast<JObject>().ToList() ?? new List<JObject>();
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Erro ao buscar anexos do chamado {chamadoId}: {ex.Message}");
                return new List<JObject>();
            }
        }

        public async Task<string?> BaixarAnexoComSeleniumAsync(int chamadoId, string fileId, string fileName, string jsessionId, string gocSession, ChromeDriver driver)
        {
            try
            {
                var url = $"{_baseUrl}/getFile?table=service_req&id={chamadoId}&getFile={fileId}";

                // Navegar para a base URL para definir o domínio dos cookies
                driver.Navigate().GoToUrl(_baseUrl);

                // Adicionar cookies de autenticação
                driver.Manage().Cookies.AddCookie(new OpenQA.Selenium.Cookie("JSESSIONID", jsessionId, "/", DateTime.Now.AddDays(1)));
                driver.Manage().Cookies.AddCookie(new OpenQA.Selenium.Cookie("__goc_session__", gocSession, "/", DateTime.Now.AddDays(1)));

                // Navegar para o URL do arquivo
                Console.WriteLine($"Navegando para o URL do anexo: {url}");
                driver.Navigate().GoToUrl(url);

                // Esperar por 15 segundos
                Console.WriteLine("Esperando por 15 segundos...");
                await Task.Delay(15000);

                Console.WriteLine("Tempo de espera concluído.");

                // Diretório de downloads
                var downloadDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                var originalFilePath = Path.Combine(downloadDirectory, fileName);


                // Verificar se o arquivo foi baixado
                if (!File.Exists(originalFilePath))
                {
                    Console.WriteLine("O arquivo não foi baixado.");
                    return null;
                }

                // Adicionar o chamadoId ao final do nome do arquivo
                var fileNameWithId = $"{Path.GetFileNameWithoutExtension(fileName)} #{chamadoId}{Path.GetExtension(fileName)}";
                var newFilePath = Path.Combine(downloadDirectory, fileNameWithId);

                // Renomear o arquivo
                File.Move(originalFilePath, newFilePath);

                Console.WriteLine($"Anexo {fileNameWithId} baixado com sucesso em: {newFilePath}");

                return newFilePath;
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Erro ao baixar o anexo {fileName}: {ex.Message}");
                return null;
            }
        }

        // Método auxiliar para obter valores de cookies
        private string GetCookieValue(string cookieName)
        {
            var cookies = _httpClient.DefaultRequestHeaders.GetValues("Cookie").FirstOrDefault();
            if (cookies == null) return string.Empty;

            var cookieParts = cookies.Split(';');
            foreach (var part in cookieParts)
            {
                var keyValue = part.Split('=');
                if (keyValue.Length == 2 && keyValue[0].Trim() == cookieName)
                {
                    return keyValue[1];
                }
            }

            return string.Empty;
        }


        // Método para poder verificar se o chamado já está aberto no topdesk por meio das notas ("Protocolo Interno do Sicoob:")
        public bool VerificarChamadoAberto(string notas)
        {
            if (notas.Contains("Protocolo Interno do Sicoob:", StringComparison.OrdinalIgnoreCase) || notas.Contains("Protocolo Interno no Sicoob:", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Chamado já processado! Número do protocolo encontrado no topdesk.");
                return true;
            }
            if (notas.Contains("Identificador único do chamado:", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Chamado já processado! Número do protocolo não encontrado no topdesk.");
                return true;
            }
            if (notas.Contains("Identificador único do chamado:", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Chamado já processado! Número do protocolo não encontrado no topdesk.");
                return true;
            }
            return false;
        }

        // Adicionar número do protocolo às notas após abertura do chamado
        public async Task AdicionarNumeroProtocoloAsync(string idServiceDesk, string numeroProtocolo, string sessionID, string gocSession)
        {
            try
            {
                // Mensagem da nota
                string mensagemNota = string.Empty;

                // Verificar se o número do protocolo foi capturado
                if (string.IsNullOrEmpty(numeroProtocolo))
                {
                    Console.WriteLine($"Número do protocolo não encontrado para o chamado {idServiceDesk}. Nenhuma nota será adicionada.");
                    return;
                }

                // Verifica se é um número de protocolo, ou um identificador único com regex
                var regexIdentificador = new System.Text.RegularExpressions.Regex(@"A\d{4}-\d{6}");
                var match = regexIdentificador.Match(numeroProtocolo);
                if (match.Success)
                {
                    Console.WriteLine("Identificador único encontrado. Mudando mensagem da nota.");
                    mensagemNota = "Número do protocolo não encontrado. Identificador único do chamado:";
                }
                else
                {
                    Console.WriteLine("Número de protocolo encontrado. Mensagem padrão.");
                    mensagemNota = "Protocolo interno do Sicoob:";
                }

                // URL do chamado
                var url = $"{_baseUrl}/api/v1/sr/{idServiceDesk}";

                // Corpo da requisição
                var body = new
                {
                    id = idServiceDesk.ToString(),
                    info = new[]
                    {
                new
                {
                    key = "notes",
                    value = new[]
                    {
                        new
                        {
                            userName = "Marco Aurélio da Silva Martins",
                            text = $@"{mensagemNota} {numeroProtocolo}

Protocolo gerado automaticamente."
                        }
                    }
                }
            }
                };

                // Preparar o conteúdo da requisição
                var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");

                // Adicionar os cookies nos cabeçalhos
                _httpClient.DefaultRequestHeaders.Remove("Cookie");
                _httpClient.DefaultRequestHeaders.Add("Cookie", $"JSESSIONID={sessionID}; __goc_session__={gocSession}");


                // Enviar a requisição PATCH
                var response = await _httpClient.PutAsync(url, content);

                // Verificar a resposta
                if (!response.IsSuccessStatusCode)
                {
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Erro ao adicionar número do protocolo: {response.StatusCode}");
                    Console.WriteLine($"Detalhes: {errorDetails}");
                }
                else
                {
                    Console.WriteLine($"Número do protocolo {numeroProtocolo} adicionado com sucesso ao chamado {idServiceDesk}.");
                }
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Erro ao adicionar número do protocolo: {ex.Message}");
            }
        }


        // Mudar status, especialista atribuído e equipe solucionadora após abertura do chamado
        public async Task AtualizarChamadoAsync(string idServiceDesk, string sessionID, string gocSession)
        {
            try
            {
                // URL para atualização do chamado
                var url = $"{_baseUrl}/api/v1/sr/{idServiceDesk}";

                // Corpo da requisição
                var body = new
                {
                    id = idServiceDesk.ToString(),
                    info = new[]
                    {
                new
                {
                    key = "status",
                    value = "8"
                },
                new
                {
                    key = "responsibility",
                    value = "1874"
                },
                new
                {
                    key = "CustomColumn16sr",
                    value = "N2 ATENDIMENTO"
                }
            }
                };

                // Conteúdo da requisição
                var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");

                // Headers da requisição
                _httpClient.DefaultRequestHeaders.Remove("Cookie");
                _httpClient.DefaultRequestHeaders.Add("Cookie", $"JSESSIONID={sessionID}; __goc_session__={gocSession}");

                // Enviar requisição PATCH
                var response = await _httpClient.PutAsync(url, content);

                // Validar resposta
                if (!response.IsSuccessStatusCode)
                {
                    var errorDetails = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Erro ao atualizar chamado {idServiceDesk}: {response.StatusCode}");
                    Console.WriteLine($"Detalhes do erro: {errorDetails}");
                }
                else
                {
                    Console.WriteLine($"Chamado {idServiceDesk} atualizado com sucesso.");
                }
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Erro ao atualizar chamado {idServiceDesk}: {ex.Message}");
            }
        }

    }
}
