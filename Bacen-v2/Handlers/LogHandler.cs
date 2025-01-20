using System;
using System.IO;

namespace Bacen_v2.Utils
{
    public static class LogHandler
    {
        private static string LogsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "logs");

        public static void LogChamados(string timestamp, int totalEncaminhados, int totalNaoAbertos, string detalhesChamados, bool possuiErro)
        {
            try
            {
                // Criar diretório se não existir
                Directory.CreateDirectory(LogsDirectory);

                // Adicionar "-ERRO" no nome do arquivo, se possuir erro
                string errorSuffix = possuiErro ? "-ERRO" : string.Empty;
                string logFileName = $"log_{timestamp}{errorSuffix}.log";
                string logFilePath = Path.Combine(LogsDirectory, logFileName);

                using (StreamWriter sw = new StreamWriter(logFilePath))
                {
                    // Cabeçalho
                    sw.WriteLine($"Iniciando recuperação de Tickets Bacen para Topdesk ({DateTime.Now:dd/MM/yyyy HH:mm:ss})");
                    sw.WriteLine(" --------------------------------------------------");
                    sw.WriteLine($"Total de chamados com status 'Encaminhado N2 Atendimento': {totalEncaminhados}");
                    sw.WriteLine($"Total de chamados não processados: {totalNaoAbertos}");
                    sw.WriteLine("---------------------------------------------------");

                    // Adicionar detalhes dos chamados
                    if (!string.IsNullOrWhiteSpace(detalhesChamados))
                    {
                        sw.WriteLine("\nDetalhes dos chamados:");
                        sw.WriteLine("---------------------------------------------------");
                        sw.WriteLine(detalhesChamados);
                    }

                    // Rodapé
                    sw.WriteLine("\n****************** Fim da Execução *****************");
                }

                Console.WriteLine($"Log salvo em: {logFilePath}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Erro ao salvar log: {ex.Message}");
                Console.ResetColor();
            }
        }

        public static string FormatarDetalhesChamado(string id, string titulo, string categoria, string status, string erroDetalhe = null)
        {
            var detalhe = $"ID: {id}, Título: {titulo}, Categoria: {categoria}, Status: {status}";
            if (!string.IsNullOrEmpty(erroDetalhe))
            {
                detalhe += $", Detalhes do Erro: {erroDetalhe}";
            }

            return detalhe;
        }
    }
}
