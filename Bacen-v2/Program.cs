using System;
using System.Collections.Generic;
using Bacen_v2.Utils; // Para utilitários como Logger e Constants
using Bacen_v2.Handlers; // Para manipuladores como AuthHandler e outros
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

            Divisao();

            Console.WriteLine("Ambiente utilizado: " + config.Environment);
            Console.WriteLine($"Usuário: {config.TopDesk.Username}");

            Divisao();

            // Preparar handlers
            Console.WriteLine("Preparando os serviços...");
            var authHandler = new AuthHandler(config);

            Divisao();

            // Realizar login no Service Desk
            await authHandler.LoginServiceDesk();
            Console.WriteLine($"Sessão iniciada com JSESSIONID: {authHandler.SessionId}");
            Console.WriteLine($"GocSession: {authHandler.GocSession}");
            Console.WriteLine($"Grupo de Atendimento ID: {authHandler.UserGroupId}");
            Console.WriteLine($"Usuário Autenticado: {authHandler.UserName}");
            Console.WriteLine($"E-mail: {authHandler.UserEmail}");

            Divisao();

            // Inicializar ServiceDesk com JSESSIONID
            var serviceDeskHandler = new ServiceDesk(config, authHandler.SessionId, authHandler.GocSession);

            while (true)
            {
                Console.WriteLine("Escolha uma opção:");
                Console.WriteLine("1 - Listar chamados com status 'ENCAMINHADO N2 ATENDIMENTO'");
                Console.WriteLine("2 - Consultar detalhes de um chamado específico pelo ID");
                Console.WriteLine("0 - Sair");

                if (!int.TryParse(Console.ReadLine(), out int opcao) || opcao < 0 || opcao > 2)
                {
                    Console.WriteLine("Opção inválida. Tente novamente.");
                    continue;
                }

                if (opcao == 0)
                {
                    Console.WriteLine("Encerrando o programa.");
                    break;
                }

                switch (opcao)
                {
                    case 1:
                        await ListarChamados(serviceDeskHandler);
                        break;
                    case 2:
                        await ConsultarChamadoEspecifico(serviceDeskHandler);
                        break;
                }

                Divisao();
            }

            Logger.Log("Inicialização concluída com sucesso.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro durante a inicialização: {ex.Message}");
            Logger.Log($"Erro crítico: {ex.Message}");
        }
    }

    private static async Task ListarChamados(ServiceDesk serviceDeskHandler)
    {
        var chamadosFiltrados = await serviceDeskHandler.GetChamadosEncaminhadoN2Async();

        if (chamadosFiltrados.Count > 0)
        {
            Console.WriteLine($"Encontrados {chamadosFiltrados.Count} chamados com status 'ENCAMINHADO N2 ATENDIMENTO':");

            foreach (var chamado in chamadosFiltrados)
            {
                var chamadoId = chamado["id"]?.ToString();
                var titulo = chamado["info"]?.FirstOrDefault(info => info["key"]?.ToString() == "title")?["valueCaption"];

                Console.WriteLine($"ID: {chamadoId}, Título: {titulo}");

                // Consultar detalhes e anexos automaticamente
                if (!string.IsNullOrEmpty(chamadoId))
                {
                    await ConsultarDetalhesEAnexos(serviceDeskHandler, int.Parse(chamadoId));
                }
            }
        }
        else
        {
            Console.WriteLine("Nenhum chamado encontrado com o status 'ENCAMINHADO N2 ATENDIMENTO'.");
        }
    }

    private static async Task ConsultarChamadoEspecifico(ServiceDesk serviceDeskHandler)
    {
        Console.WriteLine("Digite o ID do chamado que deseja consultar:");
        if (int.TryParse(Console.ReadLine(), out int chamadoId) && chamadoId > 0)
        {
            await ConsultarDetalhesEAnexos(serviceDeskHandler, chamadoId);
        }
        else
        {
            Console.WriteLine("ID inválido. Tente novamente.");
        }
    }

    private static async Task ConsultarDetalhesEAnexos(ServiceDesk serviceDeskHandler, int chamadoId)
    {
        Console.WriteLine($"Consultando detalhes do chamado ID: {chamadoId}");

        // Obter detalhes do chamado
        var detalhesChamado = await serviceDeskHandler.GetDetalhesChamadoAsync(chamadoId);

        if (detalhesChamado != null)
        {
            Console.WriteLine($"Detalhes do chamado {chamadoId}:\n{detalhesChamado}");

            // Obter anexos do chamado
            var anexos = await serviceDeskHandler.GetAnexosDoChamadoAsync(chamadoId);

            if (anexos.Count > 0)
            {
                Console.WriteLine($"Encontrados {anexos.Count} anexos no chamado {chamadoId}:");

                foreach (var anexo in anexos)
                {
                    var fileId = anexo["fileId"]?.ToString();
                    var fileName = anexo["fileName"]?.ToString();

                    if (!string.IsNullOrEmpty(fileId) && !string.IsNullOrEmpty(fileName))
                    {
                        // Delegar o download ao ServiceDesk
                        await serviceDeskHandler.BaixarAnexoAsync(chamadoId, fileId, fileName);
                    }
                }
            }
            else
            {
                Console.WriteLine($"Nenhum anexo encontrado no chamado {chamadoId}.");
            }
        }
        else
        {
            Console.WriteLine($"Erro ao obter detalhes do chamado {chamadoId}.");
        }
    }


    public static void Divisao()
    {
        Console.WriteLine("--------------------------------");
    }
}