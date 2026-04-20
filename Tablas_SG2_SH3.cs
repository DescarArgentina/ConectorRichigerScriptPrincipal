using System.Data.SqlClient;
using Newtonsoft.Json;
using System.Globalization;
using System.Text;
using System.Net;
using System.Net.Http;

namespace WEB_SERVICE_RICHIGER
{
    public class Tablas_SG2_SH3
    {
        public static async Task postSG2_SH3(string jsonData)
        {
            string url = "https://richiger-protheus-rest-val.totvs.ar/rest/TCProceso/Incluir/";

            string username = "ADMIN"; // Usuario proporcionado
            string password = "Totvs2024##"; // Contraseña proporcionada

            string codigo = "N/A";
            try
            {
                var obj = Newtonsoft.Json.Linq.JObject.Parse(jsonData);
                codigo = obj["producto"]?.ToString() ?? "N/A";
            }
            catch { }

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    // Configurar credenciales Basic Auth
                    var credentials = Encoding.ASCII.GetBytes($"{username}:{password}");
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(credentials));

                    // Configurar el contenido de la solicitud
                    var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                    // Realizar la solicitud POST
                    HttpResponseMessage response = await client.PostAsync(url, content);

                    // Leer la respuesta como string
                    string responseData = await response.Content.ReadAsStringAsync();
                    int statusCode = (int)response.StatusCode;

                    if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                    {
                        await putSG2_SH3(jsonData, codigo);
                        return;
                    }

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"[SG2_SH3] POST {codigo} -> OK ({statusCode})");
                    }
                    else
                    {
                        Console.WriteLine($"[SG2_SH3] POST {codigo} -> ERROR ({statusCode}): {responseData}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SG2_SH3] Error al consumir el servicio post para {codigo}: {ex.Message}");
                }
            }
        }

        // public static async Task putSG2_SH3(string jsonData)
        // {
        //     string url = "https://richiger-protheus-rest-val.totvs.ar/rest/TCProceso/Modificar/";
        //     string username = "ADMIN";
        //     string password = "Totvs2024##";




        //     using (HttpClient client = new HttpClient())
        //     {
        //         try
        //         {
        //             // Configurar credenciales Basic Auth
        //             var credentials = Encoding.ASCII.GetBytes($"{username}:{password}");
        //             client.DefaultRequestHeaders.Authorization =
        //                 new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(credentials));
        //             Console.WriteLine("1");
        //             // Configurar el contenido de la solicitud
        //             var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
        //             Console.WriteLine("2");
        //             // Realizar la solicitud POST
        //             HttpResponseMessage response = await client.PutAsync(url, content);

        //             Console.WriteLine("3");
        //             // Leer la respuesta como string
        //             string responseData = await response.Content.ReadAsStringAsync();

        //             Console.WriteLine($"PUT TCProceso -> {(int)response.StatusCode} {response.ReasonPhrase}\n{responseData}");


        //             // Asegurarse de que la respuesta sea exitosa
        //             //response.EnsureSuccessStatusCode();
        //             Console.WriteLine("4");
        //             // Mostrar la respuesta en consola
        //             Console.WriteLine("Respuesta del putttttttttt:");
        //             Console.WriteLine(responseData);
        //         }
        //         catch (HttpRequestException ex)
        //         {
        //             Console.WriteLine("HttpRequestException:");
        //             Console.WriteLine(ex.ToString()); // incluye InnerException y stack

        //             if (ex.InnerException != null)
        //             {
        //                 Console.WriteLine("InnerException:");
        //                 Console.WriteLine(ex.InnerException.ToString());
        //             }
        //         }
        //         catch (TaskCanceledException ex)
        //         {
        //             // suele ser timeout
        //             Console.WriteLine("Timeout/TaskCanceled:");
        //             Console.WriteLine(ex.ToString());
        //         }
        //         catch (Exception ex)
        //         {
        //             Console.WriteLine("Exception:");
        //             Console.WriteLine(ex.ToString());
        //         }

        //     }

        //     // using (var client = new HttpClient())
        //     // {
        //     //     try
        //     //     {
        //     //         var credentials = Encoding.ASCII.GetBytes($"{username}:{password}");
        //     //         client.DefaultRequestHeaders.Authorization =
        //     //             new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(credentials));

        //     //         var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

        //     //         HttpResponseMessage response = await client.PutAsync(url, content);

        //     //         if (!response.IsSuccessStatusCode)
        //     //         {
        //     //             var bodyErr = await response.Content.ReadAsStringAsync();
        //     //             Console.WriteLine($"PUT falló: {(int)response.StatusCode} {response.ReasonPhrase}");
        //     //             Console.WriteLine($"Cuerpo error: {bodyErr}");
        //     //             Console.WriteLine($"JSON enviado: {jsonData}");
        //     //             return;
        //     //         }

        //     //         string responseData = await response.Content.ReadAsStringAsync();
        //     //         Console.WriteLine("Respuesta del servicio (PUT ok):");
        //     //         Console.WriteLine(responseData);
        //     //     }
        //     //     catch (Exception ex)
        //     //     {
        //     //         Console.WriteLine($"Error al consumir el servicio put SG2_SH3: {ex.Message}");
        //     //     }
        //     // }
        // }
        public static async Task putSG2_SH3(string jsonData, string codigo = "N/A")
        {
            var url = "https://richiger-protheus-rest-val.totvs.ar/rest/TCProceso/Modificar/";
            var username = "ADMIN";
            var password = "Totvs2024##";

            var handler = new SocketsHttpHandler
            {
                // (opcional) si hay proxy/cert inspection, se ajusta acá
                AllowAutoRedirect = false
            };

            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(60),
                DefaultRequestVersion = HttpVersion.Version11,
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
            };

            client.DefaultRequestHeaders.ExpectContinue = false;

            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

            var request = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Version = HttpVersion.Version11,
                VersionPolicy = HttpVersionPolicy.RequestVersionExact,
                Content = new StringContent(jsonData, Encoding.UTF8, "application/json")
            };

            // Lee headers primero (aunque el body venga cortado, al menos capturás status/headers si llegaron)
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[SG2_SH3] PUT  {codigo} -> OK ({(int)response.StatusCode})");
            }
            else
            {
                Console.WriteLine($"[SG2_SH3] PUT  {codigo} -> ERROR ({(int)response.StatusCode}): {body}");
            }
        }

        public static List<string> jsonSG2_SH3()
        {
            //string connectionString = "Server=DEPLM-07-PC\\SQLEXPRESS;Database=RichigerBOP;Trusted_Connection=True;";
            string connectionString = "Data Source=PC-01\\SQLEXPRESS;Initial Catalog=RichigerBOP;Integrated Security=True;TrustServerCertificate=True;";


            string queryMain = @"WITH ProcessData AS (
    SELECT
        CAST(po.id_Table AS BIGINT) AS idTable,
        COALESCE(p.catalogueId, o.catalogueId) AS catalogueId,
        COALESCE(p.name, o.name) AS name,
        CAST(po.parentRef AS BIGINT) AS ParentRef,
        COALESCE(p.subType, o.subType) AS subtype,
        po.idXml AS idXml,
        f_mast.name AS masterFormName,
        uv_setup.value AS setup_value
    FROM dbo.ProcessOccurrence po
    LEFT JOIN dbo.ProcessRevision pr
           ON po.instancedRef = pr.id_Table AND po.idXml = pr.idXml
    LEFT JOIN dbo.Process p
           ON p.id_Table = pr.masterRef AND pr.idXml = p.idXml
    LEFT JOIN dbo.OperationRevision opr
           ON opr.id_Table = po.instancedRef AND po.idXml = opr.idXml
    LEFT JOIN dbo.Operation o
           ON o.id_Table = opr.masterRef AND opr.idXml = o.idXml
    LEFT JOIN dbo.Form f_mast
           ON f_mast.name = p.catalogueId + '/' + pr.revision
          AND f_mast.idXml = po.idXml
    LEFT JOIN dbo.UserValue_UserData uv_setup
           ON uv_setup.id_Father = f_mast.id_Table + 1
          AND uv_setup.idXml     = f_mast.idXml
          AND uv_setup.title     = 'ric4_Setup'
),
RootProcessData AS (
    SELECT
        pd.catalogueId AS rootProcessId,
        pd.name        AS rootProcessName,
        pd.idXml
    FROM ProcessData pd
    WHERE pd.subType = 'MEProcess'
      AND pd.ParentRef IS NULL
),
WorkAreaOcc_All AS (
    SELECT
        o.id_Table,
        o.parentRef,
        o.instancedRef,
        o.subType,
        o.idXml
    FROM dbo.Occurrence o
    WHERE o.subType = 'MEWorkArea'
    UNION ALL
    SELECT
        w.id_Table,
        w.parentRef,
        w.instancedRef,
        w.subType,
        w.idXml
    FROM dbo.WorkAreaOccurrence w
    WHERE w.subType = 'MEWorkArea'
),
RankedData AS (
    SELECT DISTINCT
        p.catalogueId  AS instancedProcess,
        wa.catalogueId AS instancedWorkArea,
        wa.name,
        po.idXml
    FROM dbo.ProcessOccurrence po
    JOIN WorkAreaOcc_All occ
      ON occ.parentRef = po.id_Table
     AND occ.idXml     = po.idXml
    JOIN dbo.ProcessRevision pr
      ON pr.id_Table = po.instancedRef AND po.idXml = pr.idXml
    JOIN dbo.Process p
      ON pr.masterRef = p.id_Table AND pr.idXml = p.idXml
    LEFT JOIN dbo.OperationRevision opr
      ON opr.id_Table = po.instancedRef AND po.idXml = opr.idXml
    LEFT JOIN dbo.Operation o
      ON o.id_Table = opr.masterRef AND opr.idXml = o.idXml
    JOIN dbo.WorkAreaRevision war
      ON war.id_Table = occ.instancedRef AND occ.idXml = war.idXml
    JOIN dbo.WorkArea wa
      ON war.masterRef = wa.id_Table AND war.idXml = wa.idXml
),
TA_ByPO AS (
    SELECT
        po.id_Table AS poId,
        po.idXml,
        SUM(
            TRY_CONVERT(decimal(18,4),
                REPLACE(NULLIF(LTRIM(RTRIM(uv.value)), ''), ',', '.')
            )
        ) AS ta_seconds
    FROM dbo.ProcessOccurrence po
    CROSS APPLY (
        SELECT TRY_CONVERT(int, REPLACE(value, '#id', '')) AS aaIdNum
        FROM STRING_SPLIT(ISNULL(po.associatedAttachmentRefs, ''), ' ')
        WHERE value IS NOT NULL AND value <> ''
    ) s
    JOIN dbo.AssociatedAttachment aa
      ON aa.id_Table = s.aaIdNum
     AND aa.idXml    = po.idXml
     AND aa.role     = 'METimeAnalysisRelation'
    JOIN dbo.Form fta
      ON fta.id_Table = TRY_CONVERT(int, REPLACE(aa.attachmentRef, '#id', ''))
     AND fta.idXml    = aa.idXml
     AND fta.name     = 'TimeAnalysis'
    JOIN dbo.UserValue_UserData uv
      ON uv.id_Father = fta.id_Table + 1
     AND uv.idXml     = fta.idXml
     AND uv.title     = 'allocated_time'
    GROUP BY po.id_Table, po.idXml
)
SELECT 
    MEProcess.catalogueId AS Process_catalogueId,
    MEProcess.name        AS Process_name,
    CAST(COALESCE(ROUND(ta.ta_seconds, 0), 0) AS INT) AS tiempo_segundos,
    MEOP.catalogueId      AS Operation_catalogueId,
    MEOP.name             AS Operation_name,
    rd.name               AS Workarea_name,
    rd.instancedWorkArea  AS Workarea_code,
    REPLACE(REPLACE(rpd.rootProcessId, 'P-', ''), 'M-', '') AS codigo,
    rpd.rootProcessName    AS Descripcion,
    '1' AS lote,
    'PA' AS tipo,
    '10' AS deposito,
    'UN' AS [Unidad de Medida],
    MEProcess.idXml,
    COALESCE(
      TRY_CONVERT(decimal(18,2), REPLACE(NULLIF(MEProcess.setup_value,''), ',', '.')),
      0.00
    ) AS SetupTime
FROM ProcessData AS MEProcess
LEFT JOIN ProcessData AS MEOP
  ON MEProcess.idTable = MEOP.ParentRef
 AND MEProcess.subType = 'MEProcess'
 AND MEOP.subtype      = 'MEOP'
 AND MEProcess.idXml   = MEOP.idXml
JOIN RankedData rd 
  ON rd.instancedProcess = MEProcess.catalogueId
 AND rd.idXml            = MEProcess.idXml
JOIN RootProcessData rpd
  ON rpd.idXml = MEProcess.idXml
JOIN dbo.ProcessOccurrence po_me
  ON po_me.id_Table = MEProcess.idTable
 AND po_me.idXml    = MEProcess.idXml
LEFT JOIN TA_ByPO ta
  ON ta.poId  = po_me.id_Table
 AND ta.idXml = po_me.idXml
WHERE MEProcess.subType = 'MEProcess'
ORDER BY MEProcess.catalogueId;

;";

            string queryFallback = @"WITH ProcessData AS (
    SELECT
        CAST(po.id_Table AS BIGINT) AS idTable,
        COALESCE(p.catalogueId, o.catalogueId) AS catalogueId,
        COALESCE(p.name, o.name) AS name,
        CAST(po.parentRef AS BIGINT) AS ParentRef,
        COALESCE(p.subType, o.subType) AS subtype,
        po.idXml AS idXml,
        f_mast.name AS masterFormName,
        uv_setup.value AS setup_value
    FROM dbo.ProcessOccurrence po
    LEFT JOIN dbo.ProcessRevision pr
           ON po.instancedRef = pr.id_Table AND po.idXml = pr.idXml
    LEFT JOIN dbo.Process p
           ON p.id_Table = pr.masterRef AND pr.idXml = p.idXml
    LEFT JOIN dbo.OperationRevision opr
           ON opr.id_Table = po.instancedRef AND po.idXml = opr.idXml
    LEFT JOIN dbo.Operation o
           ON o.id_Table = opr.masterRef AND opr.idXml = o.idXml
    LEFT JOIN dbo.Form f_mast
           ON f_mast.name = p.catalogueId + '/' + pr.revision
          AND f_mast.idXml = po.idXml
    LEFT JOIN dbo.UserValue_UserData uv_setup
           ON uv_setup.id_Father = f_mast.id_Table + 1
          AND uv_setup.idXml     = f_mast.idXml
          AND uv_setup.title     = 'ric4_Setup'
),
RootProcessData AS (
    SELECT
        pd.catalogueId AS rootProcessId,
        pd.name AS rootProcessName,
        pd.idXml
    FROM ProcessData pd
    WHERE pd.subType = 'MEProcess'
      AND pd.ParentRef IS NULL
),
RankedData AS (
    SELECT
        p.catalogueId AS instancedProcess,
        wa.catalogueId AS instancedWorkArea,
        wa.name,
        po.idXml
    FROM dbo.ProcessOccurrence po
    JOIN dbo.Occurrence AS occ
      ON occ.parentRef = po.id_Table
     AND po.idXml      = occ.idXml
    JOIN dbo.ProcessRevision pr
      ON pr.id_Table = po.instancedRef
     AND po.idXml    = pr.idXml
    JOIN dbo.Process p
      ON pr.masterRef = p.id_Table
     AND pr.idXml     = p.idXml
    LEFT JOIN dbo.OperationRevision opr
      ON opr.id_Table = po.instancedRef
     AND po.idXml     = opr.idXml
    LEFT JOIN dbo.Operation o
      ON o.id_Table   = opr.masterRef
     AND opr.idXml    = o.idXml
    JOIN dbo.WorkAreaRevision war
      ON war.id_Table = occ.instancedRef
     AND occ.idXml    = war.idXml
    JOIN dbo.WorkArea wa
      ON war.masterRef = wa.id_Table
     AND war.idXml     = wa.idXml
    WHERE occ.subType = 'MEWorkArea'
),
TA_ByPO AS (
    SELECT
        po.id_Table AS poId,
        po.idXml,
        SUM(
            TRY_CONVERT(decimal(18,4),
                REPLACE(NULLIF(LTRIM(RTRIM(uv.value)), ''), ',', '.')
            )
        ) AS ta_seconds
    FROM dbo.ProcessOccurrence po
    CROSS APPLY (
        SELECT TRY_CONVERT(int, REPLACE(value, '#id', '')) AS aaIdNum
        FROM STRING_SPLIT(ISNULL(po.associatedAttachmentRefs, ''), ' ')
        WHERE value IS NOT NULL AND value <> ''
    ) s
    JOIN dbo.AssociatedAttachment aa
      ON aa.id_Table = s.aaIdNum
     AND aa.idXml    = po.idXml
     AND aa.role     = 'METimeAnalysisRelation'
    JOIN dbo.Form fta
      ON fta.id_Table = TRY_CONVERT(int, REPLACE(aa.attachmentRef, '#id', ''))
     AND fta.idXml    = aa.idXml
     AND fta.name     = 'TimeAnalysis'
    JOIN dbo.UserValue_UserData uv
      ON uv.id_Father = fta.id_Table + 1
     AND uv.idXml     = fta.idXml
     AND uv.title     = 'allocated_time'
    GROUP BY po.id_Table, po.idXml
)
SELECT
    MEProcess.catalogueId AS Process_catalogueId,
    MEProcess.name AS Process_name,
    CAST(COALESCE(ROUND(ta.ta_seconds, 0), 0) AS INT) AS tiempo_segundos,
    MEOP.catalogueId AS Operation_catalogueId,
    MEOP.name AS Operation_name,
    rd.name AS Workarea_name,
    rd.instancedWorkArea AS Workarea_code,
    REPLACE(REPLACE(rpd.rootProcessId, 'P-', ''), 'M-', '') AS codigo,
    rpd.rootProcessName AS Descripcion,
    '1' AS lote,
    'PA' AS tipo,
    '10' AS deposito,
    'UN' AS [Unidad de Medida],
    MEProcess.idXml,
    COALESCE(
        TRY_CONVERT(decimal(18,2), REPLACE(NULLIF(MEProcess.setup_value,''), ',', '.')),
        0.00
    ) AS SetupTime
FROM ProcessData AS MEProcess
LEFT JOIN ProcessData AS MEOP
  ON MEProcess.idTable = MEOP.ParentRef
 AND MEProcess.subType = 'MEProcess'
 AND MEOP.subtype      = 'MEOP'
 AND MEProcess.idXml   = MEOP.idXml
JOIN RankedData rd
  ON rd.instancedProcess = MEProcess.catalogueId
 AND rd.idXml            = MEProcess.idXml
JOIN RootProcessData rpd
  ON rpd.idXml = MEProcess.idXml
JOIN dbo.ProcessOccurrence po_me
  ON po_me.id_Table = MEProcess.idTable
 AND po_me.idXml    = MEProcess.idXml
LEFT JOIN TA_ByPO ta
  ON ta.poId  = po_me.id_Table
 AND ta.idXml = po_me.idXml
WHERE MEProcess.subType = 'MEProcess'
ORDER BY MEProcess.catalogueId;

";

            List<string> jsonProductos = new List<string>();

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    SqlCommand command = null;
                    SqlDataReader reader = null;

                    // Verificar si existe la tabla WorkAreaOccurrence
                    bool existeWorkAreaOccurrence = false;
                    string checkTableQuery = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'WorkAreaOccurrence'";

                    using (SqlCommand checkCmd = new SqlCommand(checkTableQuery, connection))
                    {
                        int count = (int)checkCmd.ExecuteScalar();
                        existeWorkAreaOccurrence = (count > 0);
                    }

                    // Seleccionar el query adecuado
                    string queryToExecute = existeWorkAreaOccurrence ? queryMain : queryFallback;

                    if (!existeWorkAreaOccurrence)
                    {
                        Console.WriteLine("[SG2] Tabla WorkAreaOccurrence NO encontrada. Usando query alternativo (Fallback)...");
                    }
                    else
                    {
                        // Console.WriteLine("[SG2] Tabla WorkAreaOccurrence encontrada. Usando query principal...");
                    }

                    command = new SqlCommand(queryToExecute, connection);
                    command.CommandTimeout = 1200;
                    reader = command.ExecuteReader();

                    // Procesar el reader (sea el Main o el Fallback)
                    using (reader)
                    {
                        jsonProductos = GenerarJsonDesdeReader(reader);
                    }

                    if (command != null) command.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error jsonSG2_SH3: " + ex.Message);
            }

            return jsonProductos;
        }

        private static int GCD(int a, int b)
        {
            while (b != 0)
            {
                int temp = b;
                b = a % b;
                a = temp;
            }
            return a;
        }

        private static List<string> GenerarJsonDesdeReader(SqlDataReader reader)
        {
            var result = new List<string>();
            Dictionary<string, dynamic> productosDict = new Dictionary<string, dynamic>();
            Dictionary<string, int> contadoresOperacion = new Dictionary<string, int>();

            while (reader.Read())
            {
                string producto = reader["codigo"]?.ToString();
                string codigo = "01";

                string cenTrab = reader["Workarea_code"]?.ToString();
                if (!string.IsNullOrEmpty(cenTrab) && cenTrab.Trim().StartsWith("TERCERO_CI", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int seconds = reader["tiempo_segundos"] == DBNull.Value ? 0 : Convert.ToInt32(reader["tiempo_segundos"]);

                string tiempoFormatted;
                string loteFormatted;

                if (seconds <= 0)
                {
                    // Si el tiempo es 0, enviar 0.01 y lote 60 según requerimiento
                    tiempoFormatted = "00.01";
                    loteFormatted = "60";
                }
                else
                {
                    // Cálculo de Lote Óptimo para precisión
                    int gcdVal = GCD(seconds, 60);
                    int L = 60 / gcdVal; // Lote

                    // Tiempo total para el lote en segundos
                    long T_seconds = (long)seconds * L;

                    // Tiempo total en minutos (siempre exacto por definición de GCD)
                    long T_minutes = T_seconds / 60;

                    // Formatear a H.MM (Protheus usa decimal para HH:MM)
                    long hh = T_minutes / 60;
                    long mm = T_minutes % 60;

                    double tDecimal = hh + (mm / 100.0);
                    tiempoFormatted = tDecimal.ToString("00.00", CultureInfo.InvariantCulture);
                    loteFormatted = L.ToString();
                }

                // Determinar el número de operación para este producto
                if (!contadoresOperacion.ContainsKey(producto))
                {
                    contadoresOperacion[producto] = 10;
                }
                else
                {
                    contadoresOperacion[producto] += 10;
                }
                int operacion = contadoresOperacion[producto];

                string nombreProceso = reader["Process_name"]?.ToString();
                // string cenTrab = reader["Workarea_code"]?.ToString(); // Ya leido arriba
                decimal dSetup = reader["SetupTime"] == DBNull.Value ? 0 : Convert.ToDecimal(reader["SetupTime"], CultureInfo.InvariantCulture);
                string setup = dSetup == 0 ? "" : dSetup.ToString("00.00", CultureInfo.InvariantCulture);

                var procedimiento = new Procedimiento
                {
                    detalle = new List<CampoValor>
                    {
                        new CampoValor { campo = "operacion", valor = operacion.ToString() },
                        new CampoValor { campo = "recurso", valor = "" },
                        new CampoValor { campo = "tiempo", valor = tiempoFormatted },
                        new CampoValor { campo = "setup", valor = setup },
                        new CampoValor {campo = "descripcion", valor = nombreProceso },
                        new CampoValor {campo = "loteStd", valor = loteFormatted},
                        new CampoValor {campo = "centroTrab", valor = cenTrab }
                    }
                };

                if (productosDict.ContainsKey(producto))
                {
                    productosDict[producto].procedimiento.Add(procedimiento);
                }
                else
                {
                    productosDict[producto] = new
                    {
                        codigo = codigo,
                        producto = producto,
                        procedimiento = new List<Procedimiento> { procedimiento }
                    };
                }
            }

            foreach (var item in productosDict.Values)
            {
                string json = JsonConvert.SerializeObject(item, Formatting.Indented);
                result.Add(json);
            }

            return result;
        }


        public class Procedimiento
        {
            public List<CampoValor> detalle { get; set; }
        }

        public class CampoValor
        {
            public string campo { get; set; }
            public string valor { get; set; }
        }

    }
}
