using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Globalization;
using System.Windows;

namespace PlayniteDolphinMetadata
{
    public partial class DolphinMetadataSettingsView
    {
        public DolphinMetadataSettingsView()
        {
            InitializeComponent();
        }

		private void btnOpenFile_Click(object sender, RoutedEventArgs e)
		{
			var dataContext = (DolphinMetadataSettings) DataContext;

			using (var dialog = new CommonOpenFileDialog())
			{
				dialog.IsFolderPicker = true;
				CommonFileDialogResult result = dialog.ShowDialog();
				if (result == CommonFileDialogResult.Ok)
                {
					dataContext.PathToDolphinUserFolder = dialog.FileName;
                }
			}
		}
	}
}