using CommunityToolkit.Mvvm.ComponentModel;

namespace Chromedroid.Gui;

public sealed partial class CdpTargetViewModel : ObservableObject
{
    public string Id { get; }

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _url;

    public CdpTargetViewModel(CdpTarget target)
    {
        Id = target.Id;
        _title = target.Title;
        _url = target.Url;
    }

    public void Update(CdpTarget target)
    {
        Title = target.Title;
        Url = target.Url;
    }
}
