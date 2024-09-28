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

namespace NewTVPredictions.ViewModels;

[DataContract]
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

    [DataMember]
    ObservableCollection<Network> _networks = new();
    /// <summary>
    /// Collection of all networks stored in the database
    /// </summary>
    public ObservableCollection<Network> Networks => _networks;

    ObservableCollection<Evolution> _evolutionList = new();
    /// <summary>
    /// Collection of all Evolution objects
    /// </summary>
    public ObservableCollection<Evolution> EvolutionList => _evolutionList;

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

            if (SubPage.Content is null)
                SubPage.Content = new Predictions();
            else
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
    int CurrentTVSeason                                                                 
    {
        get
        {
            var now = DateTime.Now;
            if (now.Month < 9)
                return now.Year - 1;
            else
                return now.Year;
        }
    }

    int? _currentYear;
    /// <summary>
    /// The currently set TV season
    /// </summary>
    public int? CurrentYear                                                             
    {
        get => _currentYear is null ? CurrentTVSeason : _currentYear;
        set
        {
            _currentYear = value; 
            OnPropertyChanged(nameof(CurrentYear));
            
            if (SelectedNetwork is not null)
                SelectedNetwork.CurrentYear = value;
        }
    }

    /// <summary>
    /// Get and set the current TV Season from DatePicker
    /// </summary>
    public DateTimeOffset? SelectedYear                                                 
    {
        get => CurrentYear.HasValue ? new DateTimeOffset(new DateTime(CurrentYear.Value, 1, 1), TimeSpan.Zero) : null;
        set
        {
            var PreviousYear = _currentYear;

            if (value is DateTimeOffset d)
                CurrentYear = d.Year;

            OnPropertyChanged(nameof(SelectedYear));
        }
    }

    bool _importVisible = true;
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

            if (EvolutionList.Any())
                EvolutionList.Clear();

            
            var WeightedShows = new Dictionary<Network, IEnumerable<WeightedShow>>();
            foreach (var network in Networks)
            {
                EvolutionList.Add(new Evolution(network));
                WeightedShows[network] = network.GetWeightedShows();
            }

            Parallel.ForEach(EvolutionList.Select(x => x.TopModel), x => x.TestAccuracy());
            Parallel.ForEach(EvolutionList, x => x.UpdateAccuracy());
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
    }
}
