using System.Data.SqlClient;
using Newtonsoft.Json;
using System.Globalization;
using System.Text;

namespace WEB_SERVICE_RICHIGER
{
    public class Tablas_SG2_SH3
    {
        public static async Task postSG2_SH3(string jsonData)
        {
            string url = "https://richiger-protheus-rest-val.totvs.ar/rest/TCProceso/Incluir/";

            string username = "ADMIN"; // Usuario proporcionado
            string password = "Totvs2024##"; // Contraseña proporcionada

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

                    Console.WriteLine($"POST TCProceso -> {(int)response.StatusCode} {response.ReasonPhrase}\n{responseData}");

                    if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                    {
                        Console.WriteLine("Ya existe (409). Intentando PUT /Modificar...");
                        await putSG2_SH3(jsonData);
                        return;
                    }

                    // Asegurarse de que la respuesta sea exitosa
                    //response.EnsureSuccessStatusCode();

                    // Mostrar la respuesta en consola
                    Console.WriteLine("Respuesta del servicio:");
                    Console.WriteLine(responseData);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error al consumir el servicio post SG2_SH3: {ex.Message}");
                }
            }
        }

        public static async Task putSG2_SH3(string jsonData)
        {
            string url = "https://richiger-protheus-rest-val.totvs.ar/rest/TCProceso/Modificar/";
            string username = "ADMIN";
            string password = "Totvs2024##";

            using (var client = new HttpClient())
            {
                try
                {
                    var credentials = Encoding.ASCII.GetBytes($"{username}:{password}");
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(credentials));

                    var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PutAsync(url, content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var bodyErr = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"PUT falló: {(int)response.StatusCode} {response.ReasonPhrase}\n{bodyErr}");
                        return;
                    }

                    string responseData = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("Respuesta del servicio (PUT ok):");
                    Console.WriteLine(responseData);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error al consumir el servicio put SG2_SH3: {ex.Message}");
                }
            }
        }

        public static List<string> jsonSG2_SH3()
        {

            //string connectionString = "Server=DEPLM-07-PC\\SQLEXPRESS;Database=RichigerBOP;Trusted_Connection=True;";
            string connectionString = "Data Source=PC-01\\SQLEXPRESS;Initial Catalog=RichigerBOP;Integrated Security=True;TrustServerCertificate=True;";



            string query = @"WITH ProcessData AS (
    SELECT
        CAST(po.id_Table AS BIGINT)                          AS idTable,
        COALESCE(p.catalogueId, o.catalogueId)               AS catalogueId,
        COALESCE(p.name, o.name)                             AS name,
        CAST(po.parentRef AS BIGINT)                         AS ParentRef,
        COALESCE(p.subType, o.subType)                       AS subtype,
        po.idXml                                             AS idXml,
        -- (si ya traés setup_value por otro lado, podés quitar estas 3 líneas)
        f_mast.name                                          AS masterFormName,
        uv_setup.value                                       AS setup_value
    FROM dbo.ProcessOccurrence po
    LEFT JOIN dbo.ProcessRevision pr
           ON po.instancedRef = pr.id_Table AND po.idXml = pr.idXml
    LEFT JOIN dbo.Process p
           ON p.id_Table = pr.masterRef AND pr.idXml = p.idXml
    LEFT JOIN dbo.OperationRevision opr
           ON opr.id_Table = po.instancedRef AND po.idXml = opr.idXml
    LEFT JOIN dbo.Operation o
           ON o.id_Table = opr.masterRef AND opr.idXml = o.idXml

    -- master form del PR por nombre: catalogueId + '/' + revision
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

RankedData AS (
    SELECT
        p.catalogueId  AS instancedProcess,
        wa.catalogueId AS instancedWorkArea,
        wa.name,
        po.idXml
    FROM dbo.ProcessOccurrence po
    JOIN dbo.Occurrence AS occ
      ON occ.parentRef = po.id_Table AND po.idXml = occ.idXml
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
    WHERE occ.subType = 'MEWorkArea'
),

/* ========= TimeAnalysis por ProcessOccurrence =========
   - Split de po.associatedAttachmentRefs: '#id22 #id25' → (22,25)
   - Join a AssociatedAttachment (id_Table)
   - Ir al Form por attachmentRef ('#id26' → 26)
   - Tomar UserValue 'allocated_time' por uv.id_Father = Form.id_Table + 1
*/
TA_ByPO AS (
    SELECT
        po.id_Table AS poId,
        po.idXml,
        SUM( TRY_CONVERT(decimal(18,4),
              REPLACE(NULLIF(LTRIM(RTRIM(uv.value)), ''), ',', '.')
            )
        ) AS ta_seconds
    FROM dbo.ProcessOccurrence po
    CROSS APPLY (
        SELECT TRY_CONVERT(int, REPLACE(value, '#id', '')) AS aaIdNum
        FROM STRING_SPLIT( ISNULL(po.associatedAttachmentRefs, ''), ' ' )
        WHERE value IS NOT NULL AND value <> ''
    ) s
    JOIN dbo.AssociatedAttachment aa
      ON aa.id_Table = s.aaIdNum
     AND aa.idXml    = po.idXml
     AND aa.role     = 'METimeAnalysisRelation'
    JOIN dbo.Form fta
      ON fta.id_Table = TRY_CONVERT(int, REPLACE(aa.attachmentRef, '#id', ''))
     AND fta.idXml    = aa.idXml
     AND fta.name = 'TimeAnalysis'
    JOIN dbo.UserValue_UserData uv
      ON uv.id_Father = fta.id_Table + 1
     AND uv.idXml     = fta.idXml
     AND uv.title     = 'allocated_time'
    GROUP BY po.id_Table, po.idXml
)

SELECT 
    TOP 1 MEProcess.catalogueId AS Process_catalogueId,
    MEProcess.name        AS Process_name,

    CASE 
      WHEN ta.ta_seconds IS NOT NULL AND TRY_CAST(ta.ta_seconds AS FLOAT) > 0
      THEN
        CAST(
            (CAST(ta.ta_seconds AS INT) / 3600)                        -- horas
            +
            (
                ((CAST(ta.ta_seconds AS INT) % 3600) / 60)             -- minutos (0–59)
                / 100.0                                                -- -> .MM
            )
            AS decimal(18,2)
        )
      ELSE 0.10
    END AS tiempo,

    -- Podés mantener estos para debug, no afectan el cálculo
    MEOP.catalogueId      AS Operation_catalogueId,
    MEOP.name             AS Operation_name,

    rd.name               AS Workarea_name,
    rd.instancedWorkArea  AS Workarea_code,

    REPLACE(rpd.rootProcessId, 'P-','M-') AS codigo,
    rpd.rootProcessName    AS Descripcion,
     CASE 
      WHEN TRY_CAST(ta.ta_seconds AS FLOAT) >= 60 THEN '1'
      ELSE '60'
     END                   AS lote,
    'PA'                   AS tipo,
    '10'                   AS deposito,
    'UN'                   AS [Unidad de Medida],

    MEProcess.idXml,
    -- Sugerencia: normalizar acá para evitar NULL en el JSON
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

-- vincular el TA del propio ProcessOccurrence del MEProcess
JOIN dbo.ProcessOccurrence po_me
  ON po_me.id_Table = MEProcess.idTable
 AND po_me.idXml    = MEProcess.idXml
LEFT JOIN TA_ByPO ta
  ON ta.poId  = po_me.id_Table
 AND ta.idXml = po_me.idXml

WHERE MEProcess.subType = 'MEProcess'
ORDER BY MEProcess.catalogueId;";

            List<string> jsonProductos = new List<string>();
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    using (SqlCommand command = new SqlCommand(query, connection))
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        // Diccionario para agrupar por producto
                        Dictionary<string, dynamic> productosDict = new Dictionary<string, dynamic>();
                        int operacion = 0;

                        while (reader.Read())
                        {
                            string producto = reader["codigo"]?.ToString();
                            string codigo = "01"; // Se mantiene como constante por ahora

                            string tiempo = reader["tiempo"]?.ToString().Replace(',', '.');
                            if (reader["tiempo"] == DBNull.Value)
                            {
                                tiempo = "0.00"; // default
                            }
                            else
                            {
                                var dec = Convert.ToDecimal(reader["tiempo"], CultureInfo.InvariantCulture);
                                tiempo = dec.ToString("0.00", CultureInfo.InvariantCulture); // <-- SIEMPRE 2 decimales
                            }

                            operacion = 10;
                            string nombreProceso = reader["Process_name"]?.ToString();
                            string loteStd = reader["lote"]?.ToString();
                            string cenTrab = reader["Workarea_code"]?.ToString();
                            string setup = reader["SetupTime"] == DBNull.Value ? "0.00" : Convert.ToDecimal(reader["SetupTime"], CultureInfo.InvariantCulture).ToString("0.00", CultureInfo.InvariantCulture);

                            // Crear el procedimiento
                            var procedimiento = new Procedimiento
                            {
                                detalle = new List<CampoValor>
                                {
                                    new CampoValor { campo = "operacion", valor = operacion.ToString() },
                                    new CampoValor { campo = "recurso", valor = "" },
                                    new CampoValor { campo = "tiempo", valor = tiempo },
                                    new CampoValor { campo = "setup", valor = setup },
                                    new CampoValor {campo = "descripcion", valor = nombreProceso },
                                    new CampoValor {campo = "loteStd", valor = loteStd},
                                    new CampoValor {campo = "centroTrab", valor = cenTrab }
                                }
                            };

                            // Verificar si el producto ya está en el diccionario
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

                        // Convertir cada elemento del diccionario en JSON
                        foreach (var item in productosDict.Values)
                        {
                            string json = JsonConvert.SerializeObject(item, Formatting.Indented);
                            jsonProductos.Add(json);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }

            return jsonProductos;
        }

        public static List<string> jsonSB1_BOP()
        {

            //string connectionString = "Server=DEPLM-07-PC\\SQLEXPRESS;Database=RichigerBOP;Trusted_Connection=True;";
            string connectionString = "Data Source=PC-01\\SQLEXPRESS;Initial Catalog=RichigerBOP;Integrated Security=True;TrustServerCertificate=True;";
            ;

            string query = @"WITH ProcessData AS (
                SELECT
                    CAST(dbo.ProcessOccurrence.id_Table AS BIGINT) AS idTable,
                    COALESCE(dbo.Process.catalogueId, dbo.Operation.catalogueId) AS catalogueId,
                    COALESCE(dbo.Process.name, dbo.Operation.name) AS name,
                    CAST(dbo.ProcessOccurrence.parentRef AS BIGINT) AS ParentRef,
                    uud.value,
                    COALESCE(dbo.Process.subType, dbo.Operation.subType) AS subtype,
                    dbo.ProcessOccurrence.idXml
                FROM dbo.ProcessOccurrence
                LEFT JOIN dbo.ProcessRevision ON ProcessOccurrence.instancedRef = ProcessRevision.id_Table AND ProcessOccurrence.idXml = ProcessRevision.idXml
                LEFT JOIN dbo.Process ON Process.id_Table = ProcessRevision.masterRef AND ProcessRevision.idXml = Process.idXml
                LEFT JOIN dbo.OperationRevision ON OperationRevision.id_Table = ProcessOccurrence.instancedRef AND ProcessOccurrence.idXml = OperationRevision.idXml
                LEFT JOIN dbo.Operation ON Operation.id_Table = OperationRevision.masterRef AND OperationRevision.idXml = Operation.idXml
                LEFT JOIN dbo.Form f ON LEFT(f.name, LEN(f.name) - 2) = COALESCE(dbo.Process.catalogueId, dbo.Operation.catalogueId)
                LEFT JOIN dbo.Form f2 ON f.id_Table + 3 = f2.id_Table
                LEFT JOIN dbo.UserValue_UserData uud ON f2.id_Table + 1 = uud.id_Father AND uud.title = 'allocated_time'
            ),

			-- CTE para obtener el proceso raíz (parentRef IS NULL)
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
                    dbo.Process.catalogueId AS instancedProcess,
                    dbo.WorkArea.catalogueId AS instancedWorkArea,
                    dbo.WorkArea.name,
                    dbo.ProcessOccurrence.idXml
					FROM
                    dbo.ProcessOccurrence
                JOIN dbo.Occurrence AS occ ON occ.parentRef = ProcessOccurrence.id_Table AND ProcessOccurrence.idXml = occ.idXml
                JOIN dbo.ProcessRevision ON ProcessRevision.id_Table = ProcessOccurrence.instancedRef AND ProcessOccurrence.idXml = ProcessRevision.idXml
                JOIN dbo.Process ON ProcessRevision.masterRef = Process.id_Table AND ProcessRevision.idXml = Process.idXml
                LEFT JOIN dbo.OperationRevision ON OperationRevision.id_Table = ProcessOccurrence.instancedRef AND ProcessOccurrence.idXml = OperationRevision.idXml
                LEFT JOIN dbo.Operation ON Operation.id_Table = OperationRevision.masterRef AND OperationRevision.idXml = Operation.idXml
                JOIN dbo.WorkAreaRevision ON WorkAreaRevision.id_Table = occ.instancedRef AND occ.idXml = WorkAreaRevision.idXml
                JOIN dbo.WorkArea ON WorkAreaRevision.masterRef = WorkArea.id_Table AND WorkAreaRevision.idXml = WorkArea.idXml
                WHERE
                    occ.subType = 'MEWorkArea'
            )

            SELECT 
                MEProcess.catalogueId AS Process_catalogueId,
                MEProcess.name AS Process_name,
                TRY_CAST(CASE
                            WHEN MEOP.catalogueId IS NOT NULL THEN
                                CASE 
                                    WHEN ISNUMERIC(MEOP.value) = 1 AND TRY_CAST(MEOP.value AS FLOAT) IS NOT NULL
                                    THEN COALESCE(TRY_CAST(MEOP.value AS decimal(18,2))/360.0, 0.1)
                                    ELSE 0.1
                                END
                            ELSE
                                CASE 
                                    WHEN ISNUMERIC(MEProcess.value) = 1 AND TRY_CAST(MEProcess.value AS FLOAT) IS NOT NULL
                                    THEN COALESCE(TRY_CAST(MEProcess.value AS decimal(18,2))/360.0, 0.1)
                                    ELSE 0.1
                                END
                        END AS FLOAT) AS tiempo, --Tiene que ser en horas
                MEOP.catalogueId AS Operation_catalogueId,
                MEOP.name AS Operation_name,
                CASE WHEN rd.instancedWorkArea = '000485' THEN '481708' ELSE rd.instancedWorkArea END AS InstancedWorkArea,
                rd.name AS Workarea_name,
                REPLACE(rpd.rootProcessId, 'P-','M-') AS codigo,
                rpd.rootProcessName AS Descripcion,
				'10' as lote,
                'PA' as tipo,
                '10' as deposito,
                'UN' AS 'Unidad de Medida',
                MEProcess.idXml
            FROM ProcessData AS MEProcess
            LEFT JOIN ProcessData AS MEOP
                ON MEProcess.idTable = MEOP.ParentRef
                AND MEProcess.subType = 'MEProcess'
                AND MEOP.subType = 'MEOP'
                AND MEProcess.idXml = MEOP.idXml
            JOIN RankedData rd 
                ON rd.instancedProcess = MEProcess.catalogueId
                AND rd.idXml = MEProcess.idXml
            JOIN RootProcessData rpd
                ON rpd.idXml = MEProcess.idXml  -- Une con el proceso raíz del mismo idXml
            WHERE MEProcess.subType = 'MEProcess'
            ORDER BY MEProcess.idXml, rpd.rootProcessId, MEProcess.idTable, MEOP.idTable;";

            List<string> jsonProductos = new List<string>();

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    using (SqlCommand command = new SqlCommand(query, connection))
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // Construir el JSON para cada producto
                            var producto = new
                            {
                                producto = new List<Dictionary<string, string>>
                        {
                            new Dictionary<string, string> { { "campo", "codigo" }, { "valor", reader["codigo"].ToString() } },
                            new Dictionary<string, string> { { "campo", "descripcion" }, { "valor", reader["Descripcion"].ToString() } },
                            new Dictionary<string, string> { { "campo", "tipo" }, { "valor", reader["tipo"].ToString() } },
                            new Dictionary<string, string> { { "campo", "deposito" }, { "valor", reader["deposito"].ToString() } },
                            new Dictionary<string, string> { { "campo", "unMedida" }, { "valor", reader["Unidad de Medida"].ToString() } }
                        }
                            };

                            string jsonData = JsonConvert.SerializeObject(producto, Formatting.Indented);
                            Console.WriteLine(jsonData);
                            jsonProductos.Add(jsonData); // Guardar el JSON en la lista
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al consultar la base de datos: {ex.Message}");
            }
            return jsonProductos;
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
