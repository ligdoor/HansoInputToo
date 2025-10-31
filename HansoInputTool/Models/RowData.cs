using HansoInputTool.ViewModels.Base;

namespace HansoInputTool.Models
{
    public class RowData : ObservableObject
    {
        public int RowIndex { get; set; }
        public int? B_Day { get; set; }
        public int? C_Hanso { get; set; }
        public double? D_YuryoKm { get; set; }
        public double? E_MuryoKm { get; set; }
        public int? H_LateFeeOotsuki { get; set; }
        public int? K_LateMinutes { get; set; }
        public int? L_IsKoryo { get; set; }
        public string IsKoryoText => L_IsKoryo == 1 ? "✔" : "";
        public string LateValueText { get; set; }
    }
}