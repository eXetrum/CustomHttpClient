using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CustomHttpClient
{
    class StateObject
    {
        // Client socket.
        public Socket workSocket = null;
        // Size of receive buffer.
        public const int BufferSize = 256;
        // Receive buffer.
        public byte[] buffer = new byte[BufferSize];
        // Received data string.
        //public StringBuilder sb = new StringBuilder();

        public List<byte> receivedData = new List<byte>();
    }
    // Класс клиента используем для обмена приватными сообщениями
    public class Client
    {
        // Признак разделения заголовков
        public static string CRLF = "\r\n";
        // Версия используемого протокола
        static string ProtocolVersion = "HTTP/1.1";

        private ManualResetEvent connectDone = new ManualResetEvent(false);
        private ManualResetEvent sendDone = new ManualResetEvent(false);
        private ManualResetEvent receiveDone = new ManualResetEvent(false);
        private ManualResetEvent disconnectDone = new ManualResetEvent(false);
        

        // Хендлеры событий: "данные получены", "клиент отсоединен"
        //////////////// Делегаты событий ////////////////
        // "Данные отправлены"
        public delegate void DataSendEventHandler(Client sender, string data);
        // "Данные получены"
        public delegate void DataReceivedEventHandler(Client sender, string data);        
        // "Соединение разорвано"
        public delegate void DisconnectedEventHandler(Client sender);

        public event DisconnectedEventHandler Disconnected;        
        public event DataReceivedEventHandler Received;
        public event DataSendEventHandler Sended;

        // Используемый порт
        public int Port { get; private set; }
        // Хост сервера, корень сервера
        public string Host { get; private set; }
        public Uri RequestUri {get; private set;}
        // Метод запроса GET/POST
        public string Method { get; set; }
        // Заголовок юзерагента (использовать чтобы представится для сервера одним из браузеров Mozzila, Opera, Chrome, etc...)
        public string UserAgent { get; set; }
        // Реферер - откуда перешели на текущую страницу
        public string Referer { get; set; }
        // Заголовок позволяет сообщить серверу какой тип данных мы можем принимать
        public string Accept { get; set; }

        // Коллекция заголовков 
        public Dictionary<String, String> Headers { get; set; }
        // Клиентский сокет
        private Socket clientSocket;
        public IPAddress IP;
        private Encoding encoding;
        //private static Response response;

        public static string AcceptPage = "text/html,application/xhtml+xml,text/plain,application/xml;q=0.9";//,*/*;q=0.8";
        public static string AcceptImage = "image/gif, image/jpeg, image/pjpeg, image/png, */*";
        // Получить объект IPAddress по url строке или по строке айпи адреса
        public static IPAddress GetIpAddress(string tryParseThis)
        {
            try { return Dns.GetHostEntry(IPAddress.Parse(tryParseThis)).AddressList[0]; }
            catch { }

            try { return Dns.GetHostEntry(tryParseThis).AddressList[0]; }
            catch { }

            return null;
        }

        // Конструктор клиента, принимает адрес удаленного узла и используемый сервером порт (по умолчанию задаем 80)
        public Client(Uri remoteUri, int port = 80)
        {
            // Запоминаем порт
            Port = port;
            // По умолчанию задаем метод для выполнения запроса типа GET
            Method = "GET";
            // Запоминаем адрес 
            Host = remoteUri.Host;
            RequestUri = remoteUri;
            // Аксецптим такой набор
            Accept = AcceptPage;
            // Выделяем память под будущие заголовки
            Headers = new Dictionary<string, string>();

            Headers.Add("Connection", "Close");
            //Headers.Add("Connection", "Keep-Alive");
            Headers.Add("Accept-Encoding", "gzip;q=0,deflate,sdch");

            //Headers.Add("Accept-Charset", "utf-8;");
            //Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
            //Headers.Add("Pragma", "no-cache");
            //Headers.Add("Expires", "0");

            IPHostEntry ipHostInfo = Dns.GetHostEntry(Host);
            // Получаем ип аддресс связанный с хостом
            IP = ipHostInfo.AddressList[0];
            // Кодировка
            encoding = Encoding.UTF8;
        }        

        
        public Response MakeRequest(Uri uri, bool page = true)
        {
            // По умолчанию задаем кодировку
            encoding = Encoding.UTF8;
            // Содаем объект "Ответ сервер"
            Response response = new Response();
            // Создаем новые события
            sendDone = new ManualResetEvent(false);
            disconnectDone = new ManualResetEvent(false);
            // Создаем потоковый TCP сокет
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //clientSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 30000);
            IPAddress ipAddress = GetIpAddress(uri.Host);
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, Port);

            Console.WriteLine("Connect to [" + remoteEP.ToString() + "] ...");

            try
            {
                // Соединение
                clientSocket.Connect(remoteEP);
                // Формирование заголовков для отправки
                string headers = string.Empty;
                // Если запрашиваем страницу
                if (page)
                {
                    headers = 
                        "GET" + " " + uri.PathAndQuery + " " + ProtocolVersion + CRLF
                        + "Host: " + uri.Host + CRLF
                        + "Accept: " + AcceptPage + CRLF;
                }
                    // Если запросили изображение
                else
                {
                    headers =
                        "HEAD" + " " + uri.PathAndQuery + " " + ProtocolVersion + CRLF
                        + "Host: " + uri.Host + CRLF
                        + "Accept: " + AcceptImage + CRLF;
                }
                // Если заданы поля юзер агент и рефер - добавляем к заголовкам
                if (UserAgent != null)
                    headers += "User-Agent: " + UserAgent + CRLF;
                if (Referer != null)
                    headers += "Referer: " + Referer + CRLF;


                foreach (var h in Headers)
                    headers += h.Key + ": " + h.Value + CRLF;
                headers += CRLF;
                Console.WriteLine("Send headers...");
                Console.WriteLine(new string('-', 40));
                Console.WriteLine(headers);
                Console.WriteLine(new string('-', 40));
                // Отправка запроса
                Send(clientSocket, headers);
                // Ожидаем завершения отправки
                Console.WriteLine("Send waitone...");
                sendDone.WaitOne();
                // Буфер приема
                byte[] buffer = new byte[1];
                // Полны
                List<byte> headerData = new List<byte>();
                int byteRead = 0;
                Console.WriteLine(new string('-', 40));
                Console.WriteLine("Start receive data...");                
                // Читаем заголовки пока не встретим двойной CRLF
                while ((byteRead = clientSocket.Receive(buffer)) >= 0)
                {
                    headerData.AddRange(buffer);
                    if (FindEOF(headerData.ToArray(), new byte[] { 13, 10, 13, 10 }) != -1)
                        break;
                }

                // Добавляем заголовки к ответу
                response.AddHeaders(encoding.GetString(headerData.ToArray()));


                if (response.Headers.ContainsKey("Content-Type".ToLower()))
                {
                    string header = response.Headers["Content-Type".ToLower()];
                    int indx = header.IndexOf("charset=");
                    if (indx != -1)
                    {
                        string charset = header.Substring(indx + "charset=".Length);
                        Console.WriteLine("charset=" + charset);
                        try
                        {
                            encoding = Encoding.GetEncoding(charset);
                            Console.WriteLine(encoding.EncodingName);
                            //Console.ReadKey();
                        }
                        catch (Exception errorEncoding)
                        {
                            Console.WriteLine("MakeRequest, getcharset error=" + errorEncoding.Message);
                        }
                    }
                }

                Console.WriteLine("Received headers size=" + headerData.Count);
                Console.WriteLine(new string('-', 40));
                Console.WriteLine(encoding.GetString(headerData.ToArray()));

                if (response.Headers.ContainsKey("Content-Type".ToLower()) && response.Headers["Content-Type".ToLower()].Equals("application/pdf"))
                    return null;
                // Если запрашиваем изображения - на данном этапе заголовки получены из них вытащим размер изображения
                // Так что тело ответа не используем
                if (!page)
                    return response;
                // Если запрос страницы
                List<byte> bodyData = new List<byte>();
                // Читаем данные страницы с помощью сетевого потока
                NetworkStream ns = new NetworkStream(clientSocket);
                int b = 0;
                while ((b = ns.ReadByte()) != -1)
                    bodyData.Add((byte)b);
                // Закрываем поток
                ns.Close();
                // Добавляем к результату тело ответа используя кодировку UTF-8
                response.AddBody(encoding.GetString(bodyData.ToArray()));
                Console.WriteLine(new string('-', 40));
                Console.WriteLine("Received body size=" + bodyData.Count);
                Console.WriteLine(new string('-', 40));
                // Если задан обработчик события - вызываем                                   
                if (Received != null)
                    // Передаем только заголовки
                    Received(this, response.RawHeaders());

                //Receive(clientSocket);
                //receiveDone.WaitOne();

                // Возвращаем результат
                return response;

            }
            catch (Exception ex)
            {
                Console.WriteLine("Inside Client, MakeRequest error=" + ex.Message);
                connectDone.Set();
                sendDone.Set();
                receiveDone.Set();
            }
            finally
            {
                // По завершению закрываем сокет
                Close();
            }
            // Если произошла ошибка и мы не вернули ранее ответ - вернем null
            return null;
        }
        // Поиск разделителя (Используем для поиска CRLF, или CRLF + CRLF)
        int FindEOF(byte[] data, byte [] eof)
        {
            for (int i = 0; i < data.Length - eof.Length + 1; ++i)
            {
                int count = 0;
                for (int j = 0; j < eof.Length; ++j)
                    if (data[i + j] == eof[j]) count++;
                    else break;
                if (count == eof.Length) return i;
            }
            // Разделитель не найден
            return -1;
        }

        private void Send(Socket client, String data)
        {
            // Кодируем строку используя выбранную кодировку и преобразуем в массив байт
            byte[] byteData = encoding.GetBytes(data);
            // Начинаем отправку данных
            client.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), client);
        }

        private void SendCallback(IAsyncResult ar)
        {
            // Получаем клиентский сокет
            Socket client = (Socket)ar.AsyncState;
            // Завершаем отправку данных
            int bytesSent = client.EndSend(ar);
            // Выводим количество отправленных данных
            Console.WriteLine("Sent {0} bytes to server.", bytesSent);
            // Если задан обработчик события "данные получены"
            if (Sended != null)
            {
                // Собираем заголовки
                string rawHeaders = string.Empty;
                foreach (var header in Headers)
                    rawHeaders += header.Key + ": " + header.Value + CRLF;
                rawHeaders += CRLF;
                // Передаем для дальнейшего вывода 
                Sended(this, "Response headers: " + Environment.NewLine + rawHeaders);
            }
            // Сигнализируем о завершении отправки данных
            sendDone.Set();
        }

        public void Close()
        {
            try
            {
                if (clientSocket != null)
                {
                    //sendDone.WaitOne();
                    //receiveDone.WaitOne();                
                    clientSocket.Shutdown(SocketShutdown.Both);
                    clientSocket.BeginDisconnect(true, new AsyncCallback(DisconnectCallback), clientSocket);
                    disconnectDone.WaitOne();
                    clientSocket = null;
                }
            }
            catch {}
        }

        private void DisconnectCallback(IAsyncResult ar)
        {
            try
            {
                // Завершаем отключение
                Socket client = (Socket)ar.AsyncState;
                client.EndDisconnect(ar);
                // Если задан обработчик события "отсоединен"
                if (Disconnected != null)
                {
                    Disconnected(this);
                    Console.WriteLine("Disconnected");
                    Console.WriteLine(new string('-', 40));
                }
                // Сигнализируем завершение разрыва соединения
                disconnectDone.Set();
            }
            catch {}
        } 
    }

}