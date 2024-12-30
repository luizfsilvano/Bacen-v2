using System;
using System.IO;

namespace Bacen_v2.Utils
{
    public static class LogHandler
    {
        private static string LogsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Data", "logs");

        public static void LogChamados(string timestamp, int totalNaoFinalizados, int totalPendentes, string detalhesChamados)
        {
            try
            {
                // Criar diretório se não existir
                Directory.CreateDirectory(LogsDirectory);

                // Definir o nome do arquivo
                string logFileName = $"log_{timestamp}.log";
                string logFilePath = Path.Combine(LogsDirectory, logFileName);

                using (StreamWriter sw = new StreamWriter(logFilePath))
                {
                    // Escrever cabeçalho do log
                    sw.WriteLine($"Iniciando recuperação de Tickets Bacen para Topdesk ({DateTime.Now:dd/MM/yyyy HH:mm:ss})");
                    sw.WriteLine(" --------------------------------------------------");
                    sw.WriteLine($"Total de chamados nao finalizados: {totalNaoFinalizados}");
                    sw.WriteLine($"Total de chamados pendentes de abertura: {totalPendentes}");
                    sw.WriteLine("---------------------------------------------------");

                    // Adicionar detalhes dos chamados, se houver
                    if (!string.IsNullOrWhiteSpace(detalhesChamados))
                    {
                        sw.WriteLine(detalhesChamados);
                    }

                    // Finalizar log
                    sw.WriteLine("****************** Fim da Execucao *****************");
                }

                Console.WriteLine($"Log salvo em Data/logs");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao salvar log: {ex.Message}");
            }
        }
    }
}
