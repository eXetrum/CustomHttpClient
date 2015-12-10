using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CustomHttpClient
{
    public partial class UserInterface : Form
    {
        // Прикинемся "огнелисом"
        string UserAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64; rv:44.0) Gecko/20100101 Firefox/44.0";

        Thread workrerThread = null;
        ManualResetEvent workEvent = new ManualResetEvent(false);
        ManualResetEvent threadDone = new ManualResetEvent(false);

        public UserInterface()
        {
            InitializeComponent();
            // Проверяем строку адреса и если валидная - включим кнопку запуска
            btnStart.Enabled = Client.GetIpAddress(txtBoxUrl.Text) != null; 
            txtBoxPort.ShortcutsEnabled = false;

            try
            {
                // Здаем файл лог
                tw = new StreamWriter(File.Open("log.txt", FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read));
                Directory.CreateDirectory("Logs");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Неудачное создание лог файла" + Environment.NewLine + ex.Message);
            }            
        }
        // Валидация ввода адреса
        public bool IsValidIP(string addr)
        {
            //create our match pattern
            string pattern = @"^([1-9]|[1-9][0-9]|1[0-9][0-9]|2[0-4][0-9]|25[0-5])(\.([0-9]|[1-9][0-9]|1[0-9][0-9]|2[0-4][0-9]|25[0-5])){3}$";
            //create our Regular Expression object
            Regex check = new Regex(pattern);
            //boolean variable to hold the status
            bool valid = false;
            //check to make sure an ip address was provided
            if (addr == "")
            {
                //no address provided so return false
                valid = false;
            }
            else
            {
                //address provided so use the IsMatch Method
                //of the Regular Expression object
                valid = check.IsMatch(addr, 0);
            }
            //return the results
            return valid;
        }

        TextWriter tw = null;
        // Объект синхронизации логера
        private static readonly object _syncObject = new object();
        // Метод потокобезопасного сброса в файл 
        public static void Log(string logMessage, TextWriter w)
        {
            // only one thread can own this lock, so other threads
            // entering this method will wait here until lock is
            // available.
            lock (_syncObject)
            {
                w.WriteLine("{0} {1}", DateTime.Now.ToLongTimeString(),
                    DateTime.Now.ToLongDateString());
                w.WriteLine("{0}", logMessage);
                w.WriteLine("-------------------------------");
                // Update the underlying file.
                w.Flush();
            }
        }
        

        // Задержка во время обработки изображений.
        // Поставим не менее 100 милисекунд, иначе сервер может распознать слишком большое кол-во соедин. за дос атаку и сбрасывать все соединения
        static int ImageDelay = 100;
        // Задержка обработки страниц (Запросы для получения страниц).
        // Эта операция не так часто выполняется так что можно не сильно понижать скорость
        static int PageDelay = 10;
        // Метод в котором выполняется работа
        private void WorkerMethod(object data)
        {
            // Потоки вывода (будем логировать пройденные ссылки страничные и ссылки на изображения)
            StreamWriter pageDone = null;
            StreamWriter imageDone = null;

            try
            {
                // Создаем потоки вывода и связываем с файлами
                pageDone = new StreamWriter("PageDone.txt");
                imageDone = new StreamWriter("ImageDone.txt");
                // Счетчики изображений на/вне сервера
                int serverImg = 0;
                int outboundImg = 0;
                // Порт по умолчанию
                int port = 80;
                // Делаем инвок в поток в котором созданы элементы управления (поля ввода/кнопки итд)
                Invoke(new Action(() =>
                {
                    // Получаем значение порта
                    port = int.Parse(txtBoxPort.Text);
                    // Выключаем кнопку "запуск"
                    btnStart.Enabled = false;
                    // Включаем кнопку "стоп"
                    btnStop.Enabled = true;
                    // Выключаем поле ввода адреса
                    txtBoxUrl.Enabled = false;
                    // Выключаем поле ввода порта
                    txtBoxPort.Enabled = false;
                    // Очищаем списки
                    listViewServer.Items.Clear();
                    listViewOutbound.Items.Clear();
                    // Очищаем текстовое поле вывода логов
                    txtLogBox.Clear();
                }));
                // Получаем ссылку запрос на страницу
                Uri mainRequestUri = data as Uri;
                // Ссылки найденые на сервере
                Queue<Uri> foundLinks = new Queue<Uri>();
                // Запоминаем обработанные ссылки,
                // чтобы избежать циклических перемещений по серверу
                // и не обрабатывать уже обработанные страницы
                List<Uri> cachePageLinks = new List<Uri>();
                List<string> cacheImgLinks = new List<string>();
                List<string> brokenOrDenyLinks = new List<string>();
                // Создаем объект клиента, позволяющий делать запросы и получать ответы от сервера
                Client client = new Client(mainRequestUri, port);
                // Настраиваем клиент                
                client.UserAgent = UserAgent;
                client.Referer = mainRequestUri.Host;
                client.Headers.Add("Accept-Language", "en-US,en;q=0.5");

                client.Disconnected += client_Disconnected;
                client.Received += client_Received;
                client.Sended += client_Sended;
                // Добавляем в очередь новую ссылку для далтнейшей обработки
                foundLinks.Enqueue(mainRequestUri);
                bool pageMoved = false;
                // Запускаем рабочий цикл. Пока не сработало событие завершения и есть ссылки - работаем
                while (!workEvent.WaitOne(PageDelay) && foundLinks.Count > 0)
                {
                    if (pageMoved)
                    {
                        //Console.Readkey();,
                        pageMoved = false;
                    }
                    // Получаем очередную ссылку
                    Uri currentWorkUri = foundLinks.Dequeue();
                    // Добавляем в уже обработынные
                    cachePageLinks.Add(currentWorkUri);
                    // Делаем запрос и получаем ответ
                    Response response = null;
                    try
                    {
                        Console.WriteLine(new String('-', 60));
                        Console.WriteLine("Request: GET " + currentWorkUri);
                        Console.WriteLine(new String('-', 60));
                        txtLogBox.Invoke(new Action(() =>
                        {
                            txtLogBox.AppendText("GET " + currentWorkUri + Environment.NewLine);
                        }));
                        // Делаем запрос
                        response = client.MakeRequest(currentWorkUri);
                    }
                        // Если произошла ошибка - отлавливаем
                    catch (Exception ex)
                    {
                        Console.WriteLine("MakeRequest raised Exception: " + ex.Message);
                        Console.WriteLine(currentWorkUri);
                        //Console.Readkey();,
                    }
                    // Запрос выполнен неудачно. Переходим к след. ссылке.
                    if (response == null || !(response.StatusCode == 200 || response.StatusCode == 301) || response.Body.Equals(string.Empty))
                    {
                        if (response != null)
                        {
                            if (response.StatusCode == 404)
                            {
                                Console.WriteLine("404");
                                //Console.ReadKey();
                            }
                            Console.WriteLine("StatusCode=" + response.StatusCode);
                            Console.WriteLine("Content-Length=" + (response.Headers.ContainsKey("Content-Length".ToLower())
                                ? response.Headers["Content-Length".ToLower()] : "null"));
                        }
                        else
                        {
                            Console.WriteLine("Response=null");
                        }
                        //Console.Readkey();,
                        // Добавим ссылку в список "испорченных"
                        brokenOrDenyLinks.Add(currentWorkUri.ToString());
                        // Переходим к след ссылке в очереди
                        continue;
                    }
                    // Если добрались сюда - запрос выполнен успешно; Проверим нет ли переадресации
                    if (response.StatusCode == 301 && response.Headers.ContainsKey("Location".ToLower()))
                    {
                        // Если есть - получаем новый аддресс
                        string newLocation = response.Headers["Location".ToLower()];
                        Uri outUri;
                        // Пытаемся собрать URI
                        if(Uri.TryCreate(newLocation, UriKind.Absolute, out outUri))
                        {
                            // Если собрали - проверим чтобы такой ссылки еще не было в списке пройденных
                            if (!cachePageLinks.Contains(outUri))
                            {
                                Console.WriteLine("Redirrect...");
                                // Берем текущюю очередь - разворачиваем
                                var temp = new Queue<Uri>(foundLinks.ToArray().Reverse());
                                // Добавляем в конец очереди новый адрес на который нас переадресовали
                                temp.Enqueue(outUri);
                                // Снова разворачиваем очередь
                                foundLinks = new Queue<Uri>(temp.Reverse());
                                //Console.Readkey();,
                                // Выставляем маркер переадрессации
                                pageMoved = true;
                                // Переходим к след. ссылке
                                continue;
                            }
                        }
                    }
                    // Получаем все ссылки <a></a>
                    List<LinkItem> pageLinks = LinkFinder.Find(response.Body);
                    // Получаем все ссылки на изображения <img>
                    List<LinkItem> imageLinks = LinkFinder.FetchImageLinks(response.Body);
                    // Просматриваем все собранные ссылки. Добавляем в очередь найденных ссылок для дальнейшей обработки
                    for (int p = 0; p < pageLinks.Count && !workEvent.WaitOne(PageDelay); ++p)
                    {
                        // Получаем ссылку
                        if (string.IsNullOrEmpty(pageLinks[p].Href) 
                            || brokenOrDenyLinks.Contains(pageLinks[p].Href)) continue;
                        string pageUri = pageLinks[p].Href;
                        // Пробуем построить возможные URI
                        List<Uri> uriCandidats = LinkFinder.TryToMakePageUri(currentWorkUri, pageUri);
                        // Если ничего не построили
                        if (uriCandidats == null || uriCandidats.Count == 0)
                        {
                            Console.WriteLine("Broken link=" + pageUri);
                            // Заносим ссылку в список "испорченных ссылок"
                            brokenOrDenyLinks.Add(pageUri.ToString());
                            // И переходим к след. ссылке
                            continue;
                        }
                        // Иначе
                        // Перебираем все построенные ссылки
                        foreach (var u in uriCandidats)
                        {
                            // Если такую ссылку еще не обрабатывали и ссылка внутри серверная
                            if (!cachePageLinks.Contains(u)                 // Ссылка еще не обрабатывалась
                                && !foundLinks.Contains(u)                  // Ссылка уже не находится в очереди на обработку
                                && !brokenOrDenyLinks.Contains(u.ToString())// Ссылка не находится в списке "испорченных/запрещенных"
                                && !u.ToString().EndsWith(".pdf")           // Ссылка не оканчивается на .pdf (игнорируем pdf документы)
                                && !u.ToString().EndsWith(".pdf/")
                                && !u.ToString().EndsWith(".exe")           // (игнорируем exe документы)
                                && !u.ToString().EndsWith(".exe/")
                                && !u.ToString().EndsWith(".docx")
                                && !u.ToString().EndsWith(".docx/")
                                && !u.ToString().EndsWith(".doc")
                                && !u.ToString().EndsWith(".doc/")
                                && !u.ToString().EndsWith(".xls")
                                && !u.ToString().EndsWith(".xls/")
                                && !u.ToString().StartsWith("mailto"))
                            {
                                // Проверим чтобы ссылка была внутри сервера
                                // Получаем ип аддресс
                                IPAddress outIP = Client.GetIpAddress(u.Host);
                                // Если аддресс не получен по каким то причинам или не совпадает с внутри серверным
                                if (outIP == null || !outIP.Equals(client.IP))
                                {
                                    // Добавляем ссылку в список испорченных/запрещенных
                                    brokenOrDenyLinks.Add(u.ToString());
                                }
                                    // Иначе добавляем в очередь на обработку
                                else
                                {
                                    // Добавляем в очередь
                                    foundLinks.Enqueue(u);      
                                }
                            }
                        }
                        
                    }
                    // Текущее количество ссылок для обработки
                    Console.WriteLine("LINKS count=" + foundLinks.Count);
                    // Выводим ссылки 
                    Invoke(new Action(() =>
                    {
                        // Очищаем окно вывода очереди ссылок
                        txtQueue.Clear();
                        // Добавляем все ссылки
                        txtQueue.AppendText(string.Join(Environment.NewLine, foundLinks));
                        // Добавим в конце текущее количество ссылок для обработки
                        txtQueue.AppendText(Environment.NewLine + "Ожидает обработки: " + foundLinks.Count + Environment.NewLine);
                    }));
                    // Секция отсеивания ссылок на изображения
                    Queue<LinkItem> foundImages = new Queue<LinkItem>(imageLinks);
                    Console.WriteLine(new string('-', 60));
                    Console.WriteLine(new string('-', 30) + "IMAGES" + new string('-', 30));
                    Console.WriteLine(new string('-', 60));
                    // Обрабатываем построенную очередь ссылок на изображения 
                    while (foundImages.Count > 0 && !workEvent.WaitOne(ImageDelay))
                    {
                        // Получаем след. ссылку
                        LinkItem nextImgLink = foundImages.Dequeue();
                        string imgSize = string.Empty;
                        string imgUri = nextImgLink.Href;

                        // Пробуем построить URI
                        Uri imgCandidate = LinkFinder.TryToMakeImgUri(currentWorkUri, imgUri);
                        // Если ссылку не удалось построить
                        if (imgCandidate == null)
                        {
                            // Выводим сообщение
                            Console.WriteLine("Broken img uri: " + imgUri);
                            // Записываем ссылку в список "испорченых"
                            brokenOrDenyLinks.Add(imgUri.ToString());
                            // Переходим к след. ссылке в очереди
                            continue;
                        }
                        // Если ссылка уже обрабатывалась
                        if (cacheImgLinks.Contains(imgCandidate.PathAndQuery)
                            // или записана как поврежденная
                            || brokenOrDenyLinks.Contains(imgCandidate.ToString())
                            // Или ссылка по каким то причинам начинается с "mailto"
                            || imgCandidate.ToString().StartsWith("mailto"))
                        {
                            // Переходим к след. ссылке
                            continue;
                        }
                        // Добавляем ссылку в список обработанных
                        cacheImgLinks.Add(imgCandidate.PathAndQuery);

                        // Делаем запрос на получение заголовка изображения
                        Response imgResponse = null;
                        try
                        {
                            Console.WriteLine(new String('-', 60));
                            Console.WriteLine("Request: GET " + imgCandidate);
                            Console.WriteLine(new String('-', 60));

                            txtLogBox.Invoke(new Action(() =>
                            {
                                txtLogBox.AppendText("HEAD " + imgCandidate + Environment.NewLine);
                            }));
                            // Запрос
                            imgResponse = client.MakeRequest(imgCandidate, false);
                        }
                            // Отлавливаем ошибки
                        catch (Exception ex)
                        {
                            Console.WriteLine("MakeRequest raised Exception: " + ex.Message);
                            Console.WriteLine(imgCandidate);
                            //Console.ReadKey();
                            Log(new string('-', 30) + "Error" + new string('-', 30) + Environment.NewLine, tw);
                            Log(imgCandidate + Environment.NewLine, tw);
                            Log(new string('-', 60) + Environment.NewLine, tw);
                        }
                        // Если запрос не удачен
                        if (imgResponse == null 
                            || !(imgResponse.StatusCode == 200 || imgResponse.StatusCode == 301) 
                            || !imgResponse.Headers.ContainsKey("Content-Length".ToLower()))
                        {

                            if (imgResponse != null)
                            {
                                if(imgResponse.StatusCode == 404)
                                {
                                    Console.WriteLine("404");
                                    //Console.ReadKey();
                                }
                                Console.WriteLine("StatusCode=" + imgResponse.StatusCode);
                                Console.WriteLine("Content-Length=" + (imgResponse.Headers.ContainsKey("Content-Length".ToLower())
                                    ? imgResponse.Headers["Content-Length".ToLower()] : "null"));
                            }
                            else
                            {
                                Console.WriteLine("Response=null");
                            }
                            // Помещаем ссылку в список испорченных
                            brokenOrDenyLinks.Add(imgCandidate.ToString());
                            // К след. ссылке
                            continue;
                        }
                        // Если сервер задал переадресацию
                        if (imgResponse.StatusCode == 301 && imgResponse.Headers.ContainsKey("Location".ToLower()))
                        {
                            string newLocation = imgResponse.Headers["Location".ToLower()];
                            Uri outUri;
                            if (Uri.TryCreate(newLocation, UriKind.Absolute, out outUri))
                            {
                                Console.WriteLine("IMG Redirrect...");

                                LinkItem li = new LinkItem();
                                li.Href = outUri.ToString();
                                li.Text = nextImgLink.Text;
                                // Разворачиваем очередь
                                var temp = new Queue<LinkItem>(foundImages.ToArray().Reverse());
                                // Помещаем новую ссылку
                                temp.Enqueue(li);
                                // Снова вернем очередь в прежний вид но в начале будет наш адрес переадресации
                                foundImages = new Queue<LinkItem>(temp.Reverse());                                  
                                // Переходим к след. ссылке (как раз след. будет ссылкой переадресации)
                                continue;
                            }
                        }
                        // Добрались сюда - получаем размер изображ.
                        imgSize = imgResponse.Headers["Content-Length".ToLower()];
                        // Определяем ип адрес хоста, если внутри серверная ссылка - запишем в один список иначе в другой
                        if (Client.GetIpAddress(imgCandidate.Host).Equals(client.IP))
                        {
                            serverImg++;
                            listViewServer.Invoke(new Action(() =>
                            {
                                ListViewItem lvi = new ListViewItem();
                                lvi.Text = nextImgLink.Text;//imageLinks[img].Text;
                                lvi.SubItems.Add(imgCandidate.ToString());
                                lvi.SubItems.Add(imgSize);

                                listViewServer.Items.Add(lvi);

                                lblServer.Text = serverImg.ToString();
                            }));
                        }
                        else
                        {
                            outboundImg++;
                            listViewOutbound.Invoke(new Action(() =>
                            {
                                ListViewItem lvi = new ListViewItem();
                                lvi.Text = nextImgLink.Text;//imageLinks[img].Text;
                                lvi.SubItems.Add(imgCandidate.ToString());
                                lvi.SubItems.Add(imgSize);

                                listViewOutbound.Items.Add(lvi);

                                lblOutbound.Text = outboundImg.ToString();
                            }));
                        }
                        // Записываем в лог файл новую обработанную ссылку на изображение
                        imageDone.WriteLine(imgCandidate);
                        imageDone.Flush();
                    }
                    // Ссылка обработана
                    Console.WriteLine("Обработка [" + currentWorkUri + "] ссылки завершена.");
                    // Запишем в лог файл ссылку на страницу которую только что обработали
                    pageDone.WriteLine(currentWorkUri);
                    pageDone.Flush();
                }

            }
                // Отлавливаем ошибки
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
            }          
                // По завершению не забываем закрыть потоки которые открывали для лог файлов
            finally
            {
                if (pageDone != null)
                    pageDone.Close();
                if (imageDone != null)
                    imageDone.Close();
            }
            
            // Вернем в прежний вид элементы управления
            Invoke(new Action(() =>
            {
                btnStart.Enabled = true;
                btnStop.Enabled = false;
                txtBoxUrl.Enabled = true;
                txtBoxPort.Enabled = true;
            }));
            // Помечаем выполнение завершено (отслеживаем это событие при закрытии приложения)
            threadDone.Set();
            MessageBox.Show("Done");
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            try
            {
                // Сбрасываем события
                workEvent.Reset();
                threadDone.Reset();
                // Создаем новый поток
                workrerThread = new Thread(WorkerMethod);
                // Получаем данные адреса
                string uri = txtBoxUrl.Text.Trim();
                string ipaddr = txtBoxUrl.Text.Trim();
                int port = int.Parse(txtBoxPort.Text);

                if (!uri.Contains("http://"))
                    uri = "http://" + uri;
                // Запускаем поток для выполнения передав в качестве аргумента построенный URI
                workrerThread.Start(new Uri(uri));                 
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            btnStop.Enabled = false;
            workEvent.Set();
        }

        void client_Connected(Client sender, System.Net.EndPoint remoteEP)
        {
            this.Invoke(new Action(() =>
            {
                txtLogBox.AppendText("Connected to " + remoteEP.ToString() + Environment.NewLine);
            }));
        }

        void client_Disconnected(Client sender)
        {
            this.Invoke(new Action(() =>
            {
                txtLogBox.AppendText("Disconnected" + Environment.NewLine);
            }));
        }

        void client_Sended(Client sender, string data)
        {
            this.Invoke(new Action(() =>
            {
                txtLogBox.AppendText("Send headers: " + Environment.NewLine + data + Environment.NewLine);
            }));
        }

        void client_Received(Client sender, string data)
        {
            this.Invoke(new Action(() =>
            {
                txtLogBox.AppendText("Headers received: " + data + Environment.NewLine);
            }));
        }

        private void txtBoxUrl_TextChanged(object sender, EventArgs e)
        {
            string text = txtBoxUrl.Text;
            try
            {
                string isUri = text;
                string ipAddr = text;
                if (!text.Contains("http://"))
                    isUri = "http://" + isUri;

                Regex ValidIpAddressRegex = new Regex(@"^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$");
                Regex ValidHostnameRegex = new Regex(@"^(([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9])\.)*([A-Za-z0-9]|[A-Za-z0-9][A-Za-z0-9\-]*[A-Za-z0-9])$");

                btnStart.Enabled = ValidIpAddressRegex.IsMatch(text) || ValidHostnameRegex.IsMatch(text);
                return;

                
            }
            catch {} 

            //btnStart.Enabled = Client.GetIpAddress(txtBoxUrl.Text) != null;
        }

        private void UserInterface_FormClosing(object sender, FormClosingEventArgs e)
        {
            workEvent.Set();
            if (workrerThread != null)
                while (!threadDone.WaitOne(1000))
                {
                    Application.DoEvents();
                }
        }

        private void txtBoxPort_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!Char.IsDigit(e.KeyChar) && e.KeyChar != Convert.ToChar(8))
            {
                e.Handled = true;
            }
        }

        private void listViewServer_DoubleClick(object sender, EventArgs e)
        {
            if (listViewServer.SelectedItems.Count == 1)
            {
                string url = listViewServer.SelectedItems[0].SubItems[1].Text;
                if (MessageBox.Show("Открыть " + url + " ?", "Подтвердите действие.",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != System.Windows.Forms.DialogResult.Yes) return;

                ProcessStartInfo sInfo = new ProcessStartInfo(url);
                Process.Start(sInfo);
            }            
        }

        private void listViewOutbound_DoubleClick(object sender, EventArgs e)
        {
            if (listViewOutbound.SelectedItems.Count == 1)
            {
                string url = listViewOutbound.SelectedItems[0].SubItems[1].Text;
                if (MessageBox.Show("Открыть " + url + " ?", "Подтвердите действие.",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != System.Windows.Forms.DialogResult.Yes) return;

                ProcessStartInfo sInfo = new ProcessStartInfo(url);
                Process.Start(sInfo);
            }
        }
    }
}
