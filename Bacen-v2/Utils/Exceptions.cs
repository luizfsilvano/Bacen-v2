using System;
using System.Threading.Tasks;

namespace Bacen_v2.Utils
{
    internal static class Exceptions
    {
        public static async Task HandleErrorAsync(Exception ex, string chamadoId, string titulo, string categoria, string status)
        {
            try
            {
                // Formatar mensagem de erro detalhada
                string errorMessage = $"Erro: {ex.Message}\nStackTrace:\n{ex.StackTrace}";

                // Construir detalhes do chamado
                string detalhesErro = LogHandler.FormatarDetalhesChamado(
                    id: chamadoId ?? "N/A",
                    titulo: titulo ?? "Desconhecido",
                    categoria: categoria ?? "Desconhecida",
                    status: status,
                    erroDetalhe: errorMessage
                );

                // Registrar o log do chamado com informações detalhadas
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                LogHandler.LogChamados(timestamp, 1, 0, detalhesErro, possuiErro: true);

                // Exibir erro no console
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Erro no chamado ID '{chamadoId}': {ex.Message}");
                Console.ResetColor();
            }
            catch (Exception logEx)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"Falha ao registrar o erro: {logEx.Message}");
                Console.ResetColor();
            }
        }
    }
}
