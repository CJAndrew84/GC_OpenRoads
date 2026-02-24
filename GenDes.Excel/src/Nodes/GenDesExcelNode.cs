using Bentley.GenerativeComponents;
using Bentley.GenerativeComponents.AddInSupport;
using Bentley.GenerativeComponents.ElementBasedNodes;
using Bentley.GenerativeComponents.GCScript;
using Bentley.GenerativeComponents.View;
using System;
using System.Collections.Generic;
using System.Linq;
using FastExcel;
using Bentley.GenerativeComponents.GeneralPurpose;
using System.IO;
using System.Text.RegularExpressions;

namespace GenDes.Excel
{
    [GCNamespace("Excel")]
    [GCNodeTypePaletteCategory("{Gen:Des} Excel")]
    [GCNodeTypeIcon("Resources/ifc.png")]
    [GCSummary("Read and Write Excel")]
    [GCHideInheritedTechniques]
    public class GenDesExcel : ElementBasedNode
    {
        [GCDefaultTechnique]
        [GCSummary("Read Excel File")]
        public NodeUpdateResult ReadExcel
        (
            NodeUpdateContext updateContext,
            [GCFileBrowser(Bentley.GenerativeComponents.ScriptEditor.FileBrowserMode.InputFile, "Select Excel File", "Select Excel File",null,"Excel Files","*.xlsx,*.xlsm,*.xlsb,*.xls")] string filepath,
            [GCIn] string SheetName,
            [GCIn] string CellRange,
            [GCOut] ref IGCObject[][] excelData
        )
        {
            try
            {
                var data = new FileInfo(@filepath);
                FastExcel.FastExcel fastExcelinfo = new FastExcel.FastExcel(data, true);

                var worksheets = fastExcelinfo.Worksheets;
                var PEnum = worksheets.Where(x => x.Name == SheetName).FirstOrDefault();

                PEnum.Read();

                var splitRange = CellRange.Split(':');
                var startCell = splitRange.FirstOrDefault();
                var endCell = splitRange.LastOrDefault();

                var startSplitColumn = Regex.Split(startCell, @"[0-9]"); 
                var startColumn = startSplitColumn.FirstOrDefault();
                var startSplitRow = Regex.Split(startCell, @"[A-Z]");
                int startRow = Convert.ToInt32(startSplitRow.LastOrDefault());

                var endSplitColumn = Regex.Split(endCell, @"[0-9]");
                var endColumn = endSplitColumn.FirstOrDefault();
                var endSplitRow = Regex.Split(endCell, @"[A-Z]");
                int endRow = Convert.ToInt32(endSplitRow.LastOrDefault());

                FastExcel.CellRange cellRange = new CellRange(startColumn, endColumn, startRow, endRow);

                var rangeData = PEnum.GetCellsInRange(cellRange);
                
                Boxer boxer = GCEnvironment().Boxer;
                
                List<IGCObject[]> rowData = new List<IGCObject[]>();
                List<IGCObject> currentRow = new List<IGCObject>();

                int currentRowNumber = startRow;

                foreach (var cell in rangeData)
                {
                    var cellValue = cell.Value;
                    if(cellValue == null)
                    {
                        cellValue = "";
                    }
                    else
                    {
                        cellValue = cellValue.ToString();
                    }

                    if (cell.RowNumber != currentRowNumber)
                    {
                        rowData.Add(currentRow.ToArray());
                        currentRow.Clear();
                        currentRow.Add(boxer.BoxToCompatible(cellValue));
                        currentRowNumber = cell.RowNumber;
                    }
                    else
                    {
                        currentRow.Add(boxer.BoxToCompatible(cellValue));
                    }
                }

                excelData = rowData.ToArray();

            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }
            return NodeUpdateResult.Success;
        }

        [GCTechnique]
        [GCSummary("Write Excel File")]
        public NodeUpdateResult WriteExcel
        (
            NodeUpdateContext updateContext,
            [GCFileBrowser(Bentley.GenerativeComponents.ScriptEditor.FileBrowserMode.OutputFile, "Save Excel File", "Save Excel File", null, "Excel Files", "*.xlsx,*.xlsm,*.xlsb,*.xls")] string filepath,
            [GCIn] string SheetName,
            [GCIn] IGCObject[][] excelData
        )
        {
            try
            {
                var data = new FileInfo(@filepath);
                FastExcel.FastExcel fastExcelinfo = new FastExcel.FastExcel(data, true);

                var worksheets = fastExcelinfo.Worksheets;
                var PEnum = worksheets.Where(x => x.Name == SheetName).FirstOrDefault();

                if (PEnum == null)
                {
                    Worksheet worksheet = new Worksheet();
                    worksheet.Name = SheetName;
                }

                fastExcelinfo.ExcelFile.OpenWrite();

                Unboxer boxer = GCEnvironment().Unboxer;

                int currentRowNumber = 1;

                foreach (var row in excelData)
                {
                    int currentColumnNumber = 1;

                    foreach (var cellValue in row)
                    {
                        var unboxedValue = boxer.Unbox<object>(cellValue);
                        //PEnum.SetCell(currentColumnNumber, currentRowNumber, unboxedValue);
                        currentColumnNumber++;
                    }

                    currentRowNumber++;
                }

                //fastExcelinfo.ExcelFile.SaveAs(filepath);
            }
            catch (Exception ex)
            {
                return new NodeUpdateResult.TechniqueException(ex);
            }

            return NodeUpdateResult.Success;
        }


    }

}
