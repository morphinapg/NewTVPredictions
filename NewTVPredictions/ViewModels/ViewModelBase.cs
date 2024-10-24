using CommunityToolkit.Mvvm.ComponentModel;
using System.Runtime.Serialization;

namespace NewTVPredictions.ViewModels;

[DataContract(IsReference =true)]
public class ViewModelBase : ObservableObject
{
}
