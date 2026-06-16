using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Globalization;

namespace WEB_SERVICE_RICHIGER
{
    public class DataModel
    {
        public string ParentName { get; set; }
        public string ParentCodigo { get; set; }
        public string ChildName { get; set; }
        public string ChildCodigo { get; set; }
        public string CantidadHijo { get; set; }
        public string Variante { get; set; }
        public string centroTrab { get; set; }
    }


    public class Tabla_SG1
    {
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(10);
        private readonly HttpClient _httpClient;
        public Tabla_SG1()
        {
            _httpClient = new HttpClient();
        }
        public static async Task postSG1(Dictionary<string, List<List<Dictionary<string, string>>>> estructuras)
        {
            // Upsert: intenta Incluir (POST) y si el ERP devuelve 409 (ya existe), reintenta con Modificar (PUT).
            string urlPost = "https://richiger-protheus-rest-val.totvs.ar/rest/TCEstructura/Incluir/";
            string urlPut = "https://richiger-protheus-rest-val.totvs.ar/rest/TCEstructura/Modificar/";
            string username = "ADMIN";
            string password = "Totvs2024##";

            using (HttpClient client = new HttpClient { Timeout = RequestTimeout })
            {
                var credentials = Encoding.ASCII.GetBytes($"{username}:{password}");
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(credentials));

                foreach (var parent in estructuras)
                {
                    var jsonBody = new
                    {
                        producto = parent.Key,
                        qtdBase = "1",
                        estructura = parent.Value
                    };

                    string jsonData = JsonConvert.SerializeObject(jsonBody, Newtonsoft.Json.Formatting.Indented);
                    Console.WriteLine(jsonData);

                    // 1) POST /Incluir
                    int statusCodePost = 0;
                    string responsePost = string.Empty;

                    try
                    {
                        var contentPost = new StringContent(jsonData, Encoding.UTF8, "application/json");
                        HttpResponseMessage respPost = await client.PostAsync(urlPost, contentPost);

                        statusCodePost = (int)respPost.StatusCode;
                        responsePost = await respPost.Content.ReadAsStringAsync();

                        if (statusCodePost >= 200 && statusCodePost <= 299)
                        {
                            Console.WriteLine($"[SG1] POST {parent.Key} -> OK ({statusCodePost})");
                            Utilidades.EscribirEnLog($"[SG1] POST {parent.Key} -> OK ({statusCodePost})");
                            continue;
                        }

                        // 409 = ya existe en ERP: reintentar con PUT /Modificar
                        if (statusCodePost == 409)
                        {
                            var contentPut = new StringContent(jsonData, Encoding.UTF8, "application/json");
                            HttpResponseMessage respPut = await client.PutAsync(urlPut, contentPut);

                            int statusCodePut = (int)respPut.StatusCode;
                            string responsePut = await respPut.Content.ReadAsStringAsync();

                            if (statusCodePut >= 200 && statusCodePut <= 299)
                            {
                                Console.WriteLine($"[SG1] PUT  {parent.Key} -> OK ({statusCodePut})");
                                Utilidades.EscribirEnLog($"[SG1] PUT {parent.Key} -> OK ({statusCodePut})");
                            }
                            else
                            {
                                Console.WriteLine($"[SG1] PUT  {parent.Key} -> ERROR ({statusCodePut}): {responsePut}");
                                Utilidades.EscribirEnLog($"[SG1] PUT {parent.Key} -> ERROR ({statusCodePut}): {responsePut}");
                            }

                            continue;
                        }

                        // Otros códigos: loguear el cuerpo de respuesta para diagnóstico
                        Console.WriteLine($"[SG1] POST {parent.Key} -> ERROR ({statusCodePost}): {responsePost}");
                        Utilidades.EscribirEnLog($"[SG1] POST {parent.Key} -> ERROR ({statusCodePost}): {responsePost}");
                    }
                    catch (TaskCanceledException ex)
                    {
                        Console.WriteLine($"[SG1] TIMEOUT enviando estructura {parent.Key} tras {RequestTimeout.TotalSeconds:0} segundos: {ex.Message}");
                        Utilidades.EscribirEnLog($"[SG1] TIMEOUT POST {parent.Key} tras {RequestTimeout.TotalSeconds:0} segundos: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SG1] Error al enviar estructura {parent.Key}: {ex.Message}");
                        Utilidades.EscribirEnLog($"[SG1] EXCEPCIÓN POST {parent.Key}: {ex.Message}");
                    }
                }
            }
        }

        public static Dictionary<string, List<List<Dictionary<string, string>>>> jsonSG1()
        {

            string connectionString = Utilidades.ConnectionString;

            string query = @"WITH CTE_Hierarchy AS (
    SELECT DISTINCT
        Occurrence.id_table,
        ProductRevision.name,
        CASE 
          WHEN Product.productId LIKE 'M-%' 
            THEN SUBSTRING(Product.productId, 3, LEN(Product.productId))
          WHEN Product.productId LIKE 'P-%' 
            THEN SUBSTRING(Product.productId, 3, LEN(Product.productId))
          ELSE Product.productId
        END AS codigo,
        SUM(TRY_CAST(
            CASE
                WHEN UserValue_UserData.value = '' THEN '1'
                ELSE UserValue_UserData.value
            END AS FLOAT
        )) AS Cantidad,
        CAST(Occurrence.parentRef AS INT) AS parentRef,
        uud2.value AS Variante
    FROM Occurrence
    LEFT JOIN ProductRevision 
        ON Occurrence.instancedRef = ProductRevision.id_Table
    LEFT JOIN Product 
        ON ProductRevision.masterRef = Product.id_Table
    LEFT JOIN UserValue_UserData 
        ON Occurrence.id_Table + 2 = UserValue_UserData.id_Father
       AND UserValue_UserData.title = 'Quantity'
    LEFT JOIN UserValue_UserData uud2 
        ON Occurrence.id_Table - 1 = uud2.id_Father
       AND uud2.title = 'bl_formula'
    WHERE ProductRevision.subType <> 'Ric4_Ingenieria'
      AND Product.subType <> 'Ric4_Ingenieria'
    GROUP BY
        ProductRevision.name,
        Product.productId,
        Occurrence.parentRef,
        Occurrence.id_table,
        ProductRevision.id_Table,
        UserValue_UserData.value,
        uud2.value
)
SELECT DISTINCT
    Parent.name AS ParentName,
    Parent.codigo AS ParentCodigo,
    Child.name AS ChildName,
    Child.codigo AS ChildCodigo,
    SUM(Child.Cantidad) AS CantidadHijo,
    Child.Variante
FROM CTE_Hierarchy Parent
INNER JOIN CTE_Hierarchy Child
    ON Parent.id_table = Child.parentRef
WHERE Parent.codigo <> Child.codigo
GROUP BY
    Parent.id_table,
    Parent.name,
    Parent.codigo,
    Child.name,
    Child.codigo,
    Child.Variante;
";

            Dictionary<string, List<List<Dictionary<string, string>>>> estructuras = new Dictionary<string, List<List<Dictionary<string, string>>>>();
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.CommandTimeout = 5000;
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            Dictionary<string, List<DataModel>> dataByParent = new Dictionary<string, List<DataModel>>();

                            // Primero, agrupamos todos los datos por ParentCodigo para procesarlos juntos
                            while (reader.Read())
                            {
                                // Check for null or empty ParentCodigo
                                string parentName = reader["ParentName"]?.ToString() ?? string.Empty;
                                string parentCodigo = reader["ParentCodigo"]?.ToString();
                                string childName = reader["ChildName"]?.ToString() ?? string.Empty;
                                string childCodigo = reader["ChildCodigo"]?.ToString() ?? string.Empty;
                                string cantidadHijoRaw = (reader["CantidadHijo"]?.ToString() ?? "0").Replace(',', '.');

                                if (!double.TryParse(cantidadHijoRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out double cantidadVal))
                                    cantidadVal = 0;

                                cantidadVal = Math.Truncate(cantidadVal * 1000) / 1000.0;

                                string cantidadHijo = cantidadVal.ToString("0.000", CultureInfo.InvariantCulture);

                                if (string.IsNullOrEmpty(parentCodigo))
                                {
                                    Console.WriteLine("WARNING: Skipping record with null or empty ParentCodigo");
                                    continue;  // Skip this record
                                }

                                var model = new DataModel
                                {
                                    ParentName = parentName,
                                    ParentCodigo = parentCodigo,
                                    ChildName = childName,
                                    ChildCodigo = childCodigo,
                                    CantidadHijo = cantidadHijo
                                };

                                if (!dataByParent.ContainsKey(model.ParentCodigo))
                                {
                                    dataByParent[model.ParentCodigo] = new List<DataModel>();
                                }

                                dataByParent[model.ParentCodigo].Add(model);
                            }

                            // Ahora procesamos cada grupo
                            foreach (var parentGroup in dataByParent)
                            {
                                string parentCodigo = parentGroup.Key;
                                List<DataModel> children = parentGroup.Value;

                                if (string.IsNullOrEmpty(parentCodigo))
                                {
                                    Console.WriteLine("WARNING: Skipping group with null or empty ParentCodigo");
                                    continue;
                                }

                                if (!estructuras.ContainsKey(parentCodigo))
                                {
                                    estructuras[parentCodigo] = new List<List<Dictionary<string, string>>>();
                                }

                                foreach (var child in children)
                                {
                                    var childStructure = new List<Dictionary<string, string>>
                                    {
                                        new Dictionary<string, string> { { "campo", "codigo" }, { "valor", child.ChildCodigo } },
                                        new Dictionary<string, string> { { "campo", "cantidad" }, { "valor", child.CantidadHijo } }
                                    };

                                    // If variant is empty, no additional fields needed
                                    if (string.IsNullOrEmpty(child.Variante))
                                    {
                                        estructuras[parentCodigo].Add(childStructure);
                                        continue;
                                    }

                                    // Extract conditions of this child
                                    Dictionary<string, string> conditions;
                                    try
                                    {
                                        conditions = ExtractAllConditions(child.Variante);
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"Error extracting conditions from '{child.Variante}': {ex.Message}");
                                        // Add child anyway, without additional conditions
                                        estructuras[parentCodigo].Add(childStructure);
                                        continue;
                                    }

                                    // Verificar combinaciones especiales
                                    string grupoOpc = "001";
                                    string prefijoOpcional = null;

                                    // Case 1: SOLO SEMILLA + ELECTRICA
                                    if (ContainsCondition(conditions, "CONFIGURACION DE USO", "SOLO SEMILLA") &&
                                        ContainsCondition(conditions, "SEMILLA-", "ELECTRICA"))
                                    {
                                        prefijoOpcional = "SSE";
                                    }
                                    // Case 2: SOLO SEMILLA + HIDRAULICA
                                    else if (ContainsCondition(conditions, "CONFIGURACION DE USO", "SOLO SEMILLA") &&
                                             ContainsCondition(conditions, "SEMILLA-", "HIDRAULICA"))
                                    {
                                        prefijoOpcional = "SSH";
                                    }
                                    // Case 3: SOLO SEMILLA + MECANICA
                                    else if (ContainsCondition(conditions, "CONFIGURACION DE USO", "SOLO SEMILLA") &&
                                             ContainsCondition(conditions, "SEMILLA-", "MECANICA"))
                                    {
                                        prefijoOpcional = "SSM";
                                    }
                                    // Case 4: FERTILIZACION SIMPLE + ELECTRICA
                                    else if (ContainsCondition(conditions, "CONFIGURACION DE USO", "SEMILLA + SIMPLE FERTILIZACION") &&
                                             ContainsCondition(conditions, "FERTILIZACION-SIMPLE", "ELECTRICA"))
                                    {
                                        prefijoOpcional = "FSE";
                                    }
                                    // Case 5: FERTILIZACION SIMPLE + HIDRAULICA
                                    else if (ContainsCondition(conditions, "CONFIGURACION DE USO", "SEMILLA + SIMPLE FERTILIZACION") &&
                                            ContainsCondition(conditions, "FERTILIZACION-SIMPLE", "HIDRAULICA"))
                                    {
                                        prefijoOpcional = "FSH";
                                    }
                                    // Case 6: FERTILIZACION SIMPLE + MECANICA
                                    else if (ContainsCondition(conditions, "CONFIGURACION DE USO", "SEMILLA + SIMPLE FERTILIZACION") &&
                                            ContainsCondition(conditions, "FERTILIZACION-SIMPLE", "MECANICA"))
                                    {
                                        prefijoOpcional = "FSM";
                                    }
                                    else if (ContainsCondition(conditions, "CONFIGURACION DE USO", "SEMILLA + DOBLE FERTILIZACION") &&
                                            ContainsCondition(conditions, "FERTILIZACION-DOBLE", "HIDRAULICA"))
                                    {
                                        prefijoOpcional = "FDH";
                                    }
                                    else if (ContainsCondition(conditions, "CONFIGURACION DE USO", "SEMILLA + DOBLE FERTILIZACION") &&
                                            ContainsCondition(conditions, "FERTILIZACION-DOBLE", "MECANICA"))
                                    {
                                        prefijoOpcional = "FDM";
                                    }
                                    else
                                    {
                                        prefijoOpcional = "";
                                        Console.WriteLine(string.Join("", conditions.Keys));
                                    }
                                    // Only add grupo_opc and opcional if we have a defined group
                                    if (grupoOpc != null)
                                    {
                                        // Format depends on if it's a special case or normal
                                        string valorOpcional;
                                        if (prefijoOpcional != null && prefijoOpcional.Length > 1) // Special case (SSE, SSH, FSE)
                                        {
                                            valorOpcional = $"{prefijoOpcional}";
                                            childStructure.Add(new Dictionary<string, string>
                                {
                                    { "campo", "grupo_opc" },
                                    { "valor", grupoOpc }
                                });
                                            childStructure.Add(new Dictionary<string, string>
                                {
                                    { "campo", "opcional" },
                                    { "valor", prefijoOpcional }
                                });
                                        }

                                    }
                                    else // Normal case with letter (A, B, C, etc.)
                                    {
                                        //valorOpcional = $"{prefijoOpcional ?? "A"}";
                                    }

                                    estructuras[parentCodigo].Add(childStructure);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al consultar la base de datos: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Utilidades.EscribirEnLog($"[SG1] EXCEPCIÓN jsonSG1: {ex.Message} | {ex.StackTrace}");
            }

            return estructuras;
        }

        public static void poblarBaseSG1(string Nombre_Padre, string Codigo_Padre, string Nombre_Hijo, string Codigo_Hijo, string CantidadHijo)
        {

            string connectionString = Utilidades.ConnectionString;

            string query = "INSERT INTO SG1 VALUES (@Nombre_Padre, @Codigo_Padre, @Nombre_Hijo, @Codigo_Hijo, @CantidadHijo, NULL, NULL)";
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Nombre_Padre", Nombre_Padre);
                        command.Parameters.AddWithValue("@Codigo_Padre", Codigo_Padre);
                        command.Parameters.AddWithValue("@Nombre_Hijo", Nombre_Hijo);
                        command.Parameters.AddWithValue("@Codigo_Hijo", Codigo_Hijo);
                        command.Parameters.AddWithValue("@CantidadHijo", CantidadHijo);
                        command.ExecuteNonQuery();
                    }

                }
            }
            catch (Exception ex)
            {

            }
        }

        public static void ActualizarBase(int estado, string mensaje, string codigo)
        {

            string connectionString = Utilidades.ConnectionString;

            string query = @"UPDATE SG1
                          SET estado = @estado, mensaje = @mensaje
                          WHERE Codigo_Padre = @codigo
                        --AND descripcion = @descripcion 
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
                        command.ExecuteNonQuery();
                    }

                }
            }
            catch (Exception ex)
            {

            }
        }

        private static bool ContainsCondition(Dictionary<string, string> conditions, string key, string value)
        {
            return conditions.ContainsKey(key) && conditions[key] == value;
        }

        private static Dictionary<string, string> ExtractAllConditions(string varianteText)
        {
            var conditions = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(varianteText))
                return conditions;

            // Split by & to get different conditions
            string[] parts = varianteText.Split(new[] { " & " }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                try
                {
                    // Look for pattern "[Teamcenter]KEY = VALUE" or "[Teamcenter]''KEY'' = ''VALUE''"
                    string pattern = part.Trim();

                    // Extract the part after [Teamcenter]
                    int teamcenterPos = pattern.IndexOf("[Teamcenter]");
                    if (teamcenterPos >= 0)
                    {
                        string keyValuePart = pattern.Substring(teamcenterPos + "[Teamcenter]".Length).Trim();

                        // Handle case of double single quotes
                        if (keyValuePart.Contains("''"))
                        {
                            // Split by equal sign
                            string[] keyValueSplit = keyValuePart.Split('=');
                            if (keyValueSplit.Length == 2)
                            {
                                // Extract key and value, removing double single quotes
                                string keyWithQuotes = keyValueSplit[0].Trim();
                                string valueWithQuotes = keyValueSplit[1].Trim();

                                // Remove double single quotes
                                string key = keyWithQuotes.Replace("''", "").Trim();
                                string value = valueWithQuotes.Replace("''", "").Trim();

                                // Check for null key
                                if (!string.IsNullOrEmpty(key))
                                {
                                    conditions[key] = value ?? string.Empty;
                                }
                                else
                                {
                                    Console.WriteLine($"Warning: Null or empty key found in '{keyValuePart}'");
                                }
                            }
                        }
                        else
                        {
                            // No quotes, look for equal sign
                            string[] keyValue = keyValuePart.Split('=');
                            if (keyValue.Length == 2)
                            {
                                string key = keyValue[0].Trim();
                                string value = keyValue[1].Trim();

                                // Check for null key
                                if (!string.IsNullOrEmpty(key))
                                {
                                    conditions[key] = value ?? string.Empty;
                                }
                                else
                                {
                                    Console.WriteLine($"Warning: Null or empty key found in '{keyValuePart}'");
                                }
                            }
                        }
                    }
                    else
                    {
                        // Try to extract from other formats like [PREFIX]KEY = VALUE
                        Match match = Regex.Match(pattern, @"\[([^\]]+)\]([^=]+)=(.+)");
                        if (match.Success)
                        {
                            string prefix = match.Groups[1].Value.Trim();
                            string key = match.Groups[2].Value.Trim();
                            string value = match.Groups[3].Value.Trim();

                            // Check for null key
                            if (!string.IsNullOrEmpty(key))
                            {
                                conditions[key] = value ?? string.Empty;
                            }
                            else
                            {
                                Console.WriteLine($"Warning: Null or empty key found in '{pattern}'");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing condition part '{part}': {ex.Message}");
                    // Continue with next condition in case of error
                }
            }
            return conditions;
        }

        public static Dictionary<string, List<List<Dictionary<string, string>>>> jsonSG1_BOP()
        {
            string connectionString = Utilidades.ConnectionString;

            string query = @"WITH RootProcess AS (
    SELECT TOP (1)
        p.name        AS ParentName,
        CASE
            WHEN p.catalogueId LIKE 'P-%' THEN SUBSTRING(p.catalogueId, 3, LEN(p.catalogueId))
            ELSE p.catalogueId
        END AS ParentCodigo
    FROM dbo.[ProcessOccurrence] po
    INNER JOIN dbo.[ProcessRevision] pr
        ON pr.id_Table = po.instancedRef
    INNER JOIN dbo.[Process] p
        ON p.id_Table = pr.masterRef
    WHERE po.parentRef IS NULL
),
OpOccurrences AS (
    SELECT
        po_op.id_Table AS OpOccId
    FROM dbo.[ProcessOccurrence] po_op
    INNER JOIN dbo.[OperationRevision] opr
        ON opr.id_Table = po_op.instancedRef
    INNER JOIN dbo.[Operation] op
        ON op.id_Table = opr.masterRef
),
Consumed AS (
    SELECT
        rp.ParentName,
        rp.ParentCodigo,
        pr_child.name AS ChildName,
        CASE
            WHEN p_child.productId LIKE 'M-%'
                THEN SUBSTRING(p_child.productId, 3, LEN(p_child.productId))
            ELSE p_child.productId
        END AS ChildCodigo,
        CAST(SUM(
            TRY_CAST(
                CASE
                    WHEN q.value IS NULL OR q.value = '' THEN '1'
                    ELSE q.value
                END AS FLOAT
            )
        ) AS DECIMAL(18, 6)) AS CantidadHijo
    FROM dbo.[Occurrence] oc
    INNER JOIN OpOccurrences opo
        ON opo.OpOccId = oc.parentRef
    INNER JOIN dbo.[ProductRevision] pr_child
        ON pr_child.id_Table = oc.instancedRef
    INNER JOIN dbo.[Product] p_child
        ON p_child.id_Table = pr_child.masterRef
    LEFT JOIN dbo.[UserValue_UserData] q
        ON oc.id_Table + 2 = q.id_Father
       AND q.title = 'Quantity'
    CROSS JOIN RootProcess rp
    WHERE oc.subType = 'MEConsumed'
      AND pr_child.subType <> 'Ric4_Ingenieria'
      AND p_child.subType <> 'Ric4_Ingenieria'
    GROUP BY
        rp.ParentName,
        rp.ParentCodigo,
        pr_child.name,
        p_child.productId
)
SELECT
    ParentName,
    ParentCodigo,
    ChildName,
    ChildCodigo,
    CantidadHijo,
    NULL AS Variante
FROM Consumed
ORDER BY ChildCodigo;";

            Dictionary<string, List<List<Dictionary<string, string>>>> estructuras = new Dictionary<string, List<List<Dictionary<string, string>>>>();
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
                                string parentCodigo = reader["ParentCodigo"]?.ToString();
                                string childCodigo = reader["ChildCodigo"]?.ToString() ?? string.Empty;
                                string cantidadHijoRaw = (reader["CantidadHijo"]?.ToString() ?? "0").Replace(',', '.');

                                if (!double.TryParse(cantidadHijoRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out double cantidadVal))
                                    cantidadVal = 0;

                                cantidadVal = Math.Truncate(cantidadVal * 1000) / 1000.0;

                                string cantidadHijo = cantidadVal.ToString("0.000", CultureInfo.InvariantCulture);

                                if (string.IsNullOrEmpty(parentCodigo))
                                {
                                    continue;
                                }

                                if (!estructuras.ContainsKey(parentCodigo))
                                {
                                    estructuras[parentCodigo] = new List<List<Dictionary<string, string>>>();
                                }

                                // Estructura simple para BOP (sin variantes)
                                var childStructure = new List<Dictionary<string, string>>
                            {
                                new Dictionary<string, string> { { "campo", "codigo" }, { "valor", childCodigo } },
                                new Dictionary<string, string> { { "campo", "cantidad" }, { "valor", cantidadHijo } }
                            };

                                estructuras[parentCodigo].Add(childStructure);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error jsonSG1_BOP: {ex.Message}");
                Utilidades.EscribirEnLog($"[SG1] EXCEPCIÓN jsonSG1_BOP: {ex.Message}");
            }

            return estructuras;
        }
    }
}
