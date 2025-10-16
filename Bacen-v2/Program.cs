using Bacen_v2.API;
using Bacen_v2.Handlers;
using Bacen_v2.Utils;
using Microsoft.Playwright;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium.Chrome;
using Newtonsoft.Json;

class Program
{

    private static ChromeDriver driver; // Campo global para o driver
    private static IBrowser playwrightBrowser; // Campo global para o navegador Playwright
    private static System.Timers.Timer timer;      // Timer para executar a tarefa
    private static bool isRunning = false; // Flag para verificar se a tarefa está em execução
    private static string contextoExecucao = "Processamento de chamados"; // Contexto de execução


    static async Task Main(string[] args)
    {
        // Instala os navegadores caso ainda não estejam presentes
        Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });

        CreateConfigFileIfNotExists();

        _ = WebSocketServer.StartServer(); // Iniciar o WebSocket Server em segundo plano

        // Configurar nível de log para TensorFlow Lite
        Environment.SetEnvironmentVariable("TF_CPP_MIN_LOG_LEVEL", "3");

        bool animacao = true;
        var loadingTask = Task.Run(() =>
        {
            var loadingChars = new[] { '|', '/', '-', '\\' };
            var index = 0;
            while (animacao)
            {
                Console.Write($"\rInicializando contextos... {loadingChars[index++ % loadingChars.Length]}");
                Thread.Sleep(120);
            }
        });


        // Configurar opções do Chrome do contexto do Selenium
        var seleniumTask = Task.Run(() =>
        {
            // 1) Desabilita o Selenium Manager
            Environment.SetEnvironmentVariable("SELENIUM_MANAGER_DISABLED", "1");

            // 2) Cria as opções do Chrome
            var options = new ChromeOptions();
            options.AddArgument("--disable-gpu");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--headless");
            options.AddArgument("log-level=3");

            // 3) Pega o diretório de saída da aplicação (onde está o chromedriver.exe)
            var driverFolder = AppDomain.CurrentDomain.BaseDirectory;

            // 4) Cria o serviço apontando para aquela pasta
            var chromeService = ChromeDriverService.CreateDefaultService(driverFolder);
            chromeService.HideCommandPromptWindow = true;  // opcional

            // 5) Instancia o driver utilizando o serviço customizado
            driver = new ChromeDriver(chromeService, options);
        });

        // Inicializar contexto do Playwright
        var playwrightTask = Task.Run(async () =>
        {
            var playwright = await Playwright.CreateAsync();
            playwrightBrowser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true  // Mude para true ou false para visualizar o navegador.
            });
        });

        // Aguarde a inicialização de ambos os contextos
        await Task.WhenAll(seleniumTask, playwrightTask);

        // Desativar animação
        animacao = false;
        await loadingTask;

        Console.Clear();
        Console.WriteLine("Contexto do Selenium e Playwright inicializado com sucesso.");

        Console.Clear();

        // Configurar o Timer
        timer = new System.Timers.Timer
        {
            Interval = 30 * 60 * 1000, // Intervalo em milissegundos (30 minutos)
            AutoReset = true           // Repetir automaticamente
        };

        timer.Elapsed += async (sender, e) => await Automacao(); // Configurar evento
        timer.Start(); // Iniciar o timer

        // Executar a primeira vez imediatamente
        Console.WriteLine("Executando automação pela primeira vez...");
        Automacao().Wait();

        // Manter o programa ativo
        Console.WriteLine("Pressione Enter para sair...");
        Console.ReadLine();

        driver.Quit();
        driver.Dispose();
        if (playwrightBrowser != null)
        {
            await playwrightBrowser.CloseAsync();
        }
    }
    private static void CreateConfigFileIfNotExists()
    {
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var configPath = Path.Combine(basePath,"Configs", "appsettings.json");
        var configDirectory = Path.GetDirectoryName(configPath);

        // Criar diretório se não existir
        if (!Directory.Exists(configDirectory))
        {
            Directory.CreateDirectory(configDirectory);
        }

        // Criar arquivo se não existir
        if (!File.Exists(configPath))
        {
            Console.WriteLine("Configurações não encontradas. Vamos criar um novo arquivo de configuração:");

            var config = new JObject(
                new JProperty("Environment", GetEnvironmentChoice()),
                new JProperty("ServiceDesk", new JObject(
                    new JProperty("SandboxUrl", "https://servicedesksandbox.openfinancebrasil.org.br"),
                    new JProperty("ProductionUrl", "https://servicedesk.openfinancebrasil.org.br"),
                    new JProperty("Username", GetInput("ServiceDesk Username: ")),
                    new JProperty("Password", GetInput("ServiceDesk Password: ")),
                    new JProperty("userID", "1874")
                )),
                new JProperty("TopDesk", new JObject(
                    new JProperty("BaseUrl", "https://atendimento.sisbr.coop.br"),
                    new JProperty("Username", GetInput("TopDesk Username: ")),
                    new JProperty("Password", GetInput("TopDesk Password: "))
                ))
            );

            File.WriteAllText(configPath, config.ToString(Formatting.Indented));
            Console.WriteLine("\nArquivo de configuração criado com sucesso em Configs/appsettings.json");
        }
    }

    private static string GetEnvironmentChoice()
    {
        Console.WriteLine("\nEscolha o ambiente (digite 1 ou 2):");
        Console.WriteLine("1 - Production");
        Console.WriteLine("2 - Sandbox");

        while (true)
        {
            var input = Console.ReadLine();
            if (input == "1") return "Production";
            if (input == "2") return "Sandbox";
            Console.WriteLine("Opção inválida. Digite 1 ou 2:");
        }
    }

    private static string GetInput(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine();
    }


    private static async Task Automacao()
    {
        if (isRunning)
        {
            Console.WriteLine("Automação já está em execução. Pulando...");
            return;
        }

        isRunning = true; // Sinaliza que a execução começou
        
        Console.Clear();
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
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var configPath = Path.Combine(basePath, "Configs", "appsettings.json");
            var configDirectory = Path.GetDirectoryName(configPath);
            var config = ConfigLoader.Load(configPath);
            Console.WriteLine("Configurações carregadas com sucesso.");

            Console.WriteLine($"Caminho das configurações {configPath}");

            Console.WriteLine($"Aplicação iniciada Dia {DateTime.Now}.");

            // Inicializar autenticação
            var authHandler = new AuthHandler(config);
            await authHandler.LoginServiceDesk();
            Console.WriteLine("Autenticação realizada com sucesso no Service Desk.");

            // Inicializar manipuladores
            var serviceDeskHandler = new ServiceDesk(config, authHandler.SessionId, authHandler.GocSession);
            var topDeskAuth = new TopDeskAuth(config, playwrightBrowser);
            await topDeskAuth.LoginAsync();
            var topDeskHandler = new TopDeskHandler("Configs/topdesk_links.json", "Configs/chamados.json", topDeskAuth);
            var searchHandler = new SearchHandler(topDeskAuth, config);
            bool isUpdate = false;

            // Obter chamados com status "Encaminhado N2 Atendimento"
            Console.WriteLine("Obtendo chamados com status 'Encaminhado N2 Atendimento'...");
            var chamados = await serviceDeskHandler.GetChamadosEncaminhadoN2Async();

            // Verificar se existem chamados a serem processados
            int totalNaoFinalizados = chamados.Count;
            int totalPendentes = chamados.Count(chamado => chamado["status"]?.ToString() == "PENDENTE");

            if (chamados.Count == 0)
            {
                Console.WriteLine("Nenhum chamado encontrado com o status 'Encaminhado N2 Atendimento'.");

                // Logar os chamados não finalizados
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                LogHandler.LogChamados(timestamp, totalNaoFinalizados, totalPendentes, null, false);

                Console.WriteLine("Processamento de chamados concluído.");
                return;
            }

            // Logar os chamados não finalizados
            string detalhesChamados = string.Join(Environment.NewLine, chamados.Select(chamado =>
               $"ID: {chamado["id"]}, Título: {chamado["titulo"]}, Status: {chamado["status"]}")
           );

            int totalNaoAbertos = 0;

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

                    // Obter informações do chamado
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
                    var customColumns = detalhesChamado["customColumns"]?.ToString();
                    var statusProcessamento = "Sucesso";

                    // Gerar identificador único do chamado gerado automaticamente:
                    string identificador = searchHandler.GerarIdentificador(int.Parse(chamadoId));

                    // Verificar se é atualização
                    if (serviceDeskHandler.VerificarChamadoAberto(notas) == true)
                    {
                        // Pular para o próximo chamado
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.WriteLine($"\nℹ️ ATUALIZAÇÃO NO CHAMADO {chamadoId}!\n");
                        Console.ResetColor();
                        isUpdate = true;
                        continue;
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
                            var filePath = await serviceDeskHandler.BaixarAnexoComSeleniumAsync(int.Parse(chamadoId), fileId, fileName, authHandler.SessionId, authHandler.GocSession, driver);
                            if (!string.IsNullOrEmpty(filePath))
                            {
                                anexoPaths.Add(filePath);
                            }
                        }
                    }

                    // Abrir chamado no TopDesk
                    var numeroProtocolo = await topDeskHandler.AbrirChamadoAsync(categoria, titulo, id, descricao, de, usuarioSolicitante, sla, customColumns, notas, identificador, searchHandler, anexoPaths);

                    // Verificar se o chamado possui um número de protocolo gerado com sucesso
                    if (!string.IsNullOrEmpty(numeroProtocolo))
                    {
                        // Adicionar número do protocolo às notas do chamado
                        await serviceDeskHandler.AdicionarNumeroProtocoloAsync(chamadoId, numeroProtocolo, authHandler.SessionId, authHandler.GocSession);

                        // Registrar a abertura do chamado no histórico CSV
                        Historic.AddRecord(DateTime.Now, chamadoId, numeroProtocolo, identificador, "abertura");

                        // Atualizar informações no SD
                        await serviceDeskHandler.AtualizarChamadoAsync(id, authHandler.SessionId, authHandler.GocSession);

                        Console.WriteLine($"Chamado ID: {chamadoId} registrado no histórico com sucesso.");
                    }
                    else
                    {
                        Console.WriteLine($"Falha ao identificar número do protocolo.");

                        // TODO: Implementação melhorada da busca pelo identificador Unico do chamado

                        // Em cada iteração do seu loop:
                        if (string.IsNullOrWhiteSpace(numeroProtocolo))
                        {
                            // Indica erro ao não obter protocolo
                            Console.WriteLine($"Erro: número do protocolo não gerado para o chamado ID: {chamadoId}.");

                            // Registrar erro no histórico CSV
                            Historic.AddRecord(
                                DateTime.Now,
                                chamadoId,          // ID do chamado
                                null,               // sem protocolo
                                null,               // sem identificador
                                "erro_protocolo"    // tipo de registro
                            );

                            // pular para o próximo chamado
                            continue;
                        }

                        // Se chegou aqui, tem protocolo válido

                        // 1) Adicionar número do protocolo às notas do chamado
                        await serviceDeskHandler.AdicionarNumeroProtocoloAsync(
                            chamadoId,
                            numeroProtocolo,
                            authHandler.SessionId,
                            authHandler.GocSession
                        );

                        // 2) Registrar a abertura do chamado no histórico CSV
                        Historic.AddRecord(
                            DateTime.Now,
                            chamadoId,
                            numeroProtocolo,
                            null,           // não usamos mais o identificador
                            "abertura"
                        );

                        // 3) Atualizar informações no ServiceDesk
                        await serviceDeskHandler.AtualizarChamadoAsync(
                            id,
                            authHandler.SessionId,
                            authHandler.GocSession
                        );

                        Console.WriteLine($"Chamado ID: {chamadoId} registrado no histórico com sucesso.");

                    }

                        detalhesChamados += $"ID: {chamadoId}, Título: {titulo ?? "Título não informado"}, Status: {status ?? "Status não informado"}{Environment.NewLine}";

                    Console.WriteLine($"Chamado ID: {chamadoId} processado com sucesso.");
                }
                catch (Exception ex)
                {
                    // Enviar informações do erro para o Exceptions.cs
                    var chamadoId = chamado["id"]?.ToString();
                    var titulo = chamado["titulo"]?.ToString();
                    var categoria = chamado["categoria"]?.ToString();
                    var status = "Erro";

                    await Exceptions.HandleErrorAsync(ex, chamadoId, titulo, categoria, status);
                }
        }

            // Após o loop, registrar o log geral
            string detalhesChamadosLog = string.Join(Environment.NewLine, chamados.Select(chamado =>
               LogHandler.FormatarDetalhesChamado(
                   chamado["id"]?.ToString(),
                   chamado["titulo"]?.ToString(),
                   chamado["categoria"]?.ToString(),
                   chamado["status"]?.ToString())
            ));

            LogHandler.LogChamados(DateTime.Now.ToString("yyyyMMdd_HHmmss"), chamados.Count, chamados.Count - totalNaoAbertos, detalhesChamados, possuiErro: false, isUpdate);

            Console.WriteLine("Processamento de chamados concluído.");
        }
        catch (Exception ex)
        {
            string errorMessage = $"Erro ao processar chamados gerais: {ex.Message}";
            Console.WriteLine(errorMessage);

            // Adicionar log geral com erro
            LogHandler.LogChamados(DateTime.Now.ToString("yyyyMMdd_HHmmss"), 0, 0, errorMessage, possuiErro: true);

            // Registrar o erro no Exceptions.cs
            await Exceptions.HandleErrorAsync(
                ex,
                contextoExecucao,
                titulo: "Desconhecido",
                categoria: "Desconhecida",
                status: "Erro Geral"
            );
        }
        finally
        {
            isRunning = false; // Sinaliza que a execução terminou
        }
    }
}


