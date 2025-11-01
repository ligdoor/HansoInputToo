using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HansoInputTool.Models;
using NLog;
using OfficeOpenXml;

namespace HansoInputTool.Services
{
    public class TransferProgressReport
    {
        public int Current { get; set; }
        public int Total { get; set; }
        public string Message { get; set; }
    }

    public class TransferService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public async Task ExecuteAsync(
            string workInputFile,
            string bundledTemplateFile,
            string outputDir,
            int period,
            int month,
            int rNum,
            List<string> allSheetNames,
            Dictionary<string, RateInfo> rates,
            IProgress<TransferProgressReport> progress)
        {
            await Task.Run(() =>
            {
                string folderName = $"{period}期 {month}月 R{rNum} アルス搬送・霊柩車　実績月報";
                string finalOutputDir = Path.Combine(outputDir, folderName);
                Directory.CreateDirectory(finalOutputDir);

                string geppoFilename = $"{period}期 {month}月 R{rNum} アルス搬送・霊柩車　実績月報.xlsx";
                string geppoFilepath = Path.Combine(finalOutputDir, geppoFilename);
                File.Copy(workInputFile, geppoFilepath, true);
                Logger.Info($"実績月報ファイルをコピーしました: {geppoFilepath}");

                string shukeiFilename = $"{period}期 {month}月 R{rNum} アルス搬送・霊柩車　実績月報集計.xlsx";
                string shukeiFilepath = Path.Combine(finalOutputDir, shukeiFilename);
                File.Copy(bundledTemplateFile, shukeiFilepath, true);
                Logger.Info($"集計ファイルをコピーしました: {shukeiFilepath}");

                using var wbInput = new ExcelPackage(new FileInfo(workInputFile));
                using var wbGeppo = new ExcelPackage(new FileInfo(geppoFilepath));
                using var wbShukei = new ExcelPackage(new FileInfo(shukeiFilepath));

                progress.Report(new TransferProgressReport { Message = "--- 全シートの転記処理を開始 ---" });
                Logger.Info("--- 全シートの転記処理を開始 ---");

                var sheetsToProcess = allSheetNames?.Where(s => !s.Contains("登録")).ToList() ?? new List<string>();
                int totalSheets = sheetsToProcess.Count;
                int processedCount = 0;

                foreach (var sheetName in sheetsToProcess)
                {
                    progress.Report(new TransferProgressReport { Current = processedCount, Total = totalSheets, Message = $"処理中: {sheetName} ..." });

                    if (sheetName.Contains("寝台車") || sheetName.Contains("霊柩車"))
                    {
                        ProcessNormalSheet(wbInput, wbGeppo, wbShukei, sheetName, rates);
                    }
                    else if (sheetName.Contains("東日本"))
                    {
                        ProcessEastSheet(wbInput, wbShukei, sheetName);
                    }

                    processedCount++;
                    Logger.Info($"[{sheetName}] の処理が完了しました。");
                }

                progress.Report(new TransferProgressReport { Current = processedCount, Total = totalSheets, Message = "最終処理中..." });

                if (wbShukei.Workbook.Worksheets.Any(ws => ws.Name == "寝台車 29"))
                {
                    var wsOut29 = wbShukei.Workbook.Worksheets["寝台車 29"];
                    wsOut29.Cells["A1"].Value = $"R{rNum}";
                    wsOut29.Cells["B1"].Value = month;
                }
                if (wbGeppo.Workbook.Worksheets.Any(ws => ws.Name == "寝台車 29"))
                {
                    var wsGeppo29 = wbGeppo.Workbook.Worksheets["寝台車 29"];
                    wsGeppo29.Cells["A1"].Value = $"R{rNum}";
                    wsGeppo29.Cells["B1"].Value = month;
                }

                wbShukei.Save();
                wbGeppo.Save();
            });
        }

        private void ProcessNormalSheet(ExcelPackage wbInput, ExcelPackage wbGeppo, ExcelPackage wbShukei, string sheetName, Dictionary<string, RateInfo> rates)
        {
            var wsIn = wbInput.Workbook.Worksheets[sheetName];
            var wsGeppo = wbGeppo.Workbook.Worksheets[sheetName];
            var totalRowIdx = FindTotalRow(wsIn);
            if (totalRowIdx == -1) return;

            // --- 料金計算ロジック ---
            string vehicleType = rates.Keys.FirstOrDefault(vt => sheetName.Contains(vt)) ?? "寝台車";
            var ratesForSheet = rates.GetValueOrDefault(vehicleType, rates["寝台車"]);
            bool isOotsuki = sheetName.Contains("大月");

            double totalKihon = 0, totalSoko = 0, totalShinya = 0, totalSum = 0;

            for (int row = 3; row < totalRowIdx; row++)
            {
                int hansoVal = GetInt(wsIn.Cells[row, 3].Value);
                double rowKihon = 0, rowSoko = 0, rowShinya = 0;

                if (hansoVal > 0)
                {
                    double yuryoKmVal = GetDouble(wsIn.Cells[row, 4].Value);
                    bool isKoryo = GetInt(wsIn.Cells[row, 12].Value) == 1;

                    rowKihon = isKoryo ? Math.Floor((double)ratesForSheet.BaseFee / 2) : ratesForSheet.BaseFee;

                    if (yuryoKmVal > 0)
                    {
                        rowSoko = (Math.Floor(yuryoKmVal / 10) + 1) * ratesForSheet.MileageFee;
                    }

                    if (isOotsuki)
                    {
                        rowShinya = GetDouble(wsIn.Cells[row, 8].Value);
                    }
                    else
                    {
                        double shinyaMin = GetDouble(wsIn.Cells[row, 11].Value);
                        if (shinyaMin > 0)
                        {
                            double numBlocks = Math.Floor(shinyaMin / 30) + 1;
                            double variableRyo = numBlocks * ratesForSheet.LateNightUnitFee;
                            rowShinya = variableRyo + ratesForSheet.LateNightFixedFee;
                        }
                    }
                }

                wsGeppo.Cells[row, 6].Value = rowKihon > 0 ? rowKihon : null;
                wsGeppo.Cells[row, 7].Value = rowSoko > 0 ? rowSoko : null;
                wsGeppo.Cells[row, 8].Value = rowShinya > 0 ? rowShinya : null;
                double rowTotal = rowKihon + rowSoko + rowShinya;
                wsGeppo.Cells[row, 9].Value = rowTotal > 0 ? rowTotal : null;

                totalKihon += rowKihon;
                totalSoko += rowSoko;
                totalShinya += rowShinya;
                totalSum += rowTotal;
            }

            wsGeppo.Cells[totalRowIdx, 6].Value = totalKihon > 0 ? totalKihon : null;
            wsGeppo.Cells[totalRowIdx, 7].Value = totalSoko > 0 ? totalSoko : null;
            wsGeppo.Cells[totalRowIdx, 8].Value = totalShinya > 0 ? totalShinya : null;
            wsGeppo.Cells[totalRowIdx, 9].Value = totalSum > 0 ? totalSum : null;

            // --- 集計ファイルへの転記 ---
            if (wbShukei.Workbook.Worksheets.Any(ws => ws.Name == sheetName))
            {
                var wsShukei = wbShukei.Workbook.Worksheets[sheetName];
                // 合計値の再計算 (ExcelHandlerからロジックを移動)
                var totals = CalculateTotals(wsIn, totalRowIdx);
                wsShukei.Cells["E4"].Value = totals.days;
                wsShukei.Cells["G4"].Value = totals.hanso;
                wsShukei.Cells["H4"].Value = totals.yuryoKm;
                wsShukei.Cells["I4"].Value = totals.muryoKm;
                wsShukei.Cells["K4"].Value = totalSum > 0 ? totalSum : null;
            }
        }

        private (int days, int hanso, double yuryoKm, double muryoKm) CalculateTotals(ExcelWorksheet ws, int totalRowIdx)
        {
            int totalDays = 0;
            int totalHanso = 0;
            double totalYuryoKm = 0;
            double totalMuryoKm = 0;

            for (int row = 3; row < totalRowIdx; row++)
            {
                if (ws.Cells[row, 2].Value != null) totalDays++;
                totalHanso += GetInt(ws.Cells[row, 3].Value);
                totalYuryoKm += GetDouble(ws.Cells[row, 4].Value);
                totalMuryoKm += GetDouble(ws.Cells[row, 5].Value);
            }
            return (totalDays, totalHanso, totalYuryoKm, totalMuryoKm);
        }

        private void ProcessEastSheet(ExcelPackage wbInput, ExcelPackage wbShukei, string sheetName)
        {
            if (wbShukei.Workbook.Worksheets.All(ws => ws.Name != sheetName)) return;

            var wsIn = wbInput.Workbook.Worksheets[sheetName];
            var wsShukei = wbShukei.Workbook.Worksheets[sheetName];

            foreach (string cell in new[] { "E4", "G4", "H4", "I4", "K4" })
            {
                wsShukei.Cells[cell].Value = wsIn.Cells[cell].Value;
            }
            Logger.Info($"[{sheetName}] の値を転記しました。");
        }

        private int FindTotalRow(ExcelWorksheet ws)
        {
            if (ws?.Dimension == null) return -1;
            for (int row = ws.Dimension.End.Row; row >= 3; row--)
            {
                if (ws.Cells[row, 1].Value?.ToString()?.Contains("合計") == true) return row;
            }
            return -1;
        }

        // Helper methods
        private int GetInt(object val) => val == null ? 0 : Convert.ToInt32(val);
        private double GetDouble(object val) => val == null ? 0.0 : Convert.ToDouble(val);
    }
}