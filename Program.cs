using Newtonsoft.Json;
using System;
using System.Data.SqlClient;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using WEB_SERVICE_RICHIGER;

namespace Web_Service // Note: actual namespace depends on the project name.
{
    internal class Program
    {
        // Modelos para mapear los datos de SQL

        static async Task Main(string[] args)
        {

            //string connectionString = "Data Source=DEPLM-07-PC\\SQLEXPRESS;Initial Catalog=RichigerBOP;User ID=sa;Password=infodba";
            string connectionString = "Data Source=DEPLM-11-PC\\SQLEXPRESS;Initial Catalog=RichigerBOP;User ID=sa;Password=infodba;TrustServerCertificate=True;";
            //string connectionString = @"Server=PC-01\SQLEXPRESS;Database=RichigerBOP;Trusted_Connection=True;TrustServerCertificate=True;";


            XmlDocument xmlDoc = new XmlDocument();

            string nameCarpeta = @"H:\bops_richiger";

            if (Directory.Exists(nameCarpeta))
            {

                string[] archivos = Directory.GetFiles(nameCarpeta);
                int contadorXmls = 1;

                // Por cada archivo XML, este es subido a la base de datos con su identificador xml

                bool ban = true;
                foreach (string archivo in archivos)
                {
                    try
                    {
                        Console.WriteLine("Archivo Leido el diaablooooo");
                        xmlDoc.Load(archivo); //Cargar el xml                           
                        using (SqlConnection connection = new SqlConnection(connectionString))
                        {
                            connection.Open();
                            XmlNode root = xmlDoc.DocumentElement;
                            Dictionary<string, List<DataRow>> groupedDataRows = new Dictionary<string, List<DataRow>>();

                            if (ParseNode(root, groupedDataRows))
                            {
                                if (ban)
                                {
                                    BorrarTabla(connection, groupedDataRows);
                                    ban = false;
                                }

                                CreateTable(connection, groupedDataRows);
                                InsertData(connection, groupedDataRows, archivo, contadorXmls);

                                List<string> listaProcesos = new List<string>();
                                listaProcesos = Tablas_SG2_SH3.jsonSG2_SH3();

                                foreach (string producto in listaProcesos)
                                {
                                    Console.WriteLine(producto);
                                    await Tablas_SG2_SH3.postSG2_SH3(producto);
                                }

                            }

                            groupedDataRows.Clear();
                            contadorXmls++;
                        }
                        Utilidades.EscribirEnLog($"Archivo XML: {Path.GetFileName(archivo)} cargado correctamente");

                    }
                    catch (Exception ea)
                    {
                        Utilidades.EscribirEnLog($"Error al cargar el XML: {Path.GetFileName(archivo)} \nError: {ea.Message} ");
                    }

                }
            }

            //Productos sueltos:
            List<string> listaProductos = new List<string>();
            listaProductos = Tabla_SB1.jsonSB1();
            foreach (string producto in listaProductos)
            {
                await Tabla_SB1.postSB1(producto);
            }

            ////Estructura de producto
            Dictionary<string, List<List<Dictionary<string, string>>>> estructuras = new Dictionary<string, List<List<Dictionary<string, string>>>>();
            estructuras = Tabla_SG1.jsonSG1();
            await Tabla_SG1.postSG1(estructuras);

            ////Estructura de procesos:
            List<string> listaSG2 = new List<string>();
            listaSG2 = Tablas_SG2_SH3.jsonSG2_SH3();
            Console.WriteLine($"[DEBUG] SG2/SH3: {listaSG2?.Count ?? 0} items generados");
            foreach (string s in listaSG2)
            {
                Console.WriteLine(s);
                await Tablas_SG2_SH3.postSG2_SH3(s);

            }



        }

        static void LimpiarCarpeta(string carpeta) //Limpia la carpeta donde se exportan todos los Xmls, uno por uno antes de volver a exportar más de otra estructura
        {
            try
            {


                if (Directory.Exists(carpeta))
                {
                    // Borra todos los archivos en la carpeta
                    string[] archivos = Directory.GetFiles(carpeta);
                    foreach (string archivo in archivos)
                    {
                        File.Delete(archivo);
                    }

                    Utilidades.EscribirEnLog($"Carpeta {carpeta} limpiada correctamente.");
                }
            }
            catch (Exception ex)
            {
                // Maneja cualquier excepción que pueda ocurrir durante la limpieza
                Utilidades.EscribirEnLog($"Error al limpiar la carpeta: {ex.Message}");
            }
        }

        // --------------------------------------------- Metodos utilizados por el Main() Carga en Base de datos de BOP ---------------------------------------------
        static void BorrarTabla(SqlConnection connection, Dictionary<string, List<DataRow>> groupedDataRows)
        {
            foreach (var group in groupedDataRows)
            {
                try
                {
                    string tableName = group.Key;
                    string deleteTableQuery = $"IF OBJECT_ID('[{tableName}]', 'U') IS NOT NULL DROP TABLE [{tableName}]";
                    using (SqlCommand command = new SqlCommand(deleteTableQuery, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
                catch (Exception ea)
                {
                    Utilidades.EscribirEnLog($"Error al intentar borrar la tabla para su sobreescritura - Error: {ea.Message}");
                }
            }
        }

        static bool ParseNode(XmlNode node, Dictionary<string, List<DataRow>> groupedDataRows, string parentNodeName = "")
        {
            // Crear una lista de nombres de nodos a ignorar
            var listaIgnorados = new List<string> { "ApplicationRef", "AttributeContext", "DataSet",
                                                                "ExternalFile", "Folder", "ProductDef",
                                                                "ProductRevisionView", "RevisionRule", "Site", "Transform", "View" };
            try
            {
                if (node.NodeType == XmlNodeType.Element && !listaIgnorados.Contains(node.Name))
                {
                    string nodeName = node.Name; //Nombre actual del nodo

                    DataRow dataRow = new DataRow(); //Nuevo objeto datarow
                    dataRow.NombreNodo = nodeName;

                    dataRow.Atributos = new List<string>();

                    foreach (XmlAttribute attribute in node.Attributes)
                    {
                        dataRow.Atributos.Add(attribute.Name); //Guarda los nombres de los atributos
                    }

                    dataRow.XmlNode = node;
                    string tableName = GetTableName(nodeName, dataRow.Atributos, parentNodeName); //Creacion de nombre de la tabla

                    if (!groupedDataRows.ContainsKey(tableName))
                    {
                        groupedDataRows[tableName] = new List<DataRow>();
                    }
                    groupedDataRows[tableName].Add(dataRow);

                    foreach (XmlNode childNode in node.ChildNodes)
                    {
                        ParseNode(childNode, groupedDataRows, nodeName); //recursividad
                    }
                    return true;
                }
                return false;
            }
            catch (Exception ea)
            {
                Utilidades.EscribirEnLog("Excepcion controlada en el metodo ParseNode: " + ea.Message);
                return false;
            }

        }

        static string GetTableName(string nodeName, List<string> attributes, string parentNodeName)
        {
            string tableName = nodeName;
            if (!attributes.Contains("id") && tableName != "PLMXML") //Si no tiene el atributo id y no es el nodo PLMXML
            {

                tableName = $"{nodeName}_{parentNodeName}";
            }
            return tableName;
        }

        static void CreateTable(SqlConnection connection, Dictionary<string, List<DataRow>> groupedDataRows)
        {
            try
            {
                foreach (var group in groupedDataRows)
                {

                    string tableName = group.Key;
                    if (tableName == "PLMXML") // Saltar el nodo "PLMXML"
                    {
                        continue;
                    }
                    string createTableQuery = $"IF OBJECT_ID('[{tableName}]', 'U') IS NULL CREATE TABLE [{tableName}] (id INT IDENTITY(1,1) PRIMARY KEY, contenido NVARCHAR(MAX)";
                    List<string> additionalAttributes = new List<string>();
                    bool hasIdAttribute = false;

                    foreach (DataRow dataRow in group.Value)
                    {
                        foreach (string attribute in dataRow.Atributos)
                        {
                            if (!additionalAttributes.Contains(attribute) && attribute != "id") //Adicional atributo
                            {
                                additionalAttributes.Add(attribute);
                            }
                            if (attribute == "id") //Existe el atributo id
                            {
                                hasIdAttribute = true;
                            }
                        }
                    }

                    if (hasIdAttribute) // Existe el atributo "id", agregar id_Table
                    {
                        createTableQuery += ", id_Table NVARCHAR(MAX) ";
                    }
                    else // No existe el atributo "id", agregar id_Father
                    {
                        createTableQuery += ", id_Father NVARCHAR(MAX) ";
                    }
                    foreach (string columnName in additionalAttributes) //Creacion de columnas
                    {
                        if (columnName != "id")
                        {
                            createTableQuery += $", [{columnName}] NVARCHAR(MAX)";
                        }
                    }
                    createTableQuery += ", idXml INT);";
                    using (SqlCommand command = new SqlCommand(createTableQuery, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ea)
            {
                Utilidades.EscribirEnLog($"Excepcion controlada en el metodo createTable: {ea.Message}");
                throw;
            }

        }

        static void AlterTable(SqlConnection connection, string tableName, string columnName, string columnType)
        {
            try
            {
                string alterTableQuery = $"IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' AND COLUMN_NAME = '{columnName}') " +
                $"ALTER TABLE [{tableName}] ADD [{columnName}] {columnType};";

                using (SqlCommand command = new SqlCommand(alterTableQuery, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ea)
            {
                Utilidades.EscribirEnLog($"Excepcion controlada en el metodo AlterTable: {ea.Message}");
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
                        if (dataRow.NombreNodo == "PLMXML") // Saltar el nodo "PLMXML"
                            continue;
                        string insertQuery = $"INSERT INTO [{tableName}] (";
                        List<string> columnNames = new List<string>();
                        List<string> parameterNames = new List<string>();
                        List<SqlParameter> parameters = new List<SqlParameter>();
                        bool hasIdAttribute = false;


                        foreach (string columnName in dataRow.Atributos)
                        {

                            if (columnName == "id" || columnName == "instancedRef" || columnName == "masterRef" || columnName == "parentRef" || columnName == "instanceRefs") //Columna id_Table, instancedRef y masterRef
                            {
                                string attributeValue1 = dataRow.XmlNode.Attributes[columnName]?.Value;

                                if (columnName == "id" && !string.IsNullOrEmpty(attributeValue1) && attributeValue1.Length > 2)
                                {
                                    hasIdAttribute = true;
                                    columnNames.Add("[id_Table]");
                                    parameterNames.Add("@id");
                                    attributeValue1 = attributeValue1.Substring(2); //Suprimir los dos primeros caracteres
                                    parameters.Add(new SqlParameter("@id", attributeValue1));
                                }
                                if (columnName == "instancedRef" && !string.IsNullOrEmpty(attributeValue1) && attributeValue1.Length > 2)
                                {
                                    columnNames.Add("[instancedRef]");
                                    parameterNames.Add("@instancedRef");
                                    attributeValue1 = attributeValue1.Substring(3); //Suprimir los dos primeros caracteres
                                    parameters.Add(new SqlParameter("@instancedRef", attributeValue1));
                                }
                                if (columnName == "masterRef" && !string.IsNullOrEmpty(attributeValue1) && attributeValue1.Length > 2)
                                {
                                    columnNames.Add("[masterRef]");
                                    parameterNames.Add("@masterRef");
                                    attributeValue1 = attributeValue1.Substring(3); //Suprimir los dos primeros caracteres
                                    parameters.Add(new SqlParameter("@masterRef", attributeValue1));
                                }
                                if (columnName == "parentRef" && !string.IsNullOrEmpty(attributeValue1) && attributeValue1.Length > 2)
                                {
                                    columnNames.Add("[parentRef]");
                                    parameterNames.Add("@parentRef");
                                    attributeValue1 = attributeValue1.Substring(3); //Suprimir los tres primeros caracteres
                                    parameters.Add(new SqlParameter("@parentRef", attributeValue1));
                                }
                                if (columnName == "instanceRefs" && !string.IsNullOrEmpty(attributeValue1) && attributeValue1.Length > 2)
                                {
                                    columnNames.Add("[instanceRefs]");
                                    parameterNames.Add("@instanceRefs");
                                    attributeValue1 = attributeValue1.Substring(3); //Suprimir los tres primeros caracteres
                                    parameters.Add(new SqlParameter("@instanceRefs", attributeValue1));
                                }
                                continue;
                            }
                            AlterTable(connection, tableName, columnName, "NVARCHAR(MAX)");
                            columnNames.Add($"[{columnName}]"); //Columnas de otros atributos que no son id,contenido y id_father
                            parameterNames.Add($"@{columnName}");
                            string attributeValue = dataRow.XmlNode.Attributes[columnName]?.Value;
                            attributeValue = attributeValue.Replace("'", "''");
                            parameters.Add(new SqlParameter($"@{columnName}", attributeValue));

                        }
                        columnNames.Add("[contenido]");//Columna contenido
                        parameterNames.Add("@contenido");
                        parameters.Add(new SqlParameter("@contenido", dataRow.XmlNode.InnerText));

                        if (!hasIdAttribute) //Columna de tablas sin id
                        {
                            columnNames.Add("[id_Father]");
                            parameterNames.Add("@idFather");
                            XmlNode parentNode = dataRow.XmlNode.ParentNode;
                            string parentAttributeValue = parentNode?.Attributes["id"]?.Value;
                            string parentAttributeId = parentAttributeValue?.Substring(2) ?? "0";
                            parameters.Add(new SqlParameter("@idFather", parentAttributeId));
                        }
                        columnNames.Add("[idXml]"); // Agregar la columna idXml
                        parameterNames.Add("@idXml");
                        parameters.Add(new SqlParameter("@idXml", contadorXmls)); // Insertar el valor de contadorXmls

                        insertQuery += string.Join(", ", columnNames) + ") VALUES (";
                        insertQuery += string.Join(", ", parameterNames) + ");";

                        using (SqlCommand command = new SqlCommand(insertQuery, connection))
                        {
                            command.Parameters.AddRange(parameters.ToArray());
                            command.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ea)
            {
                Utilidades.EscribirEnLog($"Excepcion controlada en el metodo InsertData {ea.Message}");
            }

        }
    }
}