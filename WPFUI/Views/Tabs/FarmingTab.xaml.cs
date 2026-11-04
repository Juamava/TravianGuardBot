using MainCore.UI.ViewModels.Tabs;

using ReactiveUI;


namespace WPFUI.Views.Tabs
{
    using ReactiveUI.Primitives;

    public class FarmingTabBase : ReactiveUserControl<FarmingViewModel>
    {
    }

    /// <summary>
    /// Interaction logic for FarmingTab.xaml
    /// </summary>
    public partial class FarmingTab : FarmingTabBase
    {
        public FarmingTab()
        {
            InitializeComponent();
            this.WhenActivated(d =>
            {
                this.OneWayBind(ViewModel, vm => vm.CowListVillages.Items, v => v.CowListVillageCombo.ItemsSource).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.CowListVillages.SelectedItem, v => v.CowListVillageCombo.SelectedItem).DisposeWith(d);

                this.OneWayBind(ViewModel, vm => vm.CowLists.Items, v => v.CowListGrid.ItemsSource).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.CowLists.SelectedItem, v => v.CowListGrid.SelectedItem).DisposeWith(d);
                this.OneWayBind(ViewModel, vm => vm.CowVillages.Items, v => v.CowVillageGrid.ItemsSource).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.CowVillages.SelectedItem, v => v.CowVillageGrid.SelectedItem).DisposeWith(d);

                this.Bind(ViewModel, vm => vm.CowListName, v => v.CowListNameTextBox.Text).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.CowVillageUrl, v => v.CowVillageUrlTextBox.Text).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.CowTroopCount, v => v.CowTroopCountTextBox.Text).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.CowListMinMinutes, v => v.CowListMinMinutesTextBox.Text).DisposeWith(d);
                this.Bind(ViewModel, vm => vm.CowListRepeatMinutes, v => v.CowListRepeatMinutesTextBox.Text).DisposeWith(d);
                this.OneWayBind(ViewModel, vm => vm.NextRaidCountdown, v => v.NextRaidCountdownText.Text).DisposeWith(d);
                this.OneWayBind(ViewModel, vm => vm.NextRaidCountdown, v => v.NextRaidCountdownText.Visibility,
                    text => string.IsNullOrEmpty(text) ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible).DisposeWith(d);
                this.OneWayBind(ViewModel, vm => vm.IsCowEditor, v => v.CowListSettingsPanel.Visibility,
                    open => open ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible).DisposeWith(d);
                this.OneWayBind(ViewModel, vm => vm.IsCowEditor, v => v.CowEditorPanel.Visibility,
                    open => open ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed).DisposeWith(d);
                this.OneWayBind(ViewModel, vm => vm.IsAddingCow, v => v.AddCowVillageButton.Visibility,
                    adding => adding ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed).DisposeWith(d);
                this.OneWayBind(ViewModel, vm => vm.IsAddingCow, v => v.UpdateCowVillageButton.Visibility,
                    adding => adding ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible).DisposeWith(d);
                this.OneWayBind(ViewModel, vm => vm.IsAddingCow, v => v.DeleteCowVillageButton.Visibility,
                    adding => adding ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible).DisposeWith(d);

                this.BindCommand(ViewModel, vm => vm.CreateCowListCommand, v => v.CreateCowListButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.BeginAddCowVillageCommand, v => v.BeginAddCowVillageButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.RenameCowListCommand, v => v.RenameCowListButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.DeleteCowListCommand, v => v.DeleteCowListButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.AddCowVillageCommand, v => v.AddCowVillageButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.UpdateCowVillageCommand, v => v.UpdateCowVillageButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.SaveCowListIntervalCommand, v => v.SaveCowListIntervalButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.DeleteCowVillageCommand, v => v.DeleteCowVillageButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.SearchOasisCommand, v => v.SearchOasisButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.StartCowListCommand, v => v.StartCowListButton).DisposeWith(d);
                this.BindCommand(ViewModel, vm => vm.StopCowListCommand, v => v.StopCowListButton).DisposeWith(d);
            });
        }

        private void CowListGrid_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ViewModel?.ShowListModule();
        }
    }
}