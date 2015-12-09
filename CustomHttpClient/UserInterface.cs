using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
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
        // Аксецптим такой набор
        //string Accept = "text/html,application/xhtml+xml,application/xml;q=0.9, image/png, image/gif, image/jpeg, image/pjpeg;q=0.8, */*;q=0.7";
        string Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";

        // Если используем прокси 
        //WebProxy proxy = null;//new WebProxy("127.0.0.1", 8888);

        Thread workrerThread = null;
        ManualResetEvent workEvent = new ManualResetEvent(false);
        ManualResetEvent threadDone = new ManualResetEvent(false);

        public UserInterface()
        {
            InitializeComponent();
            // Проверяем строку адреса и если валидная - включим кнопку запуска
            btnStart.Enabled = IsValidUri(txtBoxUrl.Text);

            txtBoxPort.ShortcutsEnabled = false;

            // Здаем файл лог
            tw = new StreamWriter(File.Open("log.txt", FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read));

            
        }

        private bool IsValidUri(string uri)
        {
            if (!uri.Contains("http://") && !uri.Contains("https://"))
                uri = "http://" + uri;
            return Uri.IsWellFormedUriString(uri, UriKind.Absolute);            
        }

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

        public struct LinkItem
        {
            public string Href;
            public string Text;

            public override string ToString()
            {
                return Href + "\n\t" + Text;
            }
        }

        static class LinkFinder
        {
            public static List<LinkItem> Find(string file)
            {
                List<LinkItem> list = new List<LinkItem>();

                // 1.
                // Find all matches in file.
                MatchCollection m1 = Regex.Matches(file, @"(<a.*?>.*?</a>)",
                    RegexOptions.Singleline);
                // 2.
                // Loop over each match.
                foreach (Match m in m1)
                {
                    string value = m.Groups[1].Value;
                    LinkItem i = new LinkItem();

                    // 3.
                    // Get href attribute.
                    Match m2 = Regex.Match(value, @"href=\""(.*?)\""",
                    RegexOptions.Singleline);
                    if (m2.Success)
                    {
                        i.Href = m2.Groups[1].Value;                        
                    }

                    // 4.
                    // Remove inner tags from text.
                    string t = Regex.Replace(value, @"\s*<.*?>\s*", "",
                    RegexOptions.Singleline);
                    i.Text = t;


                    list.Add(i);
                }
                return list;
            }


            public static List<LinkItem> FetchImageLinks(string htmlSource)
            {
                List<LinkItem> list = new List<LinkItem>();

                // 1.
                // Find all matches in file.
                string regexImgSrc = @"<img.*?>";
                MatchCollection matchesImgSrc = Regex.Matches(htmlSource, regexImgSrc, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                // 2.
                // Loop over each match.
                foreach (Match m in matchesImgSrc)
                {
                    LinkItem i = new LinkItem();

                    // 3.
                    // Get href attribute.
                    Match m2 = Regex.Match(m.Value, @"src\s?=\s?\""(.*?)\""", RegexOptions.Singleline);
                    Match m22 = Regex.Match(m.Value, @"src\s?=\s?'(.*?)'", RegexOptions.Singleline);

                    if (m2.Success)
                    {
                        i.Href = m2.Groups[1].Value.Replace('\\', '/').Trim();
                    }
                    else if (m22.Success)
                    {
                        i.Href = m22.Groups[1].Value.Replace('\\', '/').Trim();
                    }
                    else
                    {
                        continue;
                    }

                    Match m3 = Regex.Match(m.Value, @"alt\s?=\s?\""(.*?)\""", RegexOptions.Singleline);

                    if (m3.Success)
                    {
                        i.Text = m3.Groups[1].Value.Replace('\\', '/').Trim();
                    }
                    list.Add(i);
                }
                return list;
            }
        }

        TextWriter tw = null;
        // Объект синхронизации
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

        List<Uri> TryToMakePageUri(Uri workUri, string parsedPageLink)
        {
            List<Uri> result = new List<Uri>();
            Uri outUri;
            // Пробуем построить URI из полученной ссылки
            if (Uri.TryCreate(parsedPageLink.Trim(), UriKind.Absolute, out outUri))
            {
                // Построили успешно - добавляем к результату
                result.Add(outUri);
                // Перебираем все сегменты (каталоги) и строим дополнительные ссылки
                Queue<string> q = new Queue<string>(outUri.Segments.ToArray().Reverse());
                // Отсекаем последний сегмент
                q.Dequeue();
                // Перебираем оставшиеся
                while (q.Count > 0)
                {
                    Uri subUri;
                    string nextFolder = "http://" + outUri.Host + string.Join("", q.ToArray().Reverse());
                    //Console.WriteLine("Subfolder=" + nextFolder);
                    if (Uri.TryCreate(nextFolder.Trim(), UriKind.Absolute, out subUri))
                        result.Add(subUri);
                    q.Dequeue();
                }
            }
            else
            {
                Queue<string> q = new Queue<string>(workUri.Segments.ToArray().Reverse());
                int dotIndex = workUri.ToString().LastIndexOf(".");
                if (dotIndex != -1)
                {                    
                    // Отсекаем последний сегмент
                    q.Dequeue(); 
                }
                
                string url1 = "http://" + workUri.Host + string.Join("", q.ToArray().Reverse());//workUri.ToString();
                if (url1.EndsWith("/") && parsedPageLink.StartsWith("/"))
                    url1 = url1.Substring(0, url1.Length - 1) + parsedPageLink;
                else if ((url1.EndsWith("/") && !parsedPageLink.StartsWith("/"))
                    || (!url1.EndsWith("/") && parsedPageLink.StartsWith("/")))
                    url1 = url1 + parsedPageLink;
                else
                    url1 = url1 + "/" + parsedPageLink;

                if (Uri.TryCreate(url1.Trim(), UriKind.Absolute, out outUri))
                    result.Add(outUri);
                ///////////////////////////////////
            }


            //Console.WriteLine(parsedPageLink);
            result = result.Distinct().ToList<Uri>();
            foreach(var r in result)
                Console.WriteLine("TryMakeUri=" + r);
            //Console.ReadKey();
            return result;

        }

        private List<Uri> TryToMakeImgUri(Uri workUri, string parsedImgLink)
        {
            List<Uri> result = new List<Uri>();
            Uri outUri;

            if (Uri.TryCreate(parsedImgLink, UriKind.Absolute, out outUri))
            {
                result.Add(outUri);
                return result;
            }

            Queue<string> q = new Queue<string>(workUri.Segments.ToArray().Reverse());
            int dotIndex = workUri.ToString().LastIndexOf(".");
            if (dotIndex != -1)
            {
                // Отсекаем последний сегмент
                q.Dequeue();
            }

            string url1 = "http://" + workUri.Host + string.Join("", q.ToArray().Reverse());//workUri.ToString();
            if (url1.EndsWith("/") && parsedImgLink.StartsWith("/"))
                url1 = url1.Substring(0, url1.Length - 1) + parsedImgLink;
            else if ((url1.EndsWith("/") && !parsedImgLink.StartsWith("/"))
                || (!url1.EndsWith("/") && parsedImgLink.StartsWith("/")))
                url1 = url1 + parsedImgLink;
            else
                url1 = url1 + "/" + parsedImgLink;

            if (Uri.TryCreate(url1.Trim(), UriKind.Absolute, out outUri))
                result.Add(outUri);
            return result;
        }

        private void WorkerMethod(object data)
        {
            StreamWriter pageDone = null;
            StreamWriter imageDone = null;

            try
            {
                pageDone = new StreamWriter("PageDone.txt");
                imageDone = new StreamWriter("ImageDone.txt");

                int serverImg = 0;
                int outboundImg = 0;
                int port = 80;
                Invoke(new Action(() =>
                {
                    port = int.Parse(txtBoxPort.Text);
                    btnStart.Enabled = false;
                    btnStop.Enabled = true;
                    listViewServer.Items.Clear();
                    listViewOutbound.Items.Clear();
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
                List<Uri> cacheImgLinks = new List<Uri>();
                // Создаем объект клиента, позволяющий делать запросы и получать ответы от сервера
                Client client = new Client(mainRequestUri, port);
                // Настраиваем клиент                
                client.UserAgent = UserAgent;
                client.Referer = mainRequestUri.Host;
                client.Headers.Add("Accept-Language", "en-US,en;q=0.5");

                client.Connected += client_Connected;
                client.Disconnected += client_Disconnected;
                client.Received += client_Received;
                client.Sended += client_Sended;
                // Добавляем в очередь новую ссылку для далтнейшей обработки
                foundLinks.Enqueue(mainRequestUri);
                // Запускаем рабочий цикл. Пока не сработало событие завершения и есть ссылки - работаем
                while (!workEvent.WaitOne(100) && foundLinks.Count > 0)
                {
                    // Получаем очередную ссылку
                    Uri currentWorkUri = foundLinks.Dequeue();
                    // Проверим что новая ссылка получена
                    if (currentWorkUri == null)
                    {
                        // Если не получена, это означает что очеред опустела, т.е. задачи закончились.
                        Console.WriteLine("Очередь пуста.");
                        // Завершаем цикл
                        break;
                    }
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
                        response = client.MakeRequest(currentWorkUri);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("MakeRequest raised Exception: " + ex.Message);
                        Console.WriteLine(currentWorkUri);
                        Console.ReadKey();
                    }
                    // Запрос выполнен неудачно. Переходим к след. ссылке.
                    if (response == null || response.StatusCode != 200 || response.Body.Equals(string.Empty))
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
                            Console.WriteLine("Response=" + null);
                        }
                        //Console.ReadKey();
                        continue;
                    }
                    // Получаем все ссылки <a></a>
                    List<LinkItem> pageLinks = LinkFinder.Find(response.Body);
                    // Получаем все ссылки на изображения <img>
                    List<LinkItem> imageLinks = LinkFinder.FetchImageLinks(response.Body);

                    
                    // Просматриваем все собранные ссылки. Добавляем в очередь найденных ссылок для дальнейшей обработки
                    for (int p = 0; p < pageLinks.Count && !workEvent.WaitOne(1); ++p)
                    {
                        // Получаем ссылку
                        if (string.IsNullOrEmpty(pageLinks[p].Href)) continue;
                        string pageUri = pageLinks[p].Href;
                        // Пробуем построить URI
                        List<Uri> uriCandidats = TryToMakePageUri(currentWorkUri, pageUri);
                        //Uri[] uriCandidats = TryToMakeUri(new Uri("http://tass.ru/ekonomika/2513837"), "http://tass.ru/borba-s-islamskim-gosudarstvom");
                        //Console.ReadKey();

                        if (uriCandidats.Count == 0)
                        {
                            Console.WriteLine("Broken link=" + pageUri);
                            continue;
                        }
                        // Перебираем все построенные ссылки
                        foreach (var u in uriCandidats)
                        {
                            // Если такую ссылку еще не обрабатывали и ссылка внутри серверная
                            if (!cachePageLinks.Contains(u) && !foundLinks.Contains(u) && client.Host.Equals(u.Host))
                            {
                                // Добавляем в очередь
                                foundLinks.Enqueue(u);
                            }
                        }
                    }
                    Console.WriteLine("LINKS count=" + foundLinks.Count);

                    Invoke(new Action(() =>
                        {
                            txtQueue.Clear();
                            txtQueue.AppendText(string.Join(Environment.NewLine, foundLinks));
                            txtQueue.AppendText(Environment.NewLine + foundLinks.Count + Environment.NewLine);
                        }));

                    //Console.ReadKey();
                    Console.WriteLine(new string('=', 60));
                    Console.WriteLine(new string('=', 30) + "IMAGES" + new string('=', 30));
                    Console.WriteLine(new string('=', 60));
                    // Обрабатываем ссылки на изображения 
                    for (int img = 0; img < imageLinks.Count && !workEvent.WaitOne(10); ++img)
                    {
                        //Console.ReadKey();
                        string imgSize = string.Empty;
                        string imgUri = imageLinks[img].Href;

                        // Пробуем построить URI
                        List<Uri> uriCandidats = TryToMakeImgUri(currentWorkUri, imgUri);
                        //Console.ReadKey();
                        if (uriCandidats.Count == 0)
                        {
                            Console.WriteLine("Broken img uri: " + imgUri);
                            continue;
                        }

                        for (int candidateImgID = 0; candidateImgID < uriCandidats.Count; ++candidateImgID)
                        {
                            if (cacheImgLinks.Contains(uriCandidats[candidateImgID])) continue;

                            cacheImgLinks.Add(uriCandidats[candidateImgID]);

                            Response imgResponse = null;
                            try
                            {
                                Console.WriteLine(new String('-', 60));
                                Console.WriteLine("Request: GET " + uriCandidats[candidateImgID]);
                                Console.WriteLine(new String('-', 60));

                                txtLogBox.Invoke(new Action(() =>
                                {
                                    txtLogBox.AppendText("HEAD " + uriCandidats[candidateImgID] + Environment.NewLine);
                                }));
                                imgResponse = client.MakeRequest(uriCandidats[candidateImgID], false);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("MakeRequest raised Exception: " + ex.Message);
                                Console.WriteLine(uriCandidats[candidateImgID]);
                                //Console.ReadKey();
                                Log(new string('-', 30) + "Error" + new string('-', 30) + Environment.NewLine, tw);
                                Log(uriCandidats[candidateImgID] + Environment.NewLine, tw);
                                Log(new string('-', 60) + Environment.NewLine, tw);
                            }


                            if (imgResponse == null || imgResponse.StatusCode != 200 || !imgResponse.Headers.ContainsKey("Content-Length".ToLower()))
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
                                    Console.WriteLine("Response=" + null);
                                }
                                //Console.ReadKey();
                                continue;
                            }

                            imgSize = imgResponse.Headers["Content-Length".ToLower()];

                            if (uriCandidats[candidateImgID].Host.Equals(client.RequestUri.Host))
                            {
                                serverImg++;
                                listViewServer.Invoke(new Action(() =>
                                {
                                    ListViewItem lvi = new ListViewItem();
                                    lvi.Text = imageLinks[img].Text;
                                    lvi.SubItems.Add(uriCandidats[candidateImgID].ToString());
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
                                    lvi.Text = imageLinks[img].Text;
                                    lvi.SubItems.Add(uriCandidats[candidateImgID].ToString());
                                    lvi.SubItems.Add(imgSize);

                                    listViewOutbound.Items.Add(lvi);

                                    lblOutbound.Text = outboundImg.ToString();
                                }));
                            }

                            imageDone.WriteLine(uriCandidats[candidateImgID]);
                            imageDone.Flush();
                        }
                    }
                    // Ссылка обработана
                    Console.WriteLine("Обработка [" + currentWorkUri + "] ссылки завершена.");
                    pageDone.WriteLine(currentWorkUri);
                    pageDone.Flush();
                    //Console.ReadKey();
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine);
            }          
            finally
            {
                if (pageDone != null)
                    pageDone.Close();
                if (imageDone != null)
                    imageDone.Close();
            }
            
            Invoke(new Action(() =>
            {
                btnStart.Enabled = true;
                btnStop.Enabled = false;
            }));
            threadDone.Set();
            MessageBox.Show("Done");
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            try
            {
                workEvent.Reset();
                threadDone.Reset();
                workrerThread = new Thread(WorkerMethod);
                
                string uri = txtBoxUrl.Text;
                int port = int.Parse(txtBoxPort.Text);
                if (!uri.Contains("http://") && !uri.Contains("https://"))
                    uri = "http://" + uri;
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


            btnStart.Enabled = IsValidUri(txtBoxUrl.Text);
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
    }
}
