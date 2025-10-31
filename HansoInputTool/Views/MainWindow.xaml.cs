using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HansoInputTool.ViewModels;

namespace HansoInputTool.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            this.DataContextChanged += (s, e) =>
            {
                if (e.NewValue is MainViewModel vm)
                {
                    vm.RequestFocusNormalTab += () => NormalDayTextBox.Focus();
                    vm.RequestFocusEastTab += () => EastJitsudoTextBox.Focus();
                }
            };

            DataContext = new MainViewModel();
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var request = new TraversalRequest(FocusNavigationDirection.Next);
                if (Keyboard.FocusedElement is UIElement elementWithFocus)
                {
                    elementWithFocus.MoveFocus(request);
                }
                e.Handled = true;
            }
        }

        private void LastNormalTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (DataContext is MainViewModel vm && vm.RegisterNormalCommand.CanExecute(null))
                {
                    vm.RegisterNormalCommand.Execute(null);
                }
                e.Handled = true;
            }
        }

        private void LastEastTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (DataContext is MainViewModel vm && vm.RegisterEastCommand.CanExecute(null))
                {
                    vm.RegisterEastCommand.Execute(null);
                }
                e.Handled = true;
            }
        }
    }
}