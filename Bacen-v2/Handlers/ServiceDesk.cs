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
using Microsoft.Playwright;

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
                    ["de"] = chamadoDetalhes["info"]?.FirstOrDefault(info => info["key"]?.ToString() == "CustomColumn155sr")?["valueCaption"],
                    ["usuarioSolicitante"] = chamadoDetalhes["info"]?.FirstOrDefault(info => info["key"]?.ToString() == "request_user")?["valueCaption"],
                    ["prioridade"] = chamadoDetalhes["info"]?.FirstOrDefault(info => info["key"]?.ToString() == "priority")?["valueCaption"],
                    ["categoria"] = chamadoDetalhes["info"]?.FirstOrDefault(info => info["key"]?.ToString() == "problem_type")?["valueCaption"],
                    ["sla"] = chamadoDetalhes["info"]?.FirstOrDefault(info => info["key"]?.ToString() == "CustomColumn118sr")?["valueCaption"],
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
                        var valueCaption = column["valueCaption"]?.ToString() ?? "Não preenchido";
                        var value = column["value"]?.ToString() ?? "Não preenchido"; // Captura o valor numérico bruto
                        var rawValue = column["value"]?.ToString(); // Captura o valor bruto, se necessário

                        Console.WriteLine($"KeyCaption: {keyCaption}, ValueCaption: {valueCaption}, RawValue: {rawValue}, Value {value}");

                        if (!string.IsNullOrEmpty(keyCaption))
                        {
                            var formattedKeyCaption = ToCamelCase(keyCaption);
                            detalhesUteis[formattedKeyCaption] = valueCaption;
                        }
                    }
                }


                if (customColumns != null)
                {
                    foreach (var column in customColumns)
                    {
                        Console.WriteLine($"Key: {column["key"]}, KeyCaption: {column["keyCaption"]}, ValueCaption: {column["valueCaption"]}");
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

        public async Task<string> BaixarAnexoComPlaywrightAsync(int chamadoId, string fileId, string fileName)
        {
            try
            {
                var url = $"{_baseUrl}/getFile?table=service_req&id={chamadoId}&getFile={fileId}";
                var downloadDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Data", "processed");
                Directory.CreateDirectory(downloadDirectory);

                // Configurar o Playwright para downloads
                var playwright = await Playwright.CreateAsync();
                var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
                var context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    AcceptDownloads = true
                });
                var page = await context.NewPageAsync();

                // Navegar para o URL do arquivo
                await page.GotoAsync(url);

                // Esperar pelo download do arquivo
                var download = await page.WaitForDownloadAsync();

                // Salvar o arquivo no diretório especificado
                var filePath = Path.Combine(downloadDirectory, fileName);
                await download.SaveAsAsync(filePath);

                Console.WriteLine($"Anexo {fileName} baixado com sucesso em: {filePath}");
                await browser.CloseAsync();

                return filePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao baixar o anexo {fileName} com Playwright: {ex.Message}");
                return null;
            }
        }

        public async Task<List<string>> BaixarAnexosComPlaywrightAsync(List<(int chamadoId, string fileId, string fileName)> anexos)
        {
            var downloadPaths = new List<string>();
            try
            {
                var downloadDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Data", "processed");
                Directory.CreateDirectory(downloadDirectory);

                var playwright = await Playwright.CreateAsync();
                var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
                var context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    AcceptDownloads = true
                });
                var page = await context.NewPageAsync();

                foreach (var (chamadoId, fileId, fileName) in anexos)
                {
                    try
                    {
                        var url = $"{_baseUrl}/getFile?table=service_req&id={chamadoId}&getFile={fileId}";

                        // Navegar para o URL do arquivo
                        await page.GotoAsync(url);

                        // Esperar pelo download do arquivo
                        var download = await page.WaitForDownloadAsync();

                        // Salvar o arquivo no diretório especificado
                        var filePath = Path.Combine(downloadDirectory, fileName);
                        await download.SaveAsAsync(filePath);
                        downloadPaths.Add(filePath);

                        Console.WriteLine($"Anexo {fileName} baixado com sucesso em: {filePath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao baixar o anexo {fileName}: {ex.Message}");
                    }
                }

                await browser.CloseAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar anexos com Playwright: {ex.Message}");
            }

            return downloadPaths;
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
