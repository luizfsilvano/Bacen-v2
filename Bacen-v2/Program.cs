using Bacen_v2.Handlers;
using Bacen_v2.Utils;
using OpenQA.Selenium.Chrome;

class Program
{

    private static ChromeDriver driver; // Campo global para o driver
    private static System.Timers.Timer timer;      // Timer para executar a tarefa
    private static bool isRunning = false; // Flag para verificar se a tarefa está em execução


    static async Task Main(string[] args)
    {
        // Configurar nível de log para TensorFlow Lite
        Environment.SetEnvironmentVariable("TF_CPP_MIN_LOG_LEVEL", "3");

        bool animacao = true;
        var loadingTask = Task.Run(() =>
        {
            var loadingChars = new[] { '|', '/', '-', '\\' };
            var index = 0;
            while (animacao)
            {
                Console.Write($"\rInicializando contexto do Selenium... {loadingChars[index++ % loadingChars.Length]}");
                Thread.Sleep(120);
            }
        });


        // Configurar opções do Chrome do contexto do Selenium
        var options = new ChromeOptions();
        options.AddArgument("--disable-gpu");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--headless");
        options.AddArgument("log-level=3");



        // Inicializar o WebDriver do Chrome
        driver = new ChromeDriver(options);

        // Desativar animação
        animacao = false;
        await loadingTask;

        Console.Clear();
        Console.WriteLine("Contexto do Selenium inicializado com sucesso.");

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
            var config = ConfigLoader.Load("Configs/appsettings.json");
            Console.WriteLine("Configurações carregadas com sucesso.");

            Console.WriteLine($"Aplicação iniciada Dia {DateTime.Now}.");

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

            // Verificar se existem chamados a serem processados
            int totalNaoFinalizados = chamados.Count;
            int totalPendentes = chamados.Count(chamado => chamado["status"]?.ToString() == "PENDENTE");

            if (chamados.Count == 0)
            {
                Console.WriteLine("Nenhum chamado encontrado com o status 'Encaminhado N2 Atendimento'.");

                // Logar os chamados não finalizados
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                LogHandler.LogChamados(timestamp, totalNaoFinalizados, totalPendentes, null);

                Console.WriteLine("Processamento de chamados concluído.");
                return;
            }

            // Logar os chamados não finalizados
            string detalhesChamados = string.Join(Environment.NewLine, chamados.Select(chamado =>
               $"ID: {chamado["id"]}, Título: {chamado["titulo"]}, Status: {chamado["status"]}")
           );

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

                    if (serviceDeskHandler.VerificarChamadoAberto(notas) == true)
                    {
                        // pular para o proximo chamado
                        Console.WriteLine($"Chamado ID: {chamadoId} já foi processado. Pulando...");
                        Console.WriteLine($"ATUALIZAÇÃO NO CHAMADO {chamadoId}!");
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
                    var numeroProtocolo = await topDeskHandler.AbrirChamadoAsync(categoria, titulo, id, descricao, de, usuarioSolicitante, sla, customColumns, notas, anexoPaths.Count > 0 ? anexoPaths[0] : null);

                    // Verificar se o chamado possui um número de protocolo gerado com sucesso
                    if (!string.IsNullOrEmpty(numeroProtocolo))
                    {
                        // Adicionar número do protocolo às notas do chamado
                        await serviceDeskHandler.AdicionarNumeroProtocoloAsync(chamadoId, numeroProtocolo, authHandler.SessionId, authHandler.GocSession);

                        // Atualizar informações no SD
                        await serviceDeskHandler.AtualizarChamadoAsync(id, authHandler.SessionId, authHandler.GocSession);

                    }
                    else
                    {
                        Console.WriteLine($"Falha ao abrir chamado no TopDesk. Pulando chamado ID: {chamadoId}");
                        continue;
                    }

                    Console.WriteLine($"Chamado ID: {chamadoId} processado com sucesso.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao processar chamado: {ex.Message}");
                    await Exceptions.HandleErrorAsync(ex);
                }
            }
            // Logar os chamados processados
            string timestampFinal = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            LogHandler.LogChamados(timestampFinal, totalNaoFinalizados, totalPendentes, detalhesChamados);

            Console.WriteLine("Processamento de chamados concluído.");
        }
        catch (Exception ex)
        {
            string errorMessage = $"Erro ao processar chamado: {ex.Message}";
            Console.WriteLine(errorMessage);
            LogHandler.LogChamados(DateTime.Now.ToString("yyyyMMdd_HHmmss"), 0, 0, errorMessage);
            await Exceptions.HandleErrorAsync(ex);
        }
        finally
        {
            isRunning = false; // Sinaliza que a execução terminou
        }
    }
}


