using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PlantUmlEditor.Model;
using System.Diagnostics;
using PlantUmlEditor.Properties;
using System.IO;
using System.ComponentModel;
using Utilities;

namespace PlantUmlEditor
{
    /// <summary>
    /// Takes a DiagramFile object into DataContext and renders the text editor and 
    /// shows the generated diagram
    /// </summary>
    public partial class DiagramViewControl : UserControl
    {
        private bool _RefreshDiagramTimerStarted = false;

        private Weak<MenuItem> _LastMenuItemClicked = default(Weak<MenuItem>);

        public DiagramViewControl()
        {
            InitializeComponent();

            foreach (MenuItem topLevelMenu in AddContextMenu.Items)
            {
                foreach (MenuItem itemMenu in topLevelMenu.Items)
                {
                    itemMenu.Click += new RoutedEventHandler(MenuItem_Click);
                }
            }
        }

        public event Action<DiagramFile> OnBeforeSave;
        public event Action<DiagramFile> OnAfterSave;
        public event Action<DiagramFile> OnClose;
        
        private DiagramFile CurrentDiagram
        {
            get
            {
                return this.DataContext as DiagramFile;
            }
        }

        private void SaveDiagram_Click(object sender, RoutedEventArgs e)
        {
            this.SaveAndRefreshDiagram();
        }

        private void SaveAndRefreshDiagram()
        {
            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            if (this.CurrentDiagram == default(DiagramFile))
                return;

            _RefreshDiagramTimerStarted = false;

            var diagramFileName = this.CurrentDiagram.DiagramFilePath;
            var pathForContentEditor = ContentEditor.Tag as string;

            if (diagramFileName != pathForContentEditor)
            {
                MessageBox.Show(Window.GetWindow(this), 
                    "Aha! This is one of those weird race condition I am getting into." + Environment.NewLine
                    + "Close the app and start again", "Weird race condition", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var content = ContentEditor.Text; 
            this.CurrentDiagram.Content = content;

            OnBeforeSave(this.CurrentDiagram);

            string plantUmlPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Thirdparty\\plantuml.exe");
            if (!File.Exists(plantUmlPath))
            {
                MessageBox.Show(Window.GetWindow(this), 
                    "Cannot find file: " + Environment.NewLine
                    + plantUmlPath, "PlantUml.exe not found", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (ParallelWork.IsAnyWorkRunning())
                return;

            Start.Work(() =>
                {
                    // A Bug in PlantUML which is having problem detecting encoding if the
                    // first line is not an empty line.
                    if (!char.IsWhiteSpace(content, 0))
                        content = Environment.NewLine + content;

                    // Save the diagram content
                    
                    File.WriteAllText(diagramFileName, content, Encoding.UTF8);

                    // Use plantuml to generate the graph again                    
                    using (var process = new Process())
                    {
                        var startInfo = new ProcessStartInfo();
                        startInfo.FileName = plantUmlPath;
                        startInfo.Arguments = "\"" + diagramFileName + "\"";
                        startInfo.WindowStyle = ProcessWindowStyle.Hidden; // OMAR: Trick #5
                        startInfo.CreateNoWindow = true; // OMAR: Trick #5
                        process.StartInfo = startInfo;
                        if (process.Start())
                        {
                            process.WaitForExit(10000);
                        }
                    }
                })
                .OnComplete(() =>
                {
                    BindingOperations.GetBindingExpression(DiagramImage, Image.SourceProperty).UpdateTarget();

                    OnAfterSave(this.CurrentDiagram);
                })
                .OnException((exception) =>
                {
                    OnAfterSave(this.CurrentDiagram);
                    MessageBox.Show(Window.GetWindow(this), exception.Message, "Error running PlantUml",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                })
                .Run();
        }

        private void CloseDiagram_Click(object sender, RoutedEventArgs e)
        {
            OnClose(this.CurrentDiagram);
        }

        private void UserControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue != null)
            {
                var newDiagram = (e.NewValue as DiagramFile);
                ContentEditor.Text = newDiagram.Content;
                ContentEditor.Tag = newDiagram.DiagramFilePath;
            }            

            if (this._LastMenuItemClicked != default(Weak<MenuItem>))
            {
                this._LastMenuItemClicked.Dispose();
                this._LastMenuItemClicked = null;
            }
        }

        private void ContentEditor_TextChanged(object sender, EventArgs e)
        {
            if (AutoRefreshCheckbox.IsChecked.Value)
            {
                if (!_RefreshDiagramTimerStarted)
                {
                    _RefreshDiagramTimerStarted = true;
                    ParallelWork.StartAfter(SaveAndRefreshDiagram, 
                                               TimeSpan.FromSeconds(
                                                   int.Parse(RefreshSecondsTextBox.Text)));
                }
            }
        }

        private void AddStuff_Click(object sender, RoutedEventArgs e)
        {
            // Trick: Open the context menu automatically whenever user
            // clicks the "Add" button
            AddContextMenu.IsOpen = true;

            // If user last added a particular diagram items, say Use case
            // item, then auto open the usecase menu so that user does not
            // have to click on use case again. Saves time when you are adding
            // a lot of items for the same diagram
            if (_LastMenuItemClicked != default(Weak<MenuItem>))
            {
                MenuItem parentMenu = (_LastMenuItemClicked.Target.Parent as MenuItem);
                parentMenu.IsSubmenuOpen = true;
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            this._LastMenuItemClicked = e.Source as MenuItem;
            this.AddCode((e.Source as MenuItem).Tag as string);
        }

        private void AddCode(string code)
        {
            ContentEditor.SelectionLength = 0;

            var formattedCode = code.Replace("\\r", Environment.NewLine) 
                + Environment.NewLine
                + Environment.NewLine;

            Clipboard.SetText(formattedCode);
            ContentEditor.Paste();

            this.SaveAndRefreshDiagram();
        }

        private void CopyToClipboard_Click(object sender, RoutedEventArgs e)
        {            
            Clipboard.SetImage(DiagramImage.Source as BitmapSource);
        }

        private void OpenInExplorer_Click(object sender, RoutedEventArgs e)
        {
            Process
                .Start("explorer.exe","/select," + this.CurrentDiagram.ImageFilePath)
                .Dispose();
        }

        private void ContentEditor_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S)
                if (Keyboard.Modifiers == ModifierKeys.Control)
                    this.SaveAndRefreshDiagram();
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: In progress
            //this.ContentEditor.Use(c =>
            //    {
            //        var textToFind = this.SearchTextBox.Text;
            //        var pos = c.SelectionStart;
                    
            //        if (textPos >= c.SelectionStart)
            //        {
            //            c.Select(pos, textToFind.Length);
            //        }
            //    });
        }

        private void ReplaceButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void CopyPath_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(this.CurrentDiagram.ImageFilePath);
        }

    }
}
