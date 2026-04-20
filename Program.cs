using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using WEB_SERVICE_RICHIGER;

namespace Web_Service
{
    internal class Program
    {
        // -----------------------------------------------------------------------------------------
        // CONFIG
        // -----------------------------------------------------------------------------------------
        private const string ConnectionString =
            "Data Source=SRV-PLM-01;Initial Catalog=procesosProductivos;User Id=infodba;Password=infodba;TrustServerCertificate=True;";

        private const string BaseProcesados = @"C:\DescarConector\02.1 PROCESADOS";
        private static readonly string CarpetaOK = Path.Combine(BaseProcesados, "OK");
        private static readonly string CarpetaERROR = Path.Combine(BaseProcesados, "ERROR");

        private const string RutaGeneralLog = @"C:\DescarConector\ScriptPrincipal.log";
        private const string NombreLogCarpeta = "procesamiento.log";
        private const string NombreBopError = "BOP_Error";

        // -----------------------------------------------------------------------------------------
        // MAIN
        // -----------------------------------------------------------------------------------------
        // Argumentos esperados (enviados por el Monitor):
        //   args[0] = mbomFolderPath       (C:\DescarConector\01.1 MBOMS\M-BOM_xxx)
        //   args[1] = mbomProcesadaPath    (...\M-BOM_xxx\MBOM_Procesada)
        //   args[2] = bopPendientesPath    (...\M-BOM_xxx\BOP_Pendientes)
        //   args[3] = bopProcesadasPath    (...\M-BOM_xxx\BOP_Procesadas)
        static async Task<int> Main(string[] args)
        {
            if (args.Length < 4)
            {
                LogGeneral("ERROR: Se requieren 4 argumentos (mbomFolder, mbomProcesada, bopPendientes, bopProcesadas).");
                Console.WriteLine("ERROR: Se requieren 4 argumentos.");
                return 1;
            }

            string mbomFolderPath = args[0].TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string mbomProcesadaPath = args[1];
            string bopPendientesPath = args[2];
            string bopProcesadasPath = args[3];

            string nombreCarpeta = Path.GetFileName(mbomFolderPath);
            string rutaLogCarpeta = Path.Combine(mbomFolderPath, NombreLogCarpeta);

            // Logs internos de SB1/SG1/SG2_SH3 van al mismo archivo de la carpeta
            Utilidades.LogFolder = mbomFolderPath;
            Utilidades.LogFileName = NombreLogCarpeta;

            LogGeneral($"INICIO carpeta: {nombreCarpeta}");
            LogCarpeta(rutaLogCarpeta, "========================================================");
            LogCarpeta(rutaLogCarpeta, $"INICIO procesamiento: {nombreCarpeta}");
            LogCarpeta(rutaLogCarpeta, "========================================================");

            try
            {
                if (!Directory.Exists(mbomFolderPath))
                {
                    LogGeneral($"ERROR: No existe carpeta {mbomFolderPath}");
                    return 1;
                }

                // 1) Buscar MBOM raíz
                var mbomFiles = Directory.GetFiles(mbomFolderPath, "*.xml", SearchOption.TopDirectoryOnly)
                    .Concat(Directory.GetFiles(mbomFolderPath, "*.plmxml", SearchOption.TopDirectoryOnly))
                    .ToArray();

                if (mbomFiles.Length == 0)
                {
                    LogGeneral($"ERROR: No se encontró XML raíz (MBOM) en {nombreCarpeta}. Carpeta → ERROR");
                    LogCarpeta(rutaLogCarpeta, "ERROR: No se encontró XML raíz (MBOM).");
                    MoverCarpetaAProcesados(mbomFolderPath, CarpetaERROR);
                    return 0;
                }

                string mbomXml = mbomFiles[0];
                string nombreMbom = Path.GetFileName(mbomXml);
                LogCarpeta(rutaLogCarpeta, $"MBOM detectada: {nombreMbom}");

                // 2) Procesar MBOM
                bool mbomOk = await ProcesarXml(mbomXml, isBopMode: false, rutaLogCarpeta);

                if (!mbomOk)
                {
                    LogGeneral($"MBOM FALLÓ en {nombreCarpeta}. Carpeta → ERROR");
                    LogCarpeta(rutaLogCarpeta, "MBOM procesamiento FALLÓ. Se omiten BOPs.");
                    Environment.CurrentDirectory = BaseProcesados;
                    MoverCarpetaAProcesados(mbomFolderPath, CarpetaERROR);
                    return 0;
                }

                LogGeneral($"MBOM OK en {nombreCarpeta}");
                LogCarpeta(rutaLogCarpeta, "MBOM procesada OK.");

                // 3) Mover MBOM a MBOM_Procesada
                try
                {
                    Directory.CreateDirectory(mbomProcesadaPath);
                    MoverArchivo(mbomXml, mbomProcesadaPath);
                    LogCarpeta(rutaLogCarpeta, $"MBOM movida a: {mbomProcesadaPath}");
                }
                catch (Exception ex)
                {
                    LogCarpeta(rutaLogCarpeta, $"ADVERTENCIA: no se pudo mover la MBOM: {ex.Message}");
                }

                // 4) Procesar BOPs
                var bopFiles = Directory.Exists(bopPendientesPath)
                    ? Directory.GetFiles(bopPendientesPath, "*.xml", SearchOption.TopDirectoryOnly)
                    : Array.Empty<string>();
                Array.Sort(bopFiles, StringComparer.OrdinalIgnoreCase);

                int bopsTotal = bopFiles.Length;
                int bopsOk = 0;
                int bopsError = 0;
                string bopErrorDir = Path.Combine(mbomFolderPath, NombreBopError);

                Directory.CreateDirectory(bopProcesadasPath);
                LogCarpeta(rutaLogCarpeta, $"BOPs a procesar: {bopsTotal}");

                foreach (string bop in bopFiles)
                {
                    string nombreBop = Path.GetFileName(bop);
                    try
                    {
                        bool bopOk = await ProcesarXml(bop, isBopMode: true, rutaLogCarpeta);
                        if (bopOk)
                        {
                            MoverArchivo(bop, bopProcesadasPath);
                            bopsOk++;
                            LogCarpeta(rutaLogCarpeta, $"BOP OK: {nombreBop}");
                        }
                        else
                        {
                            Directory.CreateDirectory(bopErrorDir);
                            MoverArchivo(bop, bopErrorDir);
                            bopsError++;
                            LogCarpeta(rutaLogCarpeta, $"BOP ERROR: {nombreBop} → {NombreBopError}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogCarpeta(rutaLogCarpeta, $"EXCEPCIÓN procesando BOP {nombreBop}: {ex.Message}");
                        try
                        {
                            Directory.CreateDirectory(bopErrorDir);
                            MoverArchivo(bop, bopErrorDir);
                        }
                        catch { }
                        bopsError++;
                    }
                }

                LogGeneral($"BOPs {nombreCarpeta}: Total={bopsTotal}, OK={bopsOk}, ERROR={bopsError}");
                LogCarpeta(rutaLogCarpeta, "========================================================");
                LogCarpeta(rutaLogCarpeta, $"Resumen BOPs: Total={bopsTotal}, OK={bopsOk}, ERROR={bopsError}");

                // 5) Mover carpeta completa a OK (criterio: si MBOM OK → OK, aunque haya BOPs fallados)
                // Salir del directorio antes de moverlo para liberar cualquier handle del proceso
                Environment.CurrentDirectory = BaseProcesados;
                MoverCarpetaAProcesados(mbomFolderPath, CarpetaOK);
                LogGeneral($"FIN carpeta {nombreCarpeta} → OK");

                return 0;
            }
            catch (Exception ex)
            {
                LogGeneral($"EXCEPCIÓN FATAL en {nombreCarpeta}: {ex.Message}");
                try
                {
                    LogCarpeta(rutaLogCarpeta, $"EXCEPCIÓN FATAL: {ex}");
                    if (Directory.Exists(mbomFolderPath))
                        MoverCarpetaAProcesados(mbomFolderPath, CarpetaERROR);
                }
                catch { }
                return 1;
            }
        }

        // -----------------------------------------------------------------------------------------
        // PROCESAR UN XML (MBOM o BOP): limpia BD → carga → envía SB1/SG1/SG2_SH3
        // -----------------------------------------------------------------------------------------
        private static async Task<bool> ProcesarXml(string rutaXml, bool isBopMode, string logCarpeta)
        {
            string nombreArchivo = Path.GetFileName(rutaXml);
            LogCarpeta(logCarpeta, $"--- Procesando {(isBopMode ? "BOP" : "MBOM")}: {nombreArchivo} ---");

            bool cargaOk = false;

            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();
                    BorrarTodasLasTablas(conn);

                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(rutaXml);
                    XmlNode root = xmlDoc.DocumentElement;
                    var grouped = new Dictionary<string, List<DataRow>>();

                    bool okParse = ParseNode(root, grouped);
                    if (!okParse || grouped.Count == 0)
                    {
                        LogCarpeta(logCarpeta, $"ParseNode sin resultados para {nombreArchivo}");
                        return false;
                    }

                    CreateTable(conn, grouped);
                    InsertData(conn, grouped, rutaXml, 1);
                    LogCarpeta(logCarpeta, $"Carga BD OK. Tablas: {grouped.Count}");
                    cargaOk = true;
                    grouped.Clear();
                }
            }
            catch (Exception ex)
            {
                LogCarpeta(logCarpeta, $"Error cargando XML {nombreArchivo}: {ex.Message}");
                return false;
            }

            if (!cargaOk) return false;

            bool envioOk = true;

            // SB1
            try
            {
                if (!isBopMode)
                {
                    if (ExisteTabla("Occurrence"))
                    {
                        var productos = Tabla_SB1.jsonSB1();
                        LogCarpeta(logCarpeta, $"SB1 MBOM: {productos.Count} productos");
                        foreach (var p in productos)
                        {
                            Console.WriteLine(p);
                            await Tabla_SB1.postSB1(p);
                        }
                    }
                    else
                    {
                        LogCarpeta(logCarpeta, "SB1 MBOM omitido: no existe tabla Occurrence.");
                    }
                }
                else
                {
                    if (ExisteTabla("ProcessOccurrence"))
                    {
                        var productos = Tabla_SB1.jsonSB1_BOP();
                        LogCarpeta(logCarpeta, $"SB1 BOP: {productos.Count} productos");
                        foreach (var p in productos)
                        {
                            Console.WriteLine(p);
                            await Tabla_SB1.postSB1(p);
                        }
                    }
                    else
                    {
                        LogCarpeta(logCarpeta, "SB1 BOP omitido: no existe tabla ProcessOccurrence.");
                    }
                }
            }
            catch (Exception ex)
            {
                envioOk = false;
                LogCarpeta(logCarpeta, $"Error en SB1: {ex.Message}");
            }

            // SG1
            try
            {
                if (!isBopMode)
                {
                    if (ExisteTabla("Occurrence"))
                    {
                        var estructuras = Tabla_SG1.jsonSG1();
                        LogCarpeta(logCarpeta, $"SG1 MBOM: {estructuras.Count} estructuras");
                        await Tabla_SG1.postSG1(estructuras);
                    }
                    else
                    {
                        LogCarpeta(logCarpeta, "SG1 MBOM omitido: no existe tabla Occurrence.");
                    }
                }
                else
                {
                    if (ExisteTabla("ProcessOccurrence"))
                    {
                        var estructuras = Tabla_SG1.jsonSG1_BOP();
                        LogCarpeta(logCarpeta, $"SG1 BOP: {estructuras.Count} estructuras");
                        await Tabla_SG1.postSG1(estructuras);
                    }
                    else
                    {
                        LogCarpeta(logCarpeta, "SG1 BOP omitido: no existe tabla ProcessOccurrence.");
                    }
                }
            }
            catch (Exception ex)
            {
                envioOk = false;
                LogCarpeta(logCarpeta, $"Error en SG1: {ex.Message}");
            }

            // SG2/SH3 (solo si hay ProcessOccurrence - típico de BOPs)
            try
            {
                if (ExisteTabla("ProcessOccurrence"))
                {
                    var procesos = Tablas_SG2_SH3.jsonSG2_SH3();
                    LogCarpeta(logCarpeta, $"SG2/SH3: {procesos.Count} items");
                    foreach (string j in procesos)
                    {
                        Console.WriteLine(j);
                        await Tablas_SG2_SH3.postSG2_SH3(j);
                    }
                }
                else
                {
                    LogCarpeta(logCarpeta, "SG2/SH3 omitido: no existe tabla ProcessOccurrence.");
                }
            }
            catch (Exception ex)
            {
                envioOk = false;
                LogCarpeta(logCarpeta, $"Error en SG2/SH3: {ex.Message}");
            }

            return envioOk;
        }

        // -----------------------------------------------------------------------------------------
        // HELPERS: movimiento y logging
        // -----------------------------------------------------------------------------------------
        private static void MoverArchivo(string archivoOrigen, string carpetaDestino)
        {
            Directory.CreateDirectory(carpetaDestino);
            string nombre = Path.GetFileName(archivoOrigen);
            string destino = Path.Combine(carpetaDestino, nombre);

            if (File.Exists(destino))
            {
                string baseName = Path.GetFileNameWithoutExtension(nombre);
                string ext = Path.GetExtension(nombre);
                destino = Path.Combine(carpetaDestino, $"{baseName}_{DateTime.Now:yyyyMMdd_HHmmssfff}{ext}");
            }

            File.Move(archivoOrigen, destino);
        }

        private static void MoverCarpetaAProcesados(string carpetaOrigen, string carpetaDestinoRaiz)
        {
            Directory.CreateDirectory(carpetaDestinoRaiz);
            string nombre = Path.GetFileName(carpetaOrigen.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string destino = Path.Combine(carpetaDestinoRaiz, nombre);

            if (Directory.Exists(destino))
                destino = Path.Combine(carpetaDestinoRaiz, $"{nombre}_{DateTime.Now:yyyyMMdd_HHmmssfff}");

            int intentos = 5;
            for (int i = 0; i < intentos; i++)
            {
                try
                {
                    Directory.Move(carpetaOrigen, destino);
                    return;
                }
                catch (IOException ex) when (i < intentos - 1)
                {
                    // Loggeamos el detalle completo para saber qué archivo está bloqueado
                    string logPath = Path.Combine(carpetaOrigen, NombreLogCarpeta);
                    LogCarpeta(logPath, $"REINTENTO {i + 1}/{ intentos - 1} mover carpeta. Motivo: {ex.Message}");
                    Thread.Sleep(2000);
                }
            }
        }

        private static void LogGeneral(string mensaje)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(RutaGeneralLog)!);
                File.AppendAllText(
                    RutaGeneralLog,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {mensaje}{Environment.NewLine}",
                    Encoding.UTF8);
            }
            catch { }
        }

        private static void LogCarpeta(string rutaLog, string mensaje)
        {
            try
            {
                string? dir = Path.GetDirectoryName(rutaLog);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(
                    rutaLog,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {mensaje}{Environment.NewLine}",
                    Encoding.UTF8);
            }
            catch { }
        }

        // -----------------------------------------------------------------------------------------
        // BORRADO TOTAL DE TABLAS (se ejecuta antes de cargar cada XML)
        // -----------------------------------------------------------------------------------------
        private static void BorrarTodasLasTablas(SqlConnection connection)
        {
            try
            {
                Utilidades.EscribirEnLog("Borrando TODAS las tablas de la base (dbo)...");

                string sql = @"
DECLARE @sql NVARCHAR(MAX);

-- 1) Drop FK constraints
SET @sql = N'';
SELECT @sql = STRING_AGG(
    N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + N'.' + QUOTENAME(OBJECT_NAME(parent_object_id)) +
    N' DROP CONSTRAINT ' + QUOTENAME(name),
    N'; '
)
FROM sys.foreign_keys;

IF @sql IS NOT NULL AND @sql <> N''
    EXEC sp_executesql @sql;

-- 2) Drop tables
SET @sql = N'';
SELECT @sql = STRING_AGG(
    N'DROP TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name),
    N'; '
)
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name = N'dbo';

IF @sql IS NOT NULL AND @sql <> N''
    EXEC sp_executesql @sql;
";

                using (SqlCommand cmd = new SqlCommand(sql, connection))
                {
                    cmd.ExecuteNonQuery();
                }

                Utilidades.EscribirEnLog("Tablas eliminadas.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al borrar tablas: {ex.Message}");
                Utilidades.EscribirEnLog($"Error al borrar tablas: {ex.Message}");
            }
        }

        private static bool ExisteTabla(string tableName)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(
                        "SELECT 1 FROM sys.tables WHERE name = @name AND schema_id = SCHEMA_ID('dbo');", conn))
                    {
                        cmd.Parameters.AddWithValue("@name", tableName);
                        object result = cmd.ExecuteScalar();
                        return result != null;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        // -----------------------------------------------------------------------------------------
        // CARGA XML -> TABLAS DINÁMICAS
        // -----------------------------------------------------------------------------------------
        static bool ParseNode(XmlNode node, Dictionary<string, List<DataRow>> groupedDataRows, string parentNodeName = "")
        {
            var listaIgnorados = new List<string>
            {
                "ApplicationRef","AssociatedDataSet","AttributeContext","DataSet","ExternalFile","Folder",
                "InstanceGraph","ProductDef","ProductInstance","ProductRevisionView","RevisionRule",
                "Site","Transform","View"
            };

            try
            {
                if (node.NodeType == XmlNodeType.Element && !listaIgnorados.Contains(node.Name))
                {
                    string nodeName = node.Name;

                    DataRow dataRow = new DataRow
                    {
                        NombreNodo = nodeName,
                        Atributos = new List<string>(),
                        XmlNode = node
                    };

                    if (node.Attributes != null)
                    {
                        foreach (XmlAttribute attribute in node.Attributes)
                            dataRow.Atributos.Add(attribute.Name);
                    }

                    string tableName = GetTableName(nodeName, dataRow.Atributos, parentNodeName);

                    if (!groupedDataRows.ContainsKey(tableName))
                        groupedDataRows[tableName] = new List<DataRow>();

                    groupedDataRows[tableName].Add(dataRow);

                    foreach (XmlNode childNode in node.ChildNodes)
                        ParseNode(childNode, groupedDataRows, nodeName);

                    return true;
                }

                foreach (XmlNode childNode in node.ChildNodes)
                    ParseNode(childNode, groupedDataRows, parentNodeName);

                return true;
            }
            catch (Exception ex)
            {
                Utilidades.EscribirEnLog("Excepción en ParseNode: " + ex.Message);
                return false;
            }
        }

        static string GetTableName(string nodeName, List<string> attributes, string parentNodeName)
        {
            string tableName = nodeName;

            if (!attributes.Contains("id") && tableName != "PLMXML")
                tableName = $"{nodeName}_{parentNodeName}";

            return tableName;
        }

        static void CreateTable(SqlConnection connection, Dictionary<string, List<DataRow>> groupedDataRows)
        {
            try
            {
                foreach (var group in groupedDataRows)
                {
                    string tableName = group.Key;
                    if (tableName == "PLMXML") continue;

                    string createTableQuery =
                        $"IF OBJECT_ID('[dbo].[{tableName}]', 'U') IS NULL " +
                        $"CREATE TABLE [dbo].[{tableName}] (id INT IDENTITY(1,1) PRIMARY KEY, contenido NVARCHAR(MAX)";

                    List<string> additionalAttributes = new List<string>();
                    bool hasIdAttribute = false;

                    foreach (DataRow dataRow in group.Value)
                    {
                        foreach (string attribute in dataRow.Atributos)
                        {
                            if (attribute == "id") hasIdAttribute = true;
                            if (attribute != "id" && !additionalAttributes.Contains(attribute))
                                additionalAttributes.Add(attribute);
                        }
                    }

                    createTableQuery += hasIdAttribute ? ", id_Table NVARCHAR(MAX)" : ", id_Father NVARCHAR(MAX)";

                    foreach (string columnName in additionalAttributes)
                        createTableQuery += $", [{columnName}] NVARCHAR(MAX)";

                    createTableQuery += ", idXml INT);";

                    using (SqlCommand command = new SqlCommand(createTableQuery, connection))
                        command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Utilidades.EscribirEnLog($"Excepción en CreateTable: {ex.Message}");
                throw;
            }
        }

        static void AlterTable(SqlConnection connection, string tableName, string columnName, string columnType)
        {
            try
            {
                string alterTableQuery =
                    $"IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME = '{tableName}' AND COLUMN_NAME = '{columnName}') " +
                    $"ALTER TABLE [dbo].[{tableName}] ADD [{columnName}] {columnType};";

                using (SqlCommand command = new SqlCommand(alterTableQuery, connection))
                    command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Utilidades.EscribirEnLog($"Excepción en AlterTable: {ex.Message}");
                throw;
            }
        }

        static void InsertData(SqlConnection connection, Dictionary<string, List<DataRow>> groupedDataRows, string xml, int contadorXmls)
        {
            try
            {
                foreach (var group in groupedDataRows)
                {
                    string tableName = group.Key;

                    foreach (DataRow dataRow in group.Value)
                    {
                        if (dataRow.NombreNodo == "PLMXML") continue;

                        string insertQuery = $"INSERT INTO [dbo].[{tableName}] (";
                        List<string> columnNames = new List<string>();
                        List<string> parameterNames = new List<string>();
                        List<SqlParameter> parameters = new List<SqlParameter>();

                        bool hasIdAttribute = false;

                        foreach (string columnName in dataRow.Atributos)
                        {
                            if (columnName == "id" || columnName == "instancedRef" || columnName == "masterRef" || columnName == "parentRef" || columnName == "instanceRefs")
                            {
                                string attributeValue1 = dataRow.XmlNode.Attributes[columnName]?.Value;

                                if (columnName == "id" && !string.IsNullOrEmpty(attributeValue1) && attributeValue1.Length > 2)
                                {
                                    hasIdAttribute = true;
                                    columnNames.Add("[id_Table]");
                                    parameterNames.Add("@id");
                                    attributeValue1 = attributeValue1.Substring(2);
                                    parameters.Add(new SqlParameter("@id", attributeValue1));
                                }
                                else if (columnName == "instancedRef" && !string.IsNullOrEmpty(attributeValue1) && attributeValue1.Length > 3)
                                {
                                    columnNames.Add("[instancedRef]");
                                    parameterNames.Add("@instancedRef");
                                    attributeValue1 = attributeValue1.Substring(3);
                                    parameters.Add(new SqlParameter("@instancedRef", attributeValue1));
                                }
                                else if (columnName == "masterRef" && !string.IsNullOrEmpty(attributeValue1) && attributeValue1.Length > 3)
                                {
                                    columnNames.Add("[masterRef]");
                                    parameterNames.Add("@masterRef");
                                    attributeValue1 = attributeValue1.Substring(3);
                                    parameters.Add(new SqlParameter("@masterRef", attributeValue1));
                                }
                                else if (columnName == "parentRef" && !string.IsNullOrEmpty(attributeValue1) && attributeValue1.Length > 3)
                                {
                                    columnNames.Add("[parentRef]");
                                    parameterNames.Add("@parentRef");
                                    attributeValue1 = attributeValue1.Substring(3);
                                    parameters.Add(new SqlParameter("@parentRef", attributeValue1));
                                }
                                else if (columnName == "instanceRefs" && !string.IsNullOrEmpty(attributeValue1))
                                {
                                    columnNames.Add("[instanceRefs]");
                                    parameterNames.Add("@instanceRefs");
                                    parameters.Add(new SqlParameter("@instanceRefs", attributeValue1));
                                }

                                continue;
                            }

                            AlterTable(connection, tableName, columnName, "NVARCHAR(MAX)");
                            columnNames.Add($"[{columnName}]");
                            parameterNames.Add($"@{columnName}");

                            string attributeValue = dataRow.XmlNode.Attributes[columnName]?.Value ?? "";
                            parameters.Add(new SqlParameter($"@{columnName}", attributeValue));
                        }

                        columnNames.Add("[contenido]");
                        parameterNames.Add("@contenido");
                        parameters.Add(new SqlParameter("@contenido", dataRow.XmlNode.InnerText ?? ""));

                        if (!hasIdAttribute)
                        {
                            columnNames.Add("[id_Father]");
                            parameterNames.Add("@idFather");

                            XmlNode parentNode = dataRow.XmlNode.ParentNode;
                            string parentAttributeValue = parentNode?.Attributes?["id"]?.Value;

                            string parentId = (!string.IsNullOrEmpty(parentAttributeValue) && parentAttributeValue.Length > 2)
                                ? parentAttributeValue.Substring(2)
                                : "0";

                            parameters.Add(new SqlParameter("@idFather", parentId));
                        }

                        columnNames.Add("[idXml]");
                        parameterNames.Add("@idXml");
                        parameters.Add(new SqlParameter("@idXml", contadorXmls));

                        insertQuery += string.Join(", ", columnNames) + ") VALUES (" + string.Join(", ", parameterNames) + ");";

                        using (SqlCommand command = new SqlCommand(insertQuery, connection))
                        {
                            command.Parameters.AddRange(parameters.ToArray());
                            command.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Utilidades.EscribirEnLog($"Excepción en InsertData: {ex.Message}");
                throw;
            }
        }
    }
}
