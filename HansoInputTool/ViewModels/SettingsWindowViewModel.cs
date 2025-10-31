using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using HansoInputTool.Models;
using HansoInputTool.ViewModels.Base;
using Newtonsoft.Json;

namespace HansoInputTool.ViewModels
{
    public class SettingsWindowViewModel : ObservableObject
    {
        private readonly string _ratesFilePath;
        private readonly MainViewModel _mainViewModel;

        public Dictionary<string, RateInfo> Rates { get; set; }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public SettingsWindowViewModel(Dictionary<string, RateInfo> currentRates, string ratesFilePath, MainViewModel mainViewModel)
        {
            Rates = JsonConvert.DeserializeObject<Dictionary<string, RateInfo>>(JsonConvert.SerializeObject(currentRates));
            _ratesFilePath = ratesFilePath;
            _mainViewModel = mainViewModel;

            SaveCommand = new RelayCommand(SaveSettings);
            CancelCommand = new RelayCommand(p => ((Window)p).Close());
        }

        private void SaveSettings(object parameter)
        {
            try
            {
                string json = JsonConvert.SerializeObject(Rates, Formatting.Indented);
                File.WriteAllText(_ratesFilePath, json);
                _mainViewModel.Rates = Rates;
                MessageBox.Show("料金設定を保存しました。", "保存完了", MessageBoxButton.OK, MessageBoxImage.Information);
                ((Window)parameter).Close();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"設定の保存に失敗しました。\n{ex.Message}", "保存エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}