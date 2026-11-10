using System.Windows;

namespace Bloxstrap.UI.Elements.Dialogs
{
    public partial class ProfileNameDialog
    {
        public string ProfileName = "";

        public ProfileNameDialog(string initialName = "")
        {
            InitializeComponent();

            NameTextBox.Text = initialName;
            NameTextBox.TextChanged += (_, _) => OKButton.IsEnabled = !String.IsNullOrWhiteSpace(NameTextBox.Text);

            Loaded += (_, _) =>
            {
                NameTextBox.Focus();
                NameTextBox.SelectAll();
            };
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrWhiteSpace(NameTextBox.Text))
                return;

            ProfileName = NameTextBox.Text.Trim();
            Close();
        }
    }
}
