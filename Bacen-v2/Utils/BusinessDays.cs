using Newtonsoft.Json;

namespace Bacen_v2.Utils
{
    internal class BusinessDays
    {
        private readonly HttpClient _httpClient;

        public BusinessDays()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://brasilapi.com.br/api/feriados/v1/")
            };
        }

        public async Task<DateTime> AddBusinessDaysAsync(DateTime startDate, int businessDays, string estado = null)
        {
            // Obter feriados do ano
            var feriados = await GetHolidaysAsync(startDate.Year);

            // Lista de datas de feriados
            var holidayDates = new HashSet<DateTime>(feriados);

            DateTime currentDate = startDate;

            while (businessDays > 0)
            {
                currentDate = currentDate.AddDays(1);

                // Verificar se é dia útil
                if (IsBusinessDay(currentDate, holidayDates))
                {
                    businessDays--;
                }
            }

            return currentDate;
        }

        private async Task<List<DateTime>> GetHolidaysAsync(int year)
        {
            try
            {
                var response = await _httpClient.GetStringAsync($"{year}");
                var holidays = JsonConvert.DeserializeObject<List<Holiday>>(response);

                // Extrair as datas dos feriados
                return holidays.ConvertAll(h => DateTime.Parse(h.Date));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao obter feriados: {ex.Message}");
                return new List<DateTime>();
            }
        }

        private bool IsBusinessDay(DateTime date, HashSet<DateTime> holidayDates)
        {
            // Verifica se não é fim de semana nem feriado
            return date.DayOfWeek != DayOfWeek.Saturday &&
                   date.DayOfWeek != DayOfWeek.Sunday &&
                   !holidayDates.Contains(date.Date);
        }

        private class Holiday
        {
            public string Name { get; set; }
            public string Date { get; set; }
        }
    }
}
