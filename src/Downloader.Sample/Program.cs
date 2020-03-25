using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;

namespace Downloader.Sample
{
    class Program
    {
        private static ProgressBar ConsoleProgress { get; set; }
        private static TaskCompletionSource<int> Tcs { get; set; }

        static void Main(string[] args)
        {
            Tcs = new TaskCompletionSource<int>();
            ConsoleProgress = new ProgressBar { BlockCount = 60 };
            Console.WriteLine("Downloading...");

            var ds = new DownloadService();
            ds.DownloadProgressChanged += OnDownloadProgressChanged;
            ds.DownloadFileCompleted += OnDownloadFileCompleted;
            // ds.DownloadFileAsync("https://download.taaghche.com/download/DBXP126H5eLD7avDHjMQp02IVVpnPnTO", "D:\\test.pdf", 10);
            // ds.DownloadFileAsync("https://qqy7vmtzwtueamf9z6as.4shared-uploadlilbox.top/Series/Forigen/Person.of.Interest/S01/720/Person.of.Interest.S01E01.720p.BluRay.PaHe.VinaDL.mkv",
            //                          @"C:\Users\Behza\Videos\FILIM\Person of Interest\PersonOfInterest.S01E01.mkv", 10);
            ds.DownloadFileAsync("http://telegramfiles.com/3352169/%5B@Movie_Soltaan%5D%20Bloodshot.2020.720p.WEB-DL.HardSub.mp4",
            @"C:\Users\Behza\Videos\FILIM\Bloodshot.2020.mkv", 1);

            Tcs.Task.Wait();
            Console.ReadKey();
        }

        private static async void OnDownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            await Task.Delay(1000);
            Console.WriteLine();

            if (e.Cancelled)
            {
                Console.WriteLine("Download canceled!");
                Tcs.TrySetCanceled(); // exit with error
            }
            else if (e.Error != null)
            {
                Console.Error.WriteLine(e.Error);
                Tcs.TrySetException(e.Error); // exit with error
            }
            else
            {
                Console.WriteLine("Download completed successfully.");
                Tcs.TrySetResult(0); // exit with no error
            }
        }

        private static void OnDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            Console.Title = $"Downloading ({e.ProgressPercentage:N2})  " +
                            $"{CalcMemoryMensurableUnit(e.BytesReceived)} / {CalcMemoryMensurableUnit(e.TotalBytesToReceive)}";
            ConsoleProgress.Report(e.ProgressPercentage / 100);
        }
        
        public static string CalcMemoryMensurableUnit(long bigUnSignedNumber)
        {
            var culture = CultureInfo.CreateSpecificCulture("en-US");

            var kb = bigUnSignedNumber / 1024; // · 1024 Bytes = 1 Kilobyte 
            var mb = kb / 1024; // · 1024 Kilobytes = 1 Megabyte 
            var gb = mb / 1024; // · 1024 Megabytes = 1 Gigabyte 
            var tb = gb / 1024; // · 1024 Gigabytes = 1 Terabyte 
            var pb = tb / 1024; // · 1024 Terabytes = 1 Petabyte 
            var eb = pb / 1024; // · 1024 Petabytes = 1 Exabyte
            var zb = eb / 1024; // · 1024 Exabytes = 1 Zettabyte 
            var yb = zb / 1024; // · 1024 Zettabytes = 1 Yottabyte 
            var bb = yb / 1024; // · 1024 Yottabytes = 1 Brontobyte
            var geoB = bb / 1024; // · 1024 Brontobytes = 1 Geopbyte
            var saganB = geoB / 1024; // . Saganbyte = 1024 Geopbyte
            var pijaB = saganB / 1024; // . Pijabyte = 1024 Saganbyte 
            var alphaB = pijaB / 1024; // . Alphabyte = 1024 Pijabyte 
            var kryatB = alphaB / 1024; // . Kryatbyte = 1024 Alphabyte 
            var amosB = kryatB / 1024; // . Amosbyte = 1024 Kryatbyte 
            var pectrolB = amosB / 1024; // . Pectrolbyte = 1024 Amosbyte
            var bolgerB = pectrolB / 1024; // . Bolgerbyte = 1024 Pectrolbyte 
            var samboB = bolgerB / 1024; // . Sambobyte = 1024 Bolgerbyte
            var quesaB = samboB / 1024; // . Quesabyte = 1024 Sambobyte 
            var kinsaB = quesaB / 1024; // . Kinsabyte = 1024 Quesabyte 
            var rutherB = kinsaB / 1024; // . Rutherbyte = 1024 Kinsabyte 
            var dubniB = rutherB / 1024; // . Dubnibyte = 1024 Rutherbyte 
            var seaborgB = dubniB / 1024; // . Seaborgbyte = 1024 Dubnibyte 
            var bohrB = seaborgB / 1024; // . Bohrbyte = 1024 Seaborgbyte 
            var hassiuB = bohrB / 1024; // . Hassiubyte = 1024 Bohrbyte 
            var meitnerbyte = hassiuB / 1024; // . Meitnerbyte = 1024 Hassiubyte
            var darmstadbyte = meitnerbyte / 1024; // . Darmstadbyte = 1024 Meitnerbyte
            var roentbyte = darmstadbyte / 1024; // . Roentbyte = 1024 Darmstadbyte
            var coperbyte = roentbyte / 1024; // . Coperbyte = 1024 Roentbyte 
            var koentekbyte = coperbyte / 1024; // . Koentekbyte = 1024 Coperbyte 
            var silvanikbyte = koentekbyte / 1024; // . Silvanikbyte = 1024 Koentekbyte 
            var golvanikbyte = silvanikbyte / 1024; // . Golvanikbyte = 1024 Silvanikbyte 
            var platvanikbyte = golvanikbyte / 1024; // . Platvanikbyte = 1024 Golvanikbyte 
            var einstanikbyte = platvanikbyte / 1024; // . Einstanikbyte = 1024 Platvanikbyte 
            var emeranikbyte = einstanikbyte / 1024; // . Emeranikbyte = 1024 Einstanikbyte 
            var rubanikbyte = emeranikbyte / 1024; // . Rubanikbyte = 1024 Emeranikbyte 
            var diamonikbyte = rubanikbyte / 1024; // . Diamonikbyte = 1024 Rubanikbyte 
            var amazonikbyte = diamonikbyte / 1024; // . Amazonikbyte = 1024 Diamonikbyte 
            var nilevanikbyte = amazonikbyte / 1024; // . Nilevanikbyte = 1024 Amazonikbyte 
            var infinitybyte = nilevanikbyte / 1024; // . Infinitybyte = 1024 Nilevanikbyte 
            var websitebyte = infinitybyte / 1024; // . Websitebyte = 1024 Infinitybyte

            return websitebyte > 1 ? string.Format(culture, "{0:N0} Websitebyte", websitebyte) :
                   infinitybyte > 1 ? string.Format(culture, "{0:N0} Infinitybyte", infinitybyte) :
                   nilevanikbyte > 1 ? string.Format(culture, "{0:N0} Nilevanikbyte", nilevanikbyte) :
                   amazonikbyte > 1 ? string.Format(culture, "{0:N0} Amazonikbyte", amazonikbyte) :
                   diamonikbyte > 1 ? string.Format(culture, "{0:N0} Diamonikbyte", diamonikbyte) :
                   rubanikbyte > 1 ? string.Format(culture, "{0:N0} Rubanikbyte", rubanikbyte) :
                   emeranikbyte > 1 ? string.Format(culture, "{0:N0} Emeranikbyte", emeranikbyte) :
                   einstanikbyte > 1 ? string.Format(culture, "{0:N0} Einstanikbyte", einstanikbyte) :
                   platvanikbyte > 1 ? string.Format(culture, "{0:N0} Platvanikbyte", platvanikbyte) :
                   golvanikbyte > 1 ? string.Format(culture, "{0:N0} Golvanikbyte", golvanikbyte) :
                   silvanikbyte > 1 ? string.Format(culture, "{0:N0} Silvanikbyte", silvanikbyte) :
                   koentekbyte > 1 ? string.Format(culture, "{0:N0} Koentekbyte", koentekbyte) :
                   coperbyte > 1 ? string.Format(culture, "{0:N0} Coperbyte", coperbyte) :
                   roentbyte > 1 ? string.Format(culture, "{0:N0} Roentbyte", roentbyte) :
                   darmstadbyte > 1 ? string.Format(culture, "{0:N0} Darmstadbyte", darmstadbyte) :
                   meitnerbyte > 1 ? string.Format(culture, "{0:N0} Meitnerbyte", meitnerbyte) :
                   hassiuB > 1 ? string.Format(culture, "{0:N0} Hassiubyte", hassiuB) :
                   bohrB > 1 ? string.Format(culture, "{0:N0} Bohrbyte", bohrB) :
                   seaborgB > 1 ? string.Format(culture, "{0:N0} Seaborgbyte", seaborgB) :
                   dubniB > 1 ? string.Format(culture, "{0:N0} Dubnibyte", dubniB) :
                   rutherB > 1 ? string.Format(culture, "{0:N0} Rutherbyte", rutherB) :
                   kinsaB > 1 ? string.Format(culture, "{0:N0} Kinsabyte", kinsaB) :
                   quesaB > 1 ? string.Format(culture, "{0:N0} Quesabyte", quesaB) :
                   samboB > 1 ? string.Format(culture, "{0:N0} Sambobyte", samboB) :
                   bolgerB > 1 ? string.Format(culture, "{0:N0} Bolgerbyte", bolgerB) :
                   pectrolB > 1 ? string.Format(culture, "{0:N0} Pectrolbyte", pectrolB) :
                   amosB > 1 ? string.Format(culture, "{0:N0} Amosbyte", amosB) :
                   kryatB > 1 ? string.Format(culture, "{0:N0} Kryatbyte", kryatB) :
                   alphaB > 1 ? string.Format(culture, "{0:N0} Alphabyte", alphaB) :
                   pijaB > 1 ? string.Format(culture, "{0:N0} Pijabyte", pijaB) :
                   saganB > 1 ? string.Format(culture, "{0:N0} Saganbyte", saganB) :
                   geoB > 1 ? string.Format(culture, "{0:N0} Geopbyte", geoB) :
                   bb > 1 ? string.Format(culture, "{0:N0} Brontobytes", bb) :
                   yb > 1 ? string.Format(culture, "{0:N0} Yottabytes", yb) :
                   zb > 1 ? string.Format(culture, "{0:N0} Zettabytes", zb) :
                   eb > 1 ? string.Format(culture, "{0:N0} Exabytes", eb) :
                   pb > 1 ? string.Format(culture, "{0:N0} Petabytes", pb) :
                   tb > 1 ? string.Format(culture, "{0:N0} Terabytes", tb) :
                   gb > 1 ? string.Format(culture, "{0:N0} Gigabytes", gb) :
                   mb > 1 ? string.Format(culture, "{0:N0} Megabytes", mb) :
                   kb > 1 ? string.Format(culture, "{0:N0} Kilobytes", kb) :
                   string.Format(culture, "{0:N0} Bytes", bigUnSignedNumber);
        }
    }
}
