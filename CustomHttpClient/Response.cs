using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CustomHttpClient
{
    public class Response
    {
        public Response()
        {
            Headers = new Dictionary<string, string>();
            Body = string.Empty;
        }

        public Response(string raw)
        {
            // Выделяем память для хранения заголовков
            Headers = new Dictionary<string, string>();
            // Тело ответа пока пусто
            Body = string.Empty;
            // Разделяем заголовки и тело запроса
            string[] dataSeparator = new string[] { Client.CRLF + Client.CRLF };
            string[] headerSeparator = new string[] { Client.CRLF };
            string[] respData = raw.Split(dataSeparator, StringSplitOptions.None);
            // Запоминаем строку всех заголовков
            string headersRaw = respData[0];
            // Если тело есть - запоминаем
            if (respData.Length > 1)
                Body = respData[1];
            // Разделяем заголовки
            Queue<string> headers = new Queue<string>(headersRaw.Split(headerSeparator, StringSplitOptions.None));
            // Получаем первую строку и разбираем протокол, статус код и расшифровку статус кода если есть
            string[] respinfo = headers.Dequeue().Split();
            // Если не указан протокол и статус код - бросаем исключение
            if (respinfo.Length < 2) throw new Exception("Response header info line error");
            // Запоминаем используемый протокол
            ProtocolVersion = respinfo[0];
            // Получаем статус код
            StatusCode = int.Parse(respinfo[1]);
            // Если есть расшифровка статус кода - запоминаем
            if (respinfo.Length > 2)
                StatusText = respinfo[2];                                
            // Обрабатываем остальные заголовки
            while (headers.Count > 0)
            {
                // Получаем очередную строку - заголовок
                string nextHeader = headers.Dequeue();
                // Поиск разделителя заголовка
                int sep = nextHeader.IndexOf(": ");
                if (sep == -1)
                    sep = nextHeader.IndexOf(":");
                if (sep == -1)
                    continue;
                // Разделяем имя и параметры заголовка
                string headerName = nextHeader.Substring(0, sep).Trim().ToLower();
                string headerParam = nextHeader.Substring(sep + 1).Trim().ToLower();
                // Если уже есть заголовок с таким именем - обновляем параметры
                if (Headers.ContainsKey(headerName))
                {
                    Headers[headerName] = headerParam;
                }
                // Иначе добавим новый
                else
                {
                    Headers.Add(headerName, headerParam);
                }
            }
        }
        // Заголовки
        public Dictionary<string, string> Headers { get; private set; }
        // Тело 
        public string Body { get; private set; }

        // Версия протокола
        public string ProtocolVersion { get; private set; }
        // Код ответа
        public int StatusCode { get; private set; }
        // Расшифровка кода ответа
        public string StatusText { get; private set; }

        // Получить все заголовки одной строкой
        public string RawHeaders()
        {
            // Собираем строку заголовков
            string raw = string.Empty;
            // Перебираем все заголовки
            foreach (var header in Headers)
                // Добавляем к результату имя и параметры очередного заголовка
                raw += header.Key + ": " + header.Value + Client.CRLF;
            // Добавляем завершающий CRLF
            raw += Client.CRLF;
            // Возвращаем результат
            return raw;
        }
        // Добавить тело ответа
        public void AddBody(string rawBody)
        {
            Body = rawBody;
        }
        // Добавить заголовки
        public void AddHeaders(string rawHeaders)
        {
            Headers = new Dictionary<string, string>();
            // Разделяем заголовки
            string[] dataSeparator = new string[] { Client.CRLF + Client.CRLF };
            string[] headerSeparator = new string[] { Client.CRLF };
            string[] respData = rawHeaders.Split(dataSeparator, StringSplitOptions.None);
            string headersRaw = (rawHeaders.Contains(Client.CRLF + Client.CRLF) ? 
                rawHeaders.Remove(rawHeaders.IndexOf(Client.CRLF + Client.CRLF)) :
                rawHeaders);
            // Разделяем заголовки
            Queue<string> headers = new Queue<string>(headersRaw.Split(headerSeparator, StringSplitOptions.None));
            // Вытаскиваем из очереди первый заголовок содержащий протокол, код ответа
            string[] respinfo = headers.Dequeue().Split();
            // Если не задан протокол или статус код - бросаем исключение
            if (respinfo.Length < 2) throw new Exception("Response header info line error");
            // Получаем версию протокола
            ProtocolVersion = respinfo[0];
            // Получаем статус код
            StatusCode = int.Parse(respinfo[1]);
            // Если есть запоминаем также расшифровку статус кода
            if (respinfo.Length > 2)
                StatusText = respinfo[2];
            // Разбираем остальные заголовки
            while (headers.Count > 0)
            {
                // Получаем очередную строку
                string nextHeader = headers.Dequeue();
                // Ищем разделитель имени заголовка и параметров
                int sep = nextHeader.IndexOf(": ");
                if (sep == -1)
                    sep = nextHeader.IndexOf(":");
                if (sep == -1)
                    // Если не найден разделитель - игнорируем данный заголовок
                    continue;
                // Если разделитель найден - получаем имя и параметры заголовок
                string headerName = nextHeader.Substring(0, sep).Trim().ToLower();
                string headerParam = nextHeader.Substring(sep + 1).Trim().ToLower();
                // Если заголовок уже есть в списке. (Не запоминаем дублирующиеся заголовки, только уникальные)
                if (Headers.ContainsKey(headerName))
                {
                    // Просто обновим заголовок
                    Headers[headerName] = headerParam;
                }
                else
                {
                    // Иначе добавим новый
                    Headers.Add(headerName, headerParam);
                }
            }
        }
        
    }
}
