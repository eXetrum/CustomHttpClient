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
    class ChunkedStream : Stream
    {
        private readonly Stream innerStream;

        internal ChunkedStream(Stream innerStream)
        {
            if (!innerStream.CanRead)
                throw new ArgumentException();
            this.innerStream = innerStream;
        }


        int currentChunk = -1;

        int currentChunkReaded;

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (currentChunk == 0)
                return 0;

            if (currentChunk == -1 || currentChunk == currentChunkReaded)
                ReadNextChunkDeclaration();

            if (currentChunk == 0)
                return 0;

            int result = innerStream.Read(buffer, offset, Math.Min(count, currentChunk - currentChunkReaded));

            currentChunkReaded += result;
            return result;
        }

        private void ReadNextChunkDeclaration()
        {
            int result;
            while (true)
            {
                string temp = ReadLine();

                if (int.TryParse(temp.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result))
                {
                    currentChunk = result;
                    currentChunkReaded = 0;
                    return;
                }
            }
        }

        private string ReadLine()
        {
            string result = "";
            while (!result.EndsWith("\r\n"))
            {
                int b = innerStream.ReadByte();
                if (b == -1)
                    return result;
                result += (char)b;
            }
            return result.Substring(0, result.Length - 2);
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }


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

    static class Ext
    {
        public static int SubListIndex<T>(this IList<T> list, int start, IList<T> sublist)
        {
            for (int listIndex = start; listIndex < list.Count - sublist.Count + 1; listIndex++)
            {
                int count = 0;
                while (count < sublist.Count && sublist[count].Equals(list[listIndex + count]))
                    count++;
                if (count == sublist.Count)
                    return listIndex;
            }
            return -1;
        }
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
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            remoteEP = new IPEndPoint(ipAddress, Port);
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

            IPHostEntry ipHostInfo = Dns.GetHostEntry(uri.Host);
            IPAddress ipAddress = ipHostInfo.AddressList[0];
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
            catch (Exception ex) {
                //Console.ReadKey();
            }
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
            catch (Exception ex)
            {
                Console.ReadKey();
            }
        }

        // Сменить текущий каталог
        public void ChangeDir(string newDir)
        {
            //CurrentPath = newDir;
        }
        // Отправить содержимое текущей папки в которой находится пользователь
        //public void SendFolders()
        //{
        //    // Список информации о файлах и папках
        //    List<FileSystemInfo> files = new List<FileSystemInfo>();
        //    // Получаем инфо о текущем каталоге
        //    DirectoryInfo df = new DirectoryInfo(CurrentPath);
        //    // Запоминаем каталог родитель для текущего каталога
        //    files.Add(df.Parent);
        //    // Перечисляем все файлы и папки и заносим их в список
        //    foreach (var d in df.EnumerateDirectories())
        //        files.Add(d);
        //    foreach (var d in df.EnumerateFiles())
        //        files.Add(d);
        //    // Формируем данные для отправки преобразуя список файлов в массив байт
        //    byte[] data = Packet.ObjectToByteArray(files);
        //    // Создаем пакет для отправки, тип ответного сообщения "LIST" (получение списка файлов и папок)
        //    Packet p = new Packet(MSG_TYPE.LIST, data);
        //    // Отправляем пакет
        //    SendData(p);
        //}
        // Отправить данные
        //public int SendData(Packet p)
        //{
        //    // Преобразуем пакет в массив байт
        //    Byte[] data = Packet.ObjectToByteArray(p);
        //    // Отправляем размер пакета
        //    clientSocket.Send(BitConverter.GetBytes(data.Length), 0, 4, SocketFlags.None);
        //    // Отправляем тело пакета
        //    int byteSend = clientSocket.Send(data, 0, data.Length, SocketFlags.None);
        //    // Вернем количество отправленных данных
        //    return byteSend;
        //}
        // Начало приема данных всегда начинается с кусочка о размере последующих данных
        //void receiveCallback(IAsyncResult ar)
        //{
        //    try
        //    {
        //        // Получаем количество переданных данных
        //        int rec = clientSocket.EndReceive(ar);
        //        // Если передано нуль
        //        if (rec == 0)
        //        {
        //            // Отключаем клиента 
        //            if (Disconnected != null)
        //            {
        //                Disconnected(this);
        //                Close();
        //                return;
        //            }
        //            // Если размер принятых данных не равен 4 (все пересылки сообщений начинаются с их отправки 4-х байт размера этих сообщений)
        //            if (rec != 4)
        //            {
        //                throw new Exception("Error file size header");
        //            }
        //        }
        //    }
        //    // Отлавливаем ошибки
        //    catch (SocketException se)
        //    {
        //        switch (se.SocketErrorCode)
        //        {
        //            case SocketError.ConnectionAborted:
        //            case SocketError.ConnectionReset:
        //                if (Disconnected != null)
        //                {
        //                    Disconnected(this);
        //                    Close();
        //                    return;
        //                }
        //                break;
        //        }
        //    }
        //    catch (ObjectDisposedException) { return; }
        //    catch (NullReferenceException) { return; }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine("Client reciveAsync: " + ex.Message);
        //        return;
        //    }
        //    // Если размер сообщения принят без ошибок - создаем буфер приема
        //    buffer = new ReceiveBuffer(BitConverter.ToInt32(lenBuffer, 0));
        //    // Запускаем ассинхронный прием пакета данных заданного размера
        //    clientSocket.BeginReceive(buffer.Buffer, 0, buffer.Buffer.Length, SocketFlags.None, new AsyncCallback(receivePacketCallback), null);
        //}
        //// Калбек функция приема пакета
        //void receivePacketCallback(IAsyncResult ar)
        //{
        //    // Получаем размер данных
        //    int rec = clientSocket.EndReceive(ar);
        //    if (rec <= 0)
        //    {
        //        // Отключаем клиента
        //        if (Disconnected != null)
        //            Disconnected(this);
        //        // Закрываем сокет и освобождаем все неиспользуемые объекты
        //        Close(); return;
        //    }
        //    // Добавляем принятые данные в поток
        //    buffer.memStream.Write(buffer.Buffer, 0, rec);
        //    // Уменьшаем количество необходимых для приема данных
        //    buffer.ToReceive -= rec;
        //    // Если еще не все приняли
        //    if (buffer.ToReceive > 0)
        //    {
        //        // Очищаем маленький буфер приема
        //        Array.Clear(buffer.Buffer, 0, buffer.Buffer.Length);
        //        // Запускаем дальнешую процедуру приема
        //        clientSocket.BeginReceive(buffer.Buffer, 0, buffer.Buffer.Length, SocketFlags.None, receivePacketCallback, null);
        //        return;
        //    }
        //    // Если все приняли - проверим есть ли обработчик события приема
        //    if (Received != null)
        //    {
        //        // Получаем весь принятый массив байт
        //        byte[] totalReceived = buffer.memStream.ToArray();
        //        // Формируем полученный пакет, предварительно расшифровав его
        //        Packet receivedPacket = (Packet)Packet.ByteArrayToObject(totalReceived);
        //        // Генерируем событие приема
        //        Received(this, receivedPacket);
        //    }
        //    buffer.Dispose();
        //    // Принимаем данные асинхронно
        //    clientSocket.BeginReceive(lenBuffer, 0, lenBuffer.Length, SocketFlags.None, new AsyncCallback(receiveCallback), null);
        //}
        //// Метод освобождения ресурсов
        //public void Close()
        //{
        //    // Закрываем сокет
        //    if (clientSocket != null)
        //    {
        //        clientSocket.Shutdown(SocketShutdown.Both);
        //        clientSocket.Close();
        //    }
        //    // Обнуляем все переменные чтобы сборщик мусора сделал свою работу
        //    clientSocket = null;
        //    if (buffer != null)
        //        buffer.Dispose();
        //    lenBuffer = null;
        //    Disconnected = null;
        //    Received = null;
        //}
    }
}
