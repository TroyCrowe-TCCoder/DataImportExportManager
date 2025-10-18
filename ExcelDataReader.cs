namespace DataImportExportManager
{
    using DataImportExportManager.Interfaces;
    using DocumentFormat.OpenXml.Office2016.Excel;
    using DocumentFormat.OpenXml.Packaging;
    using DocumentFormat.OpenXml.Spreadsheet;
    using Microsoft.AspNetCore.Http;
    using System.Data;
    using System.Text.RegularExpressions;

    public class ExcelDataReader : IExcelDataReader
    {
        public required List<string> DocumentHeaders { get; set; }

        public required DataTable Dt;

        public async Task<DataTable> GetAllExcelData(IFormFile file)
        {
            var stream = new MemoryStream();

            await file.CopyToAsync(stream);

            using (SpreadsheetDocument doc = SpreadsheetDocument.Open(stream, false))
            {
                Dt = await GetDocumentData(doc);
            }

            return Dt;
        }

        public async Task<DataTable> GetAllExcelData(string filePath)
        {
            using (SpreadsheetDocument doc = SpreadsheetDocument.Open(filePath, false))
            {
                Dt = await GetDocumentData(doc);
            }

            return Dt;
        }

        private async Task<DataTable> GetDocumentData(SpreadsheetDocument doc)
        {
            //Read the first Sheet from Excel file.
            //Sheet sheet = doc.WorkbookPart.Workbook.Sheets.GetFirstChild<Sheet>();
            Sheet sheet = doc.WorkbookPart.Workbook.Descendants<Sheet>().FirstOrDefault();


            //Get the Worksheet instance.
            WorksheetPart worksheetPart = (WorksheetPart)doc.WorkbookPart.GetPartById(sheet.Id);

            // Get all rows from the worksheet
            var rows = worksheetPart.Worksheet.GetFirstChild<SheetData>().Elements<Row>();

            //Create a new DataTable.
            var dt = new DataTable();

            //Loop through the Worksheet rows.
            foreach (Row row in rows)
            {
                //Use the first row to add columns to DataTable.
                if (row.RowIndex.Value == 1)
                {
                    if(DocumentHeaders.Count == 0)
                    { 
                        DocumentHeaders = await GetHeaders(doc, row);
                    }

                    foreach (var header in DocumentHeaders)
                    {
                        if(dt.Columns.Count == DocumentHeaders.Count)
                        {
                            break;
                        }
                        dt.Columns.Add(new DataColumn(header)); 
                    }
                }
                else
                {
                    //Add rows to DataTable.
                    var dr = dt.NewRow();
                    int i = 0;
                    foreach (Cell cell in row)
                    {
                        int columnIndex = GetColumnIndexFromCellReference(cell.CellReference.Value);

                        while (i < columnIndex - 1)
                        {
                            dr[i] = string.Empty;
                            i++;
                        }

                        dr[i] = await GetValue(doc, cell);

                        i = columnIndex;
                    }
                    dt.Rows.Add(dr);
                }
            }

            return dt;
        }

        public async Task<List<string>> GetDocumentHeaders(IFormFile file)
        {
            var stream = new MemoryStream();

            await file.CopyToAsync(stream);

            using (SpreadsheetDocument doc = SpreadsheetDocument.Open(stream, false))
            {
                return await GetHeaders(doc);
            }
        }

        public async Task<List<string>> GetDocumentHeaders(string filePath)
        {
            using (SpreadsheetDocument doc = SpreadsheetDocument.Open(filePath, false))
            {
               return await GetHeaders(doc);
            }
        }

        private async Task<List<string>> GetHeaders(SpreadsheetDocument doc)
        {
            //Read the first Sheet from Excel file.
            Sheet sheet = doc.WorkbookPart.Workbook.Sheets.GetFirstChild<Sheet>();

            //Get the Worksheet instance.
            WorksheetPart worksheetPart = (WorksheetPart)doc.WorkbookPart.GetPartById(sheet.Id);

            // Get all rows from the worksheet
            Row row = worksheetPart.Worksheet.GetFirstChild<SheetData>().Elements<Row>().First();

            return await GetHeaders(doc, row);
        }

        private async Task<List<string>> GetHeaders(SpreadsheetDocument doc, Row row)
        {
            foreach (Cell cell in row)
            {
                var dataValue = await GetValue(doc, cell);
                DocumentHeaders.Add(dataValue);
            }

            return DocumentHeaders;
        }

        private static async Task<string> GetValue(SpreadsheetDocument doc, Cell cell)
        {
            string value = string.Empty;

            if (cell.DataType != null && cell.DataType == CellValues.SharedString)
            {
                // If the cell contains a shared string reference
                SharedStringTablePart stringTablePart = doc.WorkbookPart.SharedStringTablePart;
                if (stringTablePart != null && stringTablePart.SharedStringTable != null)
                {
                    int index = int.Parse(cell.CellValue.InnerText);
                    value = stringTablePart.SharedStringTable.ElementAt(index).InnerText;
                }
            }
            else if (cell.CellValue != null)
            {
                // Otherwise, get the cell value directly
                value = cell.CellValue.InnerText;
            }

            return value;
        }

        private int GetColumnIndexFromCellReference(string cellReference)
        {
            // Extract column name (e.g., "A", "AB") from cellReference
            string columnName = Regex.Replace(cellReference.ToUpper(), @"[\d]", string.Empty);

            int columnIndex = 0;
            int multiplier = 1;

            // Iterate from right to left to convert column name to index
            foreach (char c in columnName.ToCharArray().Reverse())
            {
                columnIndex += multiplier * ((int)c - 64);
                multiplier *= 26;
            }

            return columnIndex; // Returns a 1-based column index (A=1, B=2, etc.)
        }
    }
}
