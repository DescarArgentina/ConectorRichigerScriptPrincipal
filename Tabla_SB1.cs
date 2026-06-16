using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WEB_SERVICE_RICHIGER
{
    public class Tabla_SB1
    {
        public static async Task postSB1(string jsonData)
        {

            string url = "https://richiger-protheus-rest-val.totvs.ar/rest/TCProductos/Incluir/";
            string username = "ADMIN"; // Usuario proporcionado
            string password = "Totvs2024##"; // Contraseña proporcionada

            JObject obj = JObject.Parse(jsonData);
            var productos = obj["producto"];

            string codigo = null;
            string descripcion = null;
            string revision = null;

            foreach (var item in productos)
            {
                var campo = item["campo"]?.ToString();
                var valor = item["valor"]?.ToString();

                if (campo == "codigo") codigo = valor;
                if (campo == "descripcion") descripcion = valor;
                //if (campo == "revision") revision = valor;
            }

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

                    // Leer el código de estado
                    int statusCode = (int)response.StatusCode;

                    // Leer la respuesta como string
                    string responseData = await response.Content.ReadAsStringAsync();

                    if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                    {
                        await putSB1(jsonData);
                        return;
                    }

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"[SB1] POST {codigo} -> OK ({statusCode})");
                        Utilidades.EscribirEnLog($"[SB1] POST {codigo} -> OK ({statusCode})");
                    }
                    else
                    {
                        Console.WriteLine($"[SB1] POST {codigo} -> ERROR ({statusCode}): {responseData}");
                        Utilidades.EscribirEnLog($"[SB1] POST {codigo} -> ERROR ({statusCode}): {responseData}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error al consumir el servicio SB1 post para {codigo}: {ex.Message}");
                    Utilidades.EscribirEnLog($"[SB1] EXCEPCIÓN POST {codigo}: {ex.Message}");
                }
            }
        }

        public static async Task putSB1(string jsonData)
        {

            string url = "https://richiger-protheus-rest-val.totvs.ar/rest/TCProductos/Modificar/";
            string username = "ADMIN"; // Usuario proporcionado
            string password = "Totvs2024##"; // Contraseña proporcionada

            JObject obj = JObject.Parse(jsonData);
            var productos = obj["producto"];

            string codigo = null;
            string descripcion = null;

            foreach (var item in productos)
            {
                var campo = item["campo"]?.ToString();
                var valor = item["valor"]?.ToString();

                if (campo == "codigo") codigo = valor;
                if (campo == "descripcion") descripcion = valor;
            }

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
                    HttpResponseMessage response = await client.PutAsync(url, content);

                    // Leer el código de estado
                    int statusCode = (int)response.StatusCode;

                    // Leer la respuesta como string
                    string responseData = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"[SB1] PUT  {codigo} -> OK ({statusCode})");
                        Utilidades.EscribirEnLog($"[SB1] PUT {codigo} -> OK ({statusCode})");
                    }
                    else
                    {
                        Console.WriteLine($"[SB1] PUT  {codigo} -> ERROR ({statusCode}): {responseData}");
                        Utilidades.EscribirEnLog($"[SB1] PUT {codigo} -> ERROR ({statusCode}): {responseData}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error al consumir el servicio SB1 put para {codigo}: {ex.Message}");
                    Utilidades.EscribirEnLog($"[SB1] EXCEPCIÓN PUT {codigo}: {ex.Message}");
                }
            }
        }

        public static List<string> jsonSB1()
        {
            //string connectionString = "Data Source=DEPLM-07-PC\\SQLEXPRESS;Initial Catalog=RichigerMBOM;User ID=sa;Password=infodba";
            string connectionString = Utilidades.ConnectionString;

            string query = @"SELECT DISTINCT
    Product.id_Table,
    CASE 
      WHEN Product.productId LIKE 'M-%' 
        THEN SUBSTRING(Product.productId, 3, LEN(Product.productId))
      ELSE Product.productId
    END AS codigo,
    LEFT(pr.name,60) as Descripcion,
    'PA' as tipo,
    '10' as deposito,
    MAX(CASE
        WHEN uud.title = 'Ric4_Unidad'     THEN 'UN'
        WHEN uud.title = 'Ric4_Kilogramos' THEN 'KG'
        WHEN uud.title = 'Ric4_Litros'     THEN 'L'
        WHEN uud.title = 'Ric4_Metros'     THEN 'MT'
        ELSE 'UN' 
    END) AS unMedida,
    pr.revision AS Revision,
    CASE 
      WHEN pr.name LIKE '%CONJ.CUBIERTAS%' OR pr.name LIKE '%GPS%' THEN 1 
      ELSE 0 
    END AS Fantasma
FROM Occurrence
JOIN ProductRevision pr ON Occurrence.instancedRef = pr.id_Table
JOIN Product ON pr.masterRef = Product.id_Table
LEFT JOIN Form f ON Product.ProductId = (CASE
    WHEN CHARINDEX('/', F.name) > 0 THEN LEFT(F.name, CHARINDEX('/', F.name) -1)
    ELSE F.name END)
LEFT JOIN UserValue_UserData uud ON f.id_Table + 9 = uud.id_Father
WHERE pr.subType <> 'Ric4_Ingenieria'
  AND Product.subType <> 'Ric4_Ingenieria'
GROUP BY
    Product.id_Table,
    Product.productId,
    pr.name,
    uud.title,
    Occurrence.parentRef,
    pr.revision;
";
            List<string> jsonProductos = new List<string>();

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.CommandTimeout = 1200;
                        using (SqlDataReader reader = command.ExecuteReader())
                        {

                            while (reader.Read())
                            {
                                string codigo = reader["codigo"].ToString();
                                string descripcion = reader["Descripcion"].ToString();
                                string tipo = reader["tipo"].ToString();
                                string deposito = reader["deposito"].ToString();
                                string unMedida = reader["unMedida"].ToString();
                                //string revision = reader["Revision"].ToString();
                                //string fantasma = reader["Fantasma"].ToString();
                                // Construir el JSON para cada producto
                                var producto = new
                                {
                                    producto = new List<Dictionary<string, string>>
                        {
                            new Dictionary<string, string> { { "campo", "codigo" }, { "valor",  codigo ?? ""} },
                            new Dictionary<string, string> { { "campo", "descripcion" }, { "valor",  descripcion ?? ""} },
                            new Dictionary<string, string> { { "campo", "tipo" }, { "valor",  tipo ?? ""} },
                            new Dictionary<string, string> { { "campo", "unMedida" }, { "valor",  unMedida ?? ""} },
                            new Dictionary<string, string> { { "campo", "deposito" }, { "valor",  deposito ?? ""} },
                            //new Dictionary<string, string> { { "campo", "revEstruct" },  {"valor", revision} },
                            //new Dictionary<string, string> { { "campo", "fantasma"}, {"valor",  fantasma} },
                        }
                                };

                                //poblarBase(codigo, descripcion, tipo, deposito, unMedida);
                                string jsonData = JsonConvert.SerializeObject(producto, Formatting.Indented);
                                // Console.WriteLine(jsonData);
                                jsonProductos.Add(jsonData); // Guardar el JSON en la lista
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al consultar la base de datos: {ex.Message}");
                Utilidades.EscribirEnLog($"[SB1] EXCEPCIÓN jsonSB1: {ex.Message}");
            }

            return jsonProductos;
        }

        public static List<string> jsonSB1_BOP()
        {
            string connectionString = Utilidades.ConnectionString;

            string queryMain = @"WITH ConsumedRaw AS (
    SELECT
        p.productId                              AS productId,
        LEFT(pr.name, 60)                        AS pr_name60,
        uud.title                                AS title,
        uud.value                                AS value
    FROM dbo.Occurrence o
    JOIN dbo.ProductRevision pr
        ON pr.id_Table = o.instancedRef
    JOIN dbo.Product p
        ON p.id_Table = pr.masterRef
    LEFT JOIN dbo.Form f
        ON p.productId = CASE
                            WHEN CHARINDEX('/', f.name) > 0 THEN LEFT(f.name, CHARINDEX('/', f.name) - 1)
                            ELSE f.name
                         END
    LEFT JOIN dbo.UserValue_UserData uud
        ON f.id_Table + 9 = uud.id_Father
    WHERE o.subType = 'MEConsumed'
      AND p.productId IS NOT NULL
      AND pr.subType <> 'Ric4_Ingenieria'
      AND p.subType <> 'Ric4_Ingenieria'
)
SELECT
    CASE
        WHEN productId LIKE 'M-%' THEN SUBSTRING(productId, 3, LEN(productId))
        ELSE productId
    END AS codigo,
    MAX(pr_name60) AS Descripcion,
    'PA' AS tipo,
    '10' AS deposito,
    COALESCE(
        NULLIF(
            MAX(CASE
                    WHEN title = 'Ric4_Unidad'     THEN 'UN'
                    WHEN title = 'Ric4_Kilogramos' THEN 'KG'
                    WHEN title = 'Ric4_Litros'     THEN 'L'
                    WHEN title = 'Ric4_Metros'     THEN 'MT'
                END),
            ''
        ),
        'UN'
    ) AS [Unidad de Medida]
FROM ConsumedRaw
GROUP BY CASE
        WHEN productId LIKE 'M-%' THEN SUBSTRING(productId, 3, LEN(productId))
        ELSE productId
    END
ORDER BY codigo;
;";

            string queryFallback = @"WITH ProcessData AS ( SELECT CAST(dbo.ProcessOccurrence.id_Table AS BIGINT) AS idTable, COALESCE(dbo.Process.catalogueId, dbo.Operation.catalogueId) AS catalogueId, COALESCE(dbo.Process.name, dbo.Operation.name) AS name, CAST(dbo.ProcessOccurrence.parentRef AS BIGINT) AS ParentRef, uud.value, COALESCE(dbo.Process.subType, dbo.Operation.subType) AS subtype, dbo.ProcessOccurrence.idXml FROM dbo.ProcessOccurrence LEFT JOIN dbo.ProcessRevision ON ProcessOccurrence.instancedRef = ProcessRevision.id_Table AND ProcessOccurrence.idXml = ProcessRevision.idXml LEFT JOIN dbo.Process ON Process.id_Table = ProcessRevision.masterRef AND ProcessRevision.idXml = Process.idXml LEFT JOIN dbo.OperationRevision ON OperationRevision.id_Table = ProcessOccurrence.instancedRef AND ProcessOccurrence.idXml = OperationRevision.idXml LEFT JOIN dbo.Operation ON Operation.id_Table = OperationRevision.masterRef AND OperationRevision.idXml = Operation.idXml LEFT JOIN dbo.Form f ON LEFT(f.name, LEN(f.name) - 2) = COALESCE(dbo.Process.catalogueId, dbo.Operation.catalogueId) LEFT JOIN dbo.Form f2 ON f.id_Table + 3 = f2.id_Table LEFT JOIN dbo.UserValue_UserData uud ON f2.id_Table + 1 = uud.id_Father AND uud.title = 'allocated_time' ), RootProcessData AS ( SELECT pd.catalogueId AS rootProcessId, pd.name AS rootProcessName, pd.idXml FROM ProcessData pd WHERE pd.subType = 'MEProcess' AND pd.ParentRef IS NULL ), RankedData AS ( SELECT dbo.Process.catalogueId AS instancedProcess, dbo.WorkArea.catalogueId AS instancedWorkArea, dbo.WorkArea.name, dbo.ProcessOccurrence.idXml FROM dbo.ProcessOccurrence JOIN dbo.Occurrence AS occ ON occ.parentRef = ProcessOccurrence.id_Table AND ProcessOccurrence.idXml = occ.idXml JOIN dbo.ProcessRevision ON ProcessRevision.id_Table = ProcessOccurrence.instancedRef AND ProcessOccurrence.idXml = ProcessRevision.idXml JOIN dbo.Process ON ProcessRevision.masterRef = Process.id_Table AND ProcessRevision.idXml = Process.idXml LEFT JOIN dbo.OperationRevision ON OperationRevision.id_Table = ProcessOccurrence.instancedRef AND ProcessOccurrence.idXml = OperationRevision.idXml LEFT JOIN dbo.Operation ON Operation.id_Table = OperationRevision.masterRef AND OperationRevision.idXml = Operation.idXml JOIN dbo.WorkAreaRevision ON WorkAreaRevision.id_Table = occ.instancedRef AND occ.idXml = WorkAreaRevision.idXml JOIN dbo.WorkArea ON WorkAreaRevision.masterRef = WorkArea.id_Table AND WorkAreaRevision.idXml = WorkArea.idXml WHERE occ.subType = 'MEWorkArea' ) SELECT MEProcess.catalogueId AS Process_catalogueId, MEProcess.name AS Process_name, TRY_CAST(CASE WHEN MEOP.catalogueId IS NOT NULL THEN CASE WHEN ISNUMERIC(MEOP.value) = 1 AND TRY_CAST(MEOP.value AS FLOAT) IS NOT NULL THEN COALESCE(TRY_CAST(MEOP.value AS decimal(18,2))/360.0, 0.1) ELSE 0.1 END ELSE CASE WHEN ISNUMERIC(MEProcess.value) = 1 AND TRY_CAST(MEProcess.value AS FLOAT) IS NOT NULL THEN COALESCE(TRY_CAST(MEProcess.value AS decimal(18,2))/360.0, 0.1) ELSE 0.1 END END AS FLOAT) AS tiempo, MEOP.catalogueId AS Operation_catalogueId, MEOP.name AS Operation_name, CASE WHEN rd.instancedWorkArea = '000485' THEN '481708' ELSE rd.instancedWorkArea END AS InstancedWorkArea, rd.name AS Workarea_name, REPLACE(rpd.rootProcessId, 'P-','') AS codigo, rpd.rootProcessName AS Descripcion, '10' as lote, 'PA' as tipo, '10' as deposito, 'UN' AS 'Unidad de Medida', MEProcess.idXml FROM ProcessData AS MEProcess LEFT JOIN ProcessData AS MEOP ON MEProcess.idTable = MEOP.ParentRef AND MEProcess.subType = 'MEProcess' AND MEOP.subType = 'MEOP' AND MEProcess.idXml = MEOP.idXml JOIN RankedData rd ON rd.instancedProcess = MEProcess.catalogueId AND rd.idXml = MEProcess.idXml JOIN RootProcessData rpd ON rpd.idXml = MEProcess.idXml WHERE MEProcess.subType = 'MEProcess' ORDER BY MEProcess.idXml, rpd.rootProcessId, MEProcess.idTable, MEOP.idTable";

            List<string> jsonProductos = new List<string>();

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    SqlCommand command = new SqlCommand(queryMain, connection);
                    command.CommandTimeout = 1200;
                    SqlDataReader reader = command.ExecuteReader();

                    // Procesar el reader (sea el Main o el Fallback)
                    using (reader)
                    {
                        while (reader.Read())
                        {
                            // Construir el JSON para cada producto
                            var producto = new
                            {
                                producto = new List<Dictionary<string, string>>
                        {
                            new Dictionary<string, string> { { "campo", "codigo" }, { "valor", reader["codigo"]?.ToString() ?? "" } },
                            new Dictionary<string, string> { { "campo", "descripcion" }, { "valor", reader["Descripcion"]?.ToString() ?? "" } },
                            new Dictionary<string, string> { { "campo", "tipo" }, { "valor", reader["tipo"]?.ToString() ?? "" } },
                            new Dictionary<string, string> { { "campo", "deposito" }, { "valor", reader["deposito"]?.ToString() ?? "" } },
                            new Dictionary<string, string> { { "campo", "unMedida" }, { "valor", reader["Unidad de Medida"]?.ToString() ?? "" } }
                        }
                            };

                            string jsonData = JsonConvert.SerializeObject(producto, Formatting.Indented);
                            // Console.WriteLine(jsonData);
                            jsonProductos.Add(jsonData); // Guardar el JSON en la lista
                        }
                    }

                    if (command != null) command.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error jsonSB1_BOP: {ex.Message}");
                Utilidades.EscribirEnLog($"[SB1] EXCEPCIÓN jsonSB1_BOP: {ex.Message}");
            }
            return jsonProductos;
        }

        public static void poblarBase(string codigo, string descripcion, string tipo, string deposito, string unMedida, string revision, int estado, string mensaje)
        {
            //string connectionString = "Data Source=DEPLM-07-PC\\SQLEXPRESS;Initial Catalog=ProtheusDescar;User ID=sa;Password=infodba";
            string connectionString = Utilidades.ConnectionString;

            string query = "  INSERT INTO SB1 (codigo, descripcion, tipo, deposito, unMedida, revision, estado, mensaje)\r\nSELECT @codigo, @descripcion, @tipo, @deposito, @unMedida, @revision, @estado, @mensaje\r\nWHERE NOT EXISTS (SELECT 1 FROM SB1 WHERE codigo = @codigo)";
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@codigo", codigo);
                        command.Parameters.AddWithValue("@descripcion", descripcion);
                        command.Parameters.AddWithValue("@tipo", tipo);
                        command.Parameters.AddWithValue("@deposito", deposito);
                        command.Parameters.AddWithValue("@unMedida", unMedida);
                        command.Parameters.AddWithValue("@revision", revision);
                        command.Parameters.AddWithValue("@estado", estado);
                        command.Parameters.AddWithValue("@mensaje", mensaje);
                        command.ExecuteNonQuery();
                    }

                }
            }
            catch (Exception ex)
            {

            }
        }

        public static void ActualizarBase(int estado, string mensaje, string codigo, string descripcion)
        {
            //string connectionString = "Data Source=DEPLM-07-PC\\SQLEXPRESS;Initial Catalog=ProtheusDescar;User ID=sa;Password=infodba";
            string connectionString = Utilidades.ConnectionString;

            string query = @"UPDATE SB1
                          SET estado = @estado, mensaje = @mensaje
                          WHERE codigo = @codigo AND descripcion = @descripcion 
--AND estado BETWEEN 400 AND 409";
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@estado", estado);
                        command.Parameters.AddWithValue("@mensaje", mensaje);
                        command.Parameters.AddWithValue("@codigo", codigo);
                        command.Parameters.AddWithValue("@descripcion", descripcion);
                        command.ExecuteNonQuery();
                    }

                }
            }
            catch (Exception ex)
            {

            }
        }
    }
}
