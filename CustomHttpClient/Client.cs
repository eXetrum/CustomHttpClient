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
        // "Соединение установлено"
        public delegate void ConnectionEstablishedHandler(Client sender, EndPoint remoteEP);
        // "Данные отправлены"
        public delegate void DataSendEventHandler(Client sender, string data);
        // "Данные получены"
        public delegate void DataReceivedEventHandler(Client sender, string data);        
        // "Соединение разорвано"
        public delegate void DisconnectedEventHandler(Client sender);

        public event ConnectionEstablishedHandler Connected;
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
        private IPEndPoint remoteEP;
        private Encoding encoding;

        private static Response response;

        public static string AcceptPage = "text/html,application/xhtml+xml,text/plain,application/xml;q=0.9";//,*/*;q=0.8";
        public static string AcceptImage = "image/gif, image/jpeg, image/pjpeg, image/png, */*";

        public static IPAddress GetIpAddress(string tryParseThis)
        {
            try
            {
                return Dns.GetHostEntry(IPAddress.Parse(tryParseThis)).AddressList[0];

            }
            catch { }

            try
            {
                return Dns.GetHostEntry(tryParseThis).AddressList[0];
            }
            catch { }

            return null;
        }

        public IPAddress IP;

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
            //Headers.Add("Accept-Charset", "utf-8;");
            Headers.Add("Accept-Encoding", "gzip;q=0,deflate,sdch");

            Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
            Headers.Add("Pragma", "no-cache");
            Headers.Add("Expires", "0");

            IPHostEntry ipHostInfo = Dns.GetHostEntry(Host);
            IP = ipHostInfo.AddressList[0];
            remoteEP = new IPEndPoint(IP, Port);
            // Кодировка
            encoding = Encoding.UTF8;
        }        

        
        public Response MakeRequest(Uri uri, bool page = true)
        {
            encoding = Encoding.UTF8;
            response = new Response();
            connectDone = new ManualResetEvent(false);
            sendDone = new ManualResetEvent(false);
            receiveDone = new ManualResetEvent(false);
            disconnectDone = new ManualResetEvent(false);
            
            // Создаем потоковый TCP сокет
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //clientSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 30000);
            StateObject so = new StateObject();
            so.workSocket = clientSocket;

            
            if (uri.Port == 443)
            {
                Console.WriteLine("SSL found");
                //Console.ReadKey();
            }

            //IPHostEntry ipHostInfo = Dns.GetHostEntry(uri.Host);
            //IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPAddress ipAddress = GetIpAddress(uri.Host);
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, Port);

            Console.WriteLine("Connect to [" + remoteEP.ToString() + "] ...");

            try
            {
                // Соединение
                /*clientSocket.BeginConnect(remoteEP,
                    new AsyncCallback(ConnectCallback), clientSocket);
                connectDone.WaitOne();*/
                clientSocket.Connect(remoteEP);
                // Формирование заголовков
                string headers = string.Empty;
                if (page)
                {
                    headers = 
                        "GET" + " " + uri.PathAndQuery + " " + ProtocolVersion + CRLF
                        + "Host: " + uri.Host + CRLF
                        + "Accept: " + AcceptPage + CRLF;
                }
                else
                {
                    headers =
                        "HEAD" + " " + uri.PathAndQuery + " " + ProtocolVersion + CRLF
                        + "Host: " + uri.Host + CRLF
                        + "Accept: " + AcceptImage + CRLF;
                }

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
        // Поиск разделителя
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

            return -1;
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket clientSocket = (Socket)ar.AsyncState;

                // Complete the connection.
                clientSocket.EndConnect(ar);

                Console.WriteLine("Socket connected to {0}",
                    clientSocket.RemoteEndPoint.ToString());


                if (Connected != null)
                    Connected(this, clientSocket.RemoteEndPoint);                
                // Signal that the connection has been made.
                connectDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                //Console.ReadKey();
            }
        }

        private void Send(Socket client, String data)
        {
            // Convert the string data to byte data using ASCII encoding.

            byte[] byteData = encoding.GetBytes(data);
            //byte[] byteData = Encoding.UTF8.GetBytes(data);

            // Begin sending the data to the remote device.
            client.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), client);
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket client = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.
                int bytesSent = client.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to server.", bytesSent);

                if (Sended != null)
                {
                    string rawHeaders = string.Empty;
                    foreach (var header in Headers)
                        rawHeaders += header.Key + ": " + header.Value + CRLF;
                    rawHeaders += CRLF;
                    Sended(this, rawHeaders);
                }
                // Signal that all bytes have been sent.
                sendDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                //Console.ReadKey();
            }
        }

        private void Receive(Socket client)
        {
            try
            {
                Console.WriteLine("Receive start...");
                // Create the state object.
                StateObject state = new StateObject();
                state.workSocket = client;

                // Begin receiving the data from the remote device.
                client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReceiveCallback), state);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                //Console.ReadKey();
            }
        }

        

        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the state object and the client socket 
                // from the asynchronous state object.
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.workSocket;

                // Read data from the remote device.
                int bytesRead = client.EndReceive(ar);

                if (bytesRead > 0)
                {
                    // There might be more data, so store the data received so far.
                    state.receivedData.AddRange(state.buffer);


                    //state.sb.Append(encoding.GetString(state.buffer, 0, bytesRead));

                    // Get the rest of the data.
                    client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(ReceiveCallback), state);
                }
                else
                {
                    // All the data has arrived; put it in response.
                    if (state.receivedData.Count > 1)
                    {
                        string rawData = encoding.GetString(state.receivedData.ToArray());
                        

                        response = new Response(rawData);
                        if (Received != null)
                            Received(null, response.RawHeaders());

                        //response = state.sb.ToString();
                        //Console.WriteLine("Response: " + state.sb.ToString());
                        
                    }
                    // Signal that all bytes have been received.
                    receiveDone.Set();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                //Console.ReadKey();
            }
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
                // Complete the disconnect request.
                Socket client = (Socket)ar.AsyncState;
                client.EndDisconnect(ar);

                if (Disconnected != null)
                {
                    Disconnected(this);
                    Console.WriteLine("Disconnected");
                    Console.WriteLine(new string('-', 40));
                    //connectDone.Reset();
                }
                // Signal that the disconnect is complete.
                disconnectDone.Set();
            }
            catch {}
        } 
    }

}