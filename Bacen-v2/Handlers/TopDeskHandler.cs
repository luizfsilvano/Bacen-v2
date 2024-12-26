﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Bacen_v2.Utils;
using Microsoft.Playwright;
using Newtonsoft.Json.Linq;

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
            string anexoPath = null
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

            try
            {
                Logger.Init(); // Inicializar o logger

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
                    await tituloInput.FillAsync($"Chamado Service Desk Bacen OpenFinance #{id}");
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
");
                }


                if (fields.TryGetValue("impacto", out string impactoSelector))
                {
                    var impactoInput = frameLocator.Locator(impactoSelector);
                    await impactoInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 60000 });
                    await impactoInput.FillAsync("Meu Departamento Inteiro");
                    await page.Keyboard.PressAsync("Enter");
                }

                // Anexar arquivo, se fornecido
                if (!string.IsNullOrEmpty(anexoPath) && File.Exists(anexoPath))
                {
                    Console.WriteLine("Anexando o arquivo...");
                    await frameLocator.Locator("input[type=file]").SetInputFilesAsync(anexoPath);
                }
                else
                {
                    Console.WriteLine("Nenhum anexo fornecido ou arquivo não encontrado.");
                }

                // Submeter o formulário
                Console.WriteLine("Enviando o chamado...");
                await frameLocator.Locator("input#button_submit").ClickAsync();

                // Aguardar o redirecionamento
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                await Task.Delay(2000);

                // Simular Ctrl+A (selecionar tudo)
                await page.Keyboard.PressAsync("Control+a");

                // Simular Ctrl+C (copiar para a área de transferência)
                await page.Keyboard.PressAsync("Control+c");

                // Recuperar o conteúdo da área de transferência
                var clipboardContent = await page.EvaluateAsync<string>("() => navigator.clipboard.readText()");

                // Exibir o conteúdo capturado
                Console.WriteLine("Conteúdo capturado:");
                Console.WriteLine(clipboardContent);

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
                    // Adiciona um Sleep para debug
                    await Task.Delay(200000);
                    return numeroProtocolo;
                }
                else
                {
                    Console.WriteLine("Número do protocolo não encontrado no conteúdo capturado.");
                    // Adiciona um Sleep para debug
                    await Task.Delay(200000);
                    return null;
                }
            }

            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao abrir chamado: {ex.Message}");
                Console.ReadLine();
                return null;
            }
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
