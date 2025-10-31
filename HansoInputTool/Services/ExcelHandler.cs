using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HansoInputTool.Models;
using OfficeOpenXml;

namespace HansoInputTool.Services
{
    public class ExcelHandler
    {
        private readonly string _filePath;
        private ExcelPackage _excelPackage;
        private readonly Dictionary<string, List<RowData>> _dataCache = new();

        public List<string> SheetNames { get; private set; }

        public ExcelHandler(string filePath)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            _filePath = filePath;
            Load();
        }

        public void Load()
        {
            _excelPackage?.Dispose();
            var fileInfo = new FileInfo(_filePath);
            _excelPackage = new ExcelPackage(fileInfo);
            SheetNames = _excelPackage.Workbook.Worksheets.Select(ws => ws.Name).ToList();
            _dataCache.Clear();
        }

        public void Save()
        {
            _excelPackage.Save();
        }

        public List<RowData> GetSheetDataForPreview(string sheetName)
        {
            if (_dataCache.ContainsKey(sheetName))
                return _dataCache[sheetName];

            if (sheetName == null || !SheetNames.Contains(sheetName)) return new();

            var ws = _excelPackage.Workbook.Worksheets[sheetName];
            var totalRowIndex = FindTotalRow(ws);
            if (totalRowIndex == -1) return new();

            var data = new List<RowData>();
            bool isOotsuki = sheetName.Contains("大月");

            for (int rowIndex = 3; rowIndex < totalRowIndex; rowIndex++)
            {
                if (ws.Cells[rowIndex, 2].Value == null && ws.Cells[rowIndex, 4].Value == null)
                    continue;

                var rowData = new RowData
                {
                    RowIndex = rowIndex,
                    B_Day = GetNullableInt(ws.Cells[rowIndex, 2].Value),
                    C_Hanso = GetNullableInt(ws.Cells[rowIndex, 3].Value),
                    D_YuryoKm = GetNullableDouble(ws.Cells[rowIndex, 4].Value),
                    E_MuryoKm = GetNullableDouble(ws.Cells[rowIndex, 5].Value),
                    H_LateFeeOotsuki = GetNullableInt(ws.Cells[rowIndex, 8].Value),
                    K_LateMinutes = GetNullableInt(ws.Cells[rowIndex, 11].Value),
                    L_IsKoryo = GetNullableInt(ws.Cells[rowIndex, 12].Value)
                };

                rowData.LateValueText = isOotsuki
                    ? rowData.H_LateFeeOotsuki?.ToString()
                    : rowData.K_LateMinutes?.ToString();

                data.Add(rowData);
            }
            _dataCache[sheetName] = data;
            return data;
        }

        public (int targetRow, string insertInfo) RegisterNormalData(string sheetName, Dictionary<string, double?> values, bool isKoryo)
        {
            var ws = _excelPackage.Workbook.Worksheets[sheetName];
            var totalRowIndex = FindTotalRow(ws);
            if (totalRowIndex == -1) throw new Exception($"シート '{sheetName}' に '合計' 行が見つかりません。");

            var (targetRow, insertInfo) = FindTargetRow(ws, totalRowIndex);
            UpdateRowInternal(ws, targetRow, values, isKoryo);
            _dataCache.Remove(sheetName);
            return (targetRow, insertInfo);
        }

        public void UpdateNormalData(string sheetName, int rowIndex, Dictionary<string, double?> values, bool isKoryo)
        {
            var ws = _excelPackage.Workbook.Worksheets[sheetName];
            UpdateRowInternal(ws, rowIndex, values, isKoryo);
            _dataCache.Remove(sheetName);
        }

        private void UpdateRowInternal(ExcelWorksheet ws, int rowIndex, Dictionary<string, double?> values, bool isKoryo)
        {
            bool isOotsuki = ws.Name.Contains("大月");
            double? yuryoVal = values.GetValueOrDefault("有料キロ(D)");
            int hansoVal = (yuryoVal.HasValue && yuryoVal > 0) ? 1 : 0;

            ws.Cells[rowIndex, 2].Value = values.GetValueOrDefault("日(B)");
            ws.Cells[rowIndex, 3].Value = hansoVal;
            ws.Cells[rowIndex, 4].Value = yuryoVal;
            ws.Cells[rowIndex, 5].Value = values.GetValueOrDefault("無料キロ(E)");
            ws.Cells[rowIndex, 12].Value = isKoryo ? 1 : (object)null;
            if (isOotsuki)
            {
                ws.Cells[rowIndex, 8].Value = values.GetValueOrDefault("深夜料金(H)");
                ws.Cells[rowIndex, 11].Value = null;
            }
            else
            {
                ws.Cells[rowIndex, 8].Value = null;
                ws.Cells[rowIndex, 11].Value = values.GetValueOrDefault("深夜時間(K)");
            }
        }

        public void DeleteRows(string sheetName, List<int> rowIndices)
        {
            var ws = _excelPackage.Workbook.Worksheets[sheetName];
            foreach (var rowIndex in rowIndices.OrderByDescending(r => r))
            {
                ws.DeleteRow(rowIndex);
            }
            _dataCache.Remove(sheetName);
        }

        public void RegisterEastData(string sheetName, Dictionary<string, double?> values)
        {
            var ws = _excelPackage.Workbook.Worksheets[sheetName];
            var cellMap = new Dictionary<string, string>
            {
                {"延実働車輌数", "E4"}, {"搬送回数", "G4"}, {"有料キロ数", "H4"},
                {"無料キロ数", "I4"}, {"運輸実績", "K4"}
            };
            foreach (var (key, cell) in cellMap)
            {
                if (values.TryGetValue(key, out double? value) && value.HasValue)
                {
                    ws.Cells[cell].Value = value;
                }
            }
        }

        public List<string> ClearData()
        {
            var logMessages = new List<string>();
            foreach (var ws in _excelPackage.Workbook.Worksheets)
            {
                if (ws.Name.Contains("寝台車") || ws.Name.Contains("霊柩車"))
                {
                    var totalRowIndex = FindTotalRow(ws);
                    if (totalRowIndex != -1)
                    {
                        for (int rowIndex = 3; rowIndex < totalRowIndex; rowIndex++)
                        {
                            foreach (int colIndex in new[] { 2, 3, 4, 5, 8, 11, 12 })
                            {
                                ws.Cells[rowIndex, colIndex].Value = null;
                            }
                        }
                        logMessages.Add($"[{ws.Name}] の入力値をクリアしました。");
                    }
                }
                else if (ws.Name.Contains("東日本"))
                {
                    foreach (string cell in new[] { "E4", "G4", "H4", "I4", "K4" })
                    {
                        ws.Cells[cell].Value = null;
                    }
                    logMessages.Add($"[{ws.Name}] のデータをクリアしました。");
                }
            }
            _dataCache.Clear();
            return logMessages;
        }

        public bool CheckRemainingData()
        {
            foreach (var ws in _excelPackage.Workbook.Worksheets)
            {
                if ((ws.Name.Contains("寝台車") || ws.Name.Contains("霊柩車")) && ws.Cells[3, 2].Value != null)
                {
                    return true;
                }
            }
            return false;
        }

        private int FindTotalRow(ExcelWorksheet ws)
        {
            if (ws?.Dimension == null) return -1;
            for (int row = ws.Dimension.End.Row; row >= 3; row--)
            {
                var cellValue = ws.Cells[row, 1].Value?.ToString();
                if (!string.IsNullOrEmpty(cellValue) && cellValue.Contains("合計"))
                {
                    return row;
                }
            }
            return -1;
        }

        private (int targetRow, string insertInfo) FindTargetRow(ExcelWorksheet ws, int totalRowIndex)
        {
            for (int rowNum = 3; rowNum < totalRowIndex; rowNum++)
            {
                if (ws.Cells[rowNum, 2].Value == null)
                {
                    return (rowNum, "");
                }
            }
            ws.InsertRow(totalRowIndex, 1);
            return (totalRowIndex, "空き行がないため、合計行の上に新しい行を挿入します。");
        }

        private int? GetNullableInt(object val) => val == null ? null : Convert.ToInt32(val);
        private double? GetNullableDouble(object val) => val == null ? null : Convert.ToDouble(val);
    }
}