using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using QRCoder;
using System.Drawing;
using System.Security.Principal;

namespace Clicker
{
    public partial class Form1 : Form
    {
        private HttpListener _listener;
        private Thread _serverThread;
        private string _url;
        private bool _isRunning;
        private const int PORT = 5000;
        private string _currentToken;
        private PictureBox _pictureBox;
        private Button _refreshButton;

        public Form1()
        {
            InitializeComponent();
            this.FormClosing += MainForm_FormClosing;
            this.MaximizeBox = false;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using (System.IO.Stream stream = assembly.GetManifestResourceStream("Clicker.icon.ico"))
            {
                if (stream != null)
                {
                    this.Icon = new Icon(stream);
                }
            }

            // Генерируем начальный токен
            _currentToken = GenerateRandomToken();

            // Проверяем и добавляем правило брандмауэра
            if (!IsFirewallRuleSet())
            {
                AddFirewallRule();
            }

            StartServer();
            InitializeUI();
        }

        private void InitializeUI()
        {
            // Настройка PictureBox для QR-кода
            _pictureBox = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.StretchImage,
                Dock = DockStyle.Fill
            };

            // Кнопка обновления
            _refreshButton = new Button
            {
                Text = "Обновить код",
                Dock = DockStyle.Bottom,
                Height = 40,
                Font = new Font("Arial", 10)
            };
            _refreshButton.Click += RefreshButton_Click;

            // Добавляем элементы на форму
            this.Controls.Add(_pictureBox);
            this.Controls.Add(_refreshButton);

            // Генерируем и показываем QR-код
            UpdateQRCode();

            this.Text = $"Clicker - {_url}";
            this.Size = new Size(400, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            _currentToken = GenerateRandomToken();
            UpdateQRCode();
            
        }

        private string GenerateRandomToken()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString(); // 6-значный код
        }

        private void UpdateQRCode()
        {
            try
            {
                string urlWithToken = $"{_url}?token={_currentToken}";
                QRCodeGenerator qrGenerator = new QRCodeGenerator();
                QRCodeData qrData = qrGenerator.CreateQrCode(urlWithToken, QRCodeGenerator.ECCLevel.Q);
                QRCode qrCode = new QRCode(qrData);
                Bitmap qrImage = qrCode.GetGraphic(20);
                _pictureBox.Image = qrImage;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка генерации QR-кода: {ex.Message}");
            }
        }

        private bool IsFirewallRuleSet()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = "advfirewall firewall show rule name=\"Clicker (Presentation Remote)\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                return output.Contains("Clicker (Presentation Remote)");
            }
            catch
            {
                return false;
            }
        }

        private void AddFirewallRule()
        {
            try
            {
                

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = $"advfirewall firewall add rule name=\"Clicker (Presentation Remote)\" dir=in action=allow protocol=TCP localport={PORT}",
                        Verb = "runas",
                        UseShellExecute = true,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                process.WaitForExit(2000);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка добавления правила брандмауэра: {ex.Message}");
            }
        }

        

        private void StartServer()
        {
            try
            {
                string ip = GetLocalIPAddress();
                _url = $"http://{ip}:{PORT}/control";

                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://*:{PORT}/");
                _listener.Start();

                _isRunning = true;
                _serverThread = new Thread(ListenForRequests)
                {
                    IsBackground = true
                };
                _serverThread.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска сервера: {ex.Message}");
                this.Close();
            }
        }


        

        private string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
                return "127.0.0.1"; // Возвращаем localhost если не нашли IP
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        private void ListenForRequests()
        {
            while (_isRunning)
            {
                try
                {
                    var context = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(ProcessRequest, context);
                }
                catch (HttpListenerException) { /* Игнорируем ошибки при остановке */ }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка в ListenForRequests: {ex.Message}");
                }
            }
        }

        private void ProcessRequest(object state)
        {
            var context = (HttpListenerContext)state;
            try
            {
                var request = context.Request;
                var response = context.Response;

                // Проверяем токен безопасности
                string token = request.QueryString["token"];
                if (string.IsNullOrEmpty(token) || token != _currentToken)
                {
                    SendEmptyResponse(response, 403); // Forbidden
                    return;
                }

                if (request.Url.AbsolutePath == "/control")
                {
                    SendHtmlResponse(response, GetHTMLPage());
                }
                else if (request.Url.AbsolutePath == "/keypress")
                {
                    string key = request.QueryString["key"];
                    this.Invoke((MethodInvoker)delegate { SendKeyPress(key); });
                    SendEmptyResponse(response, 200);
                }
                else
                {
                    SendEmptyResponse(response, 404);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка обработки запроса: {ex.Message}");
            }
            finally
            {
                context.Response.Close();
            }
        }


        private void SendHtmlResponse(HttpListenerResponse response, string html)
        {
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(html);
            response.ContentType = "text/html";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        private void SendEmptyResponse(HttpListenerResponse response, int statusCode)
        {
            response.StatusCode = statusCode;
            response.ContentLength64 = 0;
        }

        private void SendKeyPress(string key)
        {
            try
            {
                switch (key)
                {
                    case "down":
                        SendKeys.SendWait("{DOWN}");
                        break;
                    case "up":
                        SendKeys.SendWait("{UP}");
                        break;                    
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка отправки клавиши: {ex.Message}");
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                _isRunning = false;

                if (_listener != null && _listener.IsListening)
                {
                    _listener.Stop();
                }

                if (_serverThread != null && _serverThread.IsAlive)
                {
                    if (!_serverThread.Join(1000))
                    {
                        _serverThread.Abort();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при закрытии: {ex.Message}");
            }
        }

        private string GetHTMLPage()
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <title>Presentation Remote</title>
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <meta charset=""utf-8"">
    <style>
        * {{
            box-sizing: border-box;
            margin: 0;
            padding: 0;
        }}
        
        body {{
            font-family: Arial, sans-serif;
            height: 100vh;
            display: flex;
            flex-direction: column;
            background-color: #f5f5f5;
        }}
        
        .nav-button {{
            flex: 1;
            font-size: 24px;
            border: none;
            color: white;
            cursor: pointer;
            display: flex;
            align-items: center;
            justify-content: center;
            transition: all 0.2s;
            user-select: none;
            width: 100%;
            padding: 20px;
        }}
        
        .nav-button:active {{
            transform: scale(0.98);
            box-shadow: inset 0 0 10px rgba(0,0,0,0.3);
        }}
        
        #next-btn {{
            background-color: #4CAF50;
            height: 50vh;
        }}
        
        #prev-btn {{
            background-color: #2196F3;
            height: 50vh;
        }}
        
        @media (max-width: 600px) {{
            .nav-button {{
                font-size: 18px;
            }}
        }}
    </style>
</head>
<body>
    <button id=""next-btn"" class=""nav-button"" onclick=""sendKey('down')"">
        Следующий слайд
    </button>
    <button id=""prev-btn"" class=""nav-button"" onclick=""sendKey('up')"">
        Предыдущий слайд
    </button>

    <script>
        function sendKey(key) {{
            fetch(`/keypress?key=${{key}}&token={_currentToken}`)
                .then(response => console.log('Key sent:', key))
                .catch(err => console.error('Error:', err));
        }}
    </script>
</body>
</html>";
        }
    }
}