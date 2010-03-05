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
using PlantUmlEditor.Helper;
using System.Diagnostics;
using PlantUmlEditor.Properties;
using System.IO;

namespace PlantUmlEditor
{
    /// <summary>
    /// Interaction logic for DiagramViewControl.xaml
    /// </summary>
    public partial class DiagramViewControl : UserControl
    {
        public DiagramViewControl()
        {
            InitializeComponent();
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
            this.RefreshDiagram();
        }

        private void RefreshDiagram()
        {
            var diagramFileName = this.CurrentDiagram.DiagramFilePath;
            var content = ContentEditor.Text; //this.CurrentDiagram.Content;

            OnBeforeSave(this.CurrentDiagram);

            string plantUmlPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plantuml.exe");
            if (!File.Exists(plantUmlPath))
            {
                MessageBox.Show(Window.GetWindow(this), "Cannot find file: " + Environment.NewLine
                                                        + plantUmlPath, "PlantUml.exe not found", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            BackgroundWork.WaitForAllWork(TimeSpan.FromSeconds(20));
            BackgroundWork.DoWork(
                () =>
                {
                    // Save the diagram content
                    File.WriteAllText(diagramFileName, content);

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
                },
                () =>
                {
                    BindingOperations.GetBindingExpression(DiagramImage, Image.SourceProperty).UpdateTarget();
                    OnAfterSave(this.CurrentDiagram);
                },
                (exception) =>
                {
                    OnAfterSave(this.CurrentDiagram);
                    MessageBox.Show(Window.GetWindow(this), exception.Message, "Error running PlantUml",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                });
        }

        private void CloseDiagram_Click(object sender, RoutedEventArgs e)
        {
            OnClose(this.CurrentDiagram);
        }

        private void UserControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // OMAR: Trick #6
            if (e.NewValue != null)
            {
                var newDiagram = (e.NewValue as DiagramFile);
                ContentEditor.Text = newDiagram.Content;
            }
            if (e.OldValue != null)
            {
                var oldDiagram = (e.OldValue as DiagramFile);
                oldDiagram.Content = ContentEditor.Text;
            }
        }

        private void ContentEditor_TextChanged(object sender, EventArgs e)
        {
            if (AutoRefreshCheckbox.IsChecked.Value)
            {
                if (!BackgroundWork.IsWorkQueued())
                {
                    BackgroundWork.DoWorkAfter(RefreshDiagram, 
                                               TimeSpan.FromSeconds(
                                                   int.Parse(RefreshSecondsTextBox.Text)));
                }
            }
        }

        private void AddStuff_Click(object sender, RoutedEventArgs e)
        {
            AddContextMenu.IsOpen = true;
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            this.AddCode((e.Source as MenuItem).Tag as string);
        }

        private void AddCode(string code)
        {
            ContentEditor.SelectedText = code + Environment.NewLine;
            ContentEditor.SelectionLength = 0;
        }
    }
}
