using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GuiFunctions;
using GuiFunctions.ViewModels.ParallelSearchTask;
using Microsoft.Win32;

namespace MetaMorpheusGUI;

/// <summary>
/// Interaction logic for ParallelSearchParamsControl.xaml
/// </summary>
public partial class ParallelSearchParamsControl : UserControl
{
    public ParallelSearchParamsControl()
    {
        InitializeComponent();
        
        // Set up row selection tracking
        TransientDatabasesDataGrid.SelectionChanged += DataGrid_SelectionChanged;
    }

    private ParallelSearchParamsViewModel ViewModel => (ParallelSearchParamsViewModel)DataContext;

    private void AddDatabaseButton_Click(object sender, RoutedEventArgs e)
    {
        var openPicker = new OpenFileDialog
        {
            Filter = "Database Files|*.xml;*.xml.gz;*.fasta;*.fa",
            FilterIndex = 1,
            RestoreDirectory = true,
            Multiselect = true
        };

        if (openPicker.ShowDialog() == true)
        {
            ViewModel?.AddTransientDatabases(openPicker.FileNames
                .Where(IsValidDatabaseFile)
                .OrderBy(Path.GetFileName));
        }
    }

    private void RemoveDatabaseButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.RemoveSelectedDatabasesCommand.Execute(null);
    }

    private void ClearDatabasesButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.ClearAllDatabasesCommand.Execute(null);
    }

    private void TransientDatabaseGrid_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            ViewModel?.AddTransientDatabases(files.Where(IsValidDatabaseFile));
        }
    }

    private bool IsValidDatabaseFile(string filepath)
    {
        var extension = Path.GetExtension(filepath).ToLowerInvariant();
        bool compressed = extension.EndsWith("gz");
        extension = compressed
            ? Path.GetExtension(Path.GetFileNameWithoutExtension(filepath)).ToLowerInvariant()
            : extension;

        var validExtensions = new[] { ".xml", ".fasta", ".fa" };
        return validExtensions.Contains(extension);
    }

    private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel == null) return;

        foreach (ProteinDbForDataGrid item in e.RemovedItems)
        {
            item.IsSelected = false;
        }

        foreach (ProteinDbForDataGrid item in e.AddedItems)
        {
            item.IsSelected = true;
        }
    }

    private void DataGridCell_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridCell { Content: TextBlock textBlock } &&
            !string.IsNullOrEmpty(textBlock.Text) &&
            File.Exists(textBlock.Text))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = textBlock.Text,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Ignore errors opening files
            }
        }
    }

    private void DataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete || e.Key == Key.Back)
        {
            if (e.OriginalSource is DataGrid)
            {
                RemoveDatabaseButton_Click(sender, e);
                e.Handled = true;
            }
        }
    }
}
