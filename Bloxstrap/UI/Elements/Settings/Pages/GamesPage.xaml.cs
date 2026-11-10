using Bloxstrap.UI.ViewModels.Settings;

namespace Bloxstrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for GamesPage.xaml
    /// </summary>
    public partial class GamesPage
    {
        public GamesPage()
        {
            DataContext = new GamesViewModel();
            InitializeComponent();
        }
    }
}
