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
            btnStart.Enabled = Client.GetIpAddress(txtBoxUrl.Text) != null;

            txtBoxPort.ShortcutsEnabled = false;

            try
            {
                // Здаем файл лог
                tw = new StreamWriter(File.Open("log.txt", FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Неудачное создание лог файла" + Environment.NewLine + ex.Message);
            }

            
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
            try
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
                    if (q.Count > 0)
                    {
                        // Отсекаем последний сегмент
                        q.Dequeue();
                        // Перебираем оставшиеся
                        while (q.Count > 1)
                        {
                            Uri subUri;
                            string nextFolder = "http://" + outUri.Host + string.Join("", q.ToArray().Reverse());
                            //Console.WriteLine("Subfolder=" + nextFolder);
                            if (Uri.TryCreate(nextFolder.Trim(), UriKind.Absolute, out subUri))
                                result.Add(subUri);
                            q.Dequeue();
                        }
                    }
                }
                else
                {

                    if(Uri.TryCreate(workUri, parsedPageLink, out outUri) )
                    {
                        result.Add(outUri);
                    }

                    //Queue<string> q = new Queue<string>(workUri.Segments.ToArray().Reverse());
                    //int dotIndex = workUri.ToString().LastIndexOf(".");
                    //if (dotIndex != -1)
                    //{
                    //    // Отсекаем последний сегмент
                    //    if(q.Count > 0)
                    //        q.Dequeue();
                    //}

                    //string url1 = "http://" + workUri.Host + string.Join("", q.ToArray().Reverse());//workUri.ToString();
                    //if (url1.EndsWith("/") && parsedPageLink.StartsWith("/"))
                    //    url1 = url1.Substring(0, url1.Length - 1) + parsedPageLink;
                    //else if ((url1.EndsWith("/") && !parsedPageLink.StartsWith("/"))
                    //    || (!url1.EndsWith("/") && parsedPageLink.StartsWith("/")))
                    //    url1 = url1 + parsedPageLink;
                    //else
                    //    url1 = url1 + "/" + parsedPageLink;

                    //if (Uri.TryCreate(url1.Trim(), UriKind.Absolute, out outUri))
                    //    result.Add(outUri);
                    ///////////////////////////////////
                }


                //Console.WriteLine(parsedPageLink);
                result = result.Distinct().ToList<Uri>();
                foreach (var r in result)
                    Console.WriteLine("TryMakeUri=" + r);
                //Console.ReadKey();
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine("TryMakePageUri Error=" + ex.Message + Environment.NewLine + ex.StackTrace);
                Console.WriteLine(parsedPageLink);
                //Console.Readkey();,
            }
            return null;
        }

        private List<Uri> TryToMakeImgUri(Uri workUri, string parsedImgLink)
        {
            try
            {
                List<Uri> result = new List<Uri>();
                Uri outUri;

                if (Uri.TryCreate(parsedImgLink, UriKind.Absolute, out outUri))
                {
                    result.Add(outUri);
                    return result;
                }
                if(Uri.TryCreate(workUri, parsedImgLink, out outUri))
                {
                    result.Add(outUri);
                    return result;

                }
                return null;

                Queue<string> q = new Queue<string>(workUri.Segments.ToArray().Reverse());
                int dotIndex = workUri.ToString().LastIndexOf(".");
                if (q.Count > 0)
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

                string url2 = "http://" + workUri.Host;// + string.Join("", q.ToArray().Reverse());
                if (url2.EndsWith("/") && parsedImgLink.StartsWith("/"))
                    url2 = url2.Substring(0, url2.Length - 1) + parsedImgLink;
                else if ((url2.EndsWith("/") && !parsedImgLink.StartsWith("/"))
                    || (!url2.EndsWith("/") && parsedImgLink.StartsWith("/")))
                    url2 = url2 + parsedImgLink;
                else
                    url2 = url2 + "/" + parsedImgLink;

                if (Uri.TryCreate(url2, UriKind.Absolute, out outUri))
                    result.Add(outUri);

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine("TryMakeIMG-Uri Error=" + ex.Message);
                //Console.Readkey();,
            }
            return null;
        }

        static int ImageDelay = 10;
        static int PageDelay = 10;

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
                    txtBoxUrl.Enabled = false;
                    txtBoxPort.Enabled = false;
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
                List<string> cacheImgLinks = new List<string>();
                List<string> brokenOrDenyLinks = new List<string>();
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
                        response = client.MakeRequest(currentWorkUri);
                    }
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
                        brokenOrDenyLinks.Add(currentWorkUri.ToString());
                        continue;
                    }
                    if (response.StatusCode == 301 && response.Headers.ContainsKey("Location".ToLower()))
                    {
                        string newLocation = response.Headers["Location".ToLower()];
                        Uri outUri;
                        if(Uri.TryCreate(newLocation, UriKind.Absolute, out outUri))
                        {
                            //
                            if (!cachePageLinks.Contains(outUri))
                            {
                                Console.WriteLine("Redirrect...");
                                var temp = new Queue<Uri>(foundLinks.ToArray().Reverse());
                                temp.Enqueue(outUri);
                                foundLinks = new Queue<Uri>(temp.Reverse());
                                //Console.Readkey();,
                                pageMoved = true;
                                continue;
                            }
                        }
                    }
                    // Получаем все ссылки <a></a>
                    List<LinkItem> pageLinks = LinkFinder.Find(response.Body);
                    /*using (StreamWriter parsed = new StreamWriter("FOUND_" + 
                        currentWorkUri.Host.Replace('.', '_') + "_" + string.Join("_", currentWorkUri.Segments.ToArray().Reverse()).Replace('/', '_')))
                    {
                        foreach (var p in pageLinks)
                        {
                            parsed.WriteLine(p.Href);
                        }
                        parsed.Flush();
                    }*/
                    // Получаем все ссылки на изображения <img>
                    List<LinkItem> imageLinks = LinkFinder.FetchImageLinks(response.Body);


                    /*StreamWriter parsed2 = new StreamWriter("GOOD_" +
                            currentWorkUri.Host.Replace('.', '_') + "_" + string.Join("_", currentWorkUri.Segments.ToArray().Reverse()).Replace('/', '_'));*/

                    // Просматриваем все собранные ссылки. Добавляем в очередь найденных ссылок для дальнейшей обработки
                    for (int p = 0; p < pageLinks.Count && !workEvent.WaitOne(PageDelay); ++p)
                    {
                        // Получаем ссылку
                        if (string.IsNullOrEmpty(pageLinks[p].Href) 
                            || brokenOrDenyLinks.Contains(pageLinks[p].Href)) continue;
                        string pageUri = pageLinks[p].Href;
                        // Пробуем построить URI
                        List<Uri> uriCandidats = TryToMakePageUri(currentWorkUri, pageUri);

                        //Console.WriteLine("PAGE=" + pageUri);
                        //Console.WriteLine(string.Join("\n", uriCandidats));
                        //Console.WriteLine("COUNT=" + uriCandidats.Count); 
                        //Console.ReadKey();

                        if (uriCandidats == null || uriCandidats.Count == 0)
                        {
                            Console.WriteLine("Broken link=" + pageUri);
                            //Console.Readkey();,
                            brokenOrDenyLinks.Add(pageUri.ToString());
                            continue;
                        }
                         
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
                                    /*parsed2.WriteLine(u.ToString());
                                    parsed2.Flush();*/
                                }
                            }
                        }
                        
                    }
                    //parsed2.Close();
                    Console.WriteLine("LINKS count=" + foundLinks.Count);

                    Invoke(new Action(() =>
                        {
                            txtQueue.Clear();
                            txtQueue.AppendText(string.Join(Environment.NewLine, foundLinks));
                            txtQueue.AppendText(Environment.NewLine + foundLinks.Count + Environment.NewLine);
                        }));

                    //Console.ReadKey();
                    Queue<LinkItem> foundImages = new Queue<LinkItem>(imageLinks);
                    Console.WriteLine(new string('-', 60));
                    Console.WriteLine(new string('-', 30) + "IMAGES" + new string('-', 30));
                    Console.WriteLine(new string('-', 60));
                    // Обрабатываем ссылки на изображения 
                    //for (int img = 0; img < imageLinks.Count && !workEvent.WaitOne(500); ++img)
                    while (foundImages.Count > 0 && !workEvent.WaitOne(ImageDelay))
                    {
                        //Console.ReadKey();
                        LinkItem nextImgLink = foundImages.Dequeue();
                        string imgSize = string.Empty;
                        string imgUri = nextImgLink.Href;//imageLinks[img].Href;

                        // Пробуем построить URI
                        List<Uri> imgCandidats = TryToMakeImgUri(currentWorkUri, imgUri);
                        //Console.ReadKey();
                        if (imgCandidats == null || imgCandidats.Count == 0)
                        {
                            Console.WriteLine("Broken img uri: " + imgUri);
                            //Console.Readkey();,
                            brokenOrDenyLinks.Add(imgUri.ToString());
                            continue;
                        }

                        for (int candidateImgID = 0; candidateImgID < imgCandidats.Count && !workEvent.WaitOne(ImageDelay); ++candidateImgID)
                        {
                            if (cacheImgLinks.Contains(imgCandidats[candidateImgID].PathAndQuery) 
                                || brokenOrDenyLinks.Contains(imgCandidats[candidateImgID].ToString())
                                || imgCandidats[candidateImgID].ToString().StartsWith("mailto")) continue;

                            cacheImgLinks.Add(imgCandidats[candidateImgID].PathAndQuery);

                            Response imgResponse = null;
                            try
                            {
                                Console.WriteLine(new String('-', 60));
                                Console.WriteLine("Request: GET " + imgCandidats[candidateImgID]);
                                Console.WriteLine(new String('-', 60));

                                txtLogBox.Invoke(new Action(() =>
                                {
                                    txtLogBox.AppendText("HEAD " + imgCandidats[candidateImgID] + Environment.NewLine);
                                }));
                                imgResponse = client.MakeRequest(imgCandidats[candidateImgID], false);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("MakeRequest raised Exception: " + ex.Message);
                                Console.WriteLine(imgCandidats[candidateImgID]);
                                //Console.ReadKey();
                                Log(new string('-', 30) + "Error" + new string('-', 30) + Environment.NewLine, tw);
                                Log(imgCandidats[candidateImgID] + Environment.NewLine, tw);
                                Log(new string('-', 60) + Environment.NewLine, tw);
                            }


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
                                //Console.Readkey();,
                                brokenOrDenyLinks.Add(imgCandidats[candidateImgID].ToString());
                                continue;
                            }
                            if (imgResponse.StatusCode == 301 && imgResponse.Headers.ContainsKey("Location".ToLower()))
                            {
                                string newLocation = imgResponse.Headers["Location".ToLower()];
                                Uri outUri;
                                if (Uri.TryCreate(newLocation, UriKind.Absolute, out outUri))
                                {
                                    Console.WriteLine("IMG Redirrect...");
                                    //foundLinks.Enqueue(outUri);
                                    LinkItem li = new LinkItem();
                                    li.Href = outUri.ToString();
                                    li.Text = nextImgLink.Text;

                                    var temp = new Queue<LinkItem>(foundImages.ToArray().Reverse());
                                    temp.Enqueue(li);

                                    foundImages = new Queue<LinkItem>(temp.Reverse());
                                    


                                    //foundImages.Enqueue(li);
                                    //Console.Readkey();,
                                    continue;
                                }
                            }


                            imgSize = imgResponse.Headers["Content-Length".ToLower()];

                            if (Client.GetIpAddress(imgCandidats[candidateImgID].Host).Equals(client.IP))
                            {
                                serverImg++;
                                listViewServer.Invoke(new Action(() =>
                                {
                                    ListViewItem lvi = new ListViewItem();
                                    lvi.Text = nextImgLink.Text;//imageLinks[img].Text;
                                    lvi.SubItems.Add(imgCandidats[candidateImgID].ToString());
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
                                    lvi.SubItems.Add(imgCandidats[candidateImgID].ToString());
                                    lvi.SubItems.Add(imgSize);

                                    listViewOutbound.Items.Add(lvi);

                                    lblOutbound.Text = outboundImg.ToString();
                                }));
                            }

                            imageDone.WriteLine(imgCandidats[candidateImgID]);
                            imageDone.Flush();
                        }
                    }
                    // Ссылка обработана
                    Console.WriteLine("Обработка [" + currentWorkUri + "] ссылки завершена.");
                    pageDone.WriteLine(currentWorkUri);
                    pageDone.Flush();
                    //Console.Readkey();,
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
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
                txtBoxUrl.Enabled = true;
                txtBoxPort.Enabled = true;
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
                
                string uri = txtBoxUrl.Text.Trim();
                string ipaddr = txtBoxUrl.Text.Trim();
                int port = int.Parse(txtBoxPort.Text);

                if (!uri.Contains("http://"))
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
            string text = txtBoxUrl.Text;
            try
            {
                string isUri = text;
                string ipAddr = text;
                if (!text.Contains("http://"))
                    isUri = "http://" + isUri;

                //Uri outUri = null;
                //IPAddress outIP = null;

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
