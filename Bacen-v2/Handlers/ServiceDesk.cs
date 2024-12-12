using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Globalization;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Net;

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
                var url = $"{_baseUrl}/api/v1/sr?assigned_group=36"; // Adicione o grupo correto
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
            catch (Exception ex)
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
                    ["prioridade"] = chamadoDetalhes["info"]?.FirstOrDefault(info => info["key"]?.ToString() == "priority")?["valueCaption"],
                    ["categoria"] = chamadoDetalhes["info"]?.FirstOrDefault(info => info["key"]?.ToString() == "problem_type")?["valueCaption"],
                    ["sla"] = chamadoDetalhes["info"]?.FirstOrDefault(info => info["key"]?.ToString() == "problem_type")?["valueCaption"],
                    ["notas"] = chamadoDetalhes["info"]?.FirstOrDefault(info => info["key"]?.ToString() == "notes")?["value"]
                };

                // Verificar e adicionar automaticamente todas as CustomColumns usando keyCaption
                var customColumns = chamadoDetalhes["info"]?
                    .Where(info => info["key"]?.ToString().StartsWith("CustomColumn") == true);

                if (customColumns != null)
                {
                    foreach (var column in customColumns)
                    {
                        var keyCaption = column["keyCaption"]?.ToString();
                        var value = column["valueCaption"]?.ToString();

                        if (!string.IsNullOrEmpty(keyCaption) && !string.IsNullOrEmpty(value))
                        {
                            // Formatar o keyCaption para camelCase
                            var formattedKeyCaption = ToCamelCase(keyCaption);

                            // Adicionar ao JSON de detalhes úteis
                            detalhesUteis[formattedKeyCaption] = value;
                        }
                    }
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
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar detalhes do chamado {chamadoId}: {ex.Message}");
                return null;
            }
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
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao buscar anexos do chamado {chamadoId}: {ex.Message}");
                return new List<JObject>();
            }
        }
        public async Task BaixarAnexoAsync(int chamadoId, string fileId, string fileName)
        {
            try
            {
                // Construir URL de download
                var url = $"{_baseUrl}/getFile?table=service_req&id={chamadoId}&getFile={fileId}";

                // Fazer a requisição GET
                var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    Console.WriteLine("Endpoint não suporta chamadas programáticas. Tentando baixar via navegador...");
                    await BaixarAnexoViaNavegadorAsync(chamadoId, fileId, fileName);
                    return;
                }

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Erro ao baixar anexo {fileName} do chamado {chamadoId}: {response.StatusCode}");
                    return;
                }

                // Validar tipo de conteúdo retornado
                var contentType = response.Content.Headers.ContentType?.MediaType;
                if (contentType == null || !contentType.Contains("application/pdf"))
                {
                    Console.WriteLine($"O conteúdo retornado não parece ser um PDF válido. Tipo de conteúdo: {contentType}");
                    return;
                }

                // Salvar o conteúdo em arquivo
                var filePath = Path.Combine("Data", "processed", fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)); // Criar diretórios se necessário

                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fileStream);
                }

                Console.WriteLine($"Anexo {fileName} baixado com sucesso em: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar download do anexo {fileName}: {ex.Message}");
            }
        }

        // Caso a função via requisição não funcione
        public async Task BaixarAnexoViaNavegadorAsync(int chamadoId, string fileId, string fileName)
        {
            try
            {
                var url = $"{_baseUrl}/getFile?table=service_req&id={chamadoId}&getFile={fileId}";

                // Configurar o Selenium WebDriver em modo headless
                var options = new ChromeOptions();
                var downloadDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Data", "processed");
                options.AddUserProfilePreference("download.default_directory", downloadDirectory);
                options.AddUserProfilePreference("download.prompt_for_download", false);
                options.AddUserProfilePreference("plugins.always_open_pdf_externally", true); // Baixar PDFs diretamente

                // Ativar o modo headless
                //options.AddArgument("--headless"); // Executar sem interface gráfica
                options.AddArgument("--disable-gpu"); // Melhor performance no modo headless
                options.AddArgument("--no-sandbox"); // Evitar problemas de sandbox em alguns sistemas
                options.AddArgument("--disable-dev-shm-usage"); // Usar memória compartilhada de forma eficiente

                Directory.CreateDirectory(downloadDirectory);

                using (var driver = new ChromeDriver(options))
                {
                    // Navegar para o domínio base para adicionar cookies
                    driver.Navigate().GoToUrl(_baseUrl);

                    // Adicionar cookies de autenticação
                    driver.Manage().Cookies.AddCookie(new OpenQA.Selenium.Cookie("JSESSIONID", GetCookieValue("JSESSIONID")));
                    driver.Manage().Cookies.AddCookie(new OpenQA.Selenium.Cookie("__goc_session__", GetCookieValue("__goc_session__")));

                    // Atualizar a página para aplicar os cookies
                    driver.Navigate().Refresh();

                    // Navegar para o URL de download
                    driver.Navigate().GoToUrl(url);

                    // Esperar pelo download (não checar mais diretamente)
                    Console.WriteLine("Download iniciado...");
                    await Task.Delay(10000); // Tempo suficiente para concluir o download
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao baixar o anexo {fileName} via navegador: {ex.Message}");
            }
        }
        public async Task BaixarAnexosComSeleniumAsync(List<(int chamadoId, string fileId, string fileName)> anexos)
        {
            var options = new ChromeOptions();
            var downloadDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Data", "processed");
            options.AddUserProfilePreference("download.default_directory", downloadDirectory);
            options.AddUserProfilePreference("download.prompt_for_download", false);
            options.AddUserProfilePreference("plugins.always_open_pdf_externally", true);
            //options.AddArgument("--headless");

            Directory.CreateDirectory(downloadDirectory);

            using (var driver = new ChromeDriver(options))
            {
                // Navegar para o domínio base e autenticar uma vez
                driver.Navigate().GoToUrl(_baseUrl);
                driver.Manage().Cookies.AddCookie(new OpenQA.Selenium.Cookie("JSESSIONID", GetCookieValue("JSESSIONID")));
                driver.Manage().Cookies.AddCookie(new OpenQA.Selenium.Cookie("__goc_session__", GetCookieValue("__goc_session__")));
                driver.Navigate().Refresh();

                foreach (var (chamadoId, fileId, fileName) in anexos)
                {
                    try
                    {
                        var url = $"{_baseUrl}/getFile?table=service_req&id={chamadoId}&getFile={fileId}";
                        driver.Navigate().GoToUrl(url);

                        Console.WriteLine($"Download iniciado para o arquivo: {fileName}");
                        await Task.Delay(5000); // Aguarde o download
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao baixar o anexo {fileName}: {ex.Message}");
                    }
                }
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
    }
}
