using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

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
            if (ActivePage?.Content is not NetworkHome)
            {
                if (CurrentNetworkHome is null)
                    CurrentNetworkHome = new();

                await ReplacePage(ActivePage!, CurrentNetworkHome);
            }

            CurrentNetwork = SelectedNetwork;
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

    async void SwitchTab()
    {
        switch (SelectedTabIndex)
        {
            case ADD_SHOW:
                if (CurrentAddShow is null)
                    CurrentAddShow = new();

                SelectedNetwork?.ResetShow();

                if (SubPage?.Content is not AddShow)
                    await ReplacePage(SubPage!, CurrentAddShow);
                break;
        }
    }
}
