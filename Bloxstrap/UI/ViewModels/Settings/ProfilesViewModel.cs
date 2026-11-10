using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Wpf.Ui.Common.Interfaces;

namespace Bloxstrap.UI.ViewModels.Settings
{
    public class ProfilesViewModel : NotifyPropertyChangedViewModel, INavigationAware
    {
        public class ProfileEntry
        {
            public string Name { get; set; } = "";
            public string DetailsText { get; set; } = "";
            public ICommand ApplyCommand { get; set; } = new RelayCommand(() => { });
            public ICommand DeleteCommand { get; set; } = new RelayCommand(() => { });
        }

        private readonly UI.Elements.Settings.Pages.ProfilesPage _page;

        public ObservableCollection<ProfileEntry> ProfilesList { get; private set; } = new();

        public bool HasProfiles => ProfilesList.Count > 0;

        public bool NoProfiles => ProfilesList.Count == 0;

        public ICommand CreateProfileCommand => new RelayCommand(CreateProfile);

        public ICommand ExportSettingsCommand => new RelayCommand(ExportSettings);

        public ICommand ImportSettingsCommand => new RelayCommand(ImportSettings);

        public ProfilesViewModel(UI.Elements.Settings.Pages.ProfilesPage page)
        {
            _page = page;
        }

        public void OnNavigatedTo() => LoadData();

        public void OnNavigatedFrom() { }

        private void LoadData()
        {
            var list = new ObservableCollection<ProfileEntry>();

            foreach (var pair in App.Profiles.Prop.Items.OrderBy(x => x.Value.CreatedAt))
            {
                list.Add(new ProfileEntry
                {
                    Name = pair.Key,
                    DetailsText = String.Format(
                        Strings.Menu_Profiles_DetailsFormat,
                        pair.Value.CreatedAt.ToString("g"),
                        pair.Value.FastFlags.Count
                    ),
                    ApplyCommand = new RelayCommand(() => ApplyProfile(pair.Key)),
                    DeleteCommand = new RelayCommand(() => DeleteProfile(pair.Key)),
                });
            }

            ProfilesList = list;

            OnPropertyChanged(nameof(ProfilesList));
            OnPropertyChanged(nameof(HasProfiles));
            OnPropertyChanged(nameof(NoProfiles));
        }

        private static Dictionary<string, object> CloneFlags() =>
            App.FastFlags.Prop.ToDictionary(x => x.Key, x => (object)(x.Value?.ToString() ?? ""));

        private void CreateProfile()
        {
            string suggestedName = String.Format(Strings.Menu_Profiles_DefaultName, App.Profiles.Prop.Items.Count + 1);

            var dialog = new UI.Elements.Dialogs.ProfileNameDialog(suggestedName);
            dialog.ShowDialog();

            string name = dialog.ProfileName;

            if (String.IsNullOrEmpty(name))
                return;

            if (App.Profiles.Prop.Items.ContainsKey(name))
            {
                Frontend.ShowMessageBox(Strings.Menu_Profiles_Exists, MessageBoxImage.Warning);
                return;
            }

            App.Profiles.Prop.Items[name] = new SettingsProfile
            {
                Theme = App.Settings.Prop.Theme,
                FastFlags = CloneFlags(),
                CreatedAt = DateTime.Now,
            };

            App.Profiles.Save();

            Frontend.ShowMessageBox(String.Format(Strings.Menu_Profiles_Created, name), MessageBoxImage.Information);

            LoadData();
        }

        private void ApplyProfile(string name)
        {
            if (!App.Profiles.Prop.Items.TryGetValue(name, out SettingsProfile? profile) || profile is null)
                return;

            MessageBoxResult result = Frontend.ShowMessageBox(
                String.Format(Strings.Menu_Profiles_ApplyConfirm, name),
                MessageBoxImage.Warning,
                MessageBoxButton.YesNo
            );

            if (result != MessageBoxResult.Yes)
                return;

            // replace the current flags with the profile snapshot
            App.FastFlags.Prop.Clear();

            foreach (var pair in profile.FastFlags)
                App.FastFlags.Prop[pair.Key] = pair.Value?.ToString() ?? "";

            App.Settings.Prop.Theme = profile.Theme;

            App.Settings.Save();
            App.FastFlags.Save();

            // re-apply the theme so it takes effect immediately
            ((UI.Elements.Settings.MainWindow)Window.GetWindow(_page)!).ApplyTheme();

            Frontend.ShowMessageBox(String.Format(Strings.Menu_Profiles_Applied, name), MessageBoxImage.Information);

            LoadData();
        }

        private void DeleteProfile(string name)
        {
            MessageBoxResult result = Frontend.ShowMessageBox(
                String.Format(Strings.Menu_Profiles_DeleteConfirm, name),
                MessageBoxImage.Warning,
                MessageBoxButton.YesNo
            );

            if (result != MessageBoxResult.Yes)
                return;

            App.Profiles.Prop.Items.Remove(name);
            App.Profiles.Save();

            LoadData();
        }

        private void ExportSettings()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = $"{Strings.FileTypes_JSONFiles}|*.json",
                FileName = $"{App.ProjectName}-settings-{DateTime.Now:yyyyMMdd-HHmmss}.json",
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var globalSettings = App.GlobalSettings.PresetPaths
                    .Select(x => (Key: x.Key, Value: App.GlobalSettings.GetPreset(x.Key)))
                    .Where(x => x.Value is not null)
                    .ToDictionary(x => x.Key, x => x.Value!);

                var exportData = new
                {
                    ExportedAt = DateTime.Now.ToString("o"),
                    FishstrapVersion = App.Version,
                    Settings = App.Settings.Prop,
                    FastFlags = App.FastFlags.Prop,
                    GlobalSettings = globalSettings,
                };

                File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox(ex.Message, MessageBoxImage.Warning);
            }
        }

        private void ImportSettings()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = $"{Strings.FileTypes_JSONFiles}|*.json",
            };

            if (dialog.ShowDialog() != true)
                return;

            MessageBoxResult result = Frontend.ShowMessageBox(
                Strings.Menu_Profiles_ImportWarning,
                MessageBoxImage.Warning,
                MessageBoxButton.YesNo
            );

            if (result != MessageBoxResult.Yes)
                return;

            const string LOG_IDENT = "ProfilesViewModel::ImportSettings";

            try
            {
                string contents = File.ReadAllText(dialog.FileName);

                using var document = JsonDocument.Parse(contents, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                });

                var root = document.RootElement;

                if (root.TryGetProperty("Settings", out var settingsElement))
                {
                    var importedSettings = JsonSerializer.Deserialize<Bloxstrap.Models.Persistable.Settings>(settingsElement.GetRawText());

                    if (importedSettings is not null)
                        App.Settings.Prop = importedSettings;
                }

                if (root.TryGetProperty("FastFlags", out var flagsElement))
                {
                    var importedFlags = JsonSerializer.Deserialize<Dictionary<string, object>>(flagsElement.GetRawText());

                    App.FastFlags.Prop.Clear();

                    if (importedFlags is not null)
                        foreach (var pair in importedFlags)
                            App.FastFlags.Prop[pair.Key] = pair.Value?.ToString() ?? "";
                }

                if (root.TryGetProperty("GlobalSettings", out var globalElement))
                {
                    foreach (var pair in globalElement.EnumerateObject())
                    {
                        if (App.GlobalSettings.PresetPaths.ContainsKey(pair.Name))
                            App.GlobalSettings.SetPreset(pair.Name, pair.Value.ToString());
                    }
                }

                App.Settings.Save();
                App.FastFlags.Save();
                App.GlobalSettings.Save();

                // re-apply the theme in case it was changed by the import
                ((UI.Elements.Settings.MainWindow)Window.GetWindow(_page)!).ApplyTheme();

                Frontend.ShowMessageBox(Strings.Menu_Profiles_Imported, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);

                Frontend.ShowMessageBox(
                    String.Format(Strings.Menu_Profiles_ImportFailed, ex.Message),
                    MessageBoxImage.Warning
                );
            }
        }
    }
}
