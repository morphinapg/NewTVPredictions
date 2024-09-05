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
    UserControl _activePage = new()                                                     //This UserControl will hold whatever Page is active in the main view
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
    public UserControl SubPage => _subPage;

    public async Task ReplacePage(UserControl TargetPage, UserControl NewPage)          //Changes the active page, using an opacity transition
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
    public Network CurrentNetwork                                                       //DataContext of the current network displayed on the AddNetwork page
    {
        get => _currentNetwork;
        set
        {
            _currentNetwork = value;
            OnPropertyChanged(nameof(CurrentNetwork));
        }
    }

    public CommandHandler Add_Network => new CommandHandler(AddNetwork_Clicked);        //Activates the Add Network button, navigating to the Add Network Page
    async void AddNetwork_Clicked()
    {
        CurrentNetwork = new();
        await ReplacePage(ActivePage, new AddNetwork());

        SelectedNetwork = null;
    }

    [DataMember]
    ObservableCollection<Network> _networks = new();
    public ObservableCollection<Network> Networks => _networks;                         //Collection of all networks stored in the database

    public CommandHandler Save_Network => new CommandHandler(SaveNetwork_Clicked);      //Saves the current network to the Networks collection
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

    public CommandHandler Clear_Network => new CommandHandler(ClearNetwork_Clicked);    //Clears the currently displayed network to default empty network

    void ClearNetwork_Clicked()
    {
        CurrentNetwork = new();
    }

    Network? _selectedNetwork;
    public Network? SelectedNetwork                                                     //Property to track when the SelectedItem of the Networks ItemsList changes, in order to navigate to the NetworkHome page for that Network
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

    async void SelectNetwork()                                                          //Switch the displayed page to the NetworkHome page for the currently selected network
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


    const int                                                                           //constants defining the tab order
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
    public int SelectedTabIndex                                                         //Selected tab on Network Home page
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

    async void SwitchTab()                                                              //Switch to a tab on the NetworkHome page
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

    int CurrentTVSeason                                                                 //returns the current TV season starting year (September through August)
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
    public int? CurrentYear                                                             //The currently set TV season
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

    public DateTimeOffset? SelectedYear                                                 //Get and set the current TV Season from DatePicker
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
    public bool ImportVisible
    {
        get => _importVisible;
        set
        {
            _importVisible = value; 
            OnPropertyChanged(nameof(ImportVisible));
        }
    }

    public CommandHandler Import_Database => new CommandHandler(ImportData);
    async void ImportData()                                                                   //Import Network and Show data from old Database (this code will be removed in the future)
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
                                    NewShow.Ratings.Add((decimal)d);
                                foreach (var d in s.viewers)
                                    NewShow.Viewers.Add((decimal)d);

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
            ImportVisible = false;
    }

    async Task WriteObjectAsync<T>(string FileName, T item)                                 //Write an object to file
    {
        await Task.Run(() =>
        {
            using (var writer = new FileStream(FileName, FileMode.Create))
            {
                new DataContractSerializer(typeof(T)).WriteObject(writer, item);
            }
        });
    }

    async Task<T?> ReadObjectAsync<T>(string FileName)                                      //Save an object to file
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

    public CommandHandler Home_Click => new CommandHandler(GoHome);                         //Navigate to Home Page
    async void GoHome()
    {
        if (ActivePage.Content is not HomePage)
            await ReplacePage(ActivePage, CurrentHome);

        SelectedNetwork = null;
    }

    public MainViewModel()                                                                  //Set Active Page to Home Page when loaded
    {
        ActivePage.Content = CurrentHome;
    }
}
