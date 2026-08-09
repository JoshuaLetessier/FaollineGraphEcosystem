namespace Faolline.GraphImport
{
    /// <summary>Reads one structured input file into a <see cref="SourceTable"/> of raw rows.</summary>
    public interface IRowSource
    {
        SourceTable Read(string filePath, string tableName);
    }
}
