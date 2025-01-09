using Microsoft.Playwright;

namespace Bacen_v2.Handlers
{
    public class SearchHandler
    {
        private readonly TopDeskAuth _auth;
        private readonly string _baseUrl;

        public SearchHandler(TopDeskAuth auth, dynamic config)
        {
            _auth = auth;
            _baseUrl = config.TopDesk.BaseUrl;
        }

        public string GerarIdentificador(int numeroChamado)
        {
            var random = new Random();
            string dia = DateTime.Now.ToString("dd");
            string mes = DateTime.Now.ToString("MM");
            string ano = DateTime.Now.ToString("yy");

            return $"A{ano}{mes}{dia}-{numeroChamado}";
        }

        public async Task<string> PesquisarChamadoAsync(string identificador)
        {
            try
            {
                var page = await _auth.GetAuthenticatedPageAsync(); // Obtém a página autenticada
                string searchUrl = $"{_baseUrl}/tas/public/ssp/content/search?q={identificador}";

                Console.WriteLine($"Iniciando pesquisa pelo identificador: {identificador}");
                await page.GotoAsync(searchUrl);

                // Aguarda até que os resultados sejam carregados
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                // Capturar resultados visíveis na página
                var results = await page.ContentAsync();

                // Procurar número do protocolo do chamado pelo regex
                var regex = new System.Text.RegularExpressions.Regex(@"I\d{4}-\d{6}");
                var match = regex.Match(results);
                if (match.Success)
                {
                    string numeroProtocolo = match.Value;
                    Console.WriteLine($"Número do protocolo encontrado: {numeroProtocolo}");
                    return numeroProtocolo;
                }
                else
                {
                    Console.WriteLine("Número do protocolo não encontrado pelo identificador único.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}
