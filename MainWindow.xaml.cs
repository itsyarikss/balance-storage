using Newtonsoft.Json.Linq;
using System;
using System.Management;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Casik
{
    public partial class MainWindow : Window
    {
        private int balance = 1000;
        private Random random = new Random();
        private string currentBet = "";
        private string lastBet = "";
        private string lastNumber = "";

        private int MinNumber = 0;
        private int MaxNumber = 36;

        private const string Base64Token = ""; // TODO: Set your GitHub token here
        private const string WorkflowDispatchUrl = "https://api.github.com/repos/itsyarikss/balance-storage/actions/workflows/update_balance.yml/dispatches";
        private const string RawBaseUrl = "https://raw.githubusercontent.com/itsyarikss/balance-storage/main/balances/";

        private string GitHubToken => Encoding.UTF8.GetString(Convert.FromBase64String(Base64Token));
        private string hwid = "";

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += Window_Loaded;
            UpdateBalance();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                hwid = GetHWID();
                if (string.IsNullOrWhiteSpace(hwid))
                    hwid = "UNKNOWN_" + Environment.MachineName;

                await Task.Delay(1500);
                balance = await LoadBalanceFromGitHub(hwid);
                UpdateBalance();
                ResultText.Text = $"Баланс загружен: {hwid}";
                await Task.Delay(1000);
                ResetBet();
            }
            catch (Exception ex)
            {
                ResultText.Text = $"Ошибка загрузки баланса: {ex.Message}";
            }
        }

        private void UpdateBalance()
        {
            BalanceText.Text = $"На додеп осталось: ${balance}";
        }

        private void NumberInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NumberInput.Text))
            {
                NumberWarning.Text = "";
                SpinButton.IsEnabled = !string.IsNullOrEmpty(currentBet);
                return;
            }

            if (!int.TryParse(NumberInput.Text, out int number))
            {
                NumberWarning.Text = "Можно вводить только цифры!";
                SpinButton.IsEnabled = false;
                return;
            }

            if (number < MinNumber)
            {
                NumberWarning.Text = $"Число не может быть меньше {MinNumber}";
                SpinButton.IsEnabled = false;
                return;
            }

            if (number > MaxNumber)
            {
                NumberWarning.Text = $"Числа больше {MaxNumber} не существует";
                SpinButton.IsEnabled = false;
                return;
            }

            NumberWarning.Text = "";
            SpinButton.IsEnabled = !string.IsNullOrEmpty(currentBet);
        }

        private void BetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                currentBet = button.Name switch
                {
                    "RedButton" => "red",
                    "BlackButton" => "black",
                    "EvenButton" => "even",
                    "OddButton" => "odd",
                    "NumberButton" => !string.IsNullOrWhiteSpace(NumberInput.Text) ? "number:" + NumberInput.Text : "",
                    _ => ""
                };

                lastBet = currentBet;
                if (currentBet.StartsWith("number:"))
                    lastNumber = NumberInput.Text;

                SpinButton.IsEnabled = !string.IsNullOrEmpty(currentBet);
                ResultText.Text = $"Додеп на: {GetBetDescription(currentBet)}";
            }
        }

        private string GetBetDescription(string bet)
        {
            return bet switch
            {
                "red" => "Красное",
                "black" => "Черное",
                "even" => "ЧеРт(666)ное",
                "odd" => "Нечетное",
                _ when bet.StartsWith("number:") => $"Число {bet.Substring(7)}",
                _ => ""
            };
        }

        private async void SpinButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(BetAmountText.Text, out int betAmount) || betAmount <= 0 || betAmount > balance)
            {
                ResultText.Text = "У тебя нет столько, бомжик.";
                return;
            }

            SpinButton.IsEnabled = false;

            int result = random.Next(0, 37);
            bool isRed = IsRed(result);
            bool isEven = result != 0 && result % 2 == 0;

            bool win = false;
            int multiplier = 1;

            switch (currentBet)
            {
                case "red":
                    win = isRed;
                    break;
                case "black":
                    win = !isRed && result != 0;
                    break;
                case "even":
                    win = isEven;
                    break;
                case "odd":
                    win = !isEven;
                    break;
                default:
                    if (currentBet.StartsWith("number:"))
                    {
                        int betNumber = int.Parse(currentBet.Substring(7));
                        win = betNumber == result;
                        multiplier = 35;
                    }
                    break;
            }

            if (win)
            {
                balance += betAmount * multiplier;
                ResultText.Text = $"УРА СТАВОЧКА ЗАШЛА! Число: {result}. Баланс: ${balance}";
            }
            else
            {
                balance -= betAmount;
                ResultText.Text = $"Додеп не удался! Число: {result}. Баланс: ${balance}";
            }

            UpdateBalance();

            try
            {
                await UpdateBalanceOnGitHub(hwid, balance);
            }
            catch (Exception ex)
            {
                ResultText.Text += $" (Ошибка сохранения: {ex.Message})";
            }

            await Task.Delay(1300);
            ResetBetWithAutoSelect();
        }

        private void ResetBetWithAutoSelect()
        {
            currentBet = "";

            if (!string.IsNullOrEmpty(lastBet))
            {
                currentBet = lastBet;
                if (lastBet.StartsWith("number:"))
                {
                    NumberInput.Text = lastNumber;
                }

                ResultText.Text = $"Додеп на: {GetBetDescription(currentBet)}";
                SpinButton.IsEnabled = true;
            }
        }

        private void ResetBet()
        {
            currentBet = "";
            ResultText.Text = "";
        }

        private bool IsRed(int number)
        {
            int[] redNumbers = { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 };
            return Array.IndexOf(redNumbers, number) != -1;
        }

        private string GetHWID()
        {
            try
            {
                string hw = "";
                var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
                foreach (ManagementObject mo in searcher.Get())
                {
                    hw = mo["ProcessorId"]?.ToString() ?? "";
                    break;
                }

                if (string.IsNullOrWhiteSpace(hw))
                    hw = Environment.MachineName;

                hw = hw.Replace(" ", "_").Replace("\\", "_").Replace("/", "_").Replace(":", "_");
                return hw;
            }
            catch
            {
                return "UNKNOWN_" + Environment.MachineName;
            }
        }

        private async Task<int> LoadBalanceFromGitHub(string hwid)
        {
            string rawUrl = $"{RawBaseUrl}{hwid}.json";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

                try
                {
                    var text = await client.GetStringAsync(rawUrl);
                    var obj = JObject.Parse(text);
                    if (obj["balance"] != null && int.TryParse(obj["balance"].ToString(), out int b))
                        return b;
                }
                catch
                {
                }
            }

            return 1000;
        }

        private async Task UpdateBalanceOnGitHub(string hwid, int newBalance)
        {
            var body = new
            {
                @ref = "main",
                inputs = new
                {
                    hwid = hwid,
                    balance = newBalance.ToString()
                }
            };

            string json = JObject.FromObject(body).ToString();

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
                client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GitHubToken);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(WorkflowDispatchUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    var respText = await response.Content.ReadAsStringAsync();
                    throw new Exception($"GitHub dispatch failed: {(int)response.StatusCode} {response.ReasonPhrase}. {respText}");
                }
            }
        }
    }
}
