using System.Xml;

public class TableBucket
{
    public string TableName { get; set; } = string.Empty;

    // Conjunto de nombres de columnas encontradas
    public HashSet<string> Attributes { get; } = new HashSet<string>();

    // True si al menos un nodo de esta tabla tiene atributo "id"
    public bool HasIdAttribute { get; set; }

    // Todos los nodos XML de esta tabla (cada ocurrencia)
    public List<XmlNode> Nodes { get; } = new List<XmlNode>();
}