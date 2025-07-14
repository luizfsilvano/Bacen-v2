using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Bacen_v2.Utils
{
    internal class Historic
    {
        // Define o caminho do arquivo CSV dentro do diretório "data" no diretório base do projeto
        private static readonly string CsvFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "historic.csv");

        public static void AddRecord(DateTime dateTime, string ticket, string chamado, string uniqueId, string procedure = "abertura")
        {
            // Verifica se o diretório "data" existe; se não, cria o diretório
            var dataDirectory = Path.GetDirectoryName(CsvFilePath);
            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }

            // Verifica se o arquivo CSV já existe
            bool fileExists = File.Exists(CsvFilePath);

            // Se o arquivo não existir, cria o cabeçalho
            if (!fileExists)
            {
                var header = "DataHora;Ticket;Chamado;IdentificadorUnico;Procedimento;TotalChamadosAbertos";
                File.WriteAllText(CsvFilePath, header + Environment.NewLine, Encoding.UTF8);
            }

            // Lê todas as linhas do arquivo CSV para calcular o total de chamados abertos
            var lines = File.ReadAllLines(CsvFilePath, Encoding.UTF8).ToList();

            // Calcula o total de chamados abertos até o momento
            int totalChamadosAbertos = lines.Count - 1; // Subtrai o cabeçalho

            // Formata a data e hora para o formato desejado
            string formattedDateTime = dateTime.ToString("yyyy-MM-dd HH:mm:ss");

            // Incrementa o total de chamados abertos
            totalChamadosAbertos++;

            // Cria a linha do CSV com os dados fornecidos
            var newLine = $"{EscapeCsvValue(formattedDateTime)};{EscapeCsvValue(ticket)};{EscapeCsvValue(chamado)};{EscapeCsvValue(uniqueId)};{EscapeCsvValue(procedure)};{EscapeCsvValue(totalChamadosAbertos.ToString())}";

            // Adiciona a nova linha ao arquivo CSV
            File.AppendAllText(CsvFilePath, newLine + Environment.NewLine, Encoding.UTF8);
        }

        // Função para escapar valores CSV (se contiverem vírgulas ou quebras de linha)
        private static string EscapeCsvValue(string value)
        {
            if (value.Contains(",") || value.Contains("\n") || value.Contains("\""))
            {
                // Se o valor contiver vírgulas, quebras de linha ou aspas, coloca entre aspas e duplica as aspas internas
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }
            return value;
        }
    }
}