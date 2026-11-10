using System.Windows;
using System.Windows.Controls;

namespace Bloxstrap.UI.Elements.Dialogs
{
    public partial class FastFlagsWizardDialog
    {
        public FastFlagsWizardDialog()
        {
            InitializeComponent();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (MaxFpsRadio.IsChecked == true)
            {
                // everything low except the fps target
                App.FastFlags.SetPreset("Rendering.FRMQualityOverride", "1");
                App.FastFlags.SetPreset("Rendering.MSAA", null);
            }
            else if (BalancedRadio.IsChecked == true)
            {
                App.FastFlags.SetPreset("Rendering.FRMQualityOverride", "5");
                App.FastFlags.SetPreset("Rendering.MSAA", "2");
            }
            else if (BeautyRadio.IsChecked == true)
            {
                App.FastFlags.SetPreset("Rendering.FRMQualityOverride", "15");
                App.FastFlags.SetPreset("Rendering.MSAA", "4");
            }

            App.FastFlags.Save();

            Frontend.ShowMessageBox(Strings.Menu_FastFlags_Wizard_Done, MessageBoxImage.Information);

            Close();
        }
    }
}
