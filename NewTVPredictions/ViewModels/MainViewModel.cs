using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Threading;
using MsBox.Avalonia;
using System;
using System.Collections.ObjectModel;
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
                Duration = TimeSpan.FromSeconds(0.2)
            }
        }
    };
    public UserControl ActivePage => _activePage;

    private async Task ReplaceActivePage(UserControl newPage)                           //Changes the active page, using an opacity transition
    {
        if (ActivePage is not null)
        {

            await Dispatcher.UIThread.InvokeAsync(() => ActivePage.Opacity = 0);

            ActivePage.Content = newPage;

            await Dispatcher.UIThread.InvokeAsync(() => ActivePage.Opacity = 1);
        }
    }

    Network _currentNetwork = new();
    public Network CurrentNetwork                                                       //DataContext of the current network displayed on the active page
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
        await ReplaceActivePage(new AddNetwork());
    }

    [DataMember]
    ObservableCollection<Network> _networks = new();
    public ObservableCollection<Network> Networks => _networks;                         //Collection of all networks stored in the database

    public CommandHandler Save_Network => new CommandHandler(SaveNetwork_Clicked);      //Saves the current network to the Networks collection
    async void SaveNetwork_Clicked()
    {
        if (CurrentNetwork is null)
            await MessageBoxManager.GetMessageBoxStandard("Error", "Error retrieving network information. Network was null.", MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowAsync();
        else if (string.IsNullOrEmpty(CurrentNetwork.Name))
            await MessageBoxManager.GetMessageBoxStandard("Error", "Please give the network a name before saving!", MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowAsync();
        else if (Networks.Contains(CurrentNetwork) || Networks.Select(x => x.Name).Contains(CurrentNetwork.Name))
            await MessageBoxManager.GetMessageBoxStandard("Error", "Network with the same name already exists in database!", MsBox.Avalonia.Enums.ButtonEnum.Ok).ShowAsync();
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
}
