using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Wenku8Spider
{
    class Section
    {
        public string name;
        public Chapter image = new Chapter();
        public List<Chapter> chapters = new List<Chapter>();
        public Queue<string> imageUrls = new Queue<string>();
    }

    class Chapter
    {
        public string name;
        public string url;
        public string content;
    }

    class Book
    {
        public string url;
        public string name;
        public List<Section> sections = new List<Section>();
    }

   
    class Program
    { 
        static string HTTPGet(string url)
        {
            WebClient c = new WebClient(); 
            c.Encoding = Encoding.GetEncoding("GBK");;
            var contentb = c.DownloadString(url);
            return contentb;
        }

        static void ProcessImage(Book book,Section s)
        {
            if (s == null)
                return;

            Console.WriteLine("Processing Image: " + s.name);

            var html = HTTPGet(book.url + s.image.url);

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);


            var content = htmlDoc.DocumentNode.SelectNodes("//*[contains(@class, 'imagecontent')]  ");

            if (content == null) return;

            foreach (var img in content)
            {
                var u = img.GetAttributeValue("src", "");
                if (u == null) continue;
                s.imageUrls.Enqueue(u);
            }

            WebClient wc = new WebClient();

            Chapter cp = new Chapter();

            cp.name = "插图";
            StringBuilder sb = new StringBuilder();
            foreach (var item in s.imageUrls)
            {
                var imgBuf = wc.DownloadData(item);
                string b64 = "data:image/jpeg;base64," + Convert.ToBase64String(imgBuf);

                sb.AppendLine("<div>");
                sb.AppendLine("<img src='" + b64 + "'/>");
                sb.AppendLine("</div>");
            }

            cp.url = s.image.url;
            cp.content = sb.ToString();
            s.chapters.Add(cp);
        }

        static void ProcessChapter(Book b,Chapter chap)
        {
            if (chap == null)
                return;

            Console.WriteLine("Processing Chapter: " + chap.name);

            var html = HTTPGet(b.url + chap.url);

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            var content = htmlDoc.DocumentNode.SelectSingleNode("//*[@id=\"content\"]");

            if (content == null) return;

            chap.content = content.InnerHtml;
        }

        static void ProcessSection(Book b,Section s)
        {
            List<Task> handler = new List<Task>();
            foreach (var ch in s.chapters)
            {
                handler.Add(Task.Run(() => { ProcessChapter(b, ch); }));
                
            }

            foreach (var item in handler)
            {
                item.Wait();
            }

            ProcessImage(b, s);
        }

        static void GenerateHTML(Book b)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<html>");
            sb.AppendLine("<header>");
            sb.AppendLine("<title>");
            sb.AppendLine(b.name);
            sb.AppendLine("</title>");
            sb.AppendLine("</header>");

            sb.AppendLine("<body>");

            foreach (var s in b.sections)
            {
                foreach (var c in s.chapters)
                {
                    sb.AppendLine("<h2 class='chapter'>" + s.name + " - " + c.name + "</h2>");
                    sb.AppendLine("<div>" + c.content + "</div>");
                }
            }

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");


            var curfolder = System.IO.Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]);

            System.IO.File.WriteAllText(curfolder + "/" + b.name + ".html", sb.ToString());

        }

        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); 
            Book book = new Book();
            book.url = args[0].Replace("index.htm","");
            var html = args[0];

            var content = HTTPGet(html);

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(content);
            
            var title = htmlDoc.DocumentNode.SelectSingleNode("//body/*[@id='title']");
            book.name = title.InnerText;

            var table = htmlDoc.DocumentNode.SelectSingleNode("//body/table");

            var tds = table.SelectNodes("//td");

            Section curSection = null;
            foreach (var td in tds)
            {
                if(td.HasClass("vcss"))
                {
                    curSection = new Section();
                    book.sections.Add(curSection);
                    curSection.name = td.InnerText;
                }
                else if ( td.HasClass("ccss") && curSection != null )
                {
                    var alink = td.SelectSingleNode("a");

                    if (alink == null)
                        continue;
                    Chapter c = new Chapter();
                    c.url = alink.GetAttributeValue("href","");
                    c.name = alink.InnerText;

                    if( c.name == "插图")
                    {
                        curSection.image = c;
                    }
                    else
                        curSection.chapters.Add(c);
                }
            }

            List<Task> tasks = new List<Task>();
            foreach (var sec in book.sections)
            {
                //ProcessSection(book, sec);
                tasks.Add(Task.Run(() => { ProcessSection(book, sec); }));
            }

            foreach (var item in tasks)
            {
                item.Wait();
            }

            GenerateHTML(book);
        }
    }
}
