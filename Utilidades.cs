using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WEB_SERVICE_RICHIGER
{
    public static class Utilidades
    {
        public const string ConnectionStringRichiger =
            "Data Source=SRV-PLM-01;Initial Catalog=procesosProductivos;User Id=infodba;Password=infodba;TrustServerCertificate=True;";

        public const string ConnectionStringDescar =
            "Data Source=PC-01\\SQLEXPRESS;Initial Catalog=RichigerBOP;Integrated Security=True;TrustServerCertificate=True;";

        public static string ConnectionString { get; set; } = ConnectionStringRichiger;

        public static string LogFolder { get; set; } = @"C:\Richiger";
        public static string LogFileName { get; set; } = "log.txt";

        public static string LogPath => Path.Combine(LogFolder, LogFileName);

        public static void EscribirEnLog(string mensaje)
        {
            // Asegurarse de que el directorio exista
            if (!Directory.Exists(LogFolder))
            {
                Directory.CreateDirectory(LogFolder);
            }

            File.AppendAllText(LogPath, $"{DateTime.Now} - {mensaje}{Environment.NewLine}");
        }
    }
}
