using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using ImageViewer.Runtime;
using ImageViewer.Viewer;
using Wpf.Ui.Controls;
using WinMessageBox = System.Windows.MessageBox;
using WinMessageBoxButton = System.Windows.MessageBoxButton;
using WinMessageBoxImage = System.Windows.MessageBoxImage;
using WinMessageBoxResult = System.Windows.MessageBoxResult;

namespace ImageViewer.Views
{
    // 「复制到」窗口：每行一个目标目录 + 后缀，点行/[复制] 即把当前图片复制过去。
    public partial class CopyToWindow : FluentWindow
    {
        private const string NoTimestampDisplay = "（不添加）";
        private static readonly string[] TimestampOptions =
        {
            NoTimestampDisplay, "yyyyMMdd_HHmmss", "yyyy-MM-dd_HH-mm-ss", "yyyyMMdd", "yyyyMMddHHmmss"
        };
        private static readonly CopyConflict[] ConflictValues = { CopyConflict.Ask, CopyConflict.Overwrite, CopyConflict.Skip };
        private static readonly string[] ConflictDisplays = { "询问", "覆盖", "跳过" };

        private readonly string sourcePath;
        private readonly ObservableCollection<CopyTargetRow> rows = new ObservableCollection<CopyTargetRow>();

        public CopyToWindow(string sourcePath)
        {
            InitializeComponent();
            this.sourcePath = sourcePath;
            Title = String.IsNullOrWhiteSpace(sourcePath) ? "复制到" : "复制到 - " + Path.GetFileName(sourcePath);

            var settings = CopySettingsStore.Load(ImageViewerPaths.ConfigFilePath);

            foreach (var option in TimestampOptions) TimestampFormatComboBox.Items.Add(option);
            TimestampFormatComboBox.SelectedItem =
                Array.IndexOf(TimestampOptions, settings.TimestampFormat) >= 0 ? settings.TimestampFormat : NoTimestampDisplay;

            foreach (var display in ConflictDisplays) ConflictComboBox.Items.Add(display);
            var conflictIndex = Array.IndexOf(ConflictValues, settings.Conflict);
            ConflictComboBox.SelectedIndex = conflictIndex >= 0 ? conflictIndex : 0;

            foreach (var target in settings.Targets)
            {
                rows.Add(new CopyTargetRow { Path = target.Path, Suffix = target.Suffix });
            }
            TargetList.ItemsSource = rows;
            Renumber();
            if (rows.Count > 0) TargetList.SelectedIndex = 0;

            Diagnostics.Sink.Log(LogSeverity.Info, "ImageViewer", "打开复制到窗口：" + sourcePath, null);
        }

        private void Renumber()
        {
            for (var i = 0; i < rows.Count; i++) rows[i].Number = i + 1;
        }

        private string CurrentTimestampFormat
        {
            get
            {
                var selected = TimestampFormatComboBox.SelectedItem as string;
                return selected == null || selected == NoTimestampDisplay ? "" : selected;
            }
        }

        private CopyConflict CurrentConflict
        {
            get
            {
                var index = ConflictComboBox.SelectedIndex;
                return index >= 0 && index < ConflictValues.Length ? ConflictValues[index] : CopyConflict.Ask;
            }
        }

        private void AddClick(object sender, RoutedEventArgs e)
        {
            var row = new CopyTargetRow();
            rows.Add(row);
            Renumber();
            TargetList.SelectedItem = row;
        }

        private void RemoveClick(object sender, RoutedEventArgs e)
        {
            var row = TargetList.SelectedItem as CopyTargetRow;
            if (row == null) return;
            rows.Remove(row);
            Renumber();
        }

        private void MoveUpClick(object sender, RoutedEventArgs e) { Move(-1); }

        private void MoveDownClick(object sender, RoutedEventArgs e) { Move(1); }

        private void Move(int delta)
        {
            var row = TargetList.SelectedItem as CopyTargetRow;
            if (row == null) return;
            var index = rows.IndexOf(row);
            var destination = index + delta;
            if (destination < 0 || destination >= rows.Count) return;
            rows.Move(index, destination);
            Renumber();
            TargetList.SelectedItem = row;
        }

        private void BrowseClick(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement) == null ? null : (sender as FrameworkElement).Tag as CopyTargetRow;
            if (row == null) return;
            var selected = FolderPicker.Pick(new WindowInteropHelper(this).Handle, row.Path);
            if (!String.IsNullOrWhiteSpace(selected)) row.Path = selected;
        }

        private void CopyRowClick(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement) == null ? null : (sender as FrameworkElement).Tag as CopyTargetRow;
            if (row != null) CopyRow(row);
        }

        // 点击行任意非编辑/按钮区域即复制到该行。
        private void TargetListMouseUp(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            if (source == null) return;
            if (FindParent<System.Windows.Controls.TextBox>(source) != null) return;
            if (FindParent<System.Windows.Controls.Primitives.ButtonBase>(source) != null) return;

            var item = ItemsControl.ContainerFromElement(TargetList, source) as ListBoxItem;
            var row = item == null ? null : item.DataContext as CopyTargetRow;
            if (row == null) return;
            TargetList.SelectedItem = row;
            CopyRow(row);
        }

        private void CopyRow(CopyTargetRow row)
        {
            if (String.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                StatusText.Text = "当前没有可复制的图片。";
                return;
            }
            if (String.IsNullOrWhiteSpace(row.Path))
            {
                StatusText.Text = "该目标未设置目录。";
                return;
            }

            var fileName = CopyFileName.Build(Path.GetFileName(sourcePath), row.Suffix, CurrentTimestampFormat, DateTime.Now);
            try
            {
                var result = ImageCopyService.Copy(sourcePath, row.Path, fileName, CurrentConflict, ResolveConflict);
                if (result == CopyResult.Copied)
                {
                    // 成功即关闭（关闭会触发配置保存）；仅失败/跳过/取消时留在窗口提示。
                    Close();
                    return;
                }
                if (result == CopyResult.Skipped) StatusText.Text = "已跳过（同名文件）：" + fileName;
                else StatusText.Text = "已取消。";
            }
            catch (Exception error)
            {
                StatusText.Text = "复制失败：" + error.Message;
                Diagnostics.Sink.Log(LogSeverity.Error, "ImageViewer", "复制到目标失败：" + row.Path, error);
            }
        }

        // Windows 式冲突询问：覆盖 / 跳过 / 取消。
        private static CopyDecision ResolveConflict(string destination)
        {
            var result = WinMessageBox.Show(
                destination + "\n\n目标已存在同名文件，是否覆盖？",
                "文件已存在",
                WinMessageBoxButton.YesNoCancel,
                WinMessageBoxImage.Warning);
            if (result == WinMessageBoxResult.Yes) return CopyDecision.Overwrite;
            if (result == WinMessageBoxResult.No) return CopyDecision.Skip;
            return CopyDecision.Cancel;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            SaveSettings();
        }

        private void SaveSettings()
        {
            try
            {
                var settings = CopySettingsStore.Load(ImageViewerPaths.ConfigFilePath);
                settings.TimestampFormat = CurrentTimestampFormat;
                settings.Conflict = CurrentConflict;
                settings.Targets.Clear();
                foreach (var row in rows)
                {
                    settings.Targets.Add(new CopyTarget { Path = row.Path, Suffix = row.Suffix });
                }
                CopySettingsStore.Save(ImageViewerPaths.ConfigFilePath, settings);
            }
            catch (Exception error)
            {
                Diagnostics.Sink.Log(LogSeverity.Error, "ImageViewer", "保存复制目标配置失败", error);
            }
        }

        private static T FindParent<T>(DependencyObject node) where T : DependencyObject
        {
            var current = node;
            while (current != null)
            {
                var match = current as T;
                if (match != null) return match;
                // 非 Visual（如文本内容元素）不能走 VisualTreeHelper，否则抛异常。
                current = current is Visual || current is System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(current)
                    : LogicalTreeHelper.GetParent(current);
            }
            return null;
        }

        // 列表行：目录与后缀可编辑，序号随增删/移动刷新。
        public sealed class CopyTargetRow : BindableViewModel
        {
            private string path = "";
            private string suffix = "";
            private int number;

            public string Path
            {
                get { return path; }
                set { if (path != value) { path = value ?? ""; Changed("Path"); } }
            }

            public string Suffix
            {
                get { return suffix; }
                set { if (suffix != value) { suffix = value ?? ""; Changed("Suffix"); } }
            }

            public int Number
            {
                get { return number; }
                set { if (number != value) { number = value; Changed("Number"); } }
            }
        }
    }
}
