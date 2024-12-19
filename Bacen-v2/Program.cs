﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bacen_v2.Handlers;
using Bacen_v2.Handlers;
using Bacen_v2.Utils;
using Newtonsoft.Json.Linq;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine(@"
  ____                              ___  
 |  _ \                            |__ \ 
 | |_) | __ _  ___ ___ _ __   __   __ ) |
 |  _ < / _` |/ __/ _ \ '_ \  \ \ / // / 
 | |_) | (_| | (_|  __/ | | |  \ V // /_ 
 |____/ \__,_|\___\___|_| |_|   \_/|____|
                                          
                                         ");

        try
        {
            // Carregar configurações
            var config = ConfigLoader.Load("Configs/appsettings.json");
            Console.WriteLine("Configurações carregadas com sucesso.");

            // Inicializar logger
            Logger.Init();
            Logger.Log("Aplicação iniciada.");

            // Inicializar autenticação
            var authHandler = new AuthHandler(config);
            await authHandler.LoginServiceDesk();
            Console.WriteLine("Autenticação realizada com sucesso no Service Desk.");

            // Inicializar manipuladores
            var serviceDeskHandler = new ServiceDesk(config, authHandler.SessionId, authHandler.GocSession);
            var topDeskAuth = new TopDeskAuth(config);
            await topDeskAuth.LoginAsync();
            var topDeskHandler = new TopDeskHandler("Configs/topdesk_links.json", "Configs/chamados.json", topDeskAuth);

            // Obter chamados com status "Encaminhado N2 Atendimento"
            Console.WriteLine("Obtendo chamados com status 'Encaminhado N2 Atendimento'...");
            var chamados = await serviceDeskHandler.GetChamadosEncaminhadoN2Async();

            if (chamados.Count == 0)
            {
                Console.WriteLine("Nenhum chamado encontrado com o status 'Encaminhado N2 Atendimento'.");
                return;
            }

            foreach (var chamado in chamados)
            {
                try
                {
                    var chamadoId = chamado["id"]?.ToString();

                    if (string.IsNullOrEmpty(chamadoId))
                    {
                        Console.WriteLine("Chamado sem ID. Pulando...");
                        continue;
                    }

                    // Obter detalhes do chamado
                    var detalhesChamado = await serviceDeskHandler.GetDetalhesChamadoAsync(int.Parse(chamadoId));
                    if (detalhesChamado == null)
                    {
                        Console.WriteLine($"Detalhes do chamado ID {chamadoId} não encontrados. Pulando...");
                        continue;
                    }

                    var id = detalhesChamado["id"]?.ToString();
                    var titulo = detalhesChamado["titulo"]?.ToString();
                    var descricao = detalhesChamado["descricao"]?.ToString();
                    var status = detalhesChamado["status"]?.ToString();
                    var de = detalhesChamado["de"]?.ToString();
                    var usuarioSolicitante = detalhesChamado["usuarioSolicitante"]?.ToString();
                    var prioridade = detalhesChamado["prioridade"]?.ToString();
                    var categoria = detalhesChamado["categoria"]?.ToString().ToUpper();
                    var sla = detalhesChamado["sla"]?.ToString();
                    var notas = detalhesChamado["notas"]?.ToString();

                    // Obter as customColumns
                    var customColumns = new Dictionary<string, string>();

                    var customColumnsData = detalhesChamado["info"]?
                    .Where(info => info["key"]?.ToString().StartsWith("CustomColumn") == true);

                    if (customColumnsData != null)
                    {
                        foreach (var column in customColumnsData)
                        {
                            Console.WriteLine($"Key: {column["keyCaption"]}, Value: {column["valueCaption"]}");
                        }
                    }


                    if (customColumnsData != null)
                    {
                        foreach (var column in customColumnsData)
                        {
                            var keyCaption = column["keyCaption"]?.ToString();
                            var value = column["valueCaption"]?.ToString() ?? "Não preenchido"; // Valor padrão para campos vazios

                            if (!string.IsNullOrEmpty(keyCaption))
                            {
                                // Adicionar ao dicionário
                                customColumns[keyCaption] = value;
                            }
                        }
                    }

                    // Exibir as CustomColumns para depuração
                    foreach (var column in customColumns)
                    {
                        Console.WriteLine($"{column.Key}: {column.Value}");
                    }


                    Console.WriteLine($"Processando chamado ID: {chamadoId}, Título: {titulo}, Categoria: {categoria}");

                    // Obter anexos do chamado
                    var anexos = await serviceDeskHandler.GetAnexosDoChamadoAsync(int.Parse(chamadoId));

                    // Fazer download dos anexos
                    var anexoPaths = new List<string>();
                    foreach (var anexo in anexos)
                    {
                        var fileId = anexo["fileId"]?.ToString();
                        var fileName = anexo["fileName"]?.ToString();

                        if (!string.IsNullOrEmpty(fileId) && !string.IsNullOrEmpty(fileName))
                        {
                            Console.WriteLine($"Baixando anexo: {fileName}");
                            var filePath = await serviceDeskHandler.BaixarAnexoComPlaywrightAsync(int.Parse(chamadoId), fileId, fileName);
                            anexoPaths.Add(filePath);
                        }
                    }

                    // Abrir chamado no TopDesk
                    await topDeskHandler.AbrirChamadoAsync(categoria, titulo, id, descricao, de, usuarioSolicitante, sla, customColumns, anexoPaths.Count > 0 ? anexoPaths[0] : null);

                    Console.WriteLine($"Chamado ID: {chamadoId} processado com sucesso.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao processar chamado: {ex.Message}");
                }
            }

            Console.WriteLine("Processamento de chamados concluído.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro inesperado: {ex.Message}");
            Logger.Log($"Erro crítico: {ex.Message}");
        }
    }
}
