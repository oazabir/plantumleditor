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
using System.Collections.ObjectModel;
using System.Net;
using System.IO;
using System.Diagnostics;
using System.Web;

namespace PlantUmlEditor
{
    using System.Windows.Media.Animation;
    using System.ComponentModel;
    using System.Security.Principal;
    using System.Threading;
    using System.Windows.Interop;
    using System.Reflection;
    using System.Windows.Threading;
    using System.Diagnostics;
    using Microsoft.Win32;
    using PlantUmlEditor.Model;
    using PlantUmlEditor.CustomAnimation;
    using Utilities;
    using PlantUmlEditor.Properties;

    /// <summary>
    /// MainWindow that hosts the diagram list box and the editing environment.
    /// </summary>
    public partial class MainWindow : Window
    {
        protected DiagramViewControl DiagramView;

        private ObservableCollection<DiagramFile> _DiagramFiles = new ObservableCollection<DiagramFile>();
        private ObservableCollection<DiagramFile> _OpenDiagrams = new ObservableCollection<DiagramFile>();
        
        protected GridLength LeftColumnLastWidthBeforeAnimation
        {
            get;
            set;
        }

        public MainWindow()
        {
            InitializeComponent();            

            this.DiagramFileListBox.ItemsSource = null;
            this.DiagramTabs.ItemsSource = null;            
        }

        private void DiagramLocationTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox box = e.Source as TextBox;
            if (box.Text == box.Tag as string)
                box.Text = string.Empty;
        }

        private void DiagramLocationTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox box = e.Source as TextBox;
            if (box.Text.Length == 0)
                box.Text = box.Tag as string;
        }

        private void BrowseForFile_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.ShowNewFolderButton = true;
                System.Windows.Forms.DialogResult result = 
                dlg.ShowDialog(
                    new OldWindow( 
                        new WindowInteropHelper(this).Handle));
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    this.DiagramLocationTextBox.Text = dlg.SelectedPath;
                    this.LoadDiagramFiles(dlg.SelectedPath, () => {});
                }
            }
        }

        private void LoadDiagramFiles(string path, Action loaded)
        {
            _DiagramFiles.Clear();
            
            this.StartProgress("Loading diagrams...");

            var listbox = new Weak<ListBox>(this.DiagramFileListBox);
            Start<List<DiagramFile>>.Work(() =>
                {
                    var diagrams = new List<DiagramFile>();
            
                    foreach (string file in Directory.GetFiles(path))
                    {
                        string content = File.ReadAllText(file);
                        if (content.Length > 0)
                        {
                            string firstLine = content.Substring(0, 
                                content.IndexOf(Environment.NewLine[0]));
                            if (firstLine.StartsWith("@startuml"))
                            {
                                string imageFileName = firstLine.Substring(content.IndexOf(' ') + 1)
                                    .TrimStart('"').TrimEnd('"');

                                diagrams.Add(new DiagramFile{
                                                      Content = content,
                                                      DiagramFilePath = file,
                                                      ImageFilePath =
                                                  System.IO.Path.IsPathRooted(imageFileName) ? 
                                                    System.IO.Path.GetFullPath(imageFileName)
                                                    : System.IO.Path.GetFullPath(
                                                        System.IO.Path.Combine(path, imageFileName))
                                                  });
                            }
                        }
                    }

                    return diagrams;
                })
                .OnComplete((diagrams) =>
                {                   
                    this._DiagramFiles = new ObservableCollection<DiagramFile>(diagrams);
                    (listbox.Target as ListBox).ItemsSource = this._DiagramFiles;
                    this.StopProgress("Diagrams loaded.");
                    loaded();
                })
                .OnException((exception) =>
                {
                    MessageBox.Show(this, exception.Message, "Error loading files", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    this.StopProgress(exception.Message);
                });
        }

        private void RefreshDiagramList_Click(object sender, RoutedEventArgs e)
        {
            this.LoadDiagramFiles(this.DiagramLocationTextBox.Text, () => {});
        }

        private void DiagramLocationTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Directory.Exists(this.DiagramLocationTextBox.Text))
            {
                this.LoadDiagramFiles(this.DiagramLocationTextBox.Text, () => {});
            }
        }

        private void DiagramFileListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var diagramFile = this.DiagramFileListBox.SelectedItem as DiagramFile;
            this.OpenDiagramFile(diagramFile);
        }

        private void OpenDiagramFile(DiagramFile diagramFile)
        {
            if (!_OpenDiagrams.Contains(diagramFile))
            {
                _OpenDiagrams.Add(diagramFile);

                this.DiagramTabs.ItemsSource = _OpenDiagrams;
                this.DiagramTabs.Visibility = Visibility.Visible;
                this.WelcomePanel.Visibility = Visibility.Hidden;
            }
            
            this.DiagramTabs.SelectedItem = diagramFile;            
        }

        private void DiagramFileListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        protected void DiagramViewControl_OnAfterSave(DiagramFile diagram)
        {
            this.StopProgress("Saved.");
        }

        protected void DiagramViewControl_OnBeforeSave(DiagramFile diagram)
        {
            this.StartProgress("Saving and generating diagram...");
        }

        private void DiagramViewControl_OnClose(DiagramFile diagram)
        {
            this._OpenDiagrams.Remove(diagram);

            if (this._OpenDiagrams.Count == 0)
            {
                this.WelcomePanel.Visibility = Visibility.Visible;
            }
            else
            {
                this.DiagramTabs.SelectedItem = this._OpenDiagrams[0];
            }
        }

        private void StartProgress(string message)
        {
            this.StatusMessage.Text = message;
            this.StatusProgressBar.Visibility = Visibility.Visible;
        }

        private void StopProgress(string message)
        {
            this.StatusMessage.Text = message;
            this.StatusProgressBar.Visibility = Visibility.Hidden;
        }

        private void CreateNewDiagram_Click(object sender, RoutedEventArgs e)
        {
            this.StartNewDiagram();
        }

        private void StartNewDiagram()
        {
            SaveFileDialog dlg = new SaveFileDialog();
            
            dlg.DefaultExt = "*.txt";
            dlg.Filter = "Diagram text files (*.txt)|*.txt";
            dlg.AddExtension = true;
            if (dlg.ShowDialog().Value)
            {
                var diagramFileName = dlg.FileName;
                File.WriteAllText(diagramFileName, string.Format(
                    "@startuml \"{0}\"" + Environment.NewLine
                    + Environment.NewLine
                    + Environment.NewLine 
                    + "@enduml", 
                    System.IO.Path.GetFileNameWithoutExtension(diagramFileName) + ".png"));

                this.DiagramLocationTextBox.Text = System.IO.Path.GetDirectoryName(diagramFileName);
                Weak<ListBox> listbox = this.DiagramFileListBox;
                this.LoadDiagramFiles(this.DiagramLocationTextBox.Text, 
                                      () => 
                                      {
                                          var diagramOnList = this._DiagramFiles.First(
                                              d => d.DiagramFilePath == diagramFileName);
                                          ListBox diagramListBox = listbox;
                                          diagramListBox.SelectedItem = diagramOnList;
                                          this.OpenDiagramFile(diagramOnList);
                                      });
            }            
        }

        private void DiagramView_GotFocus(object sender, RoutedEventArgs e)
        {            
            if (this.LeftColumn.ActualWidth > this.LeftColumnLastWidthBeforeAnimation.Value)
                this.LeftColumnLastWidthBeforeAnimation = new GridLength(this.LeftColumn.ActualWidth);
            ((Storyboard)this.Resources["CollapseTheDiagramListBox"]).Begin(this, true);                        
        }

        private void DiagramView_LostFocus(object sender, RoutedEventArgs e)
        {
            var storyboard = ((Storyboard)this.Resources["ExpandTheDiagramListBox"]);
            (storyboard.Children[0] as GridLengthAnimation).To = this.LeftColumnLastWidthBeforeAnimation;
            storyboard.Begin(this, true);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!CheckGraphViz())
            {
                this.Close();
                return;
            }

            this.DiagramLocationTextBox.Text = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PlantUmlEditor\\samples\\");

            // After a while check for new version
            ParallelWork.StartAfter(CheckForUpdate, TimeSpan.FromMinutes(1));            
        }

        /// <summary>
        /// Checks if there's a new version of the app and downloads and installs 
        /// if user wants to.
        /// </summary>
        private void CheckForUpdate()
        {
            var me = new Weak<Window>(this);
            
            // Check if there's a newer version of the app
            Start<bool>.Work(() => 
            {
                return UpdateChecker.HasUpdate(Settings.Default.DownloadUrl);
            })
            .OnComplete((hasUpdate) =>
            {
                if (hasUpdate)
                {
                    if (MessageBox.Show(Window.GetWindow(me),
                        "There's a newer version available. Do you want to download and install?",
                        "New version available",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information) == MessageBoxResult.Yes)
                    {
                        ParallelWork.StartNow(() => {
                            var tempPath = System.IO.Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                Settings.Default.SetupExeName);
        
                            UpdateChecker.DownloadLatestUpdate(Settings.Default.DownloadUrl, tempPath);
                        }, () => { },
                            (x) =>
                            {
                                MessageBox.Show(Window.GetWindow(me),
                                    "Download failed. When you run next time, it will try downloading again.",
                                    "Download failed",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                            });
                    }
                }
            })
            .OnException((x) => 
            {
                MessageBox.Show(Window.GetWindow(me),
                    x.Message,
                    "Download failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation);
            });

            UpdateChecker.DownloadCompleted = new Action<AsyncCompletedEventArgs>((e) =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (e.Cancelled || e.Error != default(Exception))
                    {
                        MessageBox.Show(Window.GetWindow(me),
                                    "Download failed. When you run next time, it will try downloading again.",
                                    "Download failed",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                    }
                    else
                    {
                        Process.Start(UpdateChecker.DownloadedLocation).Dispose();
                        this.Close();
                    }
                }));
            });

            UpdateChecker.DownloadProgressChanged = new Action<DownloadProgressChangedEventArgs>((e) =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    this.StartProgress("New version downloaded " + e.ProgressPercentage + "%");
                }));
            });

            
        }

        private void NameHyperlink_Click(object sender, RoutedEventArgs e)
        {
            Process.Start((e.OriginalSource as Hyperlink).NavigateUri.ToString());                
        }

        private bool CheckGraphViz()
        {
            var graphVizPath = Environment.GetEnvironmentVariable("GRAPHVIZ_DOT");
            if (string.IsNullOrEmpty(graphVizPath))
            {
                MessageBox.Show(Window.GetWindow(this),
                    "You haven't either installed GraphViz or you haven't created " + 
                    Environment.NewLine + "the environment variable name GRAPHVIZ_DOT that points to the dot.exe" +
                    Environment.NewLine + "where GraphViz is installed. Please create and re-run.",
                    "GraphViz Environment variable not found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return false;
            }
            else
            {
                if (!File.Exists(graphVizPath))
                {
                    MessageBox.Show(Window.GetWindow(this),
                        "The path you have set in GRAPHVIZ_DOT is invalid. Please fix.",
                        "GraphViz path wrong",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return false;
                }
                else
                {
                    var filename = System.IO.Path.GetFileName(graphVizPath);
                    if (filename.ToLower() != "dot.exe")
                    {
                        MessageBox.Show(Window.GetWindow(this),
                            "The path you have set in GRAPHVIZ_DOT is not pointing to dot.exe. It has to be dot.exe's path",
                            "GraphViz path wrong",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        return false;
                    }
                }
            }

            return true;
        }
    }
}
