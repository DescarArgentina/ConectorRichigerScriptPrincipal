using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
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
            "Data Source=PC-01\\SQLEXPRESS;Initial Catalog=RichigerBOP;Integrated Security=True;TrustServerCertificate=True;";

        private const string CarpetaMbom = @"C:\Richiger\mbomRichiger";
        private const string CarpetaBop = @"C:\Richiger\BopRichiger";

        private const string CarpetaProcesadosMbom = @"C:\Richiger\mbomRichiger_procesados";
        private const string CarpetaProcesadosBop = @"C:\Richiger\BopRichiger_procesados";

        // Variable estática para definir el destino en tiempo de ejecución
        private static string CarpetaProcesados = @"C:\Richiger\bops_procesados";

        // -----------------------------------------------------------------------------------------
        // MAIN
        // -----------------------------------------------------------------------------------------


        static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("========================================================");
                Console.WriteLine("INICIO - Conector Richiger");
                Console.WriteLine($"Base de datos: RichigerBOP");
                Console.WriteLine($"Carpeta MBOM:  {CarpetaMbom}");
                Console.WriteLine($"Carpeta BOP:   {CarpetaBop}");
                Console.WriteLine($"Procesados:    {CarpetaProcesados}");
                Console.WriteLine("========================================================");
                Console.WriteLine();

                // Asegurar existencia entradas
                if (!Directory.Exists(CarpetaMbom)) Directory.CreateDirectory(CarpetaMbom);
                if (!Directory.Exists(CarpetaBop)) Directory.CreateDirectory(CarpetaBop);

                // (La carpeta de procesados se crea/asigna más adelante según selección)

                // Crear carpeta de logs para esta ejecución
                string logsRoot = @"C:\Richiger\logs";
                // Formato solicitado: d-M-yy (ej: 7-1-26) + hora
                string executionId = DateTime.Now.ToString("d-M-yy_HHmmss");
                string executionLogDir = Path.Combine(logsRoot, executionId);
                Directory.CreateDirectory(executionLogDir);

                // Configurar Utilidades para el log general
                Utilidades.LogFolder = executionLogDir;
                Utilidades.LogFileName = "General_Ejecucion.log";

                Console.WriteLine($"Carpeta de logs: {executionLogDir}");
                Utilidades.EscribirEnLog("Inicio de ejecución - " + executionId);

                string[] archivosMbom = Directory.GetFiles(CarpetaMbom, "*.xml");
                string[] archivosBop = Directory.GetFiles(CarpetaBop, "*.xml");

                string carpetaSeleccionada = "";
                bool isBopMode = false;

                if (archivosMbom.Length > 0)
                {
                    carpetaSeleccionada = CarpetaMbom;
                    CarpetaProcesados = CarpetaProcesadosMbom;
                    isBopMode = false;
                    Console.WriteLine("Detectados archivos en MBOM. Modo: MBOM.");

                    if (archivosBop.Length > 0)
                    {
                        Console.WriteLine("NOTA: También existen archivos en BOP, pero se prioriza MBOM.");
                    }
                }
                else if (archivosBop.Length > 0)
                {
                    carpetaSeleccionada = CarpetaBop;
                    CarpetaProcesados = CarpetaProcesadosBop;
                    isBopMode = true;
                    Console.WriteLine("Detectados archivos en BOP (MBOM vacío). Modo: BOP (Se omite SG1).");
                }
                else
                {
                    Console.WriteLine($"No se encontraron XML en ninguna carpeta ({CarpetaMbom} / {CarpetaBop}).");
                    Utilidades.EscribirEnLog("No se encontraron XML en las carpetas.");
                    return;
                }

                // Crear carpeta destino si no existe
                if (!Directory.Exists(CarpetaProcesados)) Directory.CreateDirectory(CarpetaProcesados);
                Console.WriteLine($"Carpeta Procesados: {CarpetaProcesados}");
                Console.WriteLine();

                string[] archivos = Directory.GetFiles(carpetaSeleccionada, "*.xml");
                Array.Sort(archivos, StringComparer.OrdinalIgnoreCase);

                if (archivos.Length == 0)
                {
                    Console.WriteLine("No se encontraron XML en la carpeta.");
                    Utilidades.EscribirEnLog("No se encontraron XML en la carpeta.");
                    return;
                }

                int okCount = 0;
                int errorCount = 0;
                int contadorXmls = 1;

                // Procesamiento secuencial: por cada XML => limpiar BD => cargar => JSONs => POST/PUT => mover a procesados
                foreach (string archivo in archivos)
                {
                    // Preparar logging individual
                    string nombreArchivo = Path.GetFileName(archivo);
                    // Log en carpeta de ejecución
                    string rutaLog = Path.Combine(executionLogDir, $"{nombreArchivo}.log");

                    // Guardar la salida original de la consola
                    TextWriter originalConsoleOut = Console.Out;
                    StreamWriter fileWriter = null;
                    MultiTextWriter multiWriter = null;

                    try
                    {
                        // Inicializar el writer del archivo y el multiwriter
                        fileWriter = new StreamWriter(rutaLog, append: true) { AutoFlush = true };
                        multiWriter = new MultiTextWriter(originalConsoleOut, fileWriter);

                        // Redirigir la consola
                        Console.SetOut(multiWriter);

                        Console.WriteLine("========================================================");
                        Console.WriteLine($"Archivo leído: {nombreArchivo}");
                        Console.WriteLine("========================================================");
                        Console.WriteLine($"Log generado en: {rutaLog}");
                        Console.WriteLine();

                        bool cargaOk = false;

                        try
                        {
                            // 1) Limpiar base para que este XML se procese aislado (sin mezclar con otros)
                            using (SqlConnection conn = new SqlConnection(ConnectionString))
                            {
                                conn.Open();
                                Console.WriteLine("Borrando TODAS las tablas de la base (dbo)...");
                                BorrarTodasLasTablas(conn);
                                Console.WriteLine("✔ Base limpia.");
                                Console.WriteLine();

                                // 2) Cargar XML a BD (tablas dinámicas por nodo)
                                XmlDocument xmlDoc = new XmlDocument();
                                xmlDoc.Load(archivo);

                                XmlNode root = xmlDoc.DocumentElement;
                                var groupedDataRows = new Dictionary<string, List<DataRow>>();

                                bool okParse = ParseNode(root, groupedDataRows);
                                if (!okParse || groupedDataRows.Count == 0)
                                {
                                    Console.WriteLine("No se detectaron nodos para cargar (ParseNode=false o sin resultados).");
                                    Utilidades.EscribirEnLog($"ParseNode sin resultados para {Path.GetFileName(archivo)}");
                                }
                                else
                                {
                                    CreateTable(conn, groupedDataRows);
                                    InsertData(conn, groupedDataRows, archivo, contadorXmls);

                                    Console.WriteLine($"Carga a BD OK. Tablas afectadas: {groupedDataRows.Count}");
                                    Utilidades.EscribirEnLog($"XML {Path.GetFileName(archivo)} cargado correctamente. Tablas: {groupedDataRows.Count}");
                                    cargaOk = true;
                                }

                                groupedDataRows.Clear();
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error al cargar el XML: {ex}");
                            Utilidades.EscribirEnLog($"Error al cargar el XML: {Path.GetFileName(archivo)}. Error: {ex}");
                        }

                        if (!cargaOk)
                        {
                            Console.WriteLine("Se omite envío (SG2/SH3, SB1, SG1) y movimiento del archivo por error de carga.");
                            Console.WriteLine();
                            errorCount++;
                            contadorXmls++;
                            continue;
                        }

                        bool envioOk = true;

                        // 3) Generación y envío de JSONs (para este XML)
                        // ---------------------------------------------------------------------------------


                        if (!isBopMode)
                        {
                            try
                            {
                                Console.WriteLine("=============== SB1 - PRODUCTOS SUELTOS - INICIO ===============");
                                if (ExisteTabla("Occurrence"))
                                {
                                    List<string> productos = Tabla_SB1.jsonSB1();

                                    foreach (var producto in productos)
                                    {
                                        Console.WriteLine("---- JSON SB1 ----");
                                        Console.WriteLine(producto);
                                        await Tabla_SB1.postSB1(producto);
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("No se ejecuta SB1: no existe la tabla dbo.Occurrence (la carga del XML no generó tablas).");
                                }
                                Console.WriteLine("================= SB1 - PRODUCTOS SUELTOS - FIN =================");
                                Console.WriteLine();
                            }
                            catch (Exception ex)
                            {
                                envioOk = false;
                                Console.WriteLine($"Error en SB1: {ex}");
                                Utilidades.EscribirEnLog($"Error en SB1 para {Path.GetFileName(archivo)}: {ex}");
                                Console.WriteLine();
                            }
                        }
                        else
                        {
                            Console.WriteLine("INFO: Se omite SB1 por estar en modo BOP.");
                            Utilidades.EscribirEnLog("Omitiendo SB1 (Modo BOP).");
                        }

                        if (!isBopMode)
                        {
                            try
                            {
                                Console.WriteLine("=================== SG1 - ESTRUCTURA - INICIO ===================");
                                if (ExisteTabla("Occurrence"))
                                {
                                    var estructuras = Tabla_SG1.jsonSG1();
                                    Console.WriteLine($"[DEBUG] SG1: {estructuras.Count} productos con estructura generados");
                                    await Tabla_SG1.postSG1(estructuras);
                                }
                                else
                                {
                                    Console.WriteLine("No se ejecuta SG1: no existe la tabla dbo.Occurrence (la carga del XML no generó tablas).");
                                }
                                Console.WriteLine("==================== SG1 - ESTRUCTURA - FIN =====================");
                                Console.WriteLine();
                            }
                            catch (Exception ex)
                            {
                                envioOk = false;
                                Console.WriteLine($"Error en SG1: {ex}");
                                Utilidades.EscribirEnLog($"Error en SG1 para {Path.GetFileName(archivo)}: {ex}");
                                Console.WriteLine();
                            }
                        }
                        else
                        {
                            Console.WriteLine("INFO: Se omite SG1 por estar en modo BOP.");
                            Utilidades.EscribirEnLog("Omitiendo SG1 (Modo BOP).");
                        }

                        // 3) SG2 / SH3 (Movido al final)
                        try
                        {
                            Console.WriteLine("=============== SG2 / SH3 - INICIO ===============");
                            if (ExisteTabla("ProcessOccurrence"))
                            {
                                List<string> procesos = Tablas_SG2_SH3.jsonSG2_SH3();
                                Console.WriteLine($"[DEBUG] SG2/SH3: {procesos.Count} items generados");

                                foreach (string json in procesos)
                                {
                                    Console.WriteLine("---- JSON SG2/SH3 ----");
                                    Console.WriteLine(json);
                                    await Tablas_SG2_SH3.postSG2_SH3(json);
                                }
                            }
                            else
                            {
                                Console.WriteLine("No se ejecuta SG2/SH3: no existe la tabla dbo.ProcessOccurrence (probable XML MBOM sin BOP).");
                            }
                            Console.WriteLine("================ SG2 / SH3 - FIN =================");
                            Console.WriteLine();
                        }
                        catch (Exception ex)
                        {
                            envioOk = false;
                            Console.WriteLine($"Error en SG2/SH3: {ex}");
                            Utilidades.EscribirEnLog($"Error en SG2/SH3 para {Path.GetFileName(archivo)}: {ex}");
                            Console.WriteLine();
                        }


                        // 4) Mover archivo a procesados si no hubo errores no controlados de envío
                        if (envioOk)
                        {
                            try
                            {
                                string destino = MoverArchivoAProcesados(archivo);
                                Console.WriteLine($"Archivo movido a: {destino}");
                                Utilidades.EscribirEnLog($"Archivo procesado y movido: {Path.GetFileName(archivo)} -> {destino}");
                                okCount++;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error al mover archivo a procesados: {ex}");
                                Utilidades.EscribirEnLog($"Error al mover archivo a procesados: {Path.GetFileName(archivo)}. Error: {ex}");
                                errorCount++;
                            }
                        }
                        else
                        {
                            Console.WriteLine("No se mueve el archivo a procesados porque hubo error(es) no controlado(s) durante el envío.");
                            Utilidades.EscribirEnLog($"No se movió a procesados (envío con error): {Path.GetFileName(archivo)}");
                            errorCount++;
                        }

                        Console.WriteLine();
                        contadorXmls++;
                    }
                    finally
                    {
                        // IMPORTANTE: Restaurar la consola original y cerrar el writer del archivo
                        if (multiWriter != null)
                        {
                            Console.SetOut(originalConsoleOut);
                            multiWriter.Dispose(); // Esto cierra fileWriter también
                        }
                    }
                }

                Console.WriteLine("========================================================");
                Console.WriteLine($"FIN - OK. Archivos procesados: {okCount}. Con error: {errorCount}.");
                Console.WriteLine("========================================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error general: {ex}");
                Utilidades.EscribirEnLog("Error general en Program.Main: " + ex);
            }
        }


        // -----------------------------------------------------------------------------------------
        // BORRADO TOTAL (requerimiento: "cada vez que se corra el programa, se borren TODAS las tablas")
        // -----------------------------------------------------------------------------------------


        private static string MoverArchivoAProcesados(string archivoOrigen)
        {
            if (string.IsNullOrWhiteSpace(archivoOrigen))
                throw new ArgumentException("Ruta de archivo origen vacía.", nameof(archivoOrigen));

            Directory.CreateDirectory(CarpetaProcesados);

            string nombre = Path.GetFileName(archivoOrigen);
            string destino = Path.Combine(CarpetaProcesados, nombre);

            // Evitar sobrescritura si el archivo ya existe en procesados
            if (File.Exists(destino))
            {
                string baseName = Path.GetFileNameWithoutExtension(nombre);
                string ext = Path.GetExtension(nombre);
                destino = Path.Combine(CarpetaProcesados, $"{baseName}_{DateTime.Now:yyyyMMdd_HHmmssfff}{ext}");
            }

            File.Move(archivoOrigen, destino);
            return destino;
        }







        private static void BorrarTodasLasTablas(SqlConnection connection)
        {
            try
            {
                Console.WriteLine("Borrando TODAS las tablas de la base (dbo)...");
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

                Console.WriteLine("✔ Tablas eliminadas.");
                Utilidades.EscribirEnLog("✔ Tablas eliminadas.");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al borrar tablas: {ex.Message}");
                Utilidades.EscribirEnLog($"❌ Error al borrar tablas: {ex.Message}");
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
        // UTILIDAD
        // -----------------------------------------------------------------------------------------
        static void LimpiarCarpeta(string carpeta)
        {
            try
            {
                if (Directory.Exists(carpeta))
                {
                    foreach (string archivo in Directory.GetFiles(carpeta))
                        File.Delete(archivo);

                    Utilidades.EscribirEnLog($"Carpeta {carpeta} limpiada correctamente.");
                }
            }
            catch (Exception ex)
            {
                Utilidades.EscribirEnLog($"Error al limpiar la carpeta: {ex.Message}");
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

                // Igual recorremos hijos aunque el nodo se ignore (para no cortar el árbol)
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

            // Si el nodo no tiene atributo "id" (y no es PLMXML), lo disambiguamos con el padre
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

                    // PK lógico (id_Table) o relación al padre (id_Father)
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
                            // Normalizaciones para ids (#idxxx / idxxx)
                            if (columnName == "id" || columnName == "instancedRef" || columnName == "masterRef" || columnName == "parentRef" || columnName == "instanceRefs")
                            {
                                string attributeValue1 = dataRow.XmlNode.Attributes[columnName]?.Value;

                                if (columnName == "id" && !string.IsNullOrEmpty(attributeValue1) && attributeValue1.Length > 2)
                                {
                                    hasIdAttribute = true;
                                    columnNames.Add("[id_Table]");
                                    parameterNames.Add("@id");
                                    attributeValue1 = attributeValue1.Substring(2); // "id123" -> "123"
                                    parameters.Add(new SqlParameter("@id", attributeValue1));
                                }
                                else if (columnName == "instancedRef" && !string.IsNullOrEmpty(attributeValue1) && attributeValue1.Length > 3)
                                {
                                    columnNames.Add("[instancedRef]");
                                    parameterNames.Add("@instancedRef");
                                    attributeValue1 = attributeValue1.Substring(3); // "#id123" -> "123"
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
                                    // Puede contener múltiples refs; se guarda textual.
                                    columnNames.Add("[instanceRefs]");
                                    parameterNames.Add("@instanceRefs");
                                    parameters.Add(new SqlParameter("@instanceRefs", attributeValue1));
                                }

                                continue;
                            }

                            // Atributos generales (NVARCHAR(MAX))
                            AlterTable(connection, tableName, columnName, "NVARCHAR(MAX)");
                            columnNames.Add($"[{columnName}]");
                            parameterNames.Add($"@{columnName}");

                            string attributeValue = dataRow.XmlNode.Attributes[columnName]?.Value ?? "";
                            parameters.Add(new SqlParameter($"@{columnName}", attributeValue));
                        }

                        // Contenido (innerText)
                        columnNames.Add("[contenido]");
                        parameterNames.Add("@contenido");
                        parameters.Add(new SqlParameter("@contenido", dataRow.XmlNode.InnerText ?? ""));

                        // FK lógico al padre si no hay id propio
                        if (!hasIdAttribute)
                        {
                            columnNames.Add("[id_Father]");
                            parameterNames.Add("@idFather");

                            XmlNode parentNode = dataRow.XmlNode.ParentNode;
                            string parentAttributeValue = parentNode?.Attributes?["id"]?.Value;

                            // "id123" -> "123"
                            string parentId = (!string.IsNullOrEmpty(parentAttributeValue) && parentAttributeValue.Length > 2)
                                ? parentAttributeValue.Substring(2)
                                : "0";

                            parameters.Add(new SqlParameter("@idFather", parentId));
                        }

                        // idXml (contador por archivo)
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
