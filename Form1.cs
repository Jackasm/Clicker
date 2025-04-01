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

        public Form1()
        {
            InitializeComponent();
            this.FormClosing += MainForm_FormClosing;
            
            // Проверяем и добавляем правило брандмауэра
            if (!IsFirewallRuleSet())
            {
                AddFirewallRule();
            }
            
            StartServer();
            ShowQRCode();
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
                        Arguments = "advfirewall firewall show rule name=\"Presentation Remote\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                return output.Contains("Presentation Remote");
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
                if (!IsAdministrator())
                {
                    MessageBox.Show("Для добавления правила брандмауэра требуются права администратора. " +
                                   "Пожалуйста, запустите программу от имени администратора.", 
                                   "Требуются права администратора", 
                                   MessageBoxButtons.OK, 
                                   MessageBoxIcon.Warning);
                    return;
                }

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = $"advfirewall firewall add rule name=\"Presentation Remote\" dir=in action=allow protocol=TCP localport={PORT}",
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

        private bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
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


        private void ShowQRCode()
        {
            try
            {
                QRCodeGenerator qrGenerator = new QRCodeGenerator();
                QRCodeData qrData = qrGenerator.CreateQrCode(_url, QRCodeGenerator.ECCLevel.Q);
                QRCode qrCode = new QRCode(qrData);
                Bitmap qrImage = qrCode.GetGraphic(20);

                var pictureBox = new PictureBox
                {
                    Image = qrImage,
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    Dock = DockStyle.Fill
                };

                this.Text = $"Presentation Remote - {_url}";
                this.Controls.Add(pictureBox);
                this.Size = new Size(400, 400);
                this.StartPosition = FormStartPosition.CenterScreen;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка генерации QR-кода: {ex.Message}");
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
                    case "right":
                        SendKeys.SendWait("{RIGHT}");
                        break;
                    case "left":
                        SendKeys.SendWait("{LEFT}");
                        break;
                    case "f5":
                        SendKeys.SendWait("{F5}");
                        break;
                    case "esc":
                        SendKeys.SendWait("{ESC}");
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
            return @"
<!DOCTYPE html>
<html>
<head>
    <title>Presentation Remote</title>
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <style>
        * {
            box-sizing: border-box;
            margin: 0;
            padding: 0;
        }
        
        body {
            font-family: Arial, sans-serif;
            height: 100vh;
            display: flex;
            flex-direction: column;
            background-color: #f5f5f5;
        }
        
        .control-panel {
            display: flex;
            height: 50vh;
            gap: 5px;
            padding: 5px;
        }
        
        .nav-button {
            flex: 1;
            font-size: 24px;
            border: none;
            border-radius: 10px;
            background-color: #4CAF50;
            color: white;
            cursor: pointer;
            display: flex;
            align-items: center;
            justify-content: center;
            transition: all 0.2s;
            user-select: none;
        }
        
        .nav-button:active {
            transform: scale(0.98);
            box-shadow: inset 0 0 10px rgba(0,0,0,0.3);
        }
        
        #prev-btn {
            background-color: #2196F3;
        }
        
        #next-btn {
            background-color: #4CAF50;
        }
        
        #start-btn {
            height: 50vh;
            margin: 5px;
            border-radius: 10px;
            font-size: 24px;
            background-color: #f44336;
        }
        
        #stop-btn {
            height: 15vh;
            margin: 5px;
            border-radius: 10px;
            font-size: 18px;
            background-color: #607D8B;
            display: none;
        }
        
        @media (max-width: 600px) {
            .nav-button, #start-btn {
                font-size: 18px;
            }
        }
    </style>
</head>
<body>
    <div class=""control-panel"">
        <button id=""prev-btn"" class=""nav-button"" onclick=""sendKey('left')"">
            Previous
        </button>
        <button id=""next-btn"" class=""nav-button"" onclick=""sendKey('right')"">
            Next
        </button>
    </div>
    <button id=""start-btn"" onclick=""sendKey('f5')"">Start Presentation (F5)</button>
    <button id=""stop-btn"" onclick=""sendKey('esc')"">Stop Presentation (ESC)</button>

    <script>
        let isPresenting = false;
        const startBtn = document.getElementById('start-btn');
        const stopBtn = document.getElementById('stop-btn');
        
        function sendKey(key) {
            if (key === 'f5') {
                isPresenting = true;
                startBtn.style.display = 'none';
                stopBtn.style.display = 'block';
            } else if (key === 'esc') {
                isPresenting = false;
                startBtn.style.display = 'block';
                stopBtn.style.display = 'none';
            }
            
            fetch(`/keypress?key=${key}`)
                .then(response => console.log('Key sent:', key))
                .catch(err => console.error('Error:', err));
        }
    </script>
</body>
</html>";
        }
    }
}