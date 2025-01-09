using Microsoft.Playwright;

namespace Bacen_v2.Handlers
{
    public class TopDeskAuth
    {
        private readonly dynamic _config;
        private readonly string _baseUrl;
        private readonly IBrowser _browser;
        private IPage _authenticatedPage;

        public TopDeskAuth(dynamic config, IBrowser browser)
        {
            _config = config;
            _baseUrl = _config.TopDesk.BaseUrl;
            _browser = browser;
        }

        public async Task LoginAsync()
        {
            try
            {
                // Obter credenciais do arquivo de configuração
                var _username = (string)_config.TopDesk.Username;
                var _password = (string)_config.TopDesk.Password;

                var context = await _browser.NewContextAsync(new BrowserNewContextOptions
                {
                    Permissions = new[] { "clipboard-read", "clipboard-write" }
                });

                _authenticatedPage = await context.NewPageAsync();

                // Navega até a página de login
                await _authenticatedPage.GotoAsync($"{_baseUrl}/tas/public/login/form");

                // Preenche os campos de usuário e senha
                await _authenticatedPage.FillAsync("input#loginname", _username);
                await _authenticatedPage.FillAsync("input#password", _password);

                // Clica no botão de login
                await _authenticatedPage.ClickAsync("input#login");

                // Sleep para aguardar o redirecionamento
                await Task.Delay(15000);

                // Aguarda um indicador de sucesso na página
                var loggedIn = await _authenticatedPage.Locator("text=Seja bem-vindo(a)").IsVisibleAsync();

                if (!loggedIn)
                {
                    throw new Exception("Falha no login. Verifique as credenciais.");
                }

                Console.WriteLine("Login realizado com sucesso!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao realizar login no TopDesk: {ex.Message}");
                throw;
            }
        }

        public async Task<IPage> GetAuthenticatedPageAsync()
        {
            if (_authenticatedPage == null)
            {
                throw new InvalidOperationException("Nenhuma sessão autenticada encontrada. Realize o login primeiro.");
            }

            return _authenticatedPage;
        }

        public async Task EncerrarSessaoAsync()
        {
            if (_browser != null)
            {
                await _authenticatedPage.Context.CloseAsync();
                _authenticatedPage = null;
                Console.WriteLine("Sessão encerrada com sucesso.");
            }
        }
    }
}
