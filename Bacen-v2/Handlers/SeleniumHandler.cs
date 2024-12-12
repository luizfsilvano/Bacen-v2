using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

public class SeleniumHandler
{
    private IWebDriver _driver;

    public void StartBrowser(string downloadDirectory = @"C:\Downloads")
    {
        var options = new ChromeOptions();
        options.AddUserProfilePreference("download.default_directory", downloadDirectory);
        options.AddUserProfilePreference("download.prompt_for_download", false);
        options.AddUserProfilePreference("disable-popup-blocking", true);
        options.AddUserProfilePreference("plugins.always_open_pdf_externally", true); // Abrir PDFs externamente, sem visualização no Chrome

        options.AddArguments("--start-maximized"); // Abrir navegador maximizado
        options.AddArguments("--disable-gpu");
        options.AddArguments("--no-sandbox");

        _driver = new ChromeDriver(options);

        // Criar o diretório de download se não existir
        if (!Directory.Exists(downloadDirectory))
        {
            Directory.CreateDirectory(downloadDirectory);
        }

        Console.WriteLine($"Navegador configurado para baixar arquivos em: {downloadDirectory}");
    }


    public IWebDriver GetDriver() => _driver;

    public void StopBrowser()
    {
        _driver?.Quit();
    }
}
