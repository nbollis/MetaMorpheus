using GuiFunctions.ViewModels.ParallelSearchTask;
using System.Windows;
using System.Windows.Controls;

namespace MetaMorpheusGUI;

/// <summary>
/// Interaction logic for ParallelSearchResultsTabView.xaml
/// </summary>
public partial class ParallelSearchResultsTabView : UserControl
{
    public ParallelSearchResultsTabView()
    {
        InitializeComponent();
    }

    private void SelectResultsDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        var dataContext = DataContext as ParallelSearchResultsTabViewModel;
        if (dataContext == null)
            return;

        Microsoft.Win32.OpenFolderDialog openFolderDialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Results Directory"
        };

        if (openFolderDialog.ShowDialog() == true)
        {
            dataContext.DirectoryFilePath = openFolderDialog.FolderName;
        }
    }

    /// <summary>
    /// Reset results file selection
    /// </summary>
    private void ResetResultsButton_Click(object sender, RoutedEventArgs e)
    {
        var dataContext = DataContext as ParallelSearchResultsTabViewModel;
        if (dataContext == null)
            return;

        dataContext.DirectoryFilePath = null;
        dataContext.ResultsViewModel.FilteredDatabaseResults.Clear();
        dataContext.StatusMessage = "Results cleared. Please load a new results directory.";
    }

    /// <summary>
    /// Load both statistical and analysis results
    /// </summary>
    private void LoadResultsButton_Click(object sender, RoutedEventArgs e)
    {
        var dataContext = DataContext as ParallelSearchResultsTabViewModel;
        if (dataContext == null)
            return;

        if (string.IsNullOrEmpty(dataContext.DirectoryFilePath))
        {
            MessageBox.Show("Please select a results directory to load.");
            return;
        }

        // Execute the command - it will handle async internally
        if (dataContext.LoadResultsCommand.CanExecute(null))
        {
            dataContext.LoadResultsCommand.Execute(dataContext.DirectoryFilePath);
        }
    }
}
