namespace DataImportExportManager.Interfaces
{
    using Microsoft.AspNetCore.Http;
    using System.Collections.Generic;
    using System.Data;

    public interface IExcelDataReader
    {
        List<string> DocumentHeaders { get; set; }

        Task<List<string>> GetDocumentHeaders(IFormFile file);

        Task<List<string>> GetDocumentHeaders(string filePath);

        Task<DataTable> GetAllExcelData(IFormFile file);

        Task<DataTable> GetAllExcelData(string filePath);
    }
}
