using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ComprobarLog;

public static class GeneradorComprobar
{
    private const string NombreSalidaDefault = "comparador.txt";

    public static string Generar(string rutaLog, string? rutaSalida = null)
    {
        if (!File.Exists(rutaLog))
            throw new FileNotFoundException("No existe el archivo de log.", rutaLog);

        string salida = string.IsNullOrWhiteSpace(rutaSalida)
            ? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(rutaLog)) ?? AppContext.BaseDirectory, NombreSalidaDefault)
            : rutaSalida;

        var archivos = LeerEstructurasDelLog(rutaLog);
        File.WriteAllText(salida, FormatearComprobar(archivos), Encoding.UTF8);
        return salida;
    }

    private static List<ArchivoComprobar> LeerEstructurasDelLog(string rutaLog)
    {
        string[] lineas = File.ReadAllLines(rutaLog, Encoding.UTF8);
        var archivos = new List<ArchivoComprobar>();
        ArchivoComprobar? actual = null;

        for (int i = 0; i < lineas.Length; i++)
        {
            string mensaje = ObtenerMensajeLog(lineas[i]);

            if (mensaje.StartsWith("--- Procesando ", StringComparison.OrdinalIgnoreCase))
            {
                actual = CrearArchivoComprobar(mensaje);
                if (actual != null)
                    archivos.Add(actual);
                continue;
            }

            if (actual == null)
                continue;

            string? tabla = null;
            string? jsonInicial = null;

            if (mensaje.StartsWith("SB1 JSON:", StringComparison.OrdinalIgnoreCase))
            {
                tabla = "SB1";
                jsonInicial = mensaje["SB1 JSON:".Length..].TrimStart();
            }
            else if (mensaje.StartsWith("SG1 JSON:", StringComparison.OrdinalIgnoreCase))
            {
                tabla = "SG1";
                jsonInicial = mensaje["SG1 JSON:".Length..].TrimStart();
            }
            else if (mensaje.StartsWith("SG2/SH3 JSON:", StringComparison.OrdinalIgnoreCase))
            {
                tabla = "SG2/SH3";
                jsonInicial = mensaje["SG2/SH3 JSON:".Length..].TrimStart();
            }

            if (tabla == null || string.IsNullOrWhiteSpace(jsonInicial))
                continue;

            string json = CapturarJson(lineas, ref i, jsonInicial);
            JsonNode? nodo = TryParseJson(json);
            string respuestaHttp = BuscarRespuestaHttp(lineas, i + 1, tabla);
            actual.Agregar(tabla, nodo ?? JsonValue.Create(json)!, respuestaHttp);
        }

        return archivos;
    }

    private static ArchivoComprobar? CrearArchivoComprobar(string mensaje)
    {
        int separador = mensaje.IndexOf(": ", StringComparison.Ordinal);
        int fin = mensaje.LastIndexOf(" ---", StringComparison.Ordinal);

        if (separador < 0 || fin <= separador)
            return null;

        string tipo = mensaje["--- Procesando ".Length..separador].Trim();
        string archivo = mensaje[(separador + 2)..fin].Trim();

        return new ArchivoComprobar(tipo, archivo);
    }

    private static string ObtenerMensajeLog(string linea)
    {
        if (linea.Length == 0 || linea[0] != '[')
            return linea;

        int cierre = linea.IndexOf("] ", StringComparison.Ordinal);
        return cierre >= 0 ? linea[(cierre + 2)..] : linea;
    }

    private static string BuscarRespuestaHttp(string[] lineas, int desde, string tabla)
    {
        string marcador = tabla == "SG2/SH3" ? "[SG2_SH3]" : $"[{tabla}]";

        for (int i = desde; i < lineas.Length; i++)
        {
            string mensaje = ObtenerMensajeLog(lineas[i]).Trim();

            if (mensaje.StartsWith("--- Procesando ", StringComparison.OrdinalIgnoreCase) ||
                mensaje.StartsWith("SB1 JSON:", StringComparison.OrdinalIgnoreCase) ||
                mensaje.StartsWith("SG1 JSON:", StringComparison.OrdinalIgnoreCase) ||
                mensaje.StartsWith("SG2/SH3 JSON:", StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }

            int inicioMarcador = mensaje.IndexOf(marcador, StringComparison.OrdinalIgnoreCase);
            if (inicioMarcador < 0 || !mensaje.Contains("->", StringComparison.Ordinal))
                continue;

            return mensaje[inicioMarcador..];
        }

        return "";
    }

    private static string CapturarJson(string[] lineas, ref int indice, string jsonInicial)
    {
        var sb = new StringBuilder();
        sb.AppendLine(jsonInicial);

        int balance = BalanceJson(jsonInicial);
        while (balance > 0 && indice + 1 < lineas.Length)
        {
            indice++;
            sb.AppendLine(lineas[indice]);
            balance += BalanceJson(lineas[indice]);
        }

        return sb.ToString();
    }

    private static int BalanceJson(string texto)
    {
        int balance = 0;
        bool dentroString = false;
        bool escape = false;

        foreach (char c in texto)
        {
            if (escape)
            {
                escape = false;
                continue;
            }

            if (c == '\\' && dentroString)
            {
                escape = true;
                continue;
            }

            if (c == '"')
            {
                dentroString = !dentroString;
                continue;
            }

            if (dentroString)
                continue;

            if (c is '{' or '[') balance++;
            if (c is '}' or ']') balance--;
        }

        return balance;
    }

    private static JsonNode? TryParseJson(string json)
    {
        try
        {
            return JsonNode.Parse(json);
        }
        catch
        {
            return null;
        }
    }

    private static string FormatearComprobar(List<ArchivoComprobar> archivos)
    {
        var sb = new StringBuilder();
        sb.AppendLine("COMPROBAR ESTRUCTURAS");
        sb.AppendLine($"Generado: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(new string('=', 80));

        if (archivos.Count == 0)
        {
            sb.AppendLine("No se encontraron estructuras JSON en el log de procesamiento.");
            return sb.ToString();
        }

        foreach (ArchivoComprobar archivo in archivos)
        {
            sb.AppendLine();
            sb.AppendLine($"ARCHIVO: {archivo.Nombre}");
            sb.AppendLine($"TIPO: {archivo.Tipo}");
            sb.AppendLine(new string('-', 80));

            EscribirTabla(sb, "SB1", archivo);
            EscribirTabla(sb, "SG1", archivo);
            EscribirTabla(sb, "SG2/SH3", archivo);
        }

        return sb.ToString();
    }

    private static void EscribirTabla(StringBuilder sb, string tabla, ArchivoComprobar archivo)
    {
        if (!archivo.Tablas.TryGetValue(tabla, out List<RegistroComprobar>? registros) || registros.Count == 0)
            return;

        sb.AppendLine();
        sb.AppendLine($"TABLA {tabla} ({registros.Count})");
        sb.AppendLine(new string('.', 80));

        for (int i = 0; i < registros.Count; i++)
        {
            sb.AppendLine($"Registro {i + 1}:");

            if (tabla == "SB1")
                EscribirSb1(sb, registros[i].Json);
            else if (tabla == "SG1")
                EscribirSg1(sb, registros[i].Json);
            else if (tabla == "SG2/SH3")
                EscribirSg2Sh3(sb, registros[i].Json);
            else
                EscribirJsonPlano(sb, registros[i].Json, "  ");

            if (!string.IsNullOrWhiteSpace(registros[i].RespuestaHttp))
                sb.AppendLine($"  Respuesta HTTP: {registros[i].RespuestaHttp}");

            sb.AppendLine();
        }
    }

    private static void EscribirSb1(StringBuilder sb, JsonNode token)
    {
        if (token["producto"] is JsonArray campos)
        {
            EscribirCampos(sb, campos, "  ");
            return;
        }

        EscribirJsonPlano(sb, token, "  ");
    }

    private static void EscribirSg1(StringBuilder sb, JsonNode token)
    {
        if (token is not JsonObject estructuras)
        {
            EscribirJsonPlano(sb, token, "  ");
            return;
        }

        foreach (KeyValuePair<string, JsonNode?> estructura in estructuras)
        {
            sb.AppendLine($"  Padre: {estructura.Key}");

            if (estructura.Value is not JsonArray hijos)
            {
                EscribirJsonPlano(sb, estructura.Value, "    ");
                continue;
            }

            for (int i = 0; i < hijos.Count; i++)
            {
                sb.AppendLine($"    Hijo {i + 1}:");
                if (hijos[i] is JsonArray campos)
                    EscribirCampos(sb, campos, "      ");
                else
                    EscribirJsonPlano(sb, hijos[i], "      ");
            }
        }
    }

    private static void EscribirSg2Sh3(StringBuilder sb, JsonNode token)
    {
        if (token is not JsonObject proceso)
        {
            EscribirJsonPlano(sb, token, "  ");
            return;
        }

        sb.AppendLine($"  codigo: {proceso["codigo"]}");
        sb.AppendLine($"  producto: {proceso["producto"]}");

        if (proceso["procedimiento"] is not JsonArray procedimientos)
        {
            EscribirJsonPlano(sb, token, "  ");
            return;
        }

        for (int i = 0; i < procedimientos.Count; i++)
        {
            sb.AppendLine($"  Operacion {i + 1}:");
            JsonNode? detalle = procedimientos[i]?["detalle"];

            if (detalle is JsonArray campos)
                EscribirCampos(sb, campos, "    ");
            else
                EscribirJsonPlano(sb, procedimientos[i], "    ");
        }
    }

    private static void EscribirCampos(StringBuilder sb, JsonArray campos, string sangria)
    {
        foreach (JsonNode? campo in campos)
        {
            string nombre = campo?["campo"]?.ToString() ?? "(sin campo)";
            string valor = campo?["valor"]?.ToString() ?? "";
            sb.AppendLine($"{sangria}{nombre}: {valor}");
        }
    }

    private static void EscribirJsonPlano(StringBuilder sb, JsonNode? token, string sangria)
    {
        if (token == null)
            return;

        string json = token.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        string[] lineas = json.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        foreach (string linea in lineas)
            sb.AppendLine($"{sangria}{linea}");
    }

    private sealed class ArchivoComprobar
    {
        public ArchivoComprobar(string tipo, string nombre)
        {
            Tipo = tipo;
            Nombre = nombre;
        }

        public string Tipo { get; }
        public string Nombre { get; }
        public Dictionary<string, List<RegistroComprobar>> Tablas { get; } = new();

        public void Agregar(string tabla, JsonNode registro, string respuestaHttp)
        {
            if (!Tablas.TryGetValue(tabla, out List<RegistroComprobar>? registros))
            {
                registros = new List<RegistroComprobar>();
                Tablas[tabla] = registros;
            }

            registros.Add(new RegistroComprobar(registro, respuestaHttp));
        }
    }

    private sealed class RegistroComprobar
    {
        public RegistroComprobar(JsonNode json, string respuestaHttp)
        {
            Json = json;
            RespuestaHttp = respuestaHttp;
        }

        public JsonNode Json { get; }
        public string RespuestaHttp { get; }
    }
}
