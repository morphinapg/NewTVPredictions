using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Timers;

namespace NewTVPredictions.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    
    UserControl _activePage = new()                                                     
    {
        Transitions = new Transitions
        {
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = TimeSpan.FromSeconds(0.1)
            }
        }
    };
    /// <summary>
    /// This UserControl will hold whatever Page is active in the main view
    /// </summary>
    public UserControl ActivePage => _activePage;

    UserControl _subPage = new()
    {
        Transitions = new Transitions
        {
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = TimeSpan.FromSeconds(0.1)
            }
        }
    };

    /// <summary>
    /// This UserControl will hold whatever page is being displayed on the current NetworkHome page
    /// </summary>
    public UserControl SubPage => _subPage;

    /// <summary>
    /// Changes the active page, using an opacity transition
    /// </summary>
    /// <param name="TargetPage">The UserControl to change</param>
    /// <param name="NewPage">the new UserControl</param>
    public async Task ReplacePage(UserControl TargetPage, UserControl NewPage)          
    {
        if (TargetPage is not null)
        {
            await Dispatcher.UIThread.InvokeAsync(() => TargetPage.Opacity = 0);

            await Task.Delay(100);

            TargetPage.Content = NewPage;

            await Dispatcher.UIThread.InvokeAsync(() => TargetPage.Opacity = 1);
        }
    }

    Network _currentNetwork = new();
    /// <summary>
    /// DataContext of the current network displayed on the AddNetwork page
    /// </summary>
    public Network CurrentNetwork                                                       
    {
        get => _currentNetwork;
        set
        {
            _currentNetwork = value;
            OnPropertyChanged(nameof(CurrentNetwork));
        }
    }

    /// <summary>
    /// //Activates the Add Network button, navigating to the Add Network Page
    /// </summary>
    public CommandHandler Add_Network => new CommandHandler(AddNetwork_Clicked);        
    async void AddNetwork_Clicked()
    {
        CurrentNetwork = new();
        await ReplacePage(ActivePage, new AddNetwork());

        SelectedNetwork = null;
    }

    ObservableCollection<Network> _networks = new();
    /// <summary>
    /// Collection of all networks stored in the database
    /// </summary>
    public ObservableCollection<Network> Networks
    {
        get => _networks;
        set
        {
            _networks = value;
            OnPropertyChanged(nameof(Networks));
        }
    }

    ObservableCollection<Evolution> _evolutionList = new();
    /// <summary>
    /// Collection of all Evolution objects
    /// </summary>
    public ObservableCollection<Evolution> EvolutionList
    {
        get => _evolutionList;
        set
        {
            _evolutionList = value;
            OnPropertyChanged(nameof(EvolutionList));
        }
    }

    /// <summary>
    /// Saves the current network to the Networks collection
    /// </summary>
    public CommandHandler Save_Network => new CommandHandler(SaveNetwork_Clicked);      
    async void SaveNetwork_Clicked()
    {
        if (CurrentNetwork is null)
            await MessageBoxManager.GetMessageBoxStandard("Error", "Error retrieving network information. Network was null.", ButtonEnum.Ok).ShowAsync();
        else if (string.IsNullOrEmpty(CurrentNetwork.Name))
            await MessageBoxManager.GetMessageBoxStandard("Error", "Please give the network a name before saving!", ButtonEnum.Ok).ShowAsync();
        else if (Networks.Contains(CurrentNetwork) || Networks.Select(x => x.Name).Contains(CurrentNetwork.Name))
            await MessageBoxManager.GetMessageBoxStandard("Error", "Network with the same name already exists in database!", ButtonEnum.Ok).ShowAsync();
        else
        {
            Networks.Add(CurrentNetwork);
            CurrentNetwork = new();

            SaveDatabase();
        }
    }

    /// <summary>
    /// Clears the currently displayed network to default empty network
    /// </summary>
    public CommandHandler Clear_Network => new CommandHandler(ClearNetwork_Clicked);    

    void ClearNetwork_Clicked()
    {
        CurrentNetwork = new();
    }

    Network? _selectedNetwork;
    /// <summary>
    /// Property to track when the SelectedItem of the Networks ItemsList changes, in order to navigate to the NetworkHome page for that Network
    /// </summary>
    public Network? SelectedNetwork                                                     
    {
        get => _selectedNetwork;
        set
        {
            _selectedNetwork = value;
            
            OnPropertyChanged(nameof(SelectedNetwork));           

            SelectNetwork();
        }
    }

    NetworkHome? CurrentNetworkHome;

    /// <summary>
    /// Switch the displayed page to the NetworkHome page for the currently selected network
    /// </summary>
    async void SelectNetwork()                                                          
    {
        if (SelectedNetwork is not null)
        {
            SelectedNetwork.CurrentYear = CurrentYear;

            if (ActivePage?.Content is not NetworkHome)
            {
                if (CurrentNetworkHome is null)
                    CurrentNetworkHome = new();

                await ReplacePage(ActivePage!, CurrentNetworkHome);
            }

            SwitchTab();
        }
    }

    AddShow? CurrentAddShow;

    /// <summary>
    /// constants defining the tab order
    /// </summary>
    const int                                                                           
        PREDICTIONS = 0,
        ADD_SHOW = 1,
        EDIT_RATINGS = 2,
        MODIFY_SHOW = 3,
        SHOWS_BY_RATING = 4,
        SHOWS_BY_FACTOR = 5,
        PREDICTION_ACCURACY = 6,
        PREDICTION_BREAKDOWN = 7,
        SIMIAR_SHOWS = 8,
        MODIFY_FACTORS = 9;


    int _selectedTabIndex = PREDICTIONS;
    /// <summary>
    /// Selected tab on Network Home page
    /// </summary>
    public int SelectedTabIndex                                                        
    {
        get => _selectedTabIndex;
        set
        {
            _selectedTabIndex = value;
            OnPropertyChanged(nameof(SelectedTabIndex));

            SwitchTab();
        }
    }

    Predictions? CurrentPredictions;
    EditRatings? CurrentEditRatings;
    ModifyShow? CurrentModifyShow;
    ShowsByFactor? CurrentShowsByFactor;

    /// <summary>
    /// Switch to a tab on the NetworkHome page
    /// </summary>
    async void SwitchTab()                                                              
    {

        switch (SelectedTabIndex)
        {
            case PREDICTIONS:
                if (SelectedNetwork is not null && SelectedNetwork.Evolution is not null)
                {
                    SelectedNetwork.Evolution.GeneratePredictions(CurrentYear, CurrentPredictions is null);
                    SelectedNetwork.UpdateFilter();
                }                    

                if (CurrentPredictions is null)
                    CurrentPredictions = new Predictions();

                if (SubPage?.Content is not Predictions)
                    await ReplacePage(SubPage!, CurrentPredictions);
                break;
            case ADD_SHOW:
                if (CurrentAddShow is null)
                    CurrentAddShow = new();

                if (SubPage?.Content is not AddShow)       
                    await ReplacePage(SubPage!, CurrentAddShow);

                SelectedNetwork?.ResetShow();

                break;
            case EDIT_RATINGS:
                if (CurrentEditRatings is null)
                    CurrentEditRatings = new();

                if (SubPage?.Content is not EditRatings)
                    await ReplacePage(SubPage!, CurrentEditRatings);

                break;
            case MODIFY_SHOW:
                if (CurrentModifyShow is null)
                    CurrentModifyShow = new();

                SelectedNetwork?.ResetShow();

                if (SubPage?.Content is not ModifyShow)
                    await ReplacePage(SubPage!, CurrentModifyShow);

                break;
            case SHOWS_BY_FACTOR:
                if (CurrentShowsByFactor is null)
                    CurrentShowsByFactor = new();

                SelectedNetwork?.SubscribeToFactors();

                if (SubPage?.Content is not ShowsByFactor)
                    await ReplacePage(SubPage!, CurrentShowsByFactor);

                SelectedNetwork?.ResetShow();
                SelectedNetwork?.Factor_Toggled(this, new EventArgs());

                break;
        }
    }

    /// <summary>
    /// returns the current TV season starting year (September through August)
    /// </summary>
    

    int? _currentYear;
    /// <summary>
    /// The currently set TV season
    /// </summary>
    public int CurrentYear                                                             
    {
        get
        {
            if (_currentYear is null)
            {
                var season = CurrentApp.CurrentYear;
                _currentYear = season;
                return season;
            }
            else
                return _currentYear.Value;
        }
        set
        {
            _currentYear = value; 
            OnPropertyChanged(nameof(CurrentYear));
            
            if (SelectedNetwork is not null)
            {
                SelectedNetwork.CurrentYear = value;

                SwitchTab();
            }                
        }
    }

    /// <summary>
    /// Get and set the current TV Season from DatePicker
    /// </summary>
    public DateTimeOffset? SelectedYear                                                 
    {
        get => new DateTimeOffset(new DateTime(CurrentYear, 1, 1), TimeSpan.Zero);
        set
        {
            var PreviousYear = _currentYear;

            if (value is DateTimeOffset d)
                CurrentYear = d.Year;

            OnPropertyChanged(nameof(SelectedYear));
        }
    }

    bool _importVisible = false;
    /// <summary>
    /// Whether the import button is visible or not
    /// </summary>
    public bool ImportVisible
    {
        get => _importVisible;
        set
        {
            _importVisible = value; 
            OnPropertyChanged(nameof(ImportVisible));
        }
    }

    bool _trainingEnabled = false;
    /// <summary>
    /// Whether the training button 
    /// </summary>
    public bool TrainingEnabled
    {
        get => _trainingEnabled;
        set
        {
            _trainingEnabled = value;
            OnPropertyChanged(nameof(TrainingEnabled));
            OnPropertyChanged(nameof(SidepanelEnabled));
        }
    }


    bool _trainingStarted = false;
    /// <summary>
    /// Whether training has already started or not
    /// </summary>
    /// 
    public bool TrainingStarted
    {
        get => _trainingStarted;
        set
        {
            _trainingStarted = value;
            OnPropertyChanged(nameof(TrainingStarted));
            OnPropertyChanged(nameof(TrainingText));
            OnPropertyChanged(nameof(SidepanelEnabled));
        }
    }
    public bool SidepanelEnabled => !TrainingStarted && TrainingEnabled;

    public string TrainingText => TrainingStarted ? "Stop Training" : "Start Training";

    /// <summary>
    /// Import Network and Show data from old Database (this code will be removed in the future)
    /// </summary>
    public CommandHandler Import_Database => new CommandHandler(ImportData);
    async void ImportData()                                                                   
    {
        var TopLevel = CurrentApp.TopLevel;

        if (TopLevel is not null)
        {
            var xmlFileType = new FilePickerFileType("XML Files");
            xmlFileType.Patterns = new[] { "*.xml" };

            var Files = await TopLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Database",
                AllowMultiple = false,
                SuggestedStartLocation = await TopLevel.StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Desktop),
                FileTypeFilter = new List<FilePickerFileType> { xmlFileType}
            });

            if (Files.Any())
            {
                var File = Files.First().Path;

                var OldData = await ReadObjectAsync<List<TV_Ratings_Predictions.Network>>(File.AbsolutePath);

                if (OldData is not null)
                {
                    foreach (var n in OldData)
                    {
                        var NewNetwork = new Network();
                        NewNetwork.Name = n.name;
                        
                        foreach(var f in n.factors)
                        {
                            if (f is not null)
                            {
                                NewNetwork.Factors.Add(new Factor(f, NewNetwork.Factors));
                            }
                        }

                        foreach(var s in n.shows)
                        {
                            if (s is not null)
                            {
                                var NewShow = new Show();
                                NewShow.Parent = NewNetwork;
                                NewShow.Name = s.Name;
                                NewShow.Season = s.Season;
                                NewShow.PreviousEpisodes = s.PreviousEpisodes;
                                NewShow.Episodes = s.Episodes;
                                NewShow.OldRating = s.OldRating;
                                NewShow.OldViewers = s.OldViewers;
                                NewShow.OldOdds = s.OldOdds;
                                
                                if (s.FinalPrediction > 0)
                                    NewShow.FinalPrediction = s.FinalPrediction;

                                for (int i = 0; i < s.factorValues.Count; i++)
                                {
                                    var Factor = new Factor(NewNetwork.Factors[i]);
                                    Factor.IsTrue = s.factorValues[i];

                                    NewShow.Factors.Add(Factor);
                                }

                                NewShow.HalfHour = s.Halfhour;
                                NewShow.Year = s.year;
                                foreach (var d in s.ratings)
                                    NewShow.Ratings.Add(d);
                                foreach (var d in s.viewers)
                                    NewShow.Viewers.Add(d);

                                NewShow.Canceled = s.Canceled;
                                NewShow.Renewed = s.Renewed;
                                NewShow.RenewalStatus = s.RenewalStatus;

                                NewNetwork.Shows.Add(NewShow);
                            }
                        }

                        Networks.Add(NewNetwork);
                    }
                }
            }
        }

        if (Networks.Any())
        {
            ImportVisible = false;

            foreach (var network in Networks)
                network.Database_Modified += Network_Database_Modified;

            SaveDatabase();

            var MaxYear = Networks.AsParallel().SelectMany(x => x.Shows).Select(x => x.Year).Max();

            if (MaxYear is not null)
                CurrentYear = MaxYear.Value;

            foreach (var network in Networks)
                network.CurrentYear = CurrentYear;


            //Check if existing Evolution models exist, and import them if so. If not, create new models
            ConcurrentDictionary<string, Network> NetworkNames = new();
            Parallel.ForEach(Networks, x => NetworkNames[x.Name] = x);
            var Evolutions = await LoadDataAsync<List<Evolution>>(EvolutionPath, EvolutionBackup);
            
            if (Evolutions is not null)
            {
                var evolutions = Evolutions.Where(x => NetworkNames.ContainsKey(x.NetworkName)).ToList();

                Parallel.ForEach(evolutions, x =>
                {
                    x.Network = NetworkNames[x.NetworkName];
                    x.Network.Evolution = x;
                });

                var MainModels = evolutions.SelectMany(x => x.FamilyTrees.SelectMany(y => y).Select(model => new { Network = x.Network, Model = model }));
                var TopModels = evolutions.SelectMany(x => x.TopModels.SelectMany(y => y).Select(model => new { Network = x.Network, Model = model }));
                var AllModels = MainModels.Concat(TopModels).Where(x => x.Model is not null);

                Parallel.ForEach(AllModels, x => x.Model.Network = x.Network);

                Parallel.ForEach(Networks.Where(x => x.Evolution is null), x =>
                {
                    var evolution = new Evolution(x);
                    x.Evolution = evolution;
                    evolutions.Add(evolution);
                });



                EvolutionList = new ObservableCollection<Evolution>(evolutions.OrderByDescending(x => x.Network.GetAverageRatingPerYear(0)[CurrentApp.CurrentYear]));

                var controller = new EvolutionController(EvolutionList.ToList());
                controller.UpdateMargins();
            }
            else
            {
                await CreateEvolutions();
            }

            Networks = new ObservableCollection<Network>(Networks.OrderByDescending(x => x.GetAverageRatingPerYear(0)[CurrentApp.CurrentYear]));

            var StatusUpdate = new Timer(1000);
            StatusUpdate.Elapsed += async (s, e) =>
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var evolution in EvolutionList) 
                        evolution.UpdateText();
                });
            };
            StatusUpdate.Start();

            SaveEvolution();

            TrainingEnabled = true;
        }            
    }

    /// <summary>
    /// Write an object to file
    /// </summary>
    /// <typeparam name="T">Type of object to write</typeparam>
    /// <param name="FileName">File Name</param>
    /// <param name="item">The object to write</param>
    async Task WriteObjectAsync<T>(string FileName, T item)                                 
    {
        await Task.Run(() =>
        {
            using (var writer = new FileStream(FileName, FileMode.Create))
            {
                new DataContractSerializer(typeof(T)).WriteObject(writer, item);
            }
        });
    }

    /// <summary>
    /// Save an object to file
    /// </summary>
    /// <typeparam name="T">Type of object to save</typeparam>
    /// <param name="FileName">File name</param>
    async Task<T?> ReadObjectAsync<T>(string FileName)                                      
    {
        var item = await Task.Run(() =>
        {
            using (var fs = new FileStream(FileName, FileMode.Open, FileAccess.Read))
            {
                return new DataContractSerializer(typeof(T)).ReadObject(fs);
            }
        });

        if (item is null) return default;

        return (T)item;
    }

    HomePage CurrentHome = new();

    /// <summary>
    /// Navigate to Home Page
    /// </summary>
    public CommandHandler Home_Click => new CommandHandler(GoHome);                         
    async void GoHome()
    {
        if (ActivePage.Content is not HomePage)
            await ReplacePage(ActivePage, CurrentHome);

        SelectedNetwork = null;
    }

    /// <summary>
    /// Set Active Page to Home Page when loaded
    /// </summary>
    public MainViewModel()                                                                  
    {
        ActivePage.Content = CurrentHome;
        TryLoadData();

        DatabaseSave.Elapsed += DatabaseSave_Elapsed;
        EvolutionSave.Elapsed += EvolutionSave_Elapsed;
    }
    

    bool CancelTraining = false;

    /// <summary>
    /// Start or stop training
    /// </summary>
    public CommandHandler Training => new CommandHandler(StartTraining);
    async void StartTraining()
    {
        if (TrainingStarted)
        {
            CancelTraining = true;
        }
        else
        {
            var Controller = new EvolutionController(EvolutionList.ToList());
            TrainingStarted = true;
            CancelTraining = false;

            await Task.Run(async () =>
            {
                Parallel.ForEach(Networks.SelectMany(x => x.Shows), x => x.CurrentOdds = null);

                var UIUpdateTimer = new Timer(100);
                UIUpdateTimer.Elapsed += UIUpdateTimer_Elapsed;
                UIUpdateTimer.Start();

                while (!CancelTraining)
                {
                    Controller.NextGeneration();
                }

                UIUpdateTimer.Stop();
                UIUpdateTimer.Elapsed -= UIUpdateTimer_Elapsed;
                await UpdateAverages();

                Controller.UpdateMargins();
                
            });

            EvolutionList = new ObservableCollection<Evolution>(EvolutionList.OrderByDescending(x => x.Network.GetAverageRatingPerYear(0)[CurrentApp.CurrentYear]));
            Networks = new ObservableCollection<Network>(Networks.OrderByDescending(x => x.GetAverageRatingPerYear(0)[CurrentApp.CurrentYear]));

            TrainingStarted = false;

            SaveEvolution();
        }       
    }

    private async void UIUpdateTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        await UpdateAverages();
    }

    /// <summary>
    /// 10 times a second during training, check if Evolution models have been updated
    /// </summary>
    async Task UpdateAverages()
    {
        var needsupdate = EvolutionList.Where(x => x.TopModelChanged);

        if (needsupdate.Any())
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var evolution in needsupdate)
                {
                    evolution.UpdateAccuracy();
                    evolution.TopModelChanged = false;
                }
            });

            SaveEvolution();
        }
    }

    /// <summary>
    /// Locations where data is saved
    /// </summary>
    string DataFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TVPredictions");
    string DatabasePath => Path.Combine(DataFolder, "Database.xml");
    string DatabaseBackup => Path.Combine(DataFolder, "Database.bak");
    string EvolutionPath => Path.Combine(DataFolder, "Evolution.xml");
    string EvolutionBackup => Path.Combine(DataFolder, "Evolution.bak");

    /// <summary>
    /// Check to see if saved data is available, and if so, load it
    /// </summary>
    async void TryLoadData()
    {
        if (!Directory.Exists(DataFolder))
            Directory.CreateDirectory(DataFolder);

        var Database = await LoadDataAsync<List<Network>>(DatabasePath, DatabaseBackup);       

        if (Database is not null)
        {
            Networks = new ObservableCollection<Network>(Database);

            foreach (var network in Networks)
                network.Database_Modified += Network_Database_Modified;

            var MaxYear = Networks.AsParallel().SelectMany(x => x.Shows).Select(x => x.Year).Max();

            if (MaxYear is not null)
                CurrentYear = MaxYear.Value;

            foreach (var network in Networks)
                network.CurrentYear = CurrentYear;

            Parallel.ForEach(Networks.SelectMany(x => x.Shows).Where(x => x.RatingsContainer is null), x => x.ResetRatingsContainer());
            foreach (var network in Networks)
                network.SubscribeToShows();

            ConcurrentDictionary<string, Network> NetworkNames = new();
            Parallel.ForEach(Networks, x => NetworkNames[x.Name] = x);

            var Evolutions = await LoadDataAsync<List<Evolution>>(EvolutionPath, EvolutionBackup);            

            if (Evolutions is not null)
            {
                var evolutions = Evolutions.Where(x => NetworkNames.ContainsKey(x.NetworkName)).ToList();

                Parallel.ForEach(evolutions, x =>
                {
                    x.Network = NetworkNames[x.NetworkName];
                    x.Network.Evolution = x;
                });

                var MainModels = evolutions.SelectMany(x => x.FamilyTrees.SelectMany(y => y).Select(model => new { Network = x.Network, Model = model }));
                var TopModels = evolutions.SelectMany(x => x.TopModels.SelectMany(y => y).Select(model => new { Network = x.Network, Model = model }));
                var AllModels = MainModels.Concat(TopModels).Where(x => x.Model is not null);

                Parallel.ForEach(AllModels, x => x.Model.Network = x.Network);

                Parallel.ForEach(Networks.Where(x => x.Evolution is null), x =>
                {
                    var evolution = new Evolution(x);
                    x.Evolution = evolution;
                    evolutions.Add(evolution);
                });

                

                EvolutionList = new ObservableCollection<Evolution>(evolutions.OrderByDescending(x => x.Network.GetAverageRatingPerYear(0)[CurrentApp.CurrentYear]));

                var controller = new EvolutionController(EvolutionList.ToList());
                controller.UpdateMargins();
            }
            else
            {
                await CreateEvolutions();
            }

            var StatusUpdate = new Timer(1000);
            StatusUpdate.Elapsed += async (s, e) =>
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var evolution in EvolutionList)
                        evolution.UpdateText();
                });
            };
            StatusUpdate.Start();
            StatusUpdate.Start();

            TrainingEnabled = true;
        }
        else
        {
            ImportVisible = true;
        }
    }

    /// <summary>
    /// When data in the Network object has been modified, save the database
    /// </summary>
    private void Network_Database_Modified(object? sender, EventArgs e)
    {
        SaveDatabase();
    }

    async Task CreateEvolutions()
    {
        var evolutions = new ConcurrentBag<Evolution>();
        await Task.Run(() =>
        {
            Parallel.ForEach(Networks, x =>
            {
                var evolution = new Evolution(x);
                x.Evolution = evolution;

                evolutions.Add(evolution);
            });
        });

        EvolutionList = new ObservableCollection<Evolution>(evolutions.OrderByDescending(x => x.Network.GetAverageRatingPerYear(0)[CurrentApp.CurrentYear]));
    }

    /// <summary>
    /// Attempt to read a data file from a primary and backup location
    /// </summary>
    /// <typeparam name="T">Type of file being read</typeparam>
    /// <param name="PrimaryPath">Main file location</param>
    /// <param name="BackupPath">Backup file location</param>
    /// <returns></returns>
    async Task<T?> LoadDataAsync<T>(string PrimaryPath, string BackupPath = "") where T : class
    {
        if (File.Exists(PrimaryPath))
        {
            try
            {
                return await ReadObjectAsync<T>(PrimaryPath);
            }
            catch
            {
                // If reading from the primary path fails, try the backup
                if (File.Exists(BackupPath))
                {
                    return await ReadObjectAsync<T>(BackupPath);
                }
            }
        }
        else if (BackupPath != "" && File.Exists(BackupPath)) // If the primary file doesn't exist, try the backup
        {
            try
            {
                return await ReadObjectAsync<T>(BackupPath);
            }
            catch { }
        }

        return null;
    }

    async Task SaveDataAsync<T>(string PrimaryPath, string BackupPath, T Item) where T : class
    {
        try
        {
            //If existing data is valid data, and make a backup if so
            if (File.Exists(PrimaryPath))
            {
                var data = await LoadDataAsync<T>(PrimaryPath);

                if (data is not null)
                    File.Copy(PrimaryPath, BackupPath, true);
            }

            //Save data
            await WriteObjectAsync<T>(PrimaryPath, Item);
        }
        catch
        { 
        }       
        
    }

    /// <summary>
    /// Timers to handle saving
    /// </summary>
    Timer
        DatabaseSave = new Timer(1000) { AutoReset = false },
        EvolutionSave = new Timer(1000) { AutoReset = false };

    /// <summary>
    /// Save Evolution file when timer elapsed
    /// </summary>
    private async void EvolutionSave_Elapsed(object? sender, ElapsedEventArgs e)
    {
        var EvolutionSave = EvolutionList.Select(x => new Evolution(x)).ToList();

        await SaveDataAsync<List<Evolution>>(EvolutionPath, EvolutionBackup, EvolutionSave);

        await Dispatcher.UIThread.InvokeAsync(() => EvolutionSaving = false);
    }

    /// <summary>
    /// Save Database file when timer elapsed
    /// </summary>
    private async void DatabaseSave_Elapsed(object? sender, ElapsedEventArgs e)
    {
        await SaveDataAsync<List<Network>>(DatabasePath, DatabaseBackup, Networks.ToList());

        await Dispatcher.UIThread.InvokeAsync(() => DatabaseSaving = false);
    }

    /// <summary>
    /// Ensure DatabaseSave timer is set to save in the next 1 second
    /// </summary>
    void SaveDatabase()
    {
        DatabaseSaving = true;
        DatabaseSave.Start();
    }

    /// <summary>
    /// Ensure EvolutionSave timer is set to save in the next 1 second
    /// </summary>
    void SaveEvolution()
    {
        EvolutionSaving = true;
        EvolutionSave.Start();
    }

    bool _databaseSaving, _evolutionSaving;
    public bool DatabaseSaving
    {
        get => _databaseSaving;
        set
        {
            _databaseSaving = value;
            OnPropertyChanged(nameof(IsSaving));
        }
    }
    public bool EvolutionSaving
    {
        get => _evolutionSaving;
        set
        {
            _evolutionSaving = value;
            OnPropertyChanged(nameof(IsSaving));
        }
    }

    public bool IsSaving => DatabaseSaving || EvolutionSaving;
}
