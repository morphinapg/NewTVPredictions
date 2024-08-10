using Avalonia.Controls;
using Avalonia.Threading;
using System.Threading.Tasks;

namespace NewTVPredictions.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    UserControl? _activePage = new();
    public UserControl? ActivePage                                                   //This UserControl will hold whatever Page is active in the main view
    {
        get => _activePage;
        set
        {
            _activePage = value;
            OnPropertyChanged(nameof(ActivePage));
        }
    }

    public CommandHandler Add_Network => new CommandHandler(AddNetwork_Clicked);    //Activates the Add Network button, navigating to the Add Network Page
    async void AddNetwork_Clicked()
    {
        await ReplaceActivePage(new AddNetwork());
    }
    private async Task ReplaceActivePage(UserControl newPage)                       //Changes the active page, using an opacity transition
    {
        if (ActivePage is not null)
        {
            var Parent = ActivePage.Parent as UserControl;

            if ( Parent is not null)
            {
                await Dispatcher.UIThread.InvokeAsync(() => Parent.Opacity = 0);

                ActivePage = newPage;

                await Dispatcher.UIThread.InvokeAsync(() => Parent.Opacity = 1);
            }
        }        
    }
}
