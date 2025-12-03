using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GuiFunctions;
using Microsoft.Win32;

namespace MetaMorpheusGUI;

/// <summary>
/// Interaction logic for ManySearchParamsControl.xaml
/// </summary>
public partial class ManySearchParamsControl : UserControl
{
    public ManySearchParamsControl()
    {
        InitializeComponent();
        
        // Set up row selection tracking
        TransientDatabasesDataGrid.SelectionChanged += DataGrid_SelectionChanged;
    }

    private ManySearchParamsViewModel ViewModel => (ManySearchParamsViewModel)DataContext;

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
            foreach (var filePath in openPicker.FileNames.OrderBy(p => Path.GetFileName(p)))
            {
                if (IsValidDatabaseFile(filePath))
                {
                    ViewModel?.AddTransientDatabase(filePath);
                }
            }
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
            foreach (var file in files.Where(IsValidDatabaseFile))
            {
                ViewModel?.AddTransientDatabase(file);
            }
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
        // Update IsSelected property on items
        if (ViewModel == null) return;

        foreach (var item in ViewModel.TransientDatabases)
        {
            item.IsSelected = TransientDatabasesDataGrid.SelectedItems.Contains(item);
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
