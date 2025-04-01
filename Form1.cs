using System;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using QRCoder;
using System.Drawing;

namespace Clicker
{
    public partial class Form1 : Form
    {
        private HttpListener _listener;
        private Thread _serverThread;
        private string _url;

        public Form1()
        {
            InitializeComponent();
            StartServer();
            ShowQRCode();
        }

        private void StartServer()
        {
            // Получаем локальный IP-адрес
            string ip = GetLocalIPAddress();
            _url = $"http://{ip}:5000/control";

            // Запускаем HTTP-сервер в отдельном потоке
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://*:5000/");
            _listener.Start();

            _serverThread = new Thread(ListenForRequests);
            _serverThread.Start();
        }

        private void ShowQRCode()
        {
            // Генерируем QR-код с URL
            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrData = qrGenerator.CreateQrCode(_url, QRCodeGenerator.ECCLevel.Q);
            QRCode qrCode = new QRCode(qrData);
            Bitmap qrImage = qrCode.GetGraphic(20);

            // Отображаем QR-код в PictureBox
            var pictureBox = new PictureBox
            {
                Image = qrImage,
                SizeMode = PictureBoxSizeMode.StretchImage,
                Dock = DockStyle.Fill
            };

            this.Text = "Presentation Remote - Scan QR Code";
            this.Controls.Add(pictureBox);
            this.Size = new Size(400, 400);
        }

        private string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with IPv4 address");
        }

        private void ListenForRequests()
        {
            while (_listener.IsListening)
            {
                var context = _listener.GetContext();
                ProcessRequest(context);
            }
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            if (request.Url.AbsolutePath == "/control")
            {
                // Отдаём HTML-страницу с кнопками
                string html = GetHTMLPage();
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(html);

                response.ContentType = "text/html";
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            else if (request.Url.AbsolutePath == "/keypress")
            {
                // Обработка нажатий (например, /keypress?key=right)
                string key = request.QueryString["key"];
                SendKeyPress(key);

                response.StatusCode = 200;
                response.Close();
            }
            else
            {
                response.StatusCode = 404;
                response.Close();
            }
        }

        private void SendKeyPress(string key)
        {
            // Используем SendWait вместо Send
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
        body {
            font-family: Arial, sans-serif;
            margin: 0;
            padding: 0;
            height: 100vh;
            display: flex;
            flex-direction: column;
        }
        
        .top-row {
            display: flex;
            height: 50vh;
        }
        
        .nav-button {
            flex: 1;
            font-size: 10vw;
            border: none;
            background-color: #4CAF50;
            color: white;
            cursor: pointer;
            display: flex;
            align-items: center;
            justify-content: center;
            transition: background-color 0.3s;
        }
        
        .nav-button:hover {
            background-color: #45a049;
        }
        
        #prev-btn {
            margin-right: 2px;
        }
        
        #next-btn {
            margin-left: 2px;
        }
        
        #start-btn {
            height: 50vh;
            width: 100%;
            font-size: 8vw;
            background-color: #f44336;
        }
        
        #start-btn:hover {
            background-color: #d32f2f;
        }
        
        .arrow {
            font-size: 15vw;
        }
    </style>
</head>
<body>
    <div class=""top-row"">
        <button id=""prev-btn"" class=""nav-button"" onclick=""sendKey('left')"">
            <span class=""arrow"">Prev</span>
        </button>
        <button id=""next-btn"" class=""nav-button"" onclick=""sendKey('right')"">
            <span class=""arrow"">Next</span>
        </button>
    </div>
    <button id=""start-btn"" onclick=""sendKey('f5')"">START PRESENTATION (F5)</button>

    <script>
        function sendKey(key) {
            fetch(`/keypress?key=${key}`)
                .then(response => console.log('Key sent:', key))
                .catch(err => console.error('Error:', err));
        }
    </script>
</body>
</html>";
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _listener.Stop();
            _serverThread.Join();
            base.OnFormClosing(e);
        }
    }
}
