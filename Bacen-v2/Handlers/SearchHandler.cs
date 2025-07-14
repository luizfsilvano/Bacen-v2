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
            // Gerar números aleatórios
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
                var page = await _auth.GetAuthenticatedPageAsync(); // Obtém a página autenticada
                string searchUrl = $"{_baseUrl}/tas/public/ssp/content/search?q={identificador}";

                Console.WriteLine($"Iniciando pesquisa pelo identificador: {identificador}");
                await page.GotoAsync(searchUrl);

                // Aguarda até que os resultados sejam carregados
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                await Task.Delay(5000);


                // Espera o foco na página
                await page.BringToFrontAsync();
                await Task.Delay(1000);

                // Simular Ctrl+A, Ctrl+C
                await page.Keyboard.PressAsync("Control+a");
                await page.Keyboard.PressAsync("Control+c");

                // Recuperar o conteúdo da área de transferência
                var results = await page.EvaluateAsync<string>("() => navigator.clipboard.readText()");

                // Procurar número do protocolo do chamado pelo regex
                var protocolRegex = new System.Text.RegularExpressions.Regex(@"I\d{4}-\d{6}");
                var protocolMatch = protocolRegex.Match(results);
                if (protocolMatch.Success)
                {
                    string numeroProtocolo = protocolMatch.Value;
                    Console.WriteLine($"Número do protocolo encontrado: {numeroProtocolo}");
                    // Verificar se o número do chamado (#67644) está correto
                    var chamadoRegex = new System.Text.RegularExpressions.Regex(@"#\d{5}");
                    var chamadoMatch = chamadoRegex.Match(results);

                    if (chamadoMatch.Success)
                    {
                        string numeroChamadoEncontrado = chamadoMatch.Value.Trim('#'); // Remove o "#" do número
                        if (numeroChamadoEncontrado == numeroChamado)
                        {
                            Console.WriteLine($"Número do chamado verificado com sucesso: {numeroChamadoEncontrado}");
                            return numeroProtocolo;
                        }
                        else
                        {
                            Console.WriteLine($"Número do chamado não corresponde. Esperado: {numeroChamado}, Encontrado: {numeroChamadoEncontrado}");
                            return null;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Número do chamado não encontrado no conteúdo capturado.");
                        return null;
                    }
                }
                else
                {
                    Console.WriteLine("Número do protocolo não encontrado pelo identificador único.");
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
