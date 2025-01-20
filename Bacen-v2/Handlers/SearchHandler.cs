using Microsoft.Playwright;
using System.Security.Cryptography;

namespace Bacen_v2.Handlers
{
    public class SearchHandler
    {
        private readonly TopDeskAuth _auth;
        private readonly string _baseUrl;
        private static readonly Random random = new Random();

        public SearchHandler(TopDeskAuth auth, dynamic config)
        {
            _auth = auth;
            _baseUrl = config.TopDesk.BaseUrl;
        }

        public string GerarIdentificador(int numeroChamado)
        {
            // Gerar n�meros aleat�rios
            byte[] randomNumber = new byte[4];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
            }

            int randomValue = BitConverter.ToInt32(randomNumber, 0) & 0x7FFFFFFF;


            string dia = DateTime.Now.ToString("dd");
            string mes = DateTime.Now.ToString("MM");
            string ano = DateTime.Now.ToString("yy");

            return $"A{ano}{mes}{dia}-{numeroChamado}{randomValue % 9000 + 1000}";
        }

        public async Task<string> PesquisarChamadoAsync(string identificador, string numeroChamado)
        {
            try
            {
                var page = await _auth.GetAuthenticatedPageAsync(); // Obt�m a p�gina autenticada
                string searchUrl = $"{_baseUrl}/tas/public/ssp/content/search?q={identificador}";

                Console.WriteLine($"Iniciando pesquisa pelo identificador: {identificador}");
                await page.GotoAsync(searchUrl);

                // Aguarda at� que os resultados sejam carregados
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                await Task.Delay(5000);

                // Simular Ctrl+A, Ctrl+C
                await page.Keyboard.PressAsync("Control+a");
                await page.Keyboard.PressAsync("Control+c");

                // Recuperar o conte�do da �rea de transfer�ncia
                var results = await page.EvaluateAsync<string>("() => navigator.clipboard.readText()");

                // Procurar n�mero do protocolo do chamado pelo regex
                var protocolRegex = new System.Text.RegularExpressions.Regex(@"I\d{4}-\d{6}");
                var protocolMatch = protocolRegex.Match(results);
                if (protocolMatch.Success)
                {
                    string numeroProtocolo = protocolMatch.Value;
                    Console.WriteLine($"N�mero do protocolo encontrado: {numeroProtocolo}");
                    // Verificar se o n�mero do chamado (#67644) est� correto
                    var chamadoRegex = new System.Text.RegularExpressions.Regex(@"#\d{5}");
                    var chamadoMatch = chamadoRegex.Match(results);

                    if (chamadoMatch.Success)
                    {
                        string numeroChamadoEncontrado = chamadoMatch.Value.Trim('#'); // Remove o "#" do n�mero
                        if (numeroChamadoEncontrado == numeroChamado)
                        {
                            Console.WriteLine($"N�mero do chamado verificado com sucesso: {numeroChamadoEncontrado}");
                            return numeroProtocolo;
                        }
                        else
                        {
                            Console.WriteLine($"N�mero do chamado n�o corresponde. Esperado: {numeroChamado}, Encontrado: {numeroChamadoEncontrado}");
                            return null;
                        }
                    }
                    else
                    {
                        Console.WriteLine("N�mero do chamado n�o encontrado no conte�do capturado.");
                        return null;
                    }
                }
                else
                {
                    Console.WriteLine("N�mero do protocolo n�o encontrado pelo identificador �nico.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao pesquisar chamado: {ex.Message}");
                throw;
            }
        }
    }
}
