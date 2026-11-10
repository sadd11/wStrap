using Bloxstrap.UI.ViewModels.Settings;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for ProfilesPage.xaml
    /// </summary>
    public partial class ProfilesPage
    {
        public ProfilesPage()
        {
            DataContext = new ProfilesViewModel(this);
            InitializeComponent();
        }
    }
}
