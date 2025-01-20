using Microsoft.Playwright;
using Newtonsoft.Json.Linq;
using System.Globalization;

namespace Bacen_v2.Handlers
{
    public class TopDeskHandler
    {
        private readonly Dictionary<string, string> _links;
        private readonly Dictionary<string, Dictionary<string, string>> _fieldMappings;
        private readonly TopDeskAuth _auth;


        public TopDeskHandler(string linksJsonPath, string fieldMappingsJsonPath, TopDeskAuth auth)
        {
            // Carregar links do JSON
            if (!File.Exists(linksJsonPath))
                throw new FileNotFoundException($"Arquivo JSON não encontrado: {linksJsonPath}");

            string linksContent = File.ReadAllText(linksJsonPath);
            var linksData = JObject.Parse(linksContent);
            _links = linksData["links"]?.ToObject<Dictionary<string, string>>()
                     ?? throw new Exception("Erro ao carregar os links do JSON.");

            // Carregar mapeamentos de campos do JSON
            if (!File.Exists(fieldMappingsJsonPath))
                throw new FileNotFoundException($"Arquivo JSON não encontrado: {fieldMappingsJsonPath}");

            string fieldMappingsContent = File.ReadAllText(fieldMappingsJsonPath);
            _fieldMappings = JObject.Parse(fieldMappingsContent)?.ToObject<Dictionary<string, Dictionary<string, string>>>()
                               ?? throw new Exception("Erro ao carregar os mapeamentos de campos do JSON.");

            _auth = auth; // Instância do TopDeskAuth
        }

        public async Task<string> AbrirChamadoAsync(
            string tipoChamado,
            string titulo,
            string id,
            string descricao,
            string de,
            string usuarioSolicitante,
            string sla,
            string customColumns,
            string notes,
            string identificador,
            SearchHandler searchHandler,
            List<string> anexoPaths = null,
            int maxRetries = 3
        )
        {


            if (!_links.TryGetValue(tipoChamado, out string url))
            {
                Console.WriteLine($"Tipo de chamado '{tipoChamado}' não encontrado.");
                return null;
            }

            if (!_fieldMappings.TryGetValue(tipoChamado, out var fields))
            {
                Console.WriteLine($"Mapeamento de campos para o tipo de chamado '{tipoChamado}' não encontrado.");
                return null;
            }

            // Verificar SLA e calcular se estiver ausente
            if (string.IsNullOrEmpty(sla))
            {
                Console.WriteLine("SLA ausente. Calculando 5 dias úteis...");
                var businessDays = new Bacen_v2.Utils.BusinessDays();
                var calculatedSla = await businessDays.AddBusinessDaysAsync(DateTime.Now, 6);
                sla = calculatedSla.ToString("dd-MM-yyyy HH:mm:ss");
                Console.WriteLine($"SLA calculado: {sla}");
            }

            int attempt = 0;

            while (attempt < maxRetries)
            {
                try
                {
                    attempt++;

                    // Reutilizar a página autenticada do TopDeskAuth
                    var page = await _auth.GetAuthenticatedPageAsync();

                    // Navegar até o link do chamado
                    Console.WriteLine($"Abrindo o link para o tipo de chamado: {tipoChamado}");
                    await page.GotoAsync(url);

                    // Aguarda o carregamento completo da página
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                    // Localizar o iframe
                    var possibleTitles = new[] { "Falha", "Solicitação" };
                    IFrameLocator frameLocator = null;
                    foreach (var title in possibleTitles)
                    {
                        // Localiza o iframe diretamente pela página
                        var iframeLocator = page.Locator($"iframe[title='{title}']");

                        // Verifica se o iframe existe e está visível
                        if (await iframeLocator.CountAsync() > 0 && await iframeLocator.IsVisibleAsync())
                        {
                            Console.WriteLine($"Iframe com título '{title}' encontrado.");
                            frameLocator = page.FrameLocator($"iframe[title='{title}']");
                            break;
                        }
                    }

                    // Verificar se nenhum iframe foi encontrado
                    if (frameLocator == null)
                    {
                        Console.WriteLine("Nenhum iframe correspondente foi encontrado.");
                        return null;
                    }

                    // Preencher os campos dinamicamente
                    if (fields.TryGetValue("titulo", out string tituloSelector))
                    {
                        var tituloInput = frameLocator.Locator(tituloSelector);
                        await tituloInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 60000 });
                        await tituloInput.FillAsync($"Chamado Service Desk Bacen OpenFinance #{id} ({identificador})");
                    }

                    if (fields.TryGetValue("ticket", out string ticketSelector))
                    {
                        var ticketInput = frameLocator.Locator(ticketSelector);
                        await ticketInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 60000 });
                        await ticketInput.FillAsync($"#{id}");
                    }

                    if (fields.TryGetValue("descricao", out string descricaoSelector))
                    {
                        var descricaoInput = frameLocator.Locator(descricaoSelector);
                        await descricaoInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 60000 });

                        // Reduzir o valor do SLA em um dia
                        if (DateTime.TryParseExact(sla, "dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime slaDate))
                        {
                            slaDate = slaDate.AddDays(-1);
                            sla = slaDate.ToString("dd-MM-yyyy HH:mm:ss");
                        }

                        // Preencher o campo de descrição
                        await descricaoInput.FillAsync($@"De: {de}
 
    Usuário Solicitante: {usuarioSolicitante}
 
    Assunto: {titulo}
 
    Conteúdo: 
    Titulo:
    {titulo}

    Descrição:
    {descricao}

    CustomColumns:
    --------------------------------
    {customColumns}Notas:
    {notes}
    --------------------------------

    Prazo SLA: {sla}
    
    Protocolo gerado automaticamente.");
                    }


                    if (fields.TryGetValue("impacto", out string impactoSelector))
                    {
                        var impactoInput = frameLocator.Locator(impactoSelector);
                        await impactoInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 60000 });
                        await impactoInput.FillAsync("Meu Departamento Inteiro");
                        await page.Keyboard.PressAsync("Enter");
                    }

                    // Anexar múltiplos arquivos, se fornecidos
                    if (anexoPaths != null && anexoPaths.Any())
                    {
                        var falhas = new List<string>(); // Declaração da variável "falhas"

                        foreach (var anexo in anexoPaths)
                        {
                            try
                            {
                                Console.WriteLine($"Tentando anexar: {Path.GetFileName(anexo)}");
                                await frameLocator.Locator("input[type=file]").SetInputFilesAsync(new[] { anexo });
                                Console.WriteLine($"Arquivo anexado: {Path.GetFileName(anexo)}");
                                    
                                // sleep para aguardar o upload do arquivo
                                await Task.Delay(5000);

                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Erro ao anexar {Path.GetFileName(anexo)}: {ex.Message}");
                            }
                        }

                        if (falhas.Any())
                        {
                            Console.WriteLine("Arquivos que não puderam ser anexados:");
                            falhas.ForEach(f => Console.WriteLine(f));
                        }
                    }
                    else
                    {
                        Console.WriteLine("Nenhum anexo fornecido.");
                    }



                    // Submeter o formulário
                    Console.WriteLine("Enviando o chamado...");
                    await frameLocator.Locator("input#button_submit").ClickAsync();

                    // Aguardar o redirecionamento
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    await Task.Delay(5000);

                    // Simular Ctrl+A
                    await page.Keyboard.PressAsync("Control+a");

                    // Simular Ctrl+C
                    await page.Keyboard.PressAsync("Control+c");

                    // Recuperar o conteúdo da área de transferência
                    var clipboardContent = await page.EvaluateAsync<string>("() => navigator.clipboard.readText()");

                    // Salvar o conteúdo em um arquivo para análise
                    var logFilePath = Path.Combine(@"C:\Users\Luiz.Silvano\Downloads", "conteudo_visivel.txt");
                    await File.WriteAllTextAsync(logFilePath, clipboardContent);
                    Console.WriteLine($"Conteúdo visível salvo em: {logFilePath}");

                    // Buscar o número do protocolo usando regex
                    var match = System.Text.RegularExpressions.Regex.Match(clipboardContent, @"I\d{4}-\d{6}");
                    if (match.Success)
                    {
                        var numeroProtocolo = match.Value;
                        Console.WriteLine($"Número do protocolo capturado: {numeroProtocolo}");
                        return numeroProtocolo;
                    }
                    else
                    {
                        Console.WriteLine("Número do protocolo não encontrado no conteúdo capturado.");

                        // Verificar se o erro é o impacto não preenchido corretamente
                        if (clipboardContent.Contains("Impacto: deve ser preenchido"))
                        {
                            Console.WriteLine("Erro: Impacto não preenchido corretamente. Fazendo retry");

                            if (attempt < maxRetries)
                            {
                                Console.WriteLine("Tentando novamente...");
                                continue;
                            }
                        }

                        // Verifica se o erro é de anexar o documento ao chamado
                        if (clipboardContent.Contains("An error occurred while trying to attach the file"))
                        {
                            Console.WriteLine("Erro: Um erro ocorreu ao tentar anexar o arquivo no chamado");
                            Console.WriteLine("Verificando se o chamado já foi aberto...");


                            // Executa a busca por identificador único
                            var protocoloEncontrado = await searchHandler.PesquisarChamadoAsync(identificador, id);

                            if (protocoloEncontrado != null)
                            {
                                Console.WriteLine($"Chamado já aberto com o protocolo: {protocoloEncontrado}");
                                return protocoloEncontrado;
                            }
                            else if (attempt < maxRetries)
                            {
                                Console.WriteLine("Chamado não encontrado, tentando novamente...");
                                continue;
                            }
                        }

                        return null;
                    }
                }

                catch (Exception ex)
                {
                    Console.WriteLine($"Erro na tentativa {attempt} no chamado #{id}: {ex.Message}");
                    if (attempt >= maxRetries)
                    {
                        Console.WriteLine("Número máximo de tentativas atingido, verifique o log.");
                        return null;
                    }
                    return null;
                }
            }
            Console.WriteLine("Chamado não foi aberto com sucesso.");
            return null;
        }



        public void ListarTiposChamados()
        {
            Console.WriteLine("Tipos de chamados disponíveis:");
            foreach (var tipo in _links.Keys)
            {
                Console.WriteLine($"- {tipo}");
            }
        }
    }
}
