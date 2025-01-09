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
                var page = await _auth.GetAuthenticatedPageAsync(); // Obt�m a p�gina autenticada
                string searchUrl = $"{_baseUrl}/tas/public/ssp/content/search?q={identificador}";

                Console.WriteLine($"Iniciando pesquisa pelo identificador: {identificador}");
                await page.GotoAsync(searchUrl);

                // Aguarda at� que os resultados sejam carregados
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                // Capturar resultados vis�veis na p�gina
                var results = await page.ContentAsync();

                // Procurar n�mero do protocolo do chamado pelo regex
                var regex = new System.Text.RegularExpressions.Regex(@"I\d{4}-\d{6}");
                var match = regex.Match(results);
                if (match.Success)
                {
                    string numeroProtocolo = match.Value;
                    Console.WriteLine($"N�mero do protocolo encontrado: {numeroProtocolo}");
                    return numeroProtocolo;
                }
                else
                {
                    Console.WriteLine("N�mero do protocolo n�o encontrado pelo identificador �nico.");
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
